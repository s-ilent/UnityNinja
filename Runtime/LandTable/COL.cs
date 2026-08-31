using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class COL
    {
        public NinjaBoundingSphere Bounds = new NinjaBoundingSphere();
        public float WidthY;
        public float WidthZ;
        public NJS_OBJECT Model;
        public uint BlockBits;
        public int Flags;

        public SA1SurfaceFlags SA1Flags => (SA1SurfaceFlags)Flags;
        public SA2SurfaceFlags SA2Flags => (SA2SurfaceFlags)Flags;
        public SA1SurfaceFlags SurfaceFlags => (SA1SurfaceFlags)Flags;

        public COL() { }

        public COL(byte[] file, int address, uint imageBase, ModelFormat format, Dictionary<int, string> labels地理 = null)
        {
            if (address + 32 > file.Length) return;

            Bounds = new NinjaBoundingSphere(file, address);
            WidthY = ByteConverter.ToSingle(file, address + 16);
            WidthZ = ByteConverter.ToSingle(file, address + 20);

            int modelAddr = (int)(ByteConverter.ToUInt32(file, address + 24) - imageBase);
            if (modelAddr >= 0 && modelAddr < file.Length)
            {
                Model = new NJS_OBJECT(file, modelAddr, imageBase, format, labels地理);
            }

            BlockBits = ByteConverter.ToUInt32(file, address + 28);
            Flags = ByteConverter.ToInt32(file, address + 32);
        }
    }
}