using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityNinja;

namespace UnityNinja.Editor
{
    public struct TrackBindingDef
    {
        public AnimFlags Mask;
        public Type ComponentType;
        public string PropertyName;
        public string GroupKey;
        public int ChannelIndex;
        public float DefaultRestValue;
        public bool InvertSign;
    }

    public readonly struct PropertyKey : IEquatable<PropertyKey>
    {
        public readonly string TargetPath;
        public readonly Type ComponentType;
        public readonly string PropertyName;

        public PropertyKey(string targetPath, Type componentType, string propertyName)
        {
            TargetPath = targetPath ?? "";
            ComponentType = componentType;
            PropertyName = propertyName ?? "";
        }

        public bool Equals(PropertyKey other) =>
            TargetPath == other.TargetPath &&
            ComponentType == other.ComponentType &&
            PropertyName == other.PropertyName;

        public override bool Equals(object obj) => obj is PropertyKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(TargetPath, ComponentType, PropertyName);
    }

    public static class NinjaMotionResolver
    {
        // ----------------------------------------------------------------------
        // Data-Driven Binding Definitions Table (DRY design)
        // ----------------------------------------------------------------------
        public static readonly TrackBindingDef[] AllTrackBindings = new[]
        {
            // Position (X: Inverted, Y: Normal, Z: Normal)
            new TrackBindingDef { Mask = AnimFlags.Position, ComponentType = typeof(Transform), PropertyName = "localPosition.x", GroupKey = "localPosition", ChannelIndex = 0, DefaultRestValue = 0f, InvertSign = true },
            new TrackBindingDef { Mask = AnimFlags.Position, ComponentType = typeof(Transform), PropertyName = "localPosition.y", GroupKey = "localPosition", ChannelIndex = 1, DefaultRestValue = 0f, InvertSign = false },
            new TrackBindingDef { Mask = AnimFlags.Position, ComponentType = typeof(Transform), PropertyName = "localPosition.z", GroupKey = "localPosition", ChannelIndex = 2, DefaultRestValue = 0f, InvertSign = false },

            // Euler Angles (Pitch: Normal, Yaw: Inverted, Roll: Inverted)
            new TrackBindingDef { Mask = AnimFlags.Rotation, ComponentType = typeof(Transform), PropertyName = "localEulerAnglesRaw.x", GroupKey = "localEulerAnglesRaw", ChannelIndex = 0, DefaultRestValue = 0f, InvertSign = false },
            new TrackBindingDef { Mask = AnimFlags.Rotation, ComponentType = typeof(Transform), PropertyName = "localEulerAnglesRaw.y", GroupKey = "localEulerAnglesRaw", ChannelIndex = 1, DefaultRestValue = 0f, InvertSign = true },
            new TrackBindingDef { Mask = AnimFlags.Rotation, ComponentType = typeof(Transform), PropertyName = "localEulerAnglesRaw.z", GroupKey = "localEulerAnglesRaw", ChannelIndex = 2, DefaultRestValue = 0f, InvertSign = true },

            // Scale (XYZ)
            new TrackBindingDef { Mask = AnimFlags.Scale, ComponentType = typeof(Transform), PropertyName = "localScale.x", GroupKey = "localScale", ChannelIndex = 0, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { Mask = AnimFlags.Scale, ComponentType = typeof(Transform), PropertyName = "localScale.y", GroupKey = "localScale", ChannelIndex = 1, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { Mask = AnimFlags.Scale, ComponentType = typeof(Transform), PropertyName = "localScale.z", GroupKey = "localScale", ChannelIndex = 2, DefaultRestValue = 1f, InvertSign = false }
        };

        public static float Bams16ToUnrolledDegrees(short rawValue, ref long accumBams, ref bool isFirst)
        {
            if (isFirst)
            {
                accumBams = rawValue;
                isFirst = false;
            }
            else
            {
                int delta = rawValue - (int)(accumBams & 0xFFFF);
                delta = (delta + 32768) % 65536 - 32768;
                accumBams += delta;
            }
            return (float)(accumBams * (180.0 / 32768.0));
        }

        public static AnimationClip ResolveMotion(
            NJS_MOTION motion,
            string clipName,
            float scale,
            string[] nodeHierarchyTargets,
            List<Transform> nodeTransforms = null)
        {
            if (motion == null || motion.Models.Count == 0) return null;

            AnimationClip clip = new AnimationClip { name = clipName };
            float framerate = 60.0f;
            float maxTime = Mathf.Max(0.1f, motion.Frames / framerate);

            Dictionary<PropertyKey, List<Keyframe>> propertyKeyframes = new Dictionary<PropertyKey, List<Keyframe>>();

            foreach (var kvp in motion.Models)
            {
                int nodeIdx = kvp.Key;
                AnimModelData data = kvp.Value;

                string targetPath = (nodeHierarchyTargets != null && nodeIdx >= 0 && nodeIdx < nodeHierarchyTargets.Length)
                    ? nodeHierarchyTargets[nodeIdx]
                    : $"Node_{nodeIdx:0000}";

                // 1. Position Channels
                if (data.Position.Count > 0)
                {
                    PropertyKey kX = new PropertyKey(targetPath, typeof(Transform), "localPosition.x");
                    PropertyKey kY = new PropertyKey(targetPath, typeof(Transform), "localPosition.y");
                    PropertyKey kZ = new PropertyKey(targetPath, typeof(Transform), "localPosition.z");

                    foreach (var kf in data.Position)
                    {
                        float t = kf.Key / framerate;
                        Vector3 p = NinjaCoordinateUtility.ToUnityPosition(kf.Value, scale);
                        AddKey(propertyKeyframes, kX, new Keyframe(t, p.x));
                        AddKey(propertyKeyframes, kY, new Keyframe(t, p.y));
                        AddKey(propertyKeyframes, kZ, new Keyframe(t, p.z));
                    }
                }

                // 2. Rotation Channels (BAMS Unrolling)
                if (data.Rotation.Count > 0)
                {
                    PropertyKey kX = new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.x");
                    PropertyKey kY = new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.y");
                    PropertyKey kZ = new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.z");

                    long accX = 0, accY = 0, accZ = 0;
                    bool fX = true, fY = true, fZ = true;

                    foreach (var kf in data.Rotation)
                    {
                        float t = kf.Key / framerate;
                        float rx = Bams16ToUnrolledDegrees((short)kf.Value.x, ref accX, ref fX);
                        float ry = -Bams16ToUnrolledDegrees((short)kf.Value.y, ref accY, ref fY);
                        float rz = -Bams16ToUnrolledDegrees((short)kf.Value.z, ref accZ, ref fZ);

                        AddKey(propertyKeyframes, kX, new Keyframe(t, rx));
                        AddKey(propertyKeyframes, kY, new Keyframe(t, ry));
                        AddKey(propertyKeyframes, kZ, new Keyframe(t, rz));
                    }
                }

                // 3. Scale Channels
                if (data.Scale.Count > 0)
                {
                    PropertyKey kX = new PropertyKey(targetPath, typeof(Transform), "localScale.x");
                    PropertyKey kY = new PropertyKey(targetPath, typeof(Transform), "localScale.y");
                    PropertyKey kZ = new PropertyKey(targetPath, typeof(Transform), "localScale.z");

                    foreach (var kf in data.Scale)
                    {
                        float t = kf.Key / framerate;
                        AddKey(propertyKeyframes, kX, new Keyframe(t, kf.Value.x));
                        AddKey(propertyKeyframes, kY, new Keyframe(t, kf.Value.y));
                        AddKey(propertyKeyframes, kZ, new Keyframe(t, kf.Value.z));
                    }
                }
            }

            // Fill companion channels to prevent frozen axes in Unity
            FillMissingCompanionChannels(propertyKeyframes, nodeTransforms, nodeHierarchyTargets, maxTime);

            foreach (var kvp in propertyKeyframes)
            {
                AnimationCurve curve = BuildMergedCurve(kvp.Value, kvp.Key, maxTime);
                if (curve != null && curve.length > 0)
                {
                    clip.SetCurve(kvp.Key.TargetPath, kvp.Key.ComponentType, kvp.Key.PropertyName, curve);
                }
            }

            clip.wrapMode = WrapMode.Loop;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            return clip;
        }

        private static void AddKey(Dictionary<PropertyKey, List<Keyframe>> dict, PropertyKey key, Keyframe kf)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<Keyframe>();
                dict[key] = list;
            }
            list.Add(kf);
        }

