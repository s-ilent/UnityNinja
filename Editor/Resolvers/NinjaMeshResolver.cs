using System;
using System.Collections.Generic;
using System.Linq;
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
        public BoneWeight BoneWeight;
        public bool HasValue;
        public bool HasWeight;
    }

    public class MeshBuffer
    {
        public readonly List<Vector3> Positions;
        public readonly List<Vector3> Normals;
        public readonly List<Vector4> Tangents;
        public readonly List<Color32> Colors;
        public readonly List<Vector2> UVs;
        public readonly List<Vector2> UV2s;
        public readonly List<BoneWeight> BoneWeights;
        public readonly Dictionary<int, List<int>> SubmeshTriangles;
        public bool HasWeights;

        public MeshBuffer(int vertexCapacity = 0)
        {
            int cap = Math.Max(0, vertexCapacity);
            Positions = new List<Vector3>(cap);
            Normals = new List<Vector3>(cap);
            Tangents = new List<Vector4>(cap);
            Colors = new List<Color32>(cap);
            UVs = new List<Vector2>(cap);
            UV2s = new List<Vector2>(cap);
            BoneWeights = new List<BoneWeight>(cap);
            SubmeshTriangles = new Dictionary<int, List<int>>();
        }

        public void AddVertex(Vector3 pos, Vector3 norm, Vector2 uv, Color32 col, BoneWeight bw = default)
        {
            Positions.Add(pos);
            Normals.Add(norm);
            UVs.Add(uv);
            Colors.Add(col);
            BoneWeights.Add(bw);
        }

        public void AddTriangle(int submeshIndex, int v0, int v1, int v2)
        {
            if (v0 == v1 || v1 == v2 || v0 == v2) return;
            if (!SubmeshTriangles.TryGetValue(submeshIndex, out var tris))
            {
                tris = new List<int>();
                SubmeshTriangles[submeshIndex] = tris;
            }
            tris.Add(v0);
            tris.Add(v1);
            tris.Add(v2);
        }

        public List<int> GetSortedSubmeshKeys()
        {
            var keys = new List<int>(SubmeshTriangles.Keys);
            keys.Sort();
            return keys;
        }

        public Mesh BuildMesh(string meshName)
        {
            if (Positions.Count == 0) return null;
            Mesh mesh = new Mesh { name = meshName };
            if (Positions.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(Positions);
            if (Normals.Count == Positions.Count) mesh.SetNormals(Normals);
            if (Tangents.Count == Positions.Count) mesh.SetTangents(Tangents);
            if (Colors.Count == Positions.Count) mesh.SetColors(Colors);
            if (UVs.Count == Positions.Count) mesh.SetUVs(0, UVs);
            if (UV2s.Count == Positions.Count) mesh.SetUVs(1, UV2s);

            if (HasWeights && BoneWeights.Count == Positions.Count)
            {
                mesh.boneWeights = BoneWeights.ToArray();
            }

            var sortedKeys = GetSortedSubmeshKeys();
            mesh.subMeshCount = sortedKeys.Count;
            for (int i = 0; i < sortedKeys.Count; i++)
            {
                mesh.SetTriangles(SubmeshTriangles[sortedKeys[i]], i);
            }

            if (Normals.Count == 0) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    public static class NinjaMeshResolver
    {
        private static readonly List<PolyChunk>[] PolyCache = new List<PolyChunk>[255];

        #region 1. Basic Attach
        public static Mesh CreateMeshFromBasicAttach(
            BasicAttach attach,
            float scale,
            string name,
            string nodeName,
            string assetName,
            string modelFolder,
            string[] texNameList,
            NinjaImportSettings settings,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            out Material[] materials)
        {
            materials = Array.Empty<Material>();
            if (attach?.Vertices == null || attach.Vertices.Length == 0 || attach.MeshSets.Count == 0)
                return null;

            MeshBuffer buffer = new MeshBuffer(attach.Vertices.Length);
            List<Material> matList = new List<Material>();

            for (int m = 0; m < attach.MeshSets.Count; m++)
            {
                var meshSet = attach.MeshSets[m];
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

                        localIdxs[i] = buffer.Positions.Count;
                        buffer.AddVertex(pos, norm, uv, col);

                        uvCursor++;
                        colCursor++;
                    }

                    if (poly is NinjaTriangle)
                    {
                        buffer.AddTriangle(m, localIdxs[0], localIdxs[2], localIdxs[1]);
                    }
                    else if (poly is NinjaQuad)
                    {
                        buffer.AddTriangle(m, localIdxs[0], localIdxs[2], localIdxs[1]);
                        buffer.AddTriangle(m, localIdxs[0], localIdxs[3], localIdxs[2]);
                    }
                    else if (poly is NinjaStrip s)
                    {
                        bool flip = !s.Reversed;
                        for (int k = 0; k < polyVertCount - 2; k++)
                        {
                            if (flip)
                                buffer.AddTriangle(m, localIdxs[k], localIdxs[k + 2], localIdxs[k + 1]);
                            else
                                buffer.AddTriangle(m, localIdxs[k + 1], localIdxs[k + 2], localIdxs[k]);
                            flip = !flip;
                        }
                    }
                }

                if (buffer.SubmeshTriangles.ContainsKey(m) && buffer.SubmeshTriangles[m].Count > 0)
                {
                    NJS_MATERIAL nMat = (attach.Materials != null && meshSet.MaterialID < attach.Materials.Count)
                        ? attach.Materials[meshSet.MaterialID]
                        : null;

                    Material resolvedMat = NinjaMaterialResolver.ResolveMaterial(
                        nMat,
                        meshSet.MaterialID,
                        nodeName,
                        assetName,
                        modelFolder,
                        texNameList,
                        settings,
                        ctx
                    );

                    matList.Add(resolvedMat);
                }
            }

            Mesh mesh = buffer.BuildMesh(name);
            materials = matList.ToArray();
            return mesh;
        }
        #endregion

        #region 2. Chunk Attach with PolyCache & Weight Resolution
        public static Mesh CreateMeshFromChunkAttach(
            ChunkAttach attach,
            float scale,
            string name,
            string nodeName,
            string assetName,
            string modelFolder,
            string[] texNameList,
            ChunkVertexEntry[] globalVertexBuffer,
            int nodeIndex,
            Matrix4x4 localToModel,
            bool isSkinned,
            NinjaImportSettings settings,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            out Material[] materials,
            out BoneWeight[] outWeights)
        {
            materials = Array.Empty<Material>();
            outWeights = null;
            if (attach == null) return null;

            // 1. Upload vertices & bone weights into global pool
            if (attach.VertexChunks != null)
            {
                foreach (var vc in attach.VertexChunks)
                {
                    bool isWeightedChunk = vc.Type is ChunkType.Vertex_VertexNinjaFlags or ChunkType.Vertex_VertexNormalNinjaFlags;

                    for (int i = 0; i < vc.VertexCount; i++)
                    {
                        int targetIdx = vc.IndexOffset + i;
                        if (targetIdx >= 0 && targetIdx < globalVertexBuffer.Length)
                        {
                            Vector3 rawLocalPos = NinjaCoordinateUtility.ToUnityPosition(vc.Vertices[i], scale);
                            Vector3 rawLocalNorm = (i < vc.Normals.Count) ? NinjaCoordinateUtility.ToUnityNormal(vc.Normals[i]) : Vector3.up;

                            Vector3 pos = isSkinned ? localToModel.MultiplyPoint3x4(rawLocalPos) : rawLocalPos;
                            Vector3 norm = isSkinned ? localToModel.MultiplyVector(rawLocalNorm).normalized : rawLocalNorm;
                            Color32 col = (i < vc.Diffuse.Count) ? vc.Diffuse[i] : new Color32(255, 255, 255, 255);

                            BoneWeight bw = default;
                            bool hasWeight = false;

                            if (isSkinned && isWeightedChunk && vc.NinjaFlags != null && i < vc.NinjaFlags.Count)
                            {
                                uint nFlag = vc.NinjaFlags[i];
                                int localTarget = (int)(nFlag & 0xFFFF);
                                int actualTarget = (vc.WeightStatus == WeightStatus.Start) ? targetIdx : localTarget;

                                uint rawW = (nFlag >> 16) & 0xFFFF;
                                float weightVal = rawW > 255 ? (rawW / 65535.0f) : (rawW / 255.0f);
                                if (weightVal <= 0.0f) weightVal = 1.0f;

                                if (actualTarget >= 0 && actualTarget < globalVertexBuffer.Length)
                                {
                                    if (vc.WeightStatus == WeightStatus.Start || !globalVertexBuffer[actualTarget].HasWeight)
                                    {
                                        bw.boneIndex0 = nodeIndex;
                                        bw.weight0 = weightVal;

                                        globalVertexBuffer[actualTarget] = new ChunkVertexEntry
                                        {
                                            Position = pos,
                                            Normal = norm,
                                            Color = col,
                                            BoneWeight = bw,
                                            HasValue = true,
                                            HasWeight = true
                                        };
                                    }
                                    else
                                    {
                                        bw = globalVertexBuffer[actualTarget].BoneWeight;
                                        if (bw.weight1 <= 0.0001f) { bw.boneIndex1 = nodeIndex; bw.weight1 = weightVal; }
                                        else if (bw.weight2 <= 0.0001f) { bw.boneIndex2 = nodeIndex; bw.weight2 = weightVal; }
                                        else if (bw.weight3 <= 0.0001f) { bw.boneIndex3 = nodeIndex; bw.weight3 = weightVal; }

                                        globalVertexBuffer[actualTarget].BoneWeight = bw;
                                    }
                                }
                                continue;
                            }

                            if (isSkinned)
                            {
                                bw.boneIndex0 = nodeIndex;
                                bw.weight0 = 1.0f;
                                hasWeight = true;
                            }

                            globalVertexBuffer[targetIdx] = new ChunkVertexEntry
                            {
                                Position = pos,
                                Normal = norm,
                                Color = col,
                                BoneWeight = bw,
                                HasValue = true,
                                HasWeight = isSkinned && hasWeight
                            };
                        }
                    }
                }
            }

            if (attach.PolyChunks == null || attach.PolyChunks.Count == 0) return null;

            List<PolyChunk> resolvedPolyChunks = FlattenPolyChunks(attach.PolyChunks);

            MeshBuffer buffer = new MeshBuffer(256);
            List<Material> matList = new List<Material>();

            NJS_MATERIAL currentMaterialState = new NJS_MATERIAL();
            int currentMaterialIndex = 0;
            int currentSubmeshIdx = 0;

            foreach (var pc in resolvedPolyChunks)
            {
                if (pc is PolyChunkTinyTextureID tid)
                {
                    currentMaterialState.TextureID = tid.TextureID;
                    currentMaterialIndex = tid.TextureID;
                }
                else if (pc is PolyChunkMaterial matChunk)
                {
                    if (matChunk.Diffuse.HasValue) currentMaterialState.DiffuseColor = matChunk.Diffuse.Value;
                    currentMaterialState.SourceAlpha = matChunk.SourceAlpha;
                    currentMaterialState.DestinationAlpha = matChunk.DestinationAlpha;
                }
                else if (pc is PolyChunkStrip stripChunk)
                {
                    foreach (var strip in stripChunk.Strips)
                    {
                        if (strip.Indexes == null || strip.Indexes.Length < 3) continue;

                        int stripLen = strip.Indexes.Length;
                        int[] localIndices = new int[stripLen];

                        for (int k = 0; k < stripLen; k++)
                        {
                            int gIdx = strip.Indexes[k];
                            bool hasVtx = gIdx >= 0 && gIdx < globalVertexBuffer.Length && globalVertexBuffer[gIdx].HasValue;

                            Vector3 pos = hasVtx ? globalVertexBuffer[gIdx].Position : Vector3.zero;
                            Vector3 norm = hasVtx ? globalVertexBuffer[gIdx].Normal : Vector3.up;

                            Color32 col = (strip.Colors != null && k < strip.Colors.Length)
                                ? strip.Colors[k]
                                : (hasVtx ? globalVertexBuffer[gIdx].Color : new Color32(255, 255, 255, 255));

                            Vector2 uv = (strip.UVs != null && k < strip.UVs.Length)
                                ? NinjaCoordinateUtility.ToUnityUV(strip.UVs[k])
                                : Vector2.zero;

                            BoneWeight bw = default;
                            if (isSkinned && hasVtx && globalVertexBuffer[gIdx].HasWeight)
                            {
                                bw = globalVertexBuffer[gIdx].BoneWeight;
                                float totalW = bw.weight0 + bw.weight1 + bw.weight2 + bw.weight3;
                                if (totalW > 0.0001f)
                                {
                                    bw.weight0 /= totalW;
                                    bw.weight1 /= totalW;
                                    bw.weight2 /= totalW;
                                    bw.weight3 /= totalW;
                                }
                                else
                                {
                                    bw.boneIndex0 = globalVertexBuffer[gIdx].BoneWeight.boneIndex0;
                                    bw.weight0 = 1.0f;
                                }
                                buffer.HasWeights = true;
                            }

                            localIndices[k] = buffer.Positions.Count;
                            buffer.AddVertex(pos, norm, uv, col, bw);
                        }

                        bool flip = !strip.Reversed;
                        for (int k = 0; k < stripLen - 2; k++)
                        {
                            int v0 = localIndices[k];
                            int v1 = localIndices[k + 1];
                            int v2 = localIndices[k + 2];

                            if (flip)
                                buffer.AddTriangle(currentSubmeshIdx, v0, v2, v1);
                            else
                                buffer.AddTriangle(currentSubmeshIdx, v1, v2, v0);

                            flip = !flip;
                        }
                    }

                    if (buffer.SubmeshTriangles.ContainsKey(currentSubmeshIdx) && buffer.SubmeshTriangles[currentSubmeshIdx].Count > 0)
                    {
                        Material resolved = NinjaMaterialResolver.ResolveMaterial(
                            currentMaterialState,
                            currentMaterialIndex,
                            nodeName,
                            assetName,
                            modelFolder,
                            texNameList,
                            settings,
                            ctx
                        );

                        matList.Add(resolved);
                        currentSubmeshIdx++;
                    }
                }
            }

            Mesh mesh = buffer.BuildMesh(name);
            if (isSkinned && buffer.HasWeights)
            {
                outWeights = buffer.BoneWeights.ToArray();
            }

            materials = matList.ToArray();
            return mesh;
        }

        private static List<PolyChunk> FlattenPolyChunks(List<PolyChunk> chunks)
        {
            List<PolyChunk> result = new List<PolyChunk>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var c = chunks[i];
                if (c is PolyChunkBitsCachePolygonList cache)
                {
                    PolyCache[cache.List] = chunks.Skip(i + 1).ToList();
                    return result;
                }
                else if (c is PolyChunkBitsDrawPolygonList draw)
                {
                    if (PolyCache[draw.List] != null)
                    {
                        result.AddRange(FlattenPolyChunks(PolyCache[draw.List]));
                    }
                }
                else
                {
                    result.Add(c);
                }
            }

            return result;
        }
        #endregion

        #region 3. Ginja & Xinja Attach Decoders
        public static Mesh CreateMeshFromGCAttach(
            GCAttach attach,
            float scale,
            string name,
            string nodeName,
            string assetName,
            string modelFolder,
            string[] texNameList,
            int nodeIndex,
            NinjaImportSettings settings,
            UnityEditor.AssetImporters.AssetImportContext ctx,
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

            MeshBuffer buffer = new MeshBuffer(positions.Count);

            BoneWeight[] weights = null;
            if (attach.VertexSkinData != null && attach.VertexSkinData.Count > 0)
            {
                weights = new BoneWeight[positions.Count];
                buffer.HasWeights = true;
                foreach (var skinSet in attach.VertexSkinData)
                {
                    if (skinSet.ElementType == GCSkinAttribute.StaticWeight)
                    {
                        for (int i = 0; i < skinSet.IndexCount; i++)
                        {
                            int vIdx = skinSet.StartingIndex + i;
                            if (vIdx < weights.Length)
                                weights[vIdx] = new BoneWeight { boneIndex0 = nodeIndex, weight0 = 1.0f };
                        }
                    }
                    else if (skinSet.ElementType is GCSkinAttribute.PartialWeightStart or GCSkinAttribute.PartialWeight)
                    {
                        for (int i = 0; i < skinSet.WeightData.Count; i++)
                        {
                            int vIdx = skinSet.WeightData[i].x;
                            float w = skinSet.WeightData[i].y / 255.0f;
                            if (vIdx < weights.Length)
                            {
                                BoneWeight bw = weights[vIdx];
                                if (bw.weight0 <= 0.0001f) { bw.boneIndex0 = nodeIndex; bw.weight0 = w; }
                                else if (bw.weight1 <= 0.0001f) { bw.boneIndex1 = nodeIndex; bw.weight1 = w; }
                                else if (bw.weight2 <= 0.0001f) { bw.boneIndex2 = nodeIndex; bw.weight2 = w; }
                                else if (bw.weight3 <= 0.0001f) { bw.boneIndex3 = nodeIndex; bw.weight3 = w; }
                                weights[vIdx] = bw;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 p = NinjaCoordinateUtility.ToUnityPosition(positions[i], scale);
                Vector3 n = (i < normals.Count) ? NinjaCoordinateUtility.ToUnityNormal(normals[i]) : Vector3.up;
                Color32 c = (i < colors.Count) ? colors[i] : new Color32(255, 255, 255, 255);
                Vector2 uv = (i < uvs.Count) ? NinjaCoordinateUtility.ToUnityUV(uvs[i]) : Vector2.zero;
                BoneWeight bw = weights != null && i < weights.Length ? weights[i] : default;

                buffer.AddVertex(p, n, uv, c, bw);
            }

            List<GCMesh> allMeshes = new List<GCMesh>();
            allMeshes.AddRange(attach.OpaqueMeshes);
            allMeshes.AddRange(attach.TranslucentMeshes);

            List<Material> matList = new List<Material>();
            int submeshIdx = 0;

            for (int m = 0; m < allMeshes.Count; m++)
            {
                var mesh = allMeshes[m];
                float uvScaleDivisor = 1.0f;

                NJS_MATERIAL matState = new NJS_MATERIAL();
                foreach (var param in mesh.Parameters)
                {
                    if (param.Type == ParameterType.VtxAttrFmt && param.VertexAttribute == GCVertexAttribute.Tex0)
                    {
                        uvScaleDivisor = GCUVScaleHelper.GetDivisor(param.UVScale);
                    }
                    else if (param.Type == ParameterType.Texture)
                    {
                        matState.TextureID = param.TextureID;
                    }
                    else if (param.Type == ParameterType.DiffuseColor)
                    {
                        matState.DiffuseColor = param.Color;
                    }
                    else if (param.Type == ParameterType.BlendAlpha)
                    {
                        matState.SourceAlpha = param.SourceAlpha;
                        matState.DestinationAlpha = param.DestAlpha;
                        matState.Flags |= 0x100000;
                    }
                }

                foreach (var prim in mesh.Primitives)
                {
                    var loops = prim.ToTriangles();
                    for (int i = 0; i < loops.Count - 2; i += 3)
                    {
                        buffer.AddTriangle(submeshIdx, loops[i].PositionIndex, loops[i + 2].PositionIndex, loops[i + 1].PositionIndex);

                        if (uvScaleDivisor > 1.0f)
                        {
                            buffer.UVs[loops[i].PositionIndex] /= uvScaleDivisor;
                            buffer.UVs[loops[i + 1].PositionIndex] /= uvScaleDivisor;
                            buffer.UVs[loops[i + 2].PositionIndex] /= uvScaleDivisor;
                        }
                    }
                }

                if (buffer.SubmeshTriangles.ContainsKey(submeshIdx) && buffer.SubmeshTriangles[submeshIdx].Count > 0)
                {
                    Material resolved = NinjaMaterialResolver.ResolveMaterial(
                        matState,
                        m,
                        nodeName,
                        assetName,
                        modelFolder,
                        texNameList,
                        settings,
                        ctx
                    );

                    matList.Add(resolved);
                    submeshIdx++;
                }
            }

            Mesh uMesh = buffer.BuildMesh(name);
            materials = matList.ToArray();
            boneWeights = weights;
            return uMesh;
        }

        public static Mesh CreateMeshFromXJAttach(
            XJAttach attach,
            float scale,
            string name,
            string nodeName,
            string assetName,
            string modelFolder,
            string[] texNameList,
            NinjaImportSettings settings,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            out Material[] materials)
        {
            materials = Array.Empty<Material>();
            if (attach == null || attach.VertexSets.Count == 0) return null;

            var vSet = attach.VertexSets[0];
            MeshBuffer buffer = new MeshBuffer(vSet.Positions.Count);

            for (int i = 0; i < vSet.Positions.Count; i++)
            {
                Vector3 p = NinjaCoordinateUtility.ToUnityPosition(vSet.Positions[i], scale);
                Vector3 n = (i < vSet.Normals.Count) ? NinjaCoordinateUtility.ToUnityNormal(vSet.Normals[i]) : Vector3.up;
                Color32 c = (i < vSet.Colors.Count) ? vSet.Colors[i] : new Color32(255, 255, 255, 255);
                Vector2 uv = (i < vSet.UVs.Count) ? NinjaCoordinateUtility.ToUnityUV(vSet.UVs[i]) : Vector2.zero;

                buffer.AddVertex(p, n, uv, c);
            }

            List<Material> matList = new List<Material>();
            List<XJMesh> allMeshes = new List<XJMesh>();
            allMeshes.AddRange(attach.OpaqueMeshes);
            allMeshes.AddRange(attach.TranslucentMeshes);

            int submeshIdx = 0;
            for (int m = 0; m < allMeshes.Count; m++)
            {
                var mesh = allMeshes[m];
                List<int> tris = mesh.TriangulateStrips();
                if (tris.Count >= 3)
                {
                    for (int t = 0; t < tris.Count - 2; t += 3)
                    {
                        buffer.AddTriangle(submeshIdx, tris[t], tris[t + 1], tris[t + 2]);
                    }

                    Material resolved = NinjaMaterialResolver.ResolveMaterial(
                        mesh.Material,
                        m,
                        nodeName,
                        assetName,
                        modelFolder,
                        texNameList,
                        settings,
                        ctx
                    );

                    matList.Add(resolved);
                    submeshIdx++;
                }
            }

            Mesh uMesh = buffer.BuildMesh(name);
            materials = matList.ToArray();
            return uMesh;
        }
        #endregion
    }
}