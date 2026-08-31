using System;
using System.Collections.Generic;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class ChunkAttach : NinjaAttach
    {
        public List<VertexChunk> VertexChunks = new List<VertexChunk>();
        public List<PolyChunk> PolyChunks = new List<PolyChunk>();

        public override bool HasWeight
        {
            get
            {
                foreach (var vc in VertexChunks)
                {
                    if (vc.Type is ChunkType.Vertex_VertexNinjaFlags or ChunkType.Vertex_VertexNormalNinjaFlags)
                        return true;
                }
                return false;
            }
        }

        public ChunkAttach(byte[] file, int address, uint imageBase, Dictionary<int, string> labels = null)
        {
            if (labels != null && labels.TryGetValue(address, out string lbl))
                Name = lbl;
            else
                Name = $"attach_{address:X8}";

            int vAddr = ByteConverter.ToInt32(file, address);
            if (vAddr != 0)
            {
                vAddr = (int)((uint)vAddr - imageBase);
                if (vAddr >= 0 && vAddr < file.Length)
                {
                    ChunkType ctype = (ChunkType)(file[vAddr] & 0xFF);
                    while (ctype != ChunkType.End && vAddr + 8 <= file.Length)
                    {
                        VertexChunk vc = new VertexChunk(file, vAddr);
                        VertexChunks.Add(vc);
                        vAddr += (vc.Size * 4) + 4;
                        if (vAddr >= file.Length) break;
                        ctype = (ChunkType)(file[vAddr] & 0xFF);
                    }
                }
            }

            int pAddr = ByteConverter.ToInt32(file, address + 4);
            if (pAddr != 0)
            {
                pAddr = (int)((uint)pAddr - imageBase);
                if (pAddr >= 0 && pAddr < file.Length)
                {
                    PolyChunk pChunk = PolyChunk.Load(file, pAddr);
                    while (pChunk.Type != ChunkType.End && pAddr < file.Length)
                    {
                        PolyChunks.Add(pChunk);
                        pAddr += pChunk.ByteSize;
                        if (pAddr >= file.Length) break;
                        pChunk = PolyChunk.Load(file, pAddr);
                    }
                }
            }

            Bounds = new NinjaBoundingSphere(file, address + 8);
        }
    }
}