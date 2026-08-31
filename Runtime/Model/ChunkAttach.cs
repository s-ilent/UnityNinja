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
                        if (ctype == ChunkType.End || ctype == ChunkType.Null) break;

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

            // 2. Read Poly Chunk Stream
            int pAddr = ByteConverter.ToInt32(file, address + 4);
            if (pAddr != 0)
            {
                pAddr = (int)((uint)pAddr - imageBase);
                if (pAddr >= 0 && pAddr + 2 <= file.Length)
                {
                    while (pAddr + 2 <= file.Length)
                    {
                        byte rawType = file[pAddr];
                        if (rawType == (byte)ChunkType.End || rawType == (byte)ChunkType.Null)
                            break;

                        // Reject vertex chunks in poly chunk stream or undefined types
                        if (rawType >= 32 && rawType <= 55)
                            break;

                        PolyChunk pChunk = PolyChunk.Load(file, pAddr);
                        if (pChunk == null || pChunk.ByteSize <= 0 || pChunk is PolyChunkNull || pChunk is PolyChunkEnd)
                            break;

                        PolyChunks.Add(pChunk);
                        pAddr += pChunk.ByteSize;
                    }
                }
            }

            Bounds = new NinjaBoundingSphere(file, address + 8);
        }
    }
}