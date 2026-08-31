using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class NJS_MESHSET
    {
        public ushort MaterialID;
        public Basic_PolyType PolyType;
        public List<NinjaPoly> Polys = new List<NinjaPoly>();
        public NinjaVertex[] PolyNormals;
        public Color32[] VertexColors;
        public NinjaUV[] UVs;

        public NJS_MESHSET(byte[] file, int address, uint imageBase, Dictionary<int, string> labels = null)
        {
            if (address + 24 > file.Length) return;

            ushort rawMat = ByteConverter.ToUInt16(file, address);
            PolyType = (Basic_PolyType)(rawMat >> 14);
            MaterialID = (ushort)(rawMat & 0x3FFF);
            int polyCount = ByteConverter.ToInt16(file, address + 2);

            int polyAddr = (int)(ByteConverter.ToUInt32(file, address + 4) - imageBase);
            int striptotal = 0;

            if (polyAddr >= 0 && polyAddr < file.Length)
            {
                for (int i = 0; i < polyCount; i++)
                {
                    NinjaPoly p = NinjaPoly.CreatePoly(PolyType, file, polyAddr);
                    if (p != null)
                    {
                        Polys.Add(p);
                        striptotal += p.Indexes.Length;
                        polyAddr += p.Size;
                    }
                }
            }

            int normAddr = (int)(ByteConverter.ToUInt32(file, address + 12) - imageBase);
            if (normAddr > 0 && normAddr < file.Length)
            {
                PolyNormals = new NinjaVertex[polyCount];
                for (int i = 0; i < polyCount; i++)
                {
                    PolyNormals[i] = new NinjaVertex(file, normAddr + i * 12);
                }
            }

            int colAddr = (int)(ByteConverter.ToUInt32(file, address + 16) - imageBase);
            if (colAddr > 0 && colAddr < file.Length)
            {
                VertexColors = new Color32[striptotal];
                for (int i = 0; i < striptotal; i++)
                {
                    VertexColors[i] = NinjaColor.FromBytes(file, colAddr + i * 4);
                }
            }

            int uvAddr = (int)(ByteConverter.ToUInt32(file, address + 20) - imageBase);
            if (uvAddr > 0 && uvAddr < file.Length)
            {
                UVs = new NinjaUV[striptotal];
                for (int i = 0; i < striptotal; i++)
                {
                    UVs[i] = new NinjaUV(file, uvAddr + i * 4, false, false);
                }
            }
        }
    }
}