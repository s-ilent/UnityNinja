using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja;
using UnityNinja.GC;
using UnityNinja.XJ;

namespace UnityNinja.Editor
{
    public static class NinjaMeshResolver
    {
        #region 1. Basic Attach (NJS_MODEL / NJBM)
        public static Mesh CreateMeshFromBasicAttach(BasicAttach attach, float scale, string name, out Material[] materials)
        {
            materials = Array.Empty<Material>();
            if (attach?.Vertices == null || attach.Vertices.Length == 0 || attach.MeshSets.Count == 0)
                return null;

            Vector3[] positions = new Vector3[attach.Vertices.Length];
            Vector3[] normals = new Vector3[attach.Vertices.Length];

            for (int i = 0; i < attach.Vertices.Length; i++)
            {
                positions[i] = NinjaCoordinateUtility.ToUnityPosition(attach.Vertices[i].ToVector3(), scale);
                normals[i] = (attach.Normals != null && i < attach.Normals.Length && attach.Normals[i] != null)
                    ? NinjaCoordinateUtility.ToUnityNormal(attach.Normals[i].ToVector3())
                    : Vector3.up;
            }

            List<List<int>> submeshTriangles = new List<List<int>>();
            List<Vector2> perVertexUVs = new List<Vector2>(new Vector2[positions.Length]);
            List<Color32> perVertexColors = new List<Color32>(new Color32[positions.Length]);
            for (int i = 0; i < perVertexColors.Count; i++) perVertexColors[i] = new Color32(255, 255, 255, 255);

            Material defaultMat = new Material(Shader.Find("Standard"));
            List<Material> matList = new List<Material>();

            foreach (var meshSet in attach.MeshSets)
            {
                List<int> tris = new List<int>();
                int uvCursor = 0;

                foreach (var poly in meshSet.Polys)
                {
                    if (poly is NinjaTriangle t)
                    {
                        tris.Add(t.Indexes[0]);
                        tris.Add(t.Indexes[2]);
                        tris.Add(t.Indexes[1]);

                        if (meshSet.UVs != null && uvCursor + 2 < meshSet.UVs.Length)
                        {
                            perVertexUVs[t.Indexes[0]] = NinjaCoordinateUtility.ToUnityUV(meshSet.UVs[uvCursor].ToVector2());
                            perVertexUVs[t.Indexes[1]] = NinjaCoordinateUtility.ToUnityUV(meshSet.UVs[uvCursor + 1].ToVector2());
                            perVertexUVs[t.Indexes[2]] = NinjaCoordinateUtility.ToUnityUV(meshSet.UVs[uvCursor + 2].ToVector2());
                        }
                        uvCursor += 3;
                    }
                    else if (poly is NinjaQuad q)
                    {
                        tris.Add(q.Indexes[0]); tris.Add(q.Indexes[2]); tris.Add(q.Indexes[1]);
                        tris.Add(q.Indexes[0]); tris.Add(q.Indexes[3]); tris.Add(q.Indexes[2]);
                        uvCursor += 4;
                    }
                    else if (poly is NinjaStrip s)
                    {
                        bool flip = !s.Reversed;
                        for (int k = 0; k < s.Indexes.Length - 2; k++)
                        {
                            if (flip)
                            {
                                tris.Add(s.Indexes[k]);
                                tris.Add(s.Indexes[k + 2]);
                                tris.Add(s.Indexes[k + 1]);
                            }
                            else
                            {
                                tris.Add(s.Indexes[k + 1]);
                                tris.Add(s.Indexes[k + 2]);
                                tris.Add(s.Indexes[k]);
                            }
                            flip = !flip;
                        }
                        uvCursor += s.Indexes.Length;
                    }
                }

                if (tris.Count > 0)
                {
                    submeshTriangles.Add(tris);
                    matList.Add(defaultMat);
                }
            }

            Mesh mesh = new Mesh { name = name };
            if (positions.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = perVertexUVs.ToArray();
            mesh.colors32 = perVertexColors.ToArray();

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
        public static Mesh CreateMeshFromChunkAttach(ChunkAttach attach, float scale, string name, out Material[] materials)
        {
            materials = Array.Empty<Material>();
            if (attach?.VertexChunks == null || attach.VertexChunks.Count == 0) return null;

            List<Vector3> positions = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Color32> colors = new List<Color32>();

            foreach (var vc in attach.VertexChunks)
            {
                int targetSize = vc.IndexOffset + vc.VertexCount;
                while (positions.Count < targetSize)
                {
                    positions.Add(Vector3.zero);
                    normals.Add(Vector3.up);
                    colors.Add(new Color32(255, 255, 255, 255));
                }

                for (int i = 0; i < vc.VertexCount; i++)
                {
                    int dst = vc.IndexOffset + i;
                    positions[dst] = NinjaCoordinateUtility.ToUnityPosition(vc.Vertices[i], scale);
                    if (i < vc.Normals.Count)
                        normals[dst] = NinjaCoordinateUtility.ToUnityNormal(vc.Normals[i]);
                    if (i < vc.Diffuse.Count)
                        colors[dst] = vc.Diffuse[i];
                }
            }

            if (positions.Count == 0) return null;

            List<List<int>> submeshes = new List<List<int>>();
            List<Vector2> perVertexUVs = new List<Vector2>(new Vector2[positions.Count]);
            List<int> currentTriangles = new List<int>();

            foreach (var pc in attach.PolyChunks)
            {
                if (pc is PolyChunkStrip stripChunk)
                {
                    foreach (var strip in stripChunk.Strips)
                    {
                        bool flip = !strip.Reversed;
                        for (int k = 0; k < strip.Indexes.Length - 2; k++)
                        {
                            ushort v0 = strip.Indexes[k];
                            ushort v1 = strip.Indexes[k + 1];
                            ushort v2 = strip.Indexes[k + 2];

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

                        if (strip.UVs != null)
                        {
                            for (int u = 0; u < strip.Indexes.Length && u < strip.UVs.Length; u++)
                            {
                                int idx = strip.Indexes[u];
                                if (idx < perVertexUVs.Count)
                                    perVertexUVs[idx] = NinjaCoordinateUtility.ToUnityUV(strip.UVs[u]);
                            }
                        }
                    }
                }
            }

            if (currentTriangles.Count > 0)
                submeshes.Add(currentTriangles);

            Mesh mesh = new Mesh { name = name };
            if (positions.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = positions.ToArray();
            mesh.normals = normals.ToArray();
            mesh.colors32 = colors.ToArray();
            mesh.uv = perVertexUVs.ToArray();

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
                        {
                            int vIdx = skinSet.StartingIndex + i;
                            if (vIdx < boneWeights.Length)
                                boneWeights[vIdx] = new BoneWeight { boneIndex0 = 0, weight0 = 1.0f };
                        }
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