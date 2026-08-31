using System;
using UnityEngine;

namespace UnityNinja
{
    /// <summary>
    /// Centralized coordinate space and math transformations between Sega Ninja (right-handed)
    /// and Unity (left-handed) coordinate systems.
    /// </summary>
    public static class NinjaCoordinateUtility
    {
        /// <summary>
        /// Maps Ninja Right-Handed coordinates (+X Right, +Y Up, +Z Forward) to Unity Left-Handed space.
        /// Inverts X: (X -> -X).
        /// </summary>
        public static Vector3 ToUnityPosition(Vector3 position, float scale = 1.0f)
        {
            float x = float.IsNaN(position.x) || float.IsInfinity(position.x) ? 0f : -position.x * scale;
            float y = float.IsNaN(position.y) || float.IsInfinity(position.y) ? 0f : position.y * scale;
            float z = float.IsNaN(position.z) || float.IsInfinity(position.z) ? 0f : position.z * scale;
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Maps Ninja Euler rotation (Pitch, Yaw, Roll) to Unity Euler degrees.
        /// Inverts Yaw and Roll: (X -> X, Y -> -Y, Z -> -Z).
        /// </summary>
        public static Vector3 ToUnityEuler(Vector3 eulerDegrees)
        {
            float x = float.IsNaN(eulerDegrees.x) || float.IsInfinity(eulerDegrees.x) ? 0f : eulerDegrees.x;
            float y = float.IsNaN(eulerDegrees.y) || float.IsInfinity(eulerDegrees.y) ? 0f : -eulerDegrees.y;
            float z = float.IsNaN(eulerDegrees.z) || float.IsInfinity(eulerDegrees.z) ? 0f : -eulerDegrees.z;
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Maps a normal vector from Ninja space to Unity space (X -> -X).
        /// </summary>
        public static Vector3 ToUnityNormal(Vector3 normal)
        {
            float x = float.IsNaN(normal.x) || float.IsInfinity(normal.x) ? 0f : -normal.x;
            float y = float.IsNaN(normal.y) || float.IsInfinity(normal.y) ? 1f : normal.y;
            float z = float.IsNaN(normal.z) || float.IsInfinity(normal.z) ? 0f : normal.z;
            return new Vector3(x, y, z).normalized;
        }

        /// <summary>
        /// Maps a tangent vector from Ninja space to Unity space (X -> -X).
        /// </summary>
        public static Vector4 ToUnityTangent(Vector3 tangent, float w = 1.0f)
        {
            float x = float.IsNaN(tangent.x) || float.IsInfinity(tangent.x) ? 1f : -tangent.x;
            float y = float.IsNaN(tangent.y) || float.IsInfinity(tangent.y) ? 0f : tangent.y;
            float z = float.IsNaN(tangent.z) || float.IsInfinity(tangent.z) ? 0f : tangent.z;
            Vector3 tanScaled = new Vector3(x, y, z).normalized;
            return new Vector4(tanScaled.x, tanScaled.y, tanScaled.z, w);
        }

        /// <summary>
        /// Maps texture coordinates from Dreamcast/Direct3D top-left origin (0,0) to Unity bottom-left origin (0,0).
        /// (U -> U, V -> 1.0 - V).
        /// </summary>
        public static Vector2 ToUnityUV(Vector2 uv)
        {
            float u = float.IsNaN(uv.x) || float.IsInfinity(uv.x) ? 0f : uv.x;
            float v = float.IsNaN(uv.y) || float.IsInfinity(uv.y) ? 0f : 1.0f - uv.y;
            return new Vector2(u, v);
        }

        /// <summary>
        /// Transforms UV offset values for Unity shader properties.
        /// Inverts V offset (U -> U, V -> -V).
        /// </summary>
        public static Vector2 ToUnityUVOffset(Vector2 offset)
        {
            float u = float.IsNaN(offset.x) || float.IsInfinity(offset.x) ? 0f : offset.x;
            float v = float.IsNaN(offset.y) || float.IsInfinity(offset.y) ? 0f : -offset.y;
            return new Vector2(u, v);
        }

        /// <summary>
        /// Transforms bounding box min/max extents from Ninja space to Unity space with scale,
        /// ensuring min is strictly less than max after X-axis inversion.
        /// </summary>
        public static void ToUnityBounds(Vector3 min, Vector3 max, float scale, out Vector3 unityMin, out Vector3 unityMax)
        {
            Vector3 p1 = ToUnityPosition(min, scale);
            Vector3 p2 = ToUnityPosition(max, scale);
            unityMin = Vector3.Min(p1, p2);
            unityMax = Vector3.Max(p1, p2);
        }
    }
}