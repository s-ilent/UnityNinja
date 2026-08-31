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
    [ScriptedImporter(2, new[] { "cgm" })]
    public class NinjaCGMImporter : ScriptedImporter
    {
        [Header("Transform")]
        public float m_Scale = 0.10f;

        [Header("Physics")]
        public bool m_GenerateMeshColliders = false;

        [Header("Materials")]
        public bool m_ImportMaterials = true;
        public bool m_DeduplicateMaterials = true;
        public bool m_TransparencyAsCoverage = false;

        [Header("Animation")]
        public bool m_ImportAnimation = true;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);

            try
            {
                byte[] rawData = File.ReadAllBytes(ctx.assetPath);
                CgmArchive archive = CgmArchive.Load(rawData);

                GameObject rootGO = new GameObject(assetName);

                var settings = new NinjaImportSettings
                {
                    Scale = m_Scale,
                    GenerateMeshColliders = m_GenerateMeshColliders,
                    ImportMaterials = m_ImportMaterials,
                    DeduplicateMaterials = m_DeduplicateMaterials,
                    TransparencyAsCoverage = m_TransparencyAsCoverage,
                    ImportAnimation = m_ImportAnimation
                };

                // 1. Decode and embed all PVR textures into AssetImportContext & settings map
                for (int t = 0; t < archive.Textures.Count; t++)
                {
                    var texEntry = archive.Textures[t];
                    Texture2D tex = PVRTextureDecoder.DecodePVR(texEntry.RawData, texEntry.Name);
                    if (tex != null)
                    {
                        ctx.AddObjectToAsset($"Texture_{texEntry.Index:00}_{texEntry.Name}", tex);
                        settings.EmbeddedTextures.Add(tex);
                        settings.EmbeddedTextureMap[texEntry.Name] = tex;
                    }
                }

                // 2. Build Sub-Models with embedded motions and direct texture binding
                for (int m = 0; m < archive.Models.Count; m++)
                {
                    var modelEntry = archive.Models[m];
                    if (modelEntry.RootModel == null) continue;

                    string[] texNames = modelEntry.TexturesUsed?.ToArray();

                    GameObject subModelGO = NinjaObjectResolver.ResolveHierarchy(
                        modelEntry.RootModel,
                        $"{modelEntry.ModelName}_{modelEntry.RootModel.Name}",
                        settings,
                        texNames,
                        ctx,
                        out List<Transform> nodeTransforms
                    );

                    if (subModelGO != null)
                    {
                        subModelGO.transform.SetParent(rootGO.transform, false);

                        if (settings.ImportAnimation && modelEntry.EmbeddedMotions != null && modelEntry.EmbeddedMotions.Count > 0)
                        {
                            NinjaAnimatorResolver.SetupModelAnimations(
                                modelEntry.RootModel,
                                subModelGO,
                                nodeTransforms,
                                modelEntry.EmbeddedMotions,
                                modelEntry.ModelName,
                                ctx.assetPath,
                                settings.Scale,
                                ctx
                            );
                        }
                    }
                }

                // 3. Build Scene Dynamic Lights (NJLI)
                if (archive.Lights.Count > 0)
                {
                    GameObject lightsContainer = new GameObject("Dynamic_Lights");
                    lightsContainer.transform.SetParent(rootGO.transform, false);

                    foreach (var light in archive.Lights)
                    {
                        GameObject lightGO = new GameObject($"Light_{light.Index:000}");
                        lightGO.transform.SetParent(lightsContainer.transform, false);

                        lightGO.transform.localPosition = NinjaCoordinateUtility.ToUnityPosition(light.Position, m_Scale);
                        if (light.Direction != Vector3.zero)
                        {
                            lightGO.transform.forward = -NinjaCoordinateUtility.ToUnityNormal(light.Direction);
                        }

                        Light lComp = lightGO.AddComponent<Light>();
                        lComp.type = (light.Direction != Vector3.zero) ? LightType.Directional : LightType.Point;
                        lComp.color = light.Color;
                        lComp.range = Mathf.Max(1.0f, light.Far * m_Scale);
                    }
                }

                ctx.AddObjectToAsset("main", rootGO);
                ctx.SetMainObject(rootGO);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NinjaCGMImporter] Failed importing CGM archive {ctx.assetPath}: {ex}");
            }
        }
    }
}