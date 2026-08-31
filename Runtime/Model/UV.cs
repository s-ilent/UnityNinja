using System;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class NinjaUV
    {
        public float u;
        public float v;

        public NinjaUV() { }
        public NinjaUV(float u, float v) { this.u = u; this.v = v; }

        public NinjaUV(byte[] file, int address, bool uvh = false, bool isChunk = false)
        {
            if (address + 4 > file.Length) return;

            double divisor = isChunk ? (uvh ? 1024.0 : 256.0) : (uvh ? 1023.0 : 255.0);
            u = (float)(ByteConverter.ToInt16(file, address) / divisor);
            v = (float)(ByteConverter.ToInt16(file, address + 2) / divisor);
        }

        public Vector2 ToVector2() => new Vector2(u, v);
    }
}