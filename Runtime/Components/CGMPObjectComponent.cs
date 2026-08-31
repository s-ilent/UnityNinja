using System;
using UnityEngine;

namespace UnityNinja
{
    [DisallowMultipleComponent]
    public class CGMPObjectComponent : MonoBehaviour
    {
        public int objectID;
        public uint flags;
        public Vector3 originalPosition;
        public Vector3 originalRotation;
        public Vector3 originalScale;
    }
}