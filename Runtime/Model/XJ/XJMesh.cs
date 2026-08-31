using System;
using System.Collections.Generic;
using UnityNinja.IO;

namespace UnityNinja.XJ
{
    [Serializable]
    public class XJMesh
    {
        public NJS_MATERIAL Material = new NJS_MATERIAL();
        public List<ushort> StripIndices = new List<ushort>();

        public XJMesh(byte[] file, int address, uint imageBase)
        {
            if (address + 20 > file.Length) return;

            int matAddr = (int)(ByteConverter.ToUInt32(file, address) - imageBase);
            uint matCount = ByteConverter.ToUInt32(file, address + 4);
            int idxAddr = (int)(ByteConverter.ToUInt32(file, address + 8) - imageBase);
            uint idxCount = ByteConverter.ToUInt32(file, address + 12);

            // Read XJ Material Commands
            if (matAddr > 0 && matAddr < file.Length)
            {
                for (uint i = 0; i < matCount && matAddr + 4 <= file.Length; i++)
                {
                    uint type = ByteConverter.ToUInt32(file, matAddr);
                    matAddr += 4;

                    switch (type)
                    {
                        case 2:
                            Material.Flags |= 0x100000; // UseAlpha
                            matAddr += 12;
                            break;
                        case 3:
                            Material.TextureID = ByteConverter.ToInt32(file, matAddr);
                            matAddr += 12;
                            break;
                        case 5:
                            Material.DiffuseColor = NinjaColor.FromBytes(file, matAddr);
                            matAddr += 12;
                            break;
                        default:
                            matAddr += 12;
                            break;
                    }
                }
            }

            // Read Strip indices
            if (idxAddr > 0 && idxAddr < file.Length)
            {
                for (uint i = 0; i < idxCount && idxAddr + 2 <= file.Length; i++)
                {
                    StripIndices.Add(ByteConverter.ToUInt16(file, idxAddr));
                    idxAddr += 2;
                }
            }
        }

        public List<int> TriangulateStrips()
        {
            List<int> tris = new List<int>();
            for (int i = 0; i < StripIndices.Count - 2; i++)
            {
                ushort a = StripIndices[i];
                ushort b = StripIndices[i + 1];
                ushort c = StripIndices[i + 2];

                if (a != b && b != c && c != a)
                {
                    if (i % 2 == 0)
                    {
                        tris.Add(a); tris.Add(c); tris.Add(b);
                    }
                    else
                    {
                        tris.Add(a); tris.Add(b); tris.Add(c);
                    }
                }
            }
            return tris;
        }
    }
}