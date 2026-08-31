using System.IO;
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
            NinjaImportSettings settings,
            string[] texNameList,
            AssetImportContext ctx,
            out List<Transform> nodeTransforms)
        {
            nodeTransforms = new List<Transform>();
            if (rootObject == null) return null;

            GameObject rootGO = new GameObject(rootName);
            string modelFolder = (ctx != null && !string.IsNullOrEmpty(ctx.assetPath)) ? Path.GetDirectoryName(ctx.assetPath) : "";

            ChunkVertexEntry[] globalChunkVertexBuffer = new ChunkVertexEntry[65536];
            bool isSkinnedHierarchy = HasSkinning(rootObject);

            NinjaMaterialResolver.ResetMaterialCache();

            BuildNode(rootObject, rootGO.transform, rootName, modelFolder, settings, texNameList, ctx, nodeTransforms, globalChunkVertexBuffer, Matrix4x4.identity, isSkinnedHierarchy, 0);

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

        private static int BuildNode(
            NJS_OBJECT node,
            Transform parentTransform,
            string assetName,
            string modelFolder,
            NinjaImportSettings settings,
            string[] texNameList,
            AssetImportContext ctx,
            List<Transform> nodeTransforms,
            ChunkVertexEntry[] globalChunkVertexBuffer,
            Matrix4x4 parentMatrix,
            bool isSkinnedHierarchy,
            int nodeIndex)
        {
            if (node == null) return nodeIndex;

            GameObject nodeGO = new GameObject(node.Name);
            nodeGO.transform.SetParent(parentTransform, false);

            Vector3 localPos = NinjaCoordinateUtility.ToUnityPosition(node.Position, settings.Scale);
            Quaternion localRot = NinjaCoordinateUtility.ToUnityRotation(node.Rotation, node.RotateZYX);
            Vector3 localScale = (node.Scale == Vector3.zero) ? Vector3.one : node.Scale;

            nodeGO.transform.localPosition = localPos;
            nodeGO.transform.localRotation = localRot;
            nodeGO.transform.localScale = localScale;

            Matrix4x4 localMat = Matrix4x4.TRS(localPos, localRot, localScale);
            Matrix4x4 currentModelMatrix = parentMatrix * localMat;

            int currentNodeIdx = nodeTransforms.Count;
            nodeTransforms.Add(nodeGO.transform);
            int nextIndex = nodeIndex + 1;

            // Mesh Attachment & Material Assignment
            if (node.Attach != null && !node.SkipDraw)
            {
                Mesh mesh = null;
                Material[] mats = null;
                BoneWeight[] weights = null;

                if (node.Attach is BasicAttach basic)
                {
                    mesh = NinjaMeshResolver.CreateMeshFromBasicAttach(
                        basic,
                        settings.Scale,
                        $"{node.Name}_Mesh",
                        node.Name,
                        assetName,
                        modelFolder,
                        texNameList,
                        settings,
                        ctx,
                        out mats
                    );
                }
                else if (node.Attach is ChunkAttach chunk)
                {
                    mesh = NinjaMeshResolver.CreateMeshFromChunkAttach(
                        chunk,
                        settings.Scale,
                        $"{node.Name}_Mesh",
                        node.Name,
                        assetName,
                        modelFolder,
                        texNameList,
                        globalChunkVertexBuffer,
                        currentNodeIdx,
                        currentModelMatrix,
                        isSkinnedHierarchy,
                        settings,
                        ctx,
                        out mats,
                        out weights
                    );
                    mesh = mesh;
                }
                else if (node.Attach is GCAttach gc)
                {
                    mesh = NinjaMeshResolver.CreateMeshFromGCAttach(
                        gc,
                        settings.Scale,
                        $"{node.Name}_Mesh",
                        node.Name,
                        assetName,
                        modelFolder,
                        texNameList,
                        currentNodeIdx,
                        settings,
                        ctx,
                        out mats,
                        out weights
                    );
                }
                else if (node.Attach is XJAttach xj)
                {
                    mesh = NinjaMeshResolver.CreateMeshFromXJAttach(
                        xj,
                        settings.Scale,
                        $"{node.Name}_Mesh",
                        node.Name,
                        assetName,
                        modelFolder,
                        texNameList,
                        settings,
                        ctx,
                        out mats
                    );
                }
                
                if (mesh != null)
                {
                    ctx?.AddObjectToAsset($"Mesh_{node.Name}", mesh);

                    if (weights != null && weights.Length > 0)
                    {
                        // Transform mesh vertices from Root Model Space into nodeGO local space so rest-pose skinning is exact
                        Matrix4x4 modelToNode = currentModelMatrix.inverse;
                        Vector3[] localVerts = mesh.vertices;
                        Vector3[] localNorms = mesh.normals;
                        for (int v = 0; v < localVerts.Length; v++)
                        {
                            localVerts[v] = modelToNode.MultiplyPoint3x4(localVerts[v]);
                            localNorms[v] = modelToNode.MultiplyVector(localNorms[v]).normalized;
                        }
                        mesh.vertices = localVerts;
                        mesh.normals = localNorms;
                        mesh.RecalculateBounds();

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

                        if (settings.GenerateMeshColliders)
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
                    nextIndex = BuildNode(child, nodeGO.transform, assetName, modelFolder, settings, texNameList, ctx, nodeTransforms, globalChunkVertexBuffer, currentModelMatrix, isSkinnedHierarchy, nextIndex);
                }
            }

            if (node.Parent == null && node.Sibling != null)
            {
                nextIndex = BuildNode(node.Sibling, parentTransform, assetName, modelFolder, settings, texNameList, ctx, nodeTransforms, globalChunkVertexBuffer, parentMatrix, isSkinnedHierarchy, nextIndex);
            }

            return nextIndex;
        }

        private static void SetupSkeletonBindPoses(GameObject rootGO, List<Transform> nodeTransforms)
        {
            Transform[] bones = nodeTransforms.ToArray();

            foreach (var smr in rootGO.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh != null)
                {
                    Matrix4x4[] bindposes = new Matrix4x4[bones.Length];
                    for (int b = 0; b < bones.Length; b++)
                    {
                        // Transform from the SkinnedMeshRenderer GameObject space into bone space
                        bindposes[b] = bones[b].worldToLocalMatrix * smr.transform.localToWorldMatrix;
                    }

                    smr.sharedMesh.bindposes = bindposes;
                    smr.bones = bones;
                    smr.rootBone = bones.Length > 0 ? bones[0] : rootGO.transform;
                }
            }
        }
    }
}