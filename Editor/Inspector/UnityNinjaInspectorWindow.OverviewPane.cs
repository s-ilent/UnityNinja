using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityNinja;
using UnityNinja.GC;
using UnityNinja.XJ;

namespace UnityNinja.Editor
{
    public partial class UnityNinjaInspectorWindow
    {
        private void DrawOverviewPane()
        {
            EditorGUILayout.LabelField("Asset Metrics & Summary", EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(m_Context.AssetPath))
            {
                EditorGUILayout.LabelField(Path.GetFileName(m_Context.AssetPath), EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);

            if (m_Context.RootModel != null)
            {
                DrawRow("Model Format", $"{m_Context.NinjaFile?.Format ?? ModelFormat.Basic}");
                DrawRow("Total Nodes", $"{m_Context.RootModel.CountAll()}");
                DrawRow("Animated Nodes", $"{m_Context.RootModel.CountAnimated()}");
                DrawRow("Total Vertices", $"{m_Context.RootModel.CountAllVertices()}");
            }

            if (m_Context.MainMotion != null)
            {
                DrawRow("Motion Frames", $"{m_Context.MainMotion.Frames}");
                DrawRow("Model Parts", $"{m_Context.MainMotion.ModelParts}");
                DrawRow("Interpolation", $"{m_Context.MainMotion.InterpolationMode}");
            }

            if (m_Context.LevelData != null)
            {
                DrawRow("COL Entries", $"{m_Context.LevelData.COLList.Count}");
                DrawRow("GeoAnims", $"{m_Context.LevelData.AnimList.Count}");
                DrawRow("Far Clip", $"{m_Context.LevelData.FarClipping:F0}m");
            }

            EditorGUILayout.Space(6);

            if (GUILayout.Button("Dump Category JSON", GUILayout.Height(26)))
            {
                DumpCurrentCategoryJson();
            }
        }

        private void DumpCurrentCategoryJson()
        {
            object targetObj = null;

            switch (m_SelectedTab)
            {
                case 0: // Node Tree
                    if (m_Context.RootModel != null)
                    {
                        var nodeList = new List<object>();
                        foreach (var n in m_Context.RootModel.EnumerateNodes())
                        {
                            nodeList.Add(new {
                                Name = n.Name,
                                Flags = $"0x{(int)n.Flags:X4}",
                                Position = n.Position,
                                Rotation = n.Rotation,
                                Scale = n.Scale,
                                Attach = n.Attach != null ? n.Attach.Name : null
                            });
                        }
                        targetObj = nodeList;
                    }
                    break;

                case 1: // Meshes
                    if (m_Context.RootModel != null)
                    {
                        var meshList = new List<object>();
                        foreach (var n in m_Context.RootModel.EnumerateNodes())
                        {
                            if (n.Attach != null) meshList.Add(n.Attach);
                        }
                        targetObj = meshList;
                    }
                    break;

                case 2: // Materials across all attach formats
                    if (m_Context.RootModel != null)
                    {
                        var matList = new List<object>();
                        foreach (var n in m_Context.RootModel.EnumerateNodes())
                        {
                            if (n.Attach is BasicAttach bs)
                                matList.AddRange(bs.Materials);
                            else if (n.Attach is ChunkAttach chunk)
                                matList.AddRange(chunk.PolyChunks);
                            else if (n.Attach is GCAttach gc)
                            {
                                foreach (var m in gc.OpaqueMeshes) matList.AddRange(m.Parameters);
                                foreach (var m in gc.TranslucentMeshes) matList.AddRange(m.Parameters);
                            }
                            else if (n.Attach is XJAttach xj)
                            {
                                foreach (var m in xj.OpaqueMeshes) matList.Add(m.Material);
                                foreach (var m in xj.TranslucentMeshes) matList.Add(m.Material);
                            }
                        }
                        targetObj = matList;
                    }
                    break;

                case 3: // Motion
                    targetObj = m_Context.MainMotion;
                    break;

                case 4: // LandTable
                    targetObj = m_Context.LevelData;
                    break;
            }

            targetObj ??= m_Context.RootModel ?? (object)m_Context.MainMotion ?? m_Context.LevelData;

            m_DumpedJsonText = NinjaJsonSerializer.Serialize(targetObj);
            GUIUtility.systemCopyBuffer = m_DumpedJsonText;
            m_ShowJsonOutput = true;
        }

        private static void DrawRow(string label, string val)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.label, GUILayout.Width(110));
            EditorGUILayout.LabelField(val, EditorStyles.boldLabel, GUILayout.Width(110));
            EditorGUILayout.EndHorizontal();
        }
    }
}