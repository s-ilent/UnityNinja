using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityNinja;

namespace UnityNinja.Editor
{
    [ScriptedImporter(2, new[] { "sa1lvl", "sa2lvl", "sa2blvl", "salvl" })]
    public class NinjaLandTableImporter : ScriptedImporter
    {
        [Header("Transform")]
        public float m_Scale = 0.10f;

        [Header("Collision & Geometry")]
        public bool m_ImportVisibleGeometry = true;
        public bool m_ImportCollisionMeshes = true;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            string ext = Path.GetExtension(ctx.assetPath).ToLowerInvariant();

            try
            {
                byte[] rawData = File.ReadAllBytes(ctx.assetPath);
                ModelFormat format = ext switch
                {
                    ".sa2lvl" => ModelFormat.Chunk,
                    ".sa2blvl" => ModelFormat.GC,
                    _ => ModelFormat.Basic
                };

                int headerAddr = (rawData.Length >= 16) ? BitConverter.ToInt32(rawData, 8) : 0;
                LandTable landTable = new LandTable(rawData, headerAddr, 0, format);

                GameObject rootGO = new GameObject(assetName);

                GameObject geoRoot = new GameObject("Visible_Geometry");
                geoRoot.transform.SetParent(rootGO.transform, false);

                GameObject colRoot = new GameObject("Collision_Surfaces");
                colRoot.transform.SetParent(rootGO.transform, false);

                for (int i = 0; i < landTable.COLList.Count; i++)
                {
                    COL col = landTable.COLList[i];
                    if (col.Model == null) continue;

                    bool isVisible = (col.Flags & (int)SA1SurfaceFlags.Visible) != 0;
                    bool isSolid = (col.Flags & (int)SA1SurfaceFlags.Solid) != 0 || (col.Flags & (int)SA1SurfaceFlags.Water) != 0;

                    Transform targetParent = isVisible ? geoRoot.transform : colRoot.transform;

                    var colSettings = new NinjaImportSettings
                    {
                        Scale = m_Scale,
                        GenerateMeshColliders = isSolid
                    };

                    GameObject colGO = NinjaObjectResolver.ResolveHierarchy(
                        col.Model,
                        $"{col.Model.Name}_[COL_{i:000}]",
                        colSettings,
                        null,
                        ctx,
                        out _
                    );

                    if (colGO != null)
                    {
                        colGO.transform.SetParent(targetParent, false);

                        var comp = colGO.AddComponent<CollisionSurfaceComponent>();
                        comp.rawFlags = col.Flags;
                    }
                }

                ctx.AddObjectToAsset("main", rootGO);
                ctx.SetMainObject(rootGO);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NinjaLandTableImporter] Failed importing LandTable {ctx.assetPath}:\n{ex}");
            }
        }
    }
}