        private static void FillMissingCompanionChannels(
            Dictionary<PropertyKey, List<Keyframe>> propertyKeyframes,
            List<Transform> nodeTransforms,
            string[] nodeHierarchyTargets,
            float maxTime)
        {
            var keys = new List<PropertyKey>(propertyKeyframes.Keys);
            HashSet<string> processedGroups = new HashSet<string>();

            foreach (var key in keys)
            {
                string group = key.PropertyName.Split('.')[0];
                string groupKey = $"{key.TargetPath}|{key.ComponentType.Name}|{group}";
                if (!processedGroups.Add(groupKey)) continue;

                Transform nodeTr = FindNodeTransform(key.TargetPath, nodeTransforms, nodeHierarchyTargets);

                foreach (var b in AllTrackBindings)
                {
                    if (b.GroupKey == group && b.ComponentType == key.ComponentType)
                    {
                        PropertyKey channelKey = new PropertyKey(key.TargetPath, b.ComponentType, b.PropertyName);
                        if (!propertyKeyframes.ContainsKey(channelKey))
                        {
                            float defVal = GetDefaultChannelValue(b, nodeTr);
                            var list = new List<Keyframe>
                            {
                                new Keyframe(0f, defVal, 0f, 0f),
                                new Keyframe(maxTime, defVal, 0f, 0f)
                            };
                            propertyKeyframes[channelKey] = list;
                        }
                    }
                }
            }
        }

        private static float GetDefaultChannelValue(TrackBindingDef b, Transform tr)
        {
            if (tr == null) return b.DefaultRestValue;
            return b.GroupKey switch
            {
                "localPosition" => b.ChannelIndex switch { 0 => tr.localPosition.x, 1 => tr.localPosition.y, _ => tr.localPosition.z },
                "localEulerAnglesRaw" => b.ChannelIndex switch { 0 => tr.localEulerAngles.x, 1 => tr.localEulerAngles.y, _ => tr.localEulerAngles.z },
                "localScale" => b.ChannelIndex switch { 0 => tr.localScale.x, 1 => tr.localScale.y, _ => tr.localScale.z },
                _ => b.DefaultRestValue
            };
        }

        private static Transform FindNodeTransform(string path, List<Transform> nodeTransforms, string[] targets)
        {
            if (nodeTransforms == null || targets == null) return null;
            for (int i = 0; i < targets.Length && i < nodeTransforms.Count; i++)
            {
                if (targets[i] == path) return nodeTransforms[i];
            }
            return null;
        }

        private static AnimationCurve BuildMergedCurve(List<Keyframe> keys, PropertyKey key, float maxTime)
        {
            if (keys == null || keys.Count == 0) return null;

            keys.Sort((a, b) => a.time.CompareTo(b.time));

            // Deduplicate keys at the same timestamp
            List<Keyframe> unique = new List<Keyframe>();
            for (int i = 0; i < keys.Count; i++)
            {
                if (unique.Count > 0 && Mathf.Abs(unique[unique.Count - 1].time - keys[i].time) < 0.0001f)
                    continue;
                unique.Add(keys[i]);
            }

            // Calculate linear/smooth tangents
            if (unique.Count >= 2)
            {
                for (int i = 0; i < unique.Count - 1; i++)
                {
                    float dt = unique[i + 1].time - unique[i].time;
                    if (dt > 0.00001f)
                    {
                        float slope = (unique[i + 1].value - unique[i].value) / dt;
                        Keyframe cur = unique[i];
                        cur.outTangent = slope;
                        unique[i] = cur;

                        Keyframe nxt = unique[i + 1];
                        nxt.inTangent = slope;
                        unique[i + 1] = nxt;
                    }
                }
            }

            // Boundary Anchoring (t = 0.0s, t = maxTime)
            if (unique[0].time > 0.0001f)
            {
                Keyframe first = unique[0];
                unique.Insert(0, new Keyframe(0f, first.value, first.inTangent, first.outTangent));
            }

            if (maxTime > 0.001f && unique[unique.Count - 1].time < maxTime - 0.0001f)
            {
                Keyframe last = unique[unique.Count - 1];
                unique.Add(new Keyframe(maxTime, last.value, last.inTangent, last.outTangent));
            }

            return new AnimationCurve(unique.ToArray());
        }

        public static string[] ComputeNodeHierarchyPaths(NJS_OBJECT root)
        {
            if (root == null) return Array.Empty<string>();
            List<string> paths = new List<string>();
            TraversePaths(root, "", paths);
            return paths.ToArray();
        }

        private static void TraversePaths(NJS_OBJECT node, string parentPath, List<string> list)
        {
            if (node == null) return;
            string currentPath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath}/{node.Name}";
            list.Add(currentPath);

            foreach (var child in node.Children)
            {
                TraversePaths(child, currentPath, list);
            }
        }
    }
}