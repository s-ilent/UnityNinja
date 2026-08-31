using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja;
using UnityNinja.GC;
using UnityNinja.XJ;

namespace UnityNinja.Editor
{
    public struct ChunkVertexEntry
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Color32 Color;
        public bool HasValue;
    }

    public static class NinjaMeshResolver
    {
        #region 1. Basic Attach (NJS_MODEL / NJBM)
        public static Mesh CreateMeshFromBasicAttach(BasicAttach attach, float scale, string name, out Material[] materials)
        {
            materials = Array.Empty<Material>();
            if (attach?.Vertices == null || attach.Vertices.Length == 0 || attach.MeshSets.Count == 0)
                return null;

            List<Vector3> localPositions = new List<Vector3>();
            List<Vector3> localNormals = new List<Vector3>();
            List<Vector2> localUVs = new List<Vector2>();
            List<Color32> localColors = new List<Color32>();

            List<List<int>> submeshTriangles = new List<List<int>>();
            List<Material> matList = new List<Material>();
            Material defaultMat = new Material(Shader.Find("Standard"));

            foreach (var meshSet in attach.MeshSets)
            {
                List<int> tris = new List<int>();
                int uvCursor = 0;
                int colCursor = 0;

                foreach (var poly in meshSet.Polys)
                {
                    int polyVertCount = poly.Indexes.Length;
                    int[] localIdxs = new int[polyVertCount];

                    for (int i = 0; i < polyVertCount; i++)
                    {
                        int gIdx = poly.Indexes[i];
                        Vector3 pos = (gIdx >= 0 && gIdx < attach.Vertices.Length)
                            ? NinjaCoordinateUtility.ToUnityPosition(attach.Vertices[gIdx].ToVector3(), scale)
                            : Vector3.zero;

                        Vector3 norm = (attach.Normals != null && gIdx >= 0 && gIdx < attach.Normals.Length && attach.Normals[gIdx] != null)
                            ? NinjaCoordinateUtility.ToUnityNormal(attach.Normals[gIdx].ToVector3())
                            : Vector3.up;

                        Vector2 uv = (meshSet.UVs != null && uvCursor < meshSet.UVs.Length && meshSet.UVs[uvCursor] != null)
                            ? NinjaCoordinateUtility.ToUnityUV(meshSet.UVs[uvCursor].ToVector2())
                            : Vector2.zero;

                        Color32 col = (meshSet.VertexColors != null && colCursor < meshSet.VertexColors.Length)
                            ? meshSet.VertexColors[colCursor]
                            : new Color32(255, 255, 255, 255);

                        localIdxs[i] = localPositions.Count;
                        localPositions.Add(pos);
                        localNormals.Add(norm);
                        localUVs.Add(uv);
                        localColors.Add(col);

                        uvCursor++;
                        colCursor++;
                    }

                    if (poly is NinjaTriangle)
                    {
                        tris.Add(localIdxs[0]);
                        tris.Add(localIdxs[2]);
                        tris.Add(localIdxs[1]);
                    }
                    else if (poly is NinjaQuad)
                    {
                        tris.Add(localIdxs[0]); tris.Add(localIdxs[2]); tris.Add(localIdxs[1]);
                        tris.Add(localIdxs[0]); tris.Add(localIdxs[3]); tris.Add(localIdxs[2]);
                    }
                    else if (poly is NinjaStrip s)
                    {
                        bool flip = !s.Reversed;
                        for (int k = 0; k < polyVertCount - 2; k++)
                        {
                            if (flip)
                            {
                                tris.Add(localIdxs[k]);
                                tris.Add(localIdxs[k + 2]);
                                tris.Add(localIdxs[k + 1]);
                            }
                            else
                            {
                                tris.Add(localIdxs[k + 1]);
                                tris.Add(localIdxs[k + 2]);
                                tris.Add(localIdxs[k]);
                            }
                            flip = !flip;
                        }
                    }
                }

                if (tris.Count > 0)
                {
                    submeshTriangles.Add(tris);
                    matList.Add(defaultMat);
                }
            }

            if (localPositions.Count == 0 || submeshTriangles.Count == 0) return null;

            Mesh mesh = new Mesh { name = name };
            if (localPositions.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = localPositions.ToArray();
            mesh.normals = localNormals.ToArray();
            mesh.uv = localUVs.ToArray();
            mesh.colors32 = localColors.ToArray();

            mesh.subMeshCount = submeshTriangles.Count;
            for (int i = 0; i < submeshTriangles.Count; i++)
            {
                mesh.SetTriangles(submeshTriangles[i], i);
            }

            mesh.RecalculateBounds();
            materials = matList.ToArray();
            return mesh;
        }
        #endregion

        #region 2. Chunk Attach (NJS_CNK_MODEL / NJCM)
        public static Mesh CreateMeshFromChunkAttach(
            ChunkAttach attach,
            float scale,
            string name,
            ChunkVertexEntry[] globalVertexBuffer,
            out Material[] materials)
        {
            materials = Array.Empty<Material>();
            if (attach == null) return null;

            // 1. Upload vertices declared on this attach into the global vertex pool
            if (attach.VertexChunks != null)
            {
                foreach (var vc in attach.VertexChunks)
                {
                    for (int i = 0; i < vc.VertexCount; i++)
                    {
                        int targetIdx = vc.IndexOffset + i;
                        if (targetIdx >= 0 && targetIdx < globalVertexBuffer.Length)
                        {
                            Vector3 pos = NinjaCoordinateUtility.ToUnityPosition(vc.Vertices[i], scale);
                            Vector3 norm = (i < vc.Normals.Count) ? NinjaCoordinateUtility.ToUnityNormal(vc.Normals[i]) : Vector3.up;
                            Color32 col = (i < vc.Diffuse.Count) ? vc.Diffuse[i] : new Color32(255, 255, 255, 255);

                            globalVertexBuffer[targetIdx] = new ChunkVertexEntry
                            {
                                Position = pos,
                                Normal = norm,
                                Color = col,
                                HasValue = true
                            };
                        }
                    }
                }
            }

            if (attach.PolyChunks == null || attach.PolyChunks.Count == 0) return null;

            // 2. Build local vertex buffer for this mesh by reading from global pool
            List<Vector3> localPositions = new List<Vector3>();
            List<Vector3> localNormals = new List<Vector3>();
            List<Color32> localColors = new List<Color32>();
            List<Vector2> localUVs = new List<Vector2>();

            List<List<int>> submeshes = new List<List<int>>();
            List<int> currentTriangles = new List<int>();

            foreach (var pc in attach.PolyChunks)
            {
                if (pc is PolyChunkStrip stripChunk)
                {
                    foreach (var strip in stripChunk.Strips)
                    {
                        if (strip.Indexes == null || strip.Indexes.Length < 3) continue;

                        int stripLen = strip.Indexes.Length;
                        int[] localIndices = new int[stripLen];

                        for (int k = 0; k < stripLen; k++)
                        {
                            int gIdx = strip.Indexes[k];
                            Vector3 pos = (gIdx >= 0 && gIdx < globalVertexBuffer.Length && globalVertexBuffer[gIdx].HasValue)
                                ? globalVertexBuffer[gIdx].Position
                                : Vector3.zero;

                            Vector3 norm = (gIdx >= 0 && gIdx < globalVertexBuffer.Length && globalVertexBuffer[gIdx].HasValue)
                                ? globalVertexBuffer[gIdx].Normal
                                : Vector3.up;

                            Color32 col = (strip.Colors != null && k < strip.Colors.Length)
                                ? strip.Colors[k]
                                : ((gIdx >= 0 && gIdx < globalVertexBuffer.Length && globalVertexBuffer[gIdx].HasValue) ? globalVertexBuffer[gIdx].Color : new Color32(255, 255, 255, 255));

                            Vector2 uv = (strip.UVs != null && k < strip.UVs.Length)
                                ? NinjaCoordinateUtility.ToUnityUV(strip.UVs[k])
                                : Vector2.zero;

                            localIndices[k] = localPositions.Count;
                            localPositions.Add(pos);
                            localNormals.Add(norm);
                            localColors.Add(col);
                            localUVs.Add(uv);
                        }

                        bool flip = !strip.Reversed;
                        for (int k = 0; k < stripLen - 2; k++)
                        {
                            int v0 = localIndices[k];
                            int v1 = localIndices[k + 1];
                            int v2 = localIndices[k + 2];

                            if (v0 != v1 && v1 != v2 && v0 != v2)
                            {
                                if (flip)
                                {
                                    currentTriangles.Add(v0); currentTriangles.Add(v2); currentTriangles.Add(v1);
                                }
                                else
                                {
                                    currentTriangles.Add(v1); currentTriangles.Add(v2); currentTriangles.Add(v0);
                                }
                            }
                            flip = !flip;
                        }
                    }
                }
            }

            if (currentTriangles.Count > 0)
            {
                submeshes.Add(currentTriangles);
            }

            if (localPositions.Count == 0 || submeshes.Count == 0) return null;

            Mesh mesh = new Mesh { name = name };
            if (localPositions.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = localPositions.ToArray();
            mesh.normals = localNormals.ToArray();
            mesh.colors32 = localColors.ToArray();
            mesh.uv = localUVs.ToArray();

            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++)
            {
                mesh.SetTriangles(submeshes[i], i);
            }

            mesh.RecalculateBounds();
            materials = new Material[submeshes.Count];
            Material defaultMat = new Material(Shader.Find("Standard"));
            for (int i = 0; i < materials.Length; i++) materials[i] = defaultMat;

            return mesh;
        }
        #endregion

        #region 3. Ginja Attach (GCAttach / GJCM)
        public static Mesh CreateMeshFromGCAttach(
            GCAttach attach,
            float scale,
            string name,
            out Material[] materials,
            out BoneWeight[] boneWeights)
        {
            materials = Array.Empty<Material>();
            boneWeights = null;

            if (attach == null) return null;

            List<Vector3> positions = attach.VertexData.Find(x => x.Attribute == GCVertexAttribute.Position)?.Positions ?? new List<Vector3>();
            List<Vector3> normals = attach.VertexData.Find(x => x.Attribute == GCVertexAttribute.Normal)?.Normals ?? new List<Vector3>();
            List<Color32> colors = attach.VertexData.Find(x => x.Attribute == GCVertexAttribute.Color0)?.Colors ?? new List<Color32>();
            List<Vector2> uvs = attach.VertexData.Find(x => x.Attribute == GCVertexAttribute.Tex0)?.UVs ?? new List<Vector2>();

            Vector3[] unityPositions = new Vector3[positions.Count];
            Vector3[] unityNormals = new Vector3[positions.Count];
            Color32[] unityColors = new Color32[positions.Count];
            Vector2[] unityUVs = new Vector2[positions.Count];

            for (int i = 0; i < positions.Count; i++)
            {
                unityPositions[i] = NinjaCoordinateUtility.ToUnityPosition(positions[i], scale);
                unityNormals[i] = (i < normals.Count) ? NinjaCoordinateUtility.ToUnityNormal(normals[i]) : Vector3.up;
                unityColors[i] = (i < colors.Count) ? colors[i] : new Color32(255, 255, 255, 255);
                unityUVs[i] = (i < uvs.Count) ? NinjaCoordinateUtility.ToUnityUV(uvs[i]) : Vector2.zero;
            }

            if (attach.VertexSkinData != null && attach.VertexSkinData.Count > 0)
            {
                boneWeights = new BoneWeight[positions.Count];
                foreach (var skinSet in attach.VertexSkinData)
                {
                    if (skinSet.ElementType == GCSkinAttribute.StaticWeight)
                    {
                        for (int i = 0; i < skinSet.IndexCount; i++)
                            boneWeights[skinSet.StartingIndex + i] = new BoneWeight { boneIndex0 = 0, weight0 = 1.0f };
                    }
                    else if (skinSet.ElementType is GCSkinAttribute.PartialWeightStart or GCSkinAttribute.PartialWeight)
                    {
                        for (int i = 0; i < skinSet.WeightData.Count; i++)
                        {
                            int vIdx = skinSet.WeightData[i].x;
                            float w = skinSet.WeightData[i].y / 255.0f;
                            if (vIdx < boneWeights.Length)
                            {
                                BoneWeight bw = boneWeights[vIdx];
                                if (bw.weight0 <= 0f) { bw.boneIndex0 = 0; bw.weight0 = w; }
                                else if (bw.weight1 <= 0f) { bw.boneIndex1 = 1; bw.weight1 = w; }
                                else if (bw.weight2 <= 0f) { bw.boneIndex2 = 2; bw.weight2 = w; }
                                else if (bw.weight3 <= 0f) { bw.boneIndex3 = 3; bw.weight3 = w; }
                                boneWeights[vIdx] = bw;
                            }
                        }
                    }
                }
            }

            List<GCMesh> allMeshes = new List<GCMesh>();
            allMeshes.AddRange(attach.OpaqueMeshes);
            allMeshes.AddRange(attach.TranslucentMeshes);

            List<List<int>> submeshes = new List<List<int>>();
            List<Material> matList = new List<Material>();
            Material defaultMat = new Material(Shader.Find("Standard"));

            foreach (var mesh in allMeshes)
            {
                List<int> triangles = new List<int>();
                float uvScaleDivisor = 1.0f;
                foreach (var param in mesh.Parameters)
                {
                    if (param.Type == ParameterType.VtxAttrFmt && param.VertexAttribute == GCVertexAttribute.Tex0)
                    {
                        uvScaleDivisor = GCUVScaleHelper.GetDivisor(param.UVScale);
                    }
                }

                foreach (var prim in mesh.Primitives)
                {
                    var loops = prim.ToTriangles();
                    for (int i = 0; i < loops.Count - 2; i += 3)
                    {
                        triangles.Add(loops[i].PositionIndex);
                        triangles.Add(loops[i + 2].PositionIndex);
                        triangles.Add(loops[i + 1].PositionIndex);

                        if (uvScaleDivisor > 1.0f)
                        {
                            unityUVs[loops[i].PositionIndex] /= uvScaleDivisor;
                            unityUVs[loops[i + 1].PositionIndex] /= uvScaleDivisor;
                            unityUVs[loops[i + 2].PositionIndex] /= uvScaleDivisor;
                        }
                    }
                }

                if (triangles.Count > 0)
                {
                    submeshes.Add(triangles);
                    matList.Add(defaultMat);
                }
            }

            Mesh uMesh = new Mesh { name = name };
            if (unityPositions.Length > 65535) uMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            uMesh.vertices = unityPositions;
            uMesh.normals = unityNormals;
            uMesh.colors32 = unityColors;
            uMesh.uv = unityUVs;

            if (boneWeights != null)
            {
                uMesh.boneWeights = boneWeights;
            }

            uMesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++)
            {
                uMesh.SetTriangles(submeshes[i], i);
            }

            uMesh.RecalculateBounds();
            materials = matList.ToArray();
            return uMesh;
        }
        #endregion

        #region 4. Xinja Attach (XJAttach / XJCM)
        public static Mesh CreateMeshFromXJAttach(XJAttach attach, float scale, string name, out Material[] materials)
        {
            materials = Array.Empty<Material>();
            if (attach == null || attach.VertexSets.Count == 0) return null;

            var vSet = attach.VertexSets[0];
            Vector3[] positions = new Vector3[vSet.Positions.Count];
            Vector3[] normals = new Vector3[vSet.Positions.Count];
            Color32[] colors = new Color32[vSet.Positions.Count];
            Vector2[] uvs = new Vector2[vSet.Positions.Count];

            for (int i = 0; i < vSet.Positions.Count; i++)
            {
                positions[i] = NinjaCoordinateUtility.ToUnityPosition(vSet.Positions[i], scale);
                normals[i] = (i < vSet.Normals.Count) ? NinjaCoordinateUtility.ToUnityNormal(vSet.Normals[i]) : Vector3.up;
                colors[i] = (i < vSet.Colors.Count) ? vSet.Colors[i] : new Color32(255, 255, 255, 255);
                uvs[i] = (i < vSet.UVs.Count) ? NinjaCoordinateUtility.ToUnityUV(vSet.UVs[i]) : Vector2.zero;
            }

            List<List<int>> submeshes = new List<List<int>>();
            List<Material> matList = new List<Material>();
            Material defaultMat = new Material(Shader.Find("Standard"));

            List<XJMesh> allMeshes = new List<XJMesh>();
            allMeshes.AddRange(attach.OpaqueMeshes);
            allMeshes.AddRange(attach.TranslucentMeshes);

            foreach (var mesh in allMeshes)
            {
                List<int> tris = mesh.TriangulateStrips();
                if (tris.Count > 0)
                {
                    submeshes.Add(tris);
                    matList.Add(defaultMat);
                }
            }

            Mesh uMesh = new Mesh { name = name };
            if (positions.Length > 65535) uMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            uMesh.vertices = positions;
            uMesh.normals = normals;
            uMesh.colors32 = colors;
            uMesh.uv = uvs;

            uMesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++)
            {
                uMesh.SetTriangles(submeshes[i], i);
            }

            uMesh.RecalculateBounds();
            materials = matList.ToArray();
            return uMesh;
        }
        #endregion
    }
}