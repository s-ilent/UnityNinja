using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityNinja;

namespace UnityNinja.Editor
{
    [ScriptedImporter(1, new[] { "nj", "njb", "gj", "gjb", "xj" })]
    public class NinjaModelImporter : ScriptedImporter
    {
        [Header("Transform")]
        public float m_Scale = 0.10f;

        [Header("Physics")]
        public bool m_GenerateMeshColliders = false;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string ext = Path.GetExtension(ctx.assetPath).ToLowerInvariant();
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);

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
                    GameObject rootGO = NinjaObjectResolver.ResolveHierarchy(
                        rootModel,
                        assetName,
                        m_Scale,
                        m_GenerateMeshColliders,
                        ctx,
                        out _
                    );

                    if (rootGO != null)
                    {
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