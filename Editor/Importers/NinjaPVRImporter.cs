using System;
using System.IO;
using UnityEngine;
using UnityEditor.AssetImporters;
using UnityNinja.IO;

namespace UnityNinja.Editor
{
    // 3rd parameter specifies overrideExtensions
    [ScriptedImporter(1, null, new[] { "pvr" })]
    public class NinjaPVRImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);

            try
            {
                byte[] rawData = File.ReadAllBytes(ctx.assetPath);
                Texture2D texture = PVRTextureDecoder.DecodePVR(rawData, assetName);

                if (texture != null)
                {
                    ctx.AddObjectToAsset("main", texture);
                    ctx.SetMainObject(texture);
                }
                else
                {
                    Debug.LogWarning($"[NinjaPVRImporter] Failed decoding Sega PVR texture: {ctx.assetPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NinjaPVRImporter] Error importing {ctx.assetPath}: {ex.Message}");
            }
        }
    }
}