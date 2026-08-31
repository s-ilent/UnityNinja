using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja.XJ
{
    [Serializable]
    public class XjVertexSet
    {
        public List<Vector3> Positions = new List<Vector3>();
        public List<Vector3> Normals = new List<Vector3>();
        public List<Color32> Colors = new List<Color32>();
        public List<Vector2> UVs = new List<Vector2>();

        public XjVertexSet(byte[] file, int address, uint imageBase)
        {
            if (address + 16 > file.Length) return;

            ushort vtxType = ByteConverter.ToUInt16(file, address);
            int vAddr = (int)(ByteConverter.ToUInt32(file, address + 4) - imageBase);
            uint vCount = ByteConverter.ToUInt32(file, address + 12);

            bool hasUV = (vtxType & 0x1) != 0;
            bool hasNormal = (vtxType & 0x2) != 0;
            bool hasColor = (vtxType & 0x4) != 0;

            if (vAddr < 0 || vAddr >= file.Length) return;

            for (uint i = 0; i < vCount && vAddr < file.Length; i++)
            {
                Positions.Add(new Vector3(ByteConverter.ToSingle(file, vAddr), ByteConverter.ToSingle(file, vAddr + 4), ByteConverter.ToSingle(file, vAddr + 8)));
                vAddr += 12;

                if (hasNormal)
                {
                    Normals.Add(new Vector3(ByteConverter.ToSingle(file, vAddr), ByteConverter.ToSingle(file, vAddr + 4), ByteConverter.ToSingle(file, vAddr + 8)));
                    vAddr += 12;
                }

                if (hasColor)
                {
                    Colors.Add(NinjaColor.FromBytes(file, vAddr));
                    vAddr += 4;
                }

                if (hasUV)
                {
                    UVs.Add(new Vector2(ByteConverter.ToSingle(file, vAddr), ByteConverter.ToSingle(file, vAddr + 4)));
                    vAddr += 8;
                }
            }
        }
    }
}