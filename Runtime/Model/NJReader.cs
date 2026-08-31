using System;
using System.Collections.Generic;
using System.Text;
using UnityNinja.IO;

namespace UnityNinja
{
    public class NinjaBinaryFile
    {
        public List<NJS_OBJECT> Models { get; } = new List<NJS_OBJECT>();
        public List<NJS_MOTION> Motions { get; } = new List<NJS_MOTION>();
        public List<string[]> Texnames { get; } = new List<string[]>();
        public ModelFormat Format { get; private set; } = ModelFormat.Basic;

        public NinjaBinaryFile(byte[] data, ModelFormat forcedFormat = ModelFormat.Basic)
        {
            if (data == null || data.Length < 16) return;

            ByteConverter.BackupBigEndian();
            int startOffset = 0;
            int imgBase = 0;

            bool isBig = false;
            string magic = Encoding.ASCII.GetString(data, 0, Math.Min(4, data.Length));

            if (magic is not ("NJBM" or "NJCM" or "GJCM" or "XJCM" or "POF0" or "NMDM" or "NSSM" or "NJTL" or "GJTL"))
            {
                for (int s = 0; s < Math.Min(data.Length - 4, 0x100); s += 2)
                {
                    string probe = Encoding.ASCII.GetString(data, s, 4);
                    if (probe is "NJBM" or "NJCM" or "GJCM" or "XJCM" or "NMDM" or "NSSM" or "NJTL" or "GJTL")
                    {
                        startOffset = s;
                        magic = probe;
                        break;
                    }
                }
            }

            uint sizeLE = BitConverter.ToUInt32(data, startOffset + 4);
            uint sizeBE = ByteConverter.ToUInt32BE(data, startOffset + 4);

            if (sizeLE > data.Length && sizeBE <= data.Length)
            {
                isBig = true;
            }

            ByteConverter.BigEndian = isBig;

            Format = magic switch
            {
                "NJBM" => ModelFormat.Basic,
                "NJCM" => ModelFormat.Chunk,
                "GJCM" => ModelFormat.GC,
                "XJCM" => ModelFormat.XJ,
                _ => forcedFormat
            };

            List<(string ChunkID, byte[] Payload, int ImageBase)> chunks = new List<(string, byte[], int)>();

            while (startOffset < data.Length - 8)
            {
                string chunkSig = Encoding.ASCII.GetString(data, startOffset, 4);
                int size = isBig ? ByteConverter.ToInt32BE(data, startOffset + 4) : BitConverter.ToInt32(data, startOffset + 4);

                if (size <= 0 || startOffset + 8 + size > data.Length)
                    break;

                byte[] chunkBytes = new byte[size];
                Array.Copy(data, startOffset + 8, chunkBytes, 0, size);

                if (chunkSig == "POF0" && chunks.Count > 0)
                {
                    var pofOffsets = POF0Helper.GetPointerListFromPOF(chunkBytes);
                    POF0Helper.FixPointersWithPOF(chunks[chunks.Count - 1].Payload, pofOffsets, imgBase);
                }
                else
                {
                    imgBase = startOffset + 8;
                    chunks.Add((chunkSig, chunkBytes, imgBase));
                }

                startOffset += size + 8;
            }

            foreach (var chunk in chunks)
            {
                if (chunk.ChunkID is "NJBM")
                {
                    Dictionary<int, string> labels = new Dictionary<int, string> { [0] = $"object_{chunk.ImageBase:X8}" };
                    Models.Add(new NJS_OBJECT(chunk.Payload, 0, (uint)chunk.ImageBase, ModelFormat.Basic, labels));
                }
                else if (chunk.ChunkID is "NJCM" or "GJCM" or "XJCM")
                {
                    Dictionary<int, string> labels = new Dictionary<int, string> { [0] = $"object_{chunk.ImageBase:X8}" };
                    ModelFormat fmt = chunk.ChunkID switch { "GJCM" => ModelFormat.GC, "XJCM" => ModelFormat.XJ, _ => ModelFormat.Chunk };
                    Models.Add(new NJS_OBJECT(chunk.Payload, 0, (uint)chunk.ImageBase, fmt, labels));
                }
                else if (chunk.ChunkID is "NMDM" or "NSSM")
                {
                    Dictionary<int, string> labels = new Dictionary<int, string> { [0] = $"motion_{chunk.ImageBase:X8}" };
                    int partCount = Models.Count > 0 ? Models[Models.Count - 1].CountAll() : 0;
                    Motions.Add(new NJS_MOTION(chunk.Payload, 0, (uint)chunk.ImageBase, partCount, labels));
                }
                else if (chunk.ChunkID is "NJTL" or "GJTL")
                {
                    int firstEntry = ByteConverter.ToInt32(chunk.Payload, 0) - chunk.ImageBase;
                    int numTextures = ByteConverter.ToInt32(chunk.Payload, 4);
                    List<string> texNames = new List<string>();

                    for (int i = 0; i < numTextures; i++)
                    {
                        int textAddress = ByteConverter.ToInt32(chunk.Payload, firstEntry + i * 12) - chunk.ImageBase;
                        if (textAddress >= 0 && textAddress < chunk.Payload.Length)
                        {
                            List<byte> nameBytes = new List<byte>();
                            int j = 0;
                            while (textAddress + j < chunk.Payload.Length && chunk.Payload[textAddress + j] != 0)
                            {
                                nameBytes.Add(chunk.Payload[textAddress + j]);
                                j++;
                            }
                            texNames.Add(Encoding.ASCII.GetString(nameBytes.ToArray()));
                        }
                    }

                    Texnames.Add(texNames.ToArray());
                }
            }

            ByteConverter.RestoreBigEndian();
        }
    }
}