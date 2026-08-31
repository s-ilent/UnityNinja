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

                // 2. Build Sub-Models Dictionary
                Dictionary<int, GameObject> resolvedModelPrototypes = new Dictionary<int, GameObject>();

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

                        resolvedModelPrototypes[m] = subModelGO;
                    }
                }

                // 3. Illbleed Stage Map Layout (CGMP)
                if (archive.MapObjects.Count > 0)
                {
                    GameObject stageObjectsRoot = new GameObject("Stage_Objects");
                    stageObjectsRoot.transform.SetParent(rootGO.transform, false);

                    for (int i = 0; i < archive.MapObjects.Count; i++)
                    {
                        var mapObj = archive.MapObjects[i];
                        string objName = $"[Obj_{mapObj.ObjectID:000}_Idx_{i:000}]";

                        GameObject objGO = new GameObject(objName);
                        objGO.transform.SetParent(stageObjectsRoot.transform, false);
                        objGO.transform.localPosition = NinjaCoordinateUtility.ToUnityPosition(mapObj.Position, m_Scale);
                        objGO.transform.localEulerAngles = NinjaCoordinateUtility.ToUnityEuler(mapObj.Rotation);
                        objGO.transform.localScale = (mapObj.Scale == Vector3.zero) ? Vector3.one : mapObj.Scale;

                        var comp = objGO.AddComponent<CGMPObjectComponent>();
                        comp.objectID = mapObj.ObjectID;
                        comp.flags = mapObj.Flags;
                        comp.originalPosition = mapObj.Position;
                        comp.originalRotation = mapObj.Rotation;
                        comp.originalScale = mapObj.Scale;

                        // Instantiate corresponding sub-model instance under this placed object
                        if (resolvedModelPrototypes.TryGetValue(mapObj.ObjectID, out GameObject modelProto) && modelProto != null)
                        {
                            GameObject modelInstance = (GameObject)UnityEngine.Object.Instantiate(modelProto);
                            modelInstance.name = modelProto.name;
                            modelInstance.transform.SetParent(objGO.transform, false);
                        }
                    }

                    // Also store the model prototypes container under root
                    GameObject modelsLib = new GameObject("Model_Prototypes");
                    modelsLib.transform.SetParent(rootGO.transform, false);
                    foreach (var kvp in resolvedModelPrototypes)
                    {
                        kvp.Value.transform.SetParent(modelsLib.transform, false);
                    }
                }
                else
                {
                    // If no CGMP layout table, attach all resolved models directly to root
                    foreach (var kvp in resolvedModelPrototypes)
                    {
                        kvp.Value.transform.SetParent(rootGO.transform, false);
                    }
                }

                // 4. Illbleed Stage Collisions (CGCL / CGLC)
                if (archive.Collisions.Count > 0)
                {
                    GameObject colRoot = new GameObject("Stage_Collisions");
                    colRoot.transform.SetParent(rootGO.transform, false);

                    for (int i = 0; i < archive.Collisions.Count; i++)
                    {
                        var col = archive.Collisions[i];
                        GameObject colGO = new GameObject($"[Col_{i:000}_Shape_{col.Shape}]");
                        colGO.transform.SetParent(colRoot.transform, false);
                        colGO.transform.localPosition = NinjaCoordinateUtility.ToUnityPosition(col.Center, m_Scale);

                        if (col.Shape == 0) // Box
                        {
                            BoxCollider box = colGO.AddComponent<BoxCollider>();
                            box.size = new Vector3(col.Size.x * m_Scale * 2f, col.Size.y * m_Scale * 2f, col.Size.z * m_Scale * 2f);
                        }
                        else // Sphere
                        {
                            SphereCollider sphere = colGO.AddComponent<SphereCollider>();
                            sphere.radius = Mathf.Max(0.1f, col.Radius * m_Scale);
                        }
                    }
                }

                // 5. Scene Dynamic Lights (NJLI / CGAL)
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

                // 6. Cameras (NCAM / NJCA)
                if (archive.Cameras.Count > 0)
                {
                    GameObject camsContainer = new GameObject("Scene_Cameras");
                    camsContainer.transform.SetParent(rootGO.transform, false);

                    foreach (var cam in archive.Cameras)
                    {
                        GameObject camGO = new GameObject($"Camera_{cam.Index:000}");
                        camGO.transform.SetParent(camsContainer.transform, false);
                        camGO.transform.localPosition = NinjaCoordinateUtility.ToUnityPosition(cam.Position, m_Scale);
                        
                        Camera cComp = camGO.AddComponent<Camera>();
                        if (cam.NearClip > 0) cComp.nearClipPlane = cam.NearClip * m_Scale;
                        if (cam.FarClip > 0) cComp.farClipPlane = cam.FarClip * m_Scale;
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