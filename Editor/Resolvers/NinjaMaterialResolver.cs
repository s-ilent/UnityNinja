using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityNinja;
using UnityNinja.IO;
using UnityNinja.GC;
using UnityNinja.XJ;

namespace UnityNinja.Editor
{
    public enum MaterialLocation
    {
        EmbedInPrefab = 0,
        UseExternalMaterials = 1
    }

    public enum MaterialSearch
    {
        Local = 0,
        RecursiveSubFolder = 1,
        ProjectDir = 2
    }

    public enum MaterialNaming
    {
        ByMaterialName = 0,
        ByModelAndMaterialName = 1,
        ByTextureName = 2
    }

    public readonly struct NinjaMaterialKey : IEquatable<NinjaMaterialKey>
    {
        public readonly int TextureID;
        public readonly string TextureName;
        public readonly Color32 DiffuseColor;
        public readonly Color32 SpecularColor;
        public readonly float Exponent;
        public readonly uint Flags;

        public NinjaMaterialKey(NJS_MATERIAL mat, string texName)
        {
            if (mat != null)
            {
                TextureID = mat.TextureID;
                TextureName = texName ?? "";
                DiffuseColor = mat.DiffuseColor;
                SpecularColor = mat.SpecularColor;
                Exponent = mat.Exponent;
                Flags = mat.Flags;
            }
            else
            {
                TextureID = -1;
                TextureName = "";
                DiffuseColor = new Color32(255, 255, 255, 255);
                SpecularColor = new Color32(0, 0, 0, 0);
                Exponent = 0.0f;
                Flags = 0;
            }
        }

        public bool Equals(NinjaMaterialKey other)
        {
            return TextureID == other.TextureID &&
                   string.Equals(TextureName, other.TextureName, StringComparison.OrdinalIgnoreCase) &&
                   DiffuseColor.r == other.DiffuseColor.r &&
                   DiffuseColor.g == other.DiffuseColor.g &&
                   DiffuseColor.b == other.DiffuseColor.b &&
                   DiffuseColor.a == other.DiffuseColor.a &&
                   SpecularColor.r == other.SpecularColor.r &&
                   SpecularColor.g == other.SpecularColor.g &&
                   SpecularColor.b == other.SpecularColor.b &&
                   SpecularColor.a == other.SpecularColor.a &&
                   Mathf.Approximately(Exponent, other.Exponent) &&
                   Flags == other.Flags;
        }

        public override bool Equals(object obj) => obj is NinjaMaterialKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TextureID;
                hash = (hash * 397) ^ (TextureName != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(TextureName) : 0);
                hash = (hash * 397) ^ (DiffuseColor.r | (DiffuseColor.g << 8) | (DiffuseColor.b << 16) | (DiffuseColor.a << 24));
                hash = (hash * 397) ^ (SpecularColor.r | (SpecularColor.g << 8) | (SpecularColor.b << 16) | (SpecularColor.a << 24));
                hash = (hash * 397) ^ Exponent.GetHashCode();
                hash = (hash * 397) ^ (int)Flags;
                return hash;
            }
        }
    }

    public static class NinjaMaterialResolver
    {
        private static readonly string[] TextureExtensions = {
            ".png", ".dds", ".tga", ".jpg", ".jpeg", ".bmp", ".psd", ".tif", ".tiff", ".pvr", ".dcpvr", ".spvr"
        };

        private static readonly Dictionary<NinjaMaterialKey, Material> s_MaterialCache = new Dictionary<NinjaMaterialKey, Material>();

        public static void ResetMaterialCache()
        {
            s_MaterialCache.Clear();
        }

        public static Material ResolveMaterial(
            NJS_MATERIAL ninjaMat,
            int materialIndex,
            string nodeName,
            string assetName,
            string modelFolder,
            string[] texNameList,
            NinjaImportSettings settings,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            settings ??= NinjaImportSettings.Default;

            // 1. Check inspector override table
            if (settings.MaterialRemaps != null)
            {
                foreach (var remap in settings.MaterialRemaps)
                {
                    if (remap.slotIndex == materialIndex && remap.overrideMaterial != null)
                        return remap.overrideMaterial;
                }
            }

            string resolvedTexName = (texNameList != null && ninjaMat != null && ninjaMat.TextureID >= 0 && ninjaMat.TextureID < texNameList.Length)
                ? texNameList[ninjaMat.TextureID]
                : "";

            // 2. Check deduplication cache
            NinjaMaterialKey key = new NinjaMaterialKey(ninjaMat, resolvedTexName);
            if (settings.DeduplicateMaterials && s_MaterialCache.TryGetValue(key, out Material cachedMat) && cachedMat != null)
            {
                return cachedMat;
            }

            string matName = DetermineMaterialName(ninjaMat, materialIndex, nodeName, assetName, resolvedTexName, settings.MaterialNaming, settings.DeduplicateMaterials);

            Material resolvedMaterial = null;

            if (settings.MaterialLocation == MaterialLocation.UseExternalMaterials)
            {
                string candidateFolder = ResolveTargetFolder(modelFolder, settings.MaterialSearchPath);
                string existingPath = $"{candidateFolder}/{matName}.mat";
                if (File.Exists(existingPath))
                {
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(existingPath);
                    if (existing != null)
                    {
                        if (settings.DeduplicateMaterials) s_MaterialCache[key] = existing;
                        return existing;
                    }
                }

                Material created = CreateMaterialInstance(ninjaMat, matName, modelFolder, assetName, resolvedTexName, settings, ctx);
                if (!Directory.Exists(candidateFolder))
                {
                    Directory.CreateDirectory(candidateFolder);
                    AssetDatabase.Refresh();
                }

                AssetDatabase.CreateAsset(created, existingPath);
                resolvedMaterial = created;
            }
            else
            {
                Material embedded = CreateMaterialInstance(ninjaMat, matName, modelFolder, assetName, resolvedTexName, settings, ctx);
                ctx?.AddObjectToAsset($"Material_{materialIndex}_{matName}", embedded);
                resolvedMaterial = embedded;
            }

            if (settings.DeduplicateMaterials && resolvedMaterial != null)
            {
                s_MaterialCache[key] = resolvedMaterial;
            }

            return resolvedMaterial;
        }

        private static string DetermineMaterialName(
            NJS_MATERIAL mat,
            int index,
            string nodeName,
            string assetName,
            string texName,
            MaterialNaming namingMode,
            bool deduplicate)
        {
            if (deduplicate)
            {
                string baseTexName = !string.IsNullOrEmpty(texName)
                    ? texName
                    : (mat != null && mat.TextureID >= 0 ? $"Tex_{mat.TextureID:00}" : "Color");

                string colorSuffix = "";
                if (mat != null)
                {
                    if (mat.DiffuseColor.r != 255 || mat.DiffuseColor.g != 255 || mat.DiffuseColor.b != 255)
                        colorSuffix += $"_D_{mat.DiffuseColor.r}_{mat.DiffuseColor.g}_{mat.DiffuseColor.b}";
                    if (mat.UseAlpha)
                        colorSuffix += "_Alpha";
                    if (mat.DoubleSided)
                        colorSuffix += "_Double";
                    if (mat.IgnoreLighting)
                        colorSuffix += "_Unlit";
                    if (mat.EnvironmentMap)
                        colorSuffix += "_Env";
                }

                return namingMode switch
                {
                    MaterialNaming.ByModelAndMaterialName => $"{assetName}_Mat_{baseTexName}{colorSuffix}",
                    _ => $"Mat_{baseTexName}{colorSuffix}"
                };
            }

            if (namingMode == MaterialNaming.ByTextureName && !string.IsNullOrEmpty(texName))
            {
                return texName;
            }

            string baseName = (mat != null && mat.TextureID >= 0)
                ? $"Mat_{index}_Tex_{mat.TextureID}"
                : $"Mat_{index}";

            return namingMode switch
            {
                MaterialNaming.ByModelAndMaterialName => $"{assetName}_{nodeName}_{baseName}",
                _ => $"{nodeName}_{baseName}"
            };
        }

        private static Material CreateMaterialInstance(
            NJS_MATERIAL ninjaMat,
            string matName,
            string modelFolder,
            string assetName,
            string texName,
            NinjaImportSettings settings,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            Shader shader = Shader.Find("Ninja/Standard") ?? Shader.Find("Standard");
            Material mat = new Material(shader) { name = matName };

            if (ninjaMat == null) return mat;

            // 1. Color Parameters & Raw Flags
            mat.SetColor("_Color", (Color)ninjaMat.DiffuseColor);
            mat.SetColor("_AmbientColor", (Color)ninjaMat.SpecularColor);
            mat.SetColor("_SpecColor", (Color)ninjaMat.SpecularColor);
            mat.SetFloat("_Shininess", Mathf.Clamp01(ninjaMat.Exponent / 64.0f));
            mat.SetFloat("_MaterialFlags", (float)ninjaMat.Flags);

            // 2. Texture Resolution
            Texture2D tex = ResolveTexture(ninjaMat.TextureID, texName, modelFolder, assetName, settings, ctx);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.SetTexture("_MainTex", tex);
            }

            // 3. Flags & States
            mat.SetFloat("_Unlit", ninjaMat.IgnoreLighting ? 1.0f : 0.0f);
            mat.SetFloat("_UseEnvMap", ninjaMat.EnvironmentMap ? 1.0f : 0.0f);
            mat.SetInt("_Cull", ninjaMat.DoubleSided ? (int)UnityEngine.Rendering.CullMode.Off : (int)UnityEngine.Rendering.CullMode.Back);

            // 4. Blending Mode Setup via Standard RenderMode
            if (ninjaMat.UseAlpha)
            {
                UnityEngine.Rendering.BlendMode srcBlend = MapAlphaInstruction(ninjaMat.SourceAlpha, UnityEngine.Rendering.BlendMode.SrcAlpha);
                UnityEngine.Rendering.BlendMode dstBlend = MapAlphaInstruction(ninjaMat.DestinationAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

                bool isAdditive = (dstBlend == UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_Mode", isAdditive ? 4.0f : 2.0f); // 4 = Additive, 2 = Transparent
                mat.SetInt("_SrcBlend", (int)srcBlend);
                mat.SetInt("_DstBlend", (int)dstBlend);
                mat.SetInt("_ZWrite", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetShaderPassEnabled("ShadowCaster", false);
                mat.SetShaderPassEnabled("DepthOnly", false);
            }
            else
            {
                mat.SetFloat("_Mode", 0.0f); // Opaque
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                mat.SetShaderPassEnabled("ShadowCaster", true);
                mat.SetShaderPassEnabled("DepthOnly", true);
            }

            return mat;
        }

        private static UnityEngine.Rendering.BlendMode MapAlphaInstruction(AlphaInstruction inst, UnityEngine.Rendering.BlendMode fallback) => inst switch
        {
            AlphaInstruction.Zero => UnityEngine.Rendering.BlendMode.Zero,
            AlphaInstruction.One => UnityEngine.Rendering.BlendMode.One,
            AlphaInstruction.OtherColor => UnityEngine.Rendering.BlendMode.SrcColor,
            AlphaInstruction.InverseOtherColor => UnityEngine.Rendering.BlendMode.OneMinusSrcColor,
            AlphaInstruction.SourceAlpha => UnityEngine.Rendering.BlendMode.SrcAlpha,
            AlphaInstruction.InverseSourceAlpha => UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha,
            AlphaInstruction.DestinationAlpha => UnityEngine.Rendering.BlendMode.DstAlpha,
            AlphaInstruction.InverseDestinationAlpha => UnityEngine.Rendering.BlendMode.OneMinusDstAlpha,
            _ => fallback
        };

        private static Texture2D ResolveTexture(
            int textureID,
            string texName,
            string modelFolder,
            string assetName,
            NinjaImportSettings settings,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            if (settings?.TextureRemaps != null)
            {
                foreach (var remap in settings.TextureRemaps)
                {
                    if (remap.textureIndex == textureID && remap.overrideTexture != null)
                        return remap.overrideTexture;
                }
            }

            List<string> candidateFolders = BuildCandidateFolders(modelFolder, settings?.MaterialSearchPath, settings?.TextureSearchPaths);
            List<string> fileNamesToSearch = new List<string>();

            if (!string.IsNullOrEmpty(texName))
            {
                fileNamesToSearch.Add(texName);
            }

            fileNamesToSearch.Add($"{assetName}_{textureID}");
            fileNamesToSearch.Add($"{assetName}_{textureID:00}");
            fileNamesToSearch.Add($"texture_{textureID:03d}");
            fileNamesToSearch.Add($"texture_{textureID:02d}");
            fileNamesToSearch.Add($"texture_{textureID}");
            fileNamesToSearch.Add($"tex_{textureID:02d}");
            fileNamesToSearch.Add($"tex_{textureID}");
            fileNamesToSearch.Add($"{textureID:000}");
            fileNamesToSearch.Add($"{textureID}");

            foreach (string folder in candidateFolders)
            {
                foreach (string fn in fileNamesToSearch)
                {
                    foreach (string ext in TextureExtensions)
                    {
                        string p = $"{folder}/{fn}{ext}".Replace('\\', '/');
                        if (File.Exists(p))
                        {
                            Texture2D loaded = LoadOrDecodeTexture(p, fn, ctx);
                            if (loaded != null) return loaded;
                        }
                    }

                    string exactPath = $"{folder}/{fn}".Replace('\\', '/');
                    if (File.Exists(exactPath))
                    {
                        Texture2D loaded = LoadOrDecodeTexture(exactPath, Path.GetFileNameWithoutExtension(fn), ctx);
                        if (loaded != null) return loaded;
                    }
                }
            }

            return null;
        }

        private static Texture2D LoadOrDecodeTexture(string path, string assetName, UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t != null)
            {
                ctx?.DependsOnSourceAsset(path);
                return t;
            }
        
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".pvr" or ".dcpvr" or ".spvr")
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Texture2D pvrTex = PVRTextureDecoder.DecodePVR(bytes, assetName);
                    if (pvrTex != null)
                    {
                        ctx?.DependsOnSourceAsset(path);
                        return pvrTex;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NinjaMaterialResolver] Direct PVR decode failed for {path}: {ex.Message}");
                }
            }
        
            return null;
        }

        private static List<string> BuildCandidateFolders(string modelFolder, string searchDir, string[] textureSearchPaths)
        {
            List<string> folders = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string dir)
            {
                if (string.IsNullOrEmpty(dir)) return;
                string norm = dir.Replace('\\', '/').TrimEnd('/');
                if (seen.Add(norm)) folders.Add(norm);
                if (seen.Add($"{norm}/Textures")) folders.Add($"{norm}/Textures");
                if (seen.Add($"{norm}/textures")) folders.Add($"{norm}/textures");
            }

            if (textureSearchPaths != null)
            {
                foreach (string dir in textureSearchPaths) Add(dir);
            }

            Add(modelFolder);
            Add(searchDir);

            return folders;
        }

        private static string ResolveTargetFolder(string modelFolder, string searchDir) =>
            (!string.IsNullOrEmpty(searchDir) && searchDir.StartsWith("Assets")) ? searchDir.Replace('\\', '/') : $"{modelFolder}/Materials";

        public static void ExtractMaterials(string assetPath, SerializedProperty locationProp, SerializedProperty searchDirProp)
        {
            string dest = EditorUtility.OpenFolderPanel("Select Destination Folder for Extracted Materials", "Assets", "");
            if (string.IsNullOrEmpty(dest) || !dest.StartsWith(Application.dataPath)) return;

            string rel = "Assets" + dest.Substring(Application.dataPath.Length);
            int count = 0;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (sub is Material mat)
                {
                    AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(mat), $"{rel}/{mat.name}.mat");
                    count++;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            locationProp.enumValueIndex = (int)MaterialLocation.UseExternalMaterials;
            searchDirProp.stringValue = rel;
            EditorUtility.DisplayDialog("Material Extraction Complete", "Successfully extracted materials to " + rel, "OK");
        }
    }
}