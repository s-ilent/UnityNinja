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

            // 1. Read Vertex Chunk Stream
            int vAddr = ByteConverter.ToInt32(file, address);
            if (vAddr != 0)
            {
                vAddr = (int)((uint)vAddr - imageBase);
                if (vAddr >= 0 && vAddr < file.Length)
                {
                    while (vAddr + 8 <= file.Length)
                    {
                        ChunkType ctype = (ChunkType)(file[vAddr] & 0xFF);
                        if (ctype == ChunkType.End) break;
                        if (ctype == ChunkType.Null)
                        {
                            vAddr += 2;
                            continue;
                        }

                        // Vertex chunks must be in the valid Vertex range (32..55)
                        if ((byte)ctype < 32 || (byte)ctype > 55) break;

                        VertexChunk vc = new VertexChunk(file, vAddr);
                        VertexChunks.Add(vc);

                        int byteSize = (vc.Size * 4) + 4;
                        if (byteSize <= 0) break;
                        vAddr += byteSize;
                    }
                }
            }

            // 2. Read Poly Chunk Stream (Skip Null padding words; break on End)
            int pAddr = ByteConverter.ToInt32(file, address + 4);
            if (pAddr != 0)
            {
                pAddr = (int)((uint)pAddr - imageBase);
                if (pAddr >= 0 && pAddr + 2 <= file.Length)
                {
                    while (pAddr + 2 <= file.Length)
                    {
                        PolyChunk pChunk = PolyChunk.Load(file, pAddr);
                        if (pChunk == null || pChunk is PolyChunkEnd || pChunk.Type == ChunkType.End)
                            break;

                        if (pChunk.Type != ChunkType.Null && !(pChunk is PolyChunkNull))
                        {
                            PolyChunks.Add(pChunk);
                        }

                        int step = pChunk.ByteSize > 0 ? pChunk.ByteSize : 2;
                        pAddr += step;
                    }
                }
            }

            Bounds = new NinjaBoundingSphere(file, address + 8);
        }
    }
}