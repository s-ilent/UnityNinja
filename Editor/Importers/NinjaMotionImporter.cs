using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityNinja;

namespace UnityNinja.Editor
{
    [ScriptedImporter(1, new[] { "njm", "gjm", "xjm", "nam" })]
    public class NinjaMotionImporter : ScriptedImporter
    {
        [Header("Transform")]
        public float m_Scale = 0.10f;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);

            try
            {
                byte[] rawData = File.ReadAllBytes(ctx.assetPath);
                NinjaBinaryFile motFile = new NinjaBinaryFile(rawData);

                if (motFile.Motions.Count > 0)
                {
                    NJS_MOTION motion = motFile.Motions[0];
                    AnimationClip clip = NinjaMotionResolver.ResolveMotion(
                        motion,
                        assetName,
                        m_Scale,
                        null,
                        null
                    );

                    if (clip != null)
                    {
                        ctx.AddObjectToAsset("main", clip);
                        ctx.SetMainObject(clip);
                        return;
                    }
                }

                Debug.LogWarning($"[NinjaMotionImporter] No valid NJS_MOTION chunk found in {ctx.assetPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NinjaMotionImporter] Failed importing motion {ctx.assetPath}:\n{ex}");
            }
        }
    }
}