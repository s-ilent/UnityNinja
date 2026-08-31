using UnityEngine;
using UnityEditor;
using UnityNinja;
using UnityNinja.GC;
using UnityNinja.XJ;

namespace UnityNinja.Editor
{
    public partial class UnityNinjaInspectorWindow
    {
        private void DrawMaterialsTab()
        {
            if (m_Context.RootModel == null)
            {
                EditorGUILayout.HelpBox("No model hierarchy loaded.", MessageType.Info);
                return;
            }

            int count = 0;

            foreach (var node in m_Context.RootModel.EnumerateNodes())
            {
                if (node.Attach == null) continue;

                // 1. Basic Attach Materials
                if (node.Attach is BasicAttach basic && basic.Materials != null && basic.Materials.Count > 0)
                {
                    EditorGUILayout.LabelField($"Materials for [{node.Name}] ({basic.Materials.Count} slots)", EditorStyles.boldLabel);
                    for (int i = 0; i < basic.Materials.Count; i++)
                    {
                        var mat = basic.Materials[i];
                        DrawMaterialCard($"Slot [{i:00}] (TexID: {mat.TextureID})", mat);
                        count++;
                    }
                }

                // 2. Chunk Attach Materials (PolyChunks)
                else if (node.Attach is ChunkAttach chunk && chunk.PolyChunks != null)
                {
                    int currentTexId = -1;
                    Color32 diffuseCol = new Color32(255, 255, 255, 255);
                    AlphaInstruction srcAlpha = AlphaInstruction.SourceAlpha;
                    AlphaInstruction dstAlpha = AlphaInstruction.InverseSourceAlpha;

                    bool hasChunkMat = false;

                    foreach (var pc in chunk.PolyChunks)
                    {
                        if (pc is PolyChunkTinyTextureID tid)
                        {
                            currentTexId = tid.TextureID;
                            hasChunkMat = true;
                        }
                        else if (pc is PolyChunkMaterial matChunk)
                        {
                            if (matChunk.Diffuse.HasValue) diffuseCol = matChunk.Diffuse.Value;
                            srcAlpha = matChunk.SourceAlpha;
                            dstAlpha = matChunk.DestinationAlpha;
                            hasChunkMat = true;
                        }
                    }

                    if (hasChunkMat)
                    {
                        EditorGUILayout.LabelField($"Chunk Material for [{node.Name}]", EditorStyles.boldLabel);
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"Active Texture ID: {(currentTexId >= 0 ? currentTexId.ToString() : "<None>")}", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.ColorField("Diffuse Color", diffuseCol);
                        EditorGUILayout.LabelField($"Alpha Blending: {srcAlpha} -> {dstAlpha}");
                        EditorGUI.indentLevel--;
                        EditorGUILayout.EndVertical();
                        count++;
                    }
                }

                // 3. Ginja Attach Parameters (GCAttach)
                else if (node.Attach is GCAttach gc)
                {
                    int meshIdx = 0;
                    foreach (var mesh in gc.OpaqueMeshes)
                    {
                        DrawGCMeshParams($"[Opaque Mesh {meshIdx++}] for {node.Name}", mesh);
                        count++;
                    }
                    meshIdx = 0;
                    foreach (var mesh in gc.TranslucentMeshes)
                    {
                        DrawGCMeshParams($"[Translucent Mesh {meshIdx++}] for {node.Name}", mesh);
                        count++;
                    }
                }

                // 4. Xinja Attach Materials (XJAttach)
                else if (node.Attach is XJAttach xj)
                {
                    int meshIdx = 0;
                    foreach (var mesh in xj.OpaqueMeshes)
                    {
                        DrawMaterialCard($"[XJ Opaque {meshIdx++}] for {node.Name}", mesh.Material);
                        count++;
                    }
                    meshIdx = 0;
                    foreach (var mesh in xj.TranslucentMeshes)
                    {
                        DrawMaterialCard($"[XJ Translucent {meshIdx++}] for {node.Name}", mesh.Material);
                        count++;
                    }
                }
            }

            if (count == 0)
            {
                EditorGUILayout.HelpBox("No material or texture parameters found in target model.", MessageType.Info);
            }
        }

        private static void DrawMaterialCard(string title, NJS_MATERIAL mat)
        {
            if (mat == null) return;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.ColorField("Diffuse", (Color)mat.DiffuseColor);
            EditorGUILayout.ColorField("Specular", (Color)mat.SpecularColor);
            EditorGUILayout.LabelField($"Exponent: {mat.Exponent:F1} | Alpha: {mat.SourceAlpha} -> {mat.DestinationAlpha}");
            EditorGUILayout.LabelField($"States: DoubleSided={mat.DoubleSided}, UseAlpha={mat.UseAlpha}, EnvMap={mat.EnvironmentMap}");
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private static void DrawGCMeshParams(string title, GCMesh mesh)
        {
            if (mesh == null) return;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            foreach (var p in mesh.Parameters)
            {
                if (p.Type == ParameterType.Texture)
                    EditorGUILayout.LabelField($"Texture ID: {p.TextureID} | TileMode: {p.TileMode}");
                else if (p.Type == ParameterType.DiffuseColor)
                    EditorGUILayout.ColorField("Diffuse", p.Color);
                else if (p.Type == ParameterType.BlendAlpha)
                    EditorGUILayout.LabelField($"Blend: {p.SourceAlpha} -> {p.DestAlpha}");
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }
}