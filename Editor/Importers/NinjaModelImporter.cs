using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityNinja;

namespace UnityNinja.Editor
{
    [ScriptedImporter(2, new[] { "nj", "njb", "gj", "gjb", "xj" })]
    public class NinjaModelImporter : ScriptedImporter
    {
        [Header("Transform")]
        public float m_Scale = 0.10f;

        [Header("Physics")]
        public bool m_GenerateMeshColliders = false;

        [Header("Materials")]
        public bool m_ImportMaterials = true;
        public MaterialLocation m_MaterialLocation = MaterialLocation.EmbedInPrefab;
        public MaterialNaming m_MaterialNaming = MaterialNaming.ByMaterialName;
        public MaterialSearch m_MaterialSearch = MaterialSearch.RecursiveSubFolder;
        public string m_MaterialSearchPath = "Assets/Materials";
        public string[] m_TextureSearchPaths = Array.Empty<string>();
        public List<MaterialRemapEntry> m_MaterialRemaps = new List<MaterialRemapEntry>();
        public List<TextureRemapEntry> m_TextureRemaps = new List<TextureRemapEntry>();

        [Header("Animation")]
        public bool m_ImportAnimation = true;

        public NinjaImportSettings GetSettings() => new NinjaImportSettings
        {
            Scale = m_Scale,
            GenerateMeshColliders = m_GenerateMeshColliders,
            ImportMaterials = m_ImportMaterials,
            MaterialLocation = m_MaterialLocation,
            MaterialNaming = m_MaterialNaming,
            MaterialSearch = m_MaterialSearch,
            MaterialSearchPath = m_MaterialSearchPath,
            TextureSearchPaths = m_TextureSearchPaths ?? Array.Empty<string>(),
            MaterialRemaps = m_MaterialRemaps,
            TextureRemaps = m_TextureRemaps,
            ImportAnimation = m_ImportAnimation
        };

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string ext = Path.GetExtension(ctx.assetPath).ToLowerInvariant();
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            NinjaImportSettings settings = GetSettings();

            try
            {
                byte[] rawData = File.ReadAllBytes(ctx.assetPath);
                ModelFormat format = ext switch
                {
                    ".gj" or ".gjb" => ModelFormat.GC,
                    ".xj" => ModelFormat.XJ,
                    _ => ModelFormat.Basic
                };

                NinjaBinaryFile njFile = new NinjaBinaryFile(rawData, format);

                if (njFile.Models.Count > 0)
                {
                    NJS_OBJECT rootModel = njFile.Models[0];
                    string[] texNameList = (njFile.Texnames != null && njFile.Texnames.Count > 0) ? njFile.Texnames[0] : null;

                    GameObject rootGO = NinjaObjectResolver.ResolveHierarchy(
                        rootModel,
                        assetName,
                        settings,
                        texNameList,
                        ctx,
                        out List<Transform> nodeTransforms
                    );

                    if (rootGO != null)
                    {
                        if (settings.ImportAnimation)
                        {
                            NinjaAnimatorResolver.SetupModelAnimations(
                                rootModel,
                                rootGO,
                                nodeTransforms,
                                assetName,
                                ctx.assetPath,
                                settings.Scale,
                                ctx
                            );
                        }

                        ctx.AddObjectToAsset("main", rootGO);
                        ctx.SetMainObject(rootGO);
                        return;
                    }
                }

                Debug.LogWarning($"[NinjaModelImporter] No root NJS_OBJECT found in {ctx.assetPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NinjaModelImporter] Failed importing {ctx.assetPath}:\n{ex}");
            }
        }
    }
}