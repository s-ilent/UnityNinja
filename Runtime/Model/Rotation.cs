using System;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class NinjaRotation
    {
        public int x;
        public int y;
        public int z;

        public NinjaRotation() { }
        public NinjaRotation(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }

        public NinjaRotation(byte[] file, int address)
        {
            if (address + 12 > file.Length) return;
            x = ByteConverter.ToInt32(file, address);
            y = ByteConverter.ToInt32(file, address + 4);
            z = ByteConverter.ToInt32(file, address + 8);
        }

        public static float BamToDegrees(int bams) => (float)((double)bams * (360.0 / 65536.0));
        public static int DegreesToBam(float deg) => (int)((double)deg * (65536.0 / 360.0));

        public Vector3 ToDegrees() => new Vector3(BamToDegrees(x), BamToDegrees(y), BamToDegrees(z));
    }
}