using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityNinja;
using UnityNinja.IO;

namespace UnityNinja.Editor
{
    [ScriptedImporter(1, new[] { "cgm" })]
    public class NinjaCGMImporter : ScriptedImporter
    {
        [Header("Transform")]
        public float m_Scale = 0.10f;

        [Header("Physics")]
        public bool m_GenerateMeshColliders = false;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);

            try
            {
                byte[] rawData = File.ReadAllBytes(ctx.assetPath);
                GameObject rootGO = new GameObject(assetName);

                // 1. Parse all embedded PVR textures (GBIX + PVRT)
                List<Texture2D> embeddedTextures = new List<Texture2D>();
                int pos = 0;
                while (pos < rawData.Length - 8)
                {
                    if (rawData[pos] == 'G' && rawData[pos+1] == 'B' && rawData[pos+2] == 'I' && rawData[pos+3] == 'X')
                    {
                        uint gbixLen = ByteConverter.ToUInt32(rawData, pos + 4);
                        int pvrtPos = pos + 8 + (int)gbixLen;
                        if (pvrtPos + 8 <= rawData.Length && rawData[pvrtPos] == 'P' && rawData[pvrtPos+1] == 'V' && rawData[pvrtPos+2] == 'R' && rawData[pvrtPos+3] == 'T')
                        {
                            uint pvrtLen = ByteConverter.ToUInt32(rawData, pvrtPos + 4);
                            int totalLen = 8 + (int)gbixLen + 8 + (int)pvrtLen;

                            byte[] pvrSlice = new byte[totalLen];
                            Array.Copy(rawData, pos, pvrSlice, 0, totalLen);

                            Texture2D tex = PVRTextureDecoder.DecodePVR(pvrSlice, $"Texture_{embeddedTextures.Count:00}");
                            if (tex != null)
                            {
                                ctx.AddObjectToAsset($"Texture_{embeddedTextures.Count:00}", tex);
                                embeddedTextures.Add(tex);
                            }
                            pos += totalLen;
                            continue;
                        }
                    }
                    pos++;
                }

                // 2. Parse Ninja Binary Models inside archive
                NinjaBinaryFile njFile = new NinjaBinaryFile(rawData, ModelFormat.Chunk);

                var settings = new NinjaImportSettings
                {
                    Scale = m_Scale,
                    GenerateMeshColliders = m_GenerateMeshColliders,
                    ImportMaterials = true
                };

                for (int m = 0; m < njFile.Models.Count; m++)
                {
                    NJS_OBJECT model = njFile.Models[m];
                    string[] texNames = (njFile.Texnames != null && m < njFile.Texnames.Count) ? njFile.Texnames[m] : null;

                    GameObject subModelGO = NinjaObjectResolver.ResolveHierarchy(
                        model,
                        $"Model_{m:00}_{model.Name}",
                        settings,
                        texNames,
                        ctx,
                        out _
                    );

                    if (subModelGO != null)
                    {
                        subModelGO.transform.SetParent(rootGO.transform, false);
                    }
                }

                ctx.AddObjectToAsset("main", rootGO);
                ctx.SetMainObject(rootGO);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NinjaCGMImporter] Failed importing CGM archive {ctx.assetPath}:\n{ex}");
            }
        }
    }
}