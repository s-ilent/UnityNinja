using System;
using System.Collections.Generic;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class BasicAttach : NinjaAttach
    {
        public NinjaVertex[] Vertices;
        public NinjaVertex[] Normals;
        public List<NJS_MESHSET> MeshSets = new List<NJS_MESHSET>();
        public List<NJS_MATERIAL> Materials = new List<NJS_MATERIAL>();

        public BasicAttach(byte[] file, int address, uint imageBase, bool isDX, Dictionary<int, string> labels = null)
        {
            if (labels != null && labels.TryGetValue(address, out string lbl))
                Name = lbl;
            else
                Name = $"attach_{address:X8}";

            int vCount = ByteConverter.ToInt32(file, address + 8);
            int vAddr = (int)(ByteConverter.ToUInt32(file, address) - imageBase);

            if (vAddr >= 0 && vAddr < file.Length)
            {
                Vertices = new NinjaVertex[vCount];
                for (int i = 0; i < vCount; i++)
                    Vertices[i] = new NinjaVertex(file, vAddr + i * 12);
            }

            int nAddr = ByteConverter.ToInt32(file, address + 4);
            if (nAddr != 0)
            {
                nAddr = (int)((uint)nAddr - imageBase);
                if (nAddr >= 0 && nAddr < file.Length)
                {
                    Normals = new NinjaVertex[vCount];
                    for (int i = 0; i < vCount; i++)
                        Normals[i] = new NinjaVertex(file, nAddr + i * 12);
                }
            }

            int meshCount = ByteConverter.ToInt16(file, address + 20);
            int meshAddr = (int)(ByteConverter.ToUInt32(file, address + 12) - imageBase);
            int meshStride = isDX ? 28 : 24;

            if (meshAddr > 0 && meshAddr < file.Length)
            {
                for (int i = 0; i < meshCount; i++)
                {
                    MeshSets.Add(new NJS_MESHSET(file, meshAddr + i * meshStride, imageBase, labels));
                }
            }

            int matCount = ByteConverter.ToInt16(file, address + 22);
            int matAddr = (int)(ByteConverter.ToUInt32(file, address + 16) - imageBase);

            if (matAddr > 0 && matAddr < file.Length)
            {
                for (int i = 0; i < matCount; i++)
                {
                    Materials.Add(new NJS_MATERIAL(file, matAddr + i * NJS_MATERIAL.Size, labels));
                }
            }

            Bounds = new NinjaBoundingSphere(file, address + 24);
        }
    }
}