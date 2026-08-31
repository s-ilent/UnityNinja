using System;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class NinjaVertex
    {
        public float x;
        public float y;
        public float z;

        public const int Size = 12;

        public NinjaVertex() { }
        public NinjaVertex(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public NinjaVertex(byte[] file, int address)
        {
            if (address + 12 > file.Length) return;
            x = ByteConverter.ToSingle(file, address);
            y = ByteConverter.ToSingle(file, address + 4);
            z = ByteConverter.ToSingle(file, address + 8);
        }

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }
}