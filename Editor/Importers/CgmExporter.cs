using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja.Editor
{
    public static class CgmExporter
    {
        private const string MENU_EXTRACT = "Assets/UnityNinja/Extract CGM Archive to Folder...";

        [MenuItem(MENU_EXTRACT, false, 20)]
        public static void ExtractSelectedCgms()
        {
            if (Selection.objects == null || Selection.objects.Length == 0) return;

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (IsCgmPath(path))
                {
                    ExtractCgmToDirectory(path);
                }
            }
        }

        [MenuItem(MENU_EXTRACT, true)]
        public static bool ValidateExtractSelectedCgms()
        {
            if (Selection.objects == null || Selection.objects.Length == 0) return false;
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (IsCgmPath(path)) return true;
            }
            return false;
        }

        private static bool IsCgmPath(string path) =>
            !string.IsNullOrEmpty(path) && path.EndsWith(".cgm", StringComparison.OrdinalIgnoreCase);

        public static void ExtractCgmToDirectory(string assetPath)
        {
            string absPath = ResolveAbsolutePath(assetPath);
            if (!File.Exists(absPath)) return;

            string folderName = Path.GetFileNameWithoutExtension(assetPath) + "_extracted";
            string targetDir = Path.Combine(Path.GetDirectoryName(absPath), folderName).Replace('\\', '/');

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            AssetDatabase.StartAssetEditing();

            try
            {
                byte[] rawData = File.ReadAllBytes(absPath);
                CgmArchive archive = CgmArchive.Load(rawData);

                int pngCount = 0;
                int modelCount = 0;
                int lightCount = 0;
                int unknownCount = 0;

                // 1. Export decoded textures as PNG & raw PVR
                foreach (var texEntry in archive.Textures)
                {
                    string baseName = texEntry.Name;
                    Texture2D tex = PVRTextureDecoder.DecodePVR(texEntry.RawData, baseName);
                    if (tex != null)
                    {
                        byte[] pngBytes = tex.EncodeToPNG();
                        string pngPath = Path.Combine(targetDir, baseName + ".png");
                        File.WriteAllBytes(pngPath, pngBytes);
                        pngCount++;
                        UnityEngine.Object.DestroyImmediate(tex);
                    }

                    string pvrPath = Path.Combine(targetDir, baseName + ".pvr");
                    File.WriteAllBytes(pvrPath, texEntry.RawData);
                }

                // 2. Export merged standalone .nj / .gj models
                foreach (var mdlEntry in archive.Models)
                {
                    string ext = mdlEntry.ChunkTag == "GJCM" ? "gj" : "nj";
                    string modelFileName = mdlEntry.ModelName + "." + ext;
                    string modelPath = Path.Combine(targetDir, modelFileName);

                    File.WriteAllBytes(modelPath, mdlEntry.ModelBytes);
                    modelCount++;
                }

                // 3. Export Dynamic Lights as Prefab & JSON
                if (archive.Lights.Count > 0)
                {
                    GameObject lightsRootGO = new GameObject("Scene_Lights");
                    try
                    {
                        foreach (var lightEntry in archive.Lights)
                        {
                            GameObject lightGO = new GameObject($"Light_{lightEntry.Index:000}");
                            lightGO.transform.SetParent(lightsRootGO.transform, false);
                            lightGO.transform.localPosition = NinjaCoordinateUtility.ToUnityPosition(lightEntry.Position, 0.1f);
                            if (lightEntry.Direction != Vector3.zero)
                            {
                                lightGO.transform.forward = -NinjaCoordinateUtility.ToUnityNormal(lightEntry.Direction);
                            }

                            Light l = lightGO.AddComponent<Light>();
                            l.type = (lightEntry.Direction != Vector3.zero) ? LightType.Directional : LightType.Point;
                            l.color = lightEntry.Color;
                            l.range = Mathf.Max(1.0f, lightEntry.Far * 0.1f);
                        }

                        string lightsRelPath = GetRelativeAssetPath(Path.Combine(targetDir, "Scene_Lights.prefab"));
                        if (!string.IsNullOrEmpty(lightsRelPath))
                        {
                            PrefabUtility.SaveAsPrefabAsset(lightsRootGO, lightsRelPath);
                            lightCount = archive.Lights.Count;
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(lightsRootGO);
                    }
                }

                // 4. Export Unknown Chunks
                foreach (var unk in archive.UnknownChunks)
                {
                    string unkFileName = $"unknown_{unk.Index:000}_{unk.Tag}.bin";
                    string unkPath = Path.Combine(targetDir, unkFileName);
                    File.WriteAllBytes(unkPath, unk.RawData);
                    unknownCount++;
                }

                // 5. Write manifest.json
                string manifestPath = Path.Combine(targetDir, "manifest.json");
                NinjaJsonSerializer.SerializeToFile(manifestPath, archive);

                EditorUtility.DisplayDialog(
                    "CGM Extraction Complete",
                    "Successfully extracted " + Path.GetFileName(assetPath) + ":\n" +
                    "• Destination: " + folderName + "/\n" +
                    "• Textures Converted (PNG + PVR): " + pngCount + "\n" +
                    "• Standalone Models (.nj/.gj): " + modelCount + "\n" +
                    "• Scene Lights Saved: " + lightCount + "\n" +
                    "• Unknown Blocks: " + unknownCount,
                    "OK"
                );
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        private static string ResolveAbsolutePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return "";
            if (Path.IsPathRooted(assetPath)) return assetPath.Replace('\\', '/');
            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }

        private static string GetRelativeAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return "";
            string dataPath = Application.dataPath.Replace('\\', '/');
            string norm = absolutePath.Replace('\\', '/');
            if (norm.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + norm.Substring(dataPath.Length);
            }
            return "";
        }
    }
}