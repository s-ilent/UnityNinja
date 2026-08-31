using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.AssetImporters;
using UnityNinja;

namespace UnityNinja.Editor
{
    public static class NinjaAnimatorResolver
    {
        private static readonly string[] MotionExtensions = { ".njm", ".gjm", ".xjm", ".nam" };

        public static void SetupModelAnimations(
            NJS_OBJECT rootModel,
            GameObject rootGO,
            List<Transform> nodeTransforms,
            List<NJS_MOTION> embeddedMotions,
            string assetName,
            string assetPath,
            float scale,
            AssetImportContext ctx)
        {
            if (rootGO == null || rootModel == null) return;

            string[] paths = NinjaMotionResolver.ComputeNodeHierarchyPaths(rootModel);
            List<AnimationClip> loadedClips = new List<AnimationClip>();

            // 1. Process Embedded Animations (.nj file containing NMDM / NSSM chunks)
            if (embeddedMotions != null && embeddedMotions.Count > 0)
            {
                for (int i = 0; i < embeddedMotions.Count; i++)
                {
                    string clipName = $"{assetName}_Motion_{i}";
                    AnimationClip clip = NinjaMotionResolver.ResolveMotion(
                        embeddedMotions[i],
                        clipName,
                        scale,
                        paths,
                        nodeTransforms
                    );

                    if (clip != null)
                    {
                        ctx.AddObjectToAsset($"Embedded_Motion_{i}", clip);
                        loadedClips.Add(clip);
                    }
                }
            }

            // 2. Discover Companion Animation Files in same directory
            string baseDir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
            {
                foreach (string ext in MotionExtensions)
                {
                    string candidate = Path.Combine(baseDir, assetName + ext).Replace('\\', '/');
                    if (File.Exists(candidate))
                    {
                        LoadClipsFromFile(candidate, assetName, scale, paths, nodeTransforms, loadedClips, ctx);
                    }
                }
            }

            // 3. Build and Attach Animator Controller
            if (loadedClips.Count > 0)
            {
                Animator animator = rootGO.AddComponent<Animator>();

                AnimatorController controller = new AnimatorController { name = $"{assetName}_Controller" };
                ctx.AddObjectToAsset("AnimatorController", controller);

                controller.AddLayer("Base Layer");
                var sm = controller.layers[0].stateMachine;

                if (sm != null)
                {
                    ctx.AddObjectToAsset("BaseStateMachine", sm);
                    AnimatorState st = sm.AddState(loadedClips[0].name);
                    st.motion = loadedClips[0];
                    sm.defaultState = st;
                    ctx.AddObjectToAsset("DefaultState", st);
                }

                animator.runtimeAnimatorController = controller;
            }
        }

        private static void LoadClipsFromFile(
            string filePath,
            string assetName,
            float scale,
            string[] paths,
            List<Transform> nodeTransforms,
            List<AnimationClip> loadedClips,
            AssetImportContext ctx)
        {
            try
            {
                byte[] rawBytes = File.ReadAllBytes(filePath);
                NinjaBinaryFile motFile = new NinjaBinaryFile(rawBytes);

                for (int i = 0; i < motFile.Motions.Count; i++)
                {
                    string clipName = $"{Path.GetFileNameWithoutExtension(filePath)}_{i}";
                    AnimationClip clip = NinjaMotionResolver.ResolveMotion(
                        motFile.Motions[i],
                        clipName,
                        scale,
                        paths,
                        nodeTransforms
                    );

                    if (clip != null)
                    {
                        ctx.DependsOnSourceAsset(filePath);
                        ctx.AddObjectToAsset($"Motion_{loadedClips.Count}_{clipName}", clip);
                        loadedClips.Add(clip);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NinjaAnimatorResolver] Failed loading motion {filePath}: {ex.Message}");
            }
        }
    }
}