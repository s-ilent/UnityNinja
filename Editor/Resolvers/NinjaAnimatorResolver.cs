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
            string assetName,
            string assetPath,
            float scale,
            AssetImportContext ctx)
        {
            if (rootGO == null || rootModel == null) return;

            string[] paths = NinjaMotionResolver.ComputeNodeHierarchyPaths(rootModel);
            List<AnimationClip> loadedClips = new List<AnimationClip>();

            string baseDir = Path.GetDirectoryName(assetPath);

            // Discover companion animation files
            foreach (string ext in MotionExtensions)
            {
                string candidate = Path.Combine(baseDir, assetName + ext).Replace('\\', '/');
                if (File.Exists(candidate))
                {
                    try
                    {
                        byte[] rawBytes = File.ReadAllBytes(candidate);
                        NinjaBinaryFile motFile = new NinjaBinaryFile(rawBytes);

                        for (int i = 0; i < motFile.Motions.Count; i++)
                        {
                            string clipName = $"{assetName}_Motion_{i}";
                            AnimationClip clip = NinjaMotionResolver.ResolveMotion(
                                motFile.Motions[i],
                                clipName,
                                scale,
                                paths,
                                nodeTransforms
                            );

                            if (clip != null)
                            {
                                ctx.DependsOnSourceAsset(candidate);
                                ctx.AddObjectToAsset($"Motion_{i}", clip);
                                loadedClips.Add(clip);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[NinjaAnimatorResolver] Failed loading motion {candidate}: {ex.Message}");
                    }
                }
            }

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
    }
}