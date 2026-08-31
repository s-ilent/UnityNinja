using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityNinja
{
    [Serializable]
    public class AnimModelData
    {
        public Dictionary<int, Vector3> Position = new Dictionary<int, Vector3>();
        public Dictionary<int, NinjaRotation> Rotation = new Dictionary<int, NinjaRotation>();
        public Dictionary<int, Vector3> Scale = new Dictionary<int, Vector3>();
        public Dictionary<int, Vector3> Vector = new Dictionary<int, Vector3>();
        public Dictionary<int, Vector3> Target = new Dictionary<int, Vector3>();
        public Dictionary<int, int> Roll = new Dictionary<int, int>();
        public Dictionary<int, int> Angle = new Dictionary<int, int>();
        public Dictionary<int, Color32> Color = new Dictionary<int, Color32>();
        public Dictionary<int, float> Intensity = new Dictionary<int, float>();
        public Dictionary<int, Quaternion> Quaternion = new Dictionary<int, Quaternion>();

        public string PositionName { get; set; }
        public string RotationName { get; set; }
        public string ScaleName { get; set; }
        public string VectorName { get; set; }

        public bool HasData => Position.Count > 0 || Rotation.Count > 0 || Scale.Count > 0 ||
                               Vector.Count > 0 || Target.Count > 0 || Roll.Count > 0 ||
                               Angle.Count > 0 || Color.Count > 0 || Intensity.Count > 0 ||
                               Quaternion.Count > 0;
    }
}