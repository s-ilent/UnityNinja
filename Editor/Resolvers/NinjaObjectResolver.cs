using System.Collections.Generic;
using UnityEngine;
using UnityEditor.AssetImporters;
using UnityNinja;
using UnityNinja.GC;
using UnityNinja.XJ;

namespace UnityNinja.Editor
{
    public static class NinjaObjectResolver
    {
        public static GameObject ResolveHierarchy(
            NJS_OBJECT rootObject,
            string rootName,
            float scale,
            bool generateColliders,
            AssetImportContext ctx,
            out List<Transform> nodeTransforms)
        {
            nodeTransforms = new List<Transform>();
            if (rootObject == null) return null;

            GameObject rootGO = new GameObject(rootName);

            // Persistent 32K hardware vertex pool for Chunk models across the hierarchy
            ChunkVertexEntry[] globalChunkVertexBuffer = new ChunkVertexEntry[32768];

            bool isSkinnedHierarchy = HasSkinning(rootObject);

            BuildNode(rootObject, rootGO.transform, scale, generateColliders, ctx, nodeTransforms, globalChunkVertexBuffer);

            if (isSkinnedHierarchy)
            {
                SetupSkeletonBindPoses(rootGO, nodeTransforms);
            }

            return rootGO;
        }

        public static bool HasSkinning(NJS_OBJECT obj)
        {
            foreach (var node in obj.EnumerateNodes())
            {
                if (node.Attach != null && node.Attach.HasWeight)
                    return true;
            }
            return false;
        }

        private static void BuildNode(
            NJS_OBJECT node,
            Transform parentTransform,
            float scale,
            bool generateColliders,
            AssetImportContext ctx,
            List<Transform> nodeTransforms,
            ChunkVertexEntry[] globalChunkVertexBuffer)
        {
            if (node == null) return;

            GameObject nodeGO = new GameObject(node.Name);
            nodeGO.transform.SetParent(parentTransform, false);

            nodeGO.transform.localPosition = NinjaCoordinateUtility.ToUnityPosition(node.Position, scale);
            nodeGO.transform.localEulerAngles = NinjaCoordinateUtility.ToUnityEuler(node.Rotation);
            nodeGO.transform.localScale = (node.Scale == Vector3.zero) ? Vector3.one : node.Scale;

            nodeTransforms.Add(nodeGO.transform);

            // Mesh Attachment
            if (node.Attach != null && !node.SkipDraw)
            {
                Mesh mesh = null;
                Material[] mats = null;
                BoneWeight[] weights = null;

                if (node.Attach is BasicAttach basic)
                {
                    mesh = NinjaMeshResolver.CreateMeshFromBasicAttach(basic, scale, $"{node.Name}_Mesh", out mats);
                }
                else if (node.Attach is ChunkAttach chunk)
                {
                    mesh = NinjaMeshResolver.CreateMeshFromChunkAttach(chunk, scale, $"{node.Name}_Mesh", globalChunkVertexBuffer, out mats);
                }
                else if (node.Attach is GCAttach gc)
                {
                    mesh = NinjaMeshResolver.CreateMeshFromGCAttach(gc, scale, $"{node.Name}_Mesh", out mats, out weights);
                }
                else if (node.Attach is XJAttach xj)
                {
                    mesh = NinjaMeshResolver.CreateMeshFromXJAttach(xj, scale, $"{node.Name}_Mesh", out mats);
                }

                if (mesh != null)
                {
                    ctx?.AddObjectToAsset($"Mesh_{node.Name}", mesh);

                    if (weights != null && weights.Length > 0)
                    {
                        SkinnedMeshRenderer smr = nodeGO.AddComponent<SkinnedMeshRenderer>();
                        smr.sharedMesh = mesh;
                        smr.sharedMaterials = mats;
                    }
                    else
                    {
                        MeshFilter mf = nodeGO.AddComponent<MeshFilter>();
                        mf.sharedMesh = mesh;

                        MeshRenderer mr = nodeGO.AddComponent<MeshRenderer>();
                        mr.sharedMaterials = mats;

                        if (generateColliders)
                        {
                            MeshCollider mc = nodeGO.AddComponent<MeshCollider>();
                            mc.sharedMesh = mesh;
                        }
                    }
                }
            }

            if (!node.SkipChildren)
            {
                foreach (var child in node.Children)
                {
                    BuildNode(child, nodeGO.transform, scale, generateColliders, ctx, nodeTransforms, globalChunkVertexBuffer);
                }
            }
        }

        private static void SetupSkeletonBindPoses(GameObject rootGO, List<Transform> nodeTransforms)
        {
            Transform[] bones = nodeTransforms.ToArray();
            Matrix4x4[] bindposes = new Matrix4x4[bones.Length];

            for (int b = 0; b < bones.Length; b++)
            {
                bindposes[b] = bones[b].worldToLocalMatrix * rootGO.transform.localToWorldMatrix;
            }

            foreach (var smr in rootGO.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh != null)
                {
                    smr.sharedMesh.bindposes = bindposes;
                    smr.bones = bones;
                    smr.rootBone = bones.Length > 0 ? bones[0] : rootGO.transform;
                }
            }
        }
    }
}