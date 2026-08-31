using System;
using UnityEngine;
using UnityEditor;
using UnityNinja;
using UnityNinja.GC;
using UnityNinja.XJ;

namespace UnityNinja.Editor
{
    public partial class UnityNinjaInspectorWindow
    {
        private int m_MeshPage = 0;

        private void DrawMeshesTab()
        {
            if (m_Context.RootModel == null)
            {
                EditorGUILayout.HelpBox("No model attach data present.", MessageType.Info);
                return;
            }

            foreach (var node in m_Context.RootModel.EnumerateNodes())
            {
                if (node.Attach == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Mesh Attach: {node.Attach.Name} ({node.Attach.GetType().Name})", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                if (node.Attach is BasicAttach basic)
                {
                    EditorGUILayout.LabelField($"Vertices: {basic.Vertices?.Length ?? 0} | Normals: {basic.Normals?.Length ?? 0} | MeshSets: {basic.MeshSets.Count}");
                    for (int m = 0; m < basic.MeshSets.Count; m++)
                    {
                        var ms = basic.MeshSets[m];
                        EditorGUILayout.LabelField($"  [MeshSet {m:00}] Type: {ms.PolyType} | MatID: {ms.MaterialID} | Polys: {ms.Polys.Count} | UVs: {ms.UVs?.Length ?? 0}");
                    }
                }
                else if (node.Attach is ChunkAttach chunk)
                {
                    EditorGUILayout.LabelField($"Vertex Chunks: {chunk.VertexChunks.Count} | Poly Chunks: {chunk.PolyChunks.Count} | Skinned: {chunk.HasWeight}");
                    for (int v = 0; v < chunk.VertexChunks.Count; v++)
                    {
                        var vc = chunk.VertexChunks[v];
                        EditorGUILayout.LabelField($"  [VChunk {v:00}] {vc.Type} - Count: {vc.VertexCount}, Offset: {vc.IndexOffset}");
                    }
                    for (int p = 0; p < chunk.PolyChunks.Count; p++)
                    {
                        var pc = chunk.PolyChunks[p];
                        EditorGUILayout.LabelField($"  [PChunk {p:00}] {pc.Type}");
                    }
                }
                else if (node.Attach is GCAttach gc)
                {
                    EditorGUILayout.LabelField($"GC Vertex Sets: {gc.VertexData.Count} | Skinned Sets: {gc.VertexSkinData.Count} | Opaque: {gc.OpaqueMeshes.Count} | Translucent: {gc.TranslucentMeshes.Count}");
                }
                else if (node.Attach is XJAttach xj)
                {
                    EditorGUILayout.LabelField($"XJ Vertex Sets: {xj.VertexSets.Count} | Opaque: {xj.OpaqueMeshes.Count} | Translucent: {xj.TranslucentMeshes.Count}");
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }
    }
}