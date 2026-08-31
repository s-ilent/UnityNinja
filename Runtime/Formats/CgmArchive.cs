using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    public class CgmTextureEntry
    {
        public string Name;
        public int Width;
        public int Height;
        public PVRColorFormat ColorFormat;
        public PVRDataFormat DataFormat;
        public byte[] RawData;
    }

    public class CgmModelEntry
    {
        public int Index;
        public string ChunkTag;
        public string ModelName;
        public byte[] ModelBytes;
        public List<string> TexturesUsed = new List<string>();
        public NJS_OBJECT RootModel;
    }

    public class CgmArchive
    {
        public List<CgmTextureEntry> Textures { get; } = new List<CgmTextureEntry>();
        public List<CgmModelEntry> Models { get; } = new List<CgmModelEntry>();

        public static CgmArchive Load(byte[] data)
        {
            CgmArchive archive = new CgmArchive();
            archive.Parse(data);
            return archive;
        }

        private void Parse(byte[] data)
        {
            if (data == null || data.Length < 16) return;

            // 1. Parse all texture names from NJTL / GJTL chunks
            List<string> allTexNames = new List<string>();
            int scanPos = 0;
            while (scanPos < data.Length - 8)
            {
                string tag = Encoding.ASCII.GetString(data, scanPos, 4);
                if (tag is "NJTL" or "GJTL")
                {
                    uint len = ByteConverter.ToUInt32(data, scanPos + 4);
                    if (scanPos + 8 + len <= data.Length)
                    {
                        byte[] chunkBytes = new byte[len];
                        Array.Copy(data, scanPos + 8, chunkBytes, 0, len);
                        allTexNames.AddRange(ParseNjtlNames(chunkBytes));
                    }
                }
                scanPos++;
            }

            // 2. Scan and decode all PVR textures (GBIX + PVRT)
            int pvrIndex = 0;
            int pos = 0;
            Dictionary<string, CgmTextureEntry> uniqueTextures = new Dictionary<string, CgmTextureEntry>(StringComparer.OrdinalIgnoreCase);

            while (pos < data.Length - 8)
            {
                string tag = Encoding.ASCII.GetString(data, pos, 4);
                if (tag == "GBIX")
                {
                    uint gbixLen = ByteConverter.ToUInt32(data, pos + 4);
                    int pvrtPos = pos + 8 + (int)gbixLen;
                    if (pvrtPos + 16 <= data.Length && Encoding.ASCII.GetString(data, pvrtPos, 4) == "PVRT")
                    {
                        uint pvrtLen = ByteConverter.ToUInt32(data, pvrtPos + 4);
                        int totalLen = 8 + (int)gbixLen + 8 + (int)pvrtLen;

                        byte[] pvrSlice = new byte[totalLen];
                        Array.Copy(data, pos, pvrSlice, 0, totalLen);

                        PVRColorFormat colFmt = (PVRColorFormat)data[pvrtPos + 8];
                        PVRDataFormat dataFmt = (PVRDataFormat)data[pvrtPos + 9];
                        int w = ByteConverter.ToUInt16(data, pvrtPos + 12);
                        int h = ByteConverter.ToUInt16(data, pvrtPos + 14);

                        string texName = (pvrIndex < allTexNames.Count) ? allTexNames[pvrIndex] : $"texture_{pvrIndex:03d}";
                        if (texName.EndsWith(".pvr", StringComparison.OrdinalIgnoreCase))
                            texName = Path.GetFileNameWithoutExtension(texName);

                        var entry = new CgmTextureEntry
                        {
                            Name = texName,
                            Width = w,
                            Height = h,
                            ColorFormat = colFmt,
                            DataFormat = dataFmt,
                            RawData = pvrSlice
                        };

                        // Deduplicate: Keep higher resolution texture if repeated
                        if (!uniqueTextures.TryGetValue(texName, out var prev) || (w * h > prev.Width * prev.Height))
                        {
                            uniqueTextures[texName] = entry;
                        }

                        pvrIndex++;
                        pos += totalLen;
                        continue;
                    }
                }
                pos++;
            }

            Textures.AddRange(uniqueTextures.Values);

            // 3. Scan & Merge NJTL + Model Chunks (NJCM / GJCM / NJBM + POF0)
            pos = 0;
            int modelIdx = 0;
            byte[] activeNjtlBytes = null;
            List<string> activeNjtlNames = new List<string>();

            while (pos < data.Length - 8)
            {
                string tag = Encoding.ASCII.GetString(data, pos, 4);

                if (tag is "NJTL" or "GJTL")
                {
                    uint njtlLen = ByteConverter.ToUInt32(data, pos + 4);
                    int njtlTotalLen = 8 + (int)njtlLen;

                    int pofPos = pos + njtlTotalLen;
                    int pofTotalLen = 0;
                    if (pofPos + 8 <= data.Length && Encoding.ASCII.GetString(data, pofPos, 4) == "POF0")
                    {
                        uint pofLen = ByteConverter.ToUInt32(data, pofPos + 4);
                        pofTotalLen = 8 + (int)pofLen;
                    }

                    int totalLen = njtlTotalLen + pofTotalLen;
                    activeNjtlBytes = new byte[totalLen];
                    Array.Copy(data, pos, activeNjtlBytes, 0, totalLen);

                    byte[] payload = new byte[njtlLen];
                    Array.Copy(data, pos + 8, payload, 0, njtlLen);
                    activeNjtlNames = ParseNjtlNames(payload);

                    pos += totalLen;
                    continue;
                }
                else if (tag is "NJCM" or "GJCM" or "NJBM")
                {
                    uint mdlLen = ByteConverter.ToUInt32(data, pos + 4);
                    int mdlTotalLen = 8 + (int)mdlLen;

                    int pofPos = pos + mdlTotalLen;
                    int pofTotalLen = 0;
                    if (pofPos + 8 <= data.Length && Encoding.ASCII.GetString(data, pofPos, 4) == "POF0")
                    {
                        uint pofLen = ByteConverter.ToUInt32(data, pofPos + 4);
                        pofTotalLen = 8 + (int)pofLen;
                    }

                    int modelChunkTotalLen = mdlTotalLen + pofTotalLen;
                    byte[] modelChunkBytes = new byte[modelChunkTotalLen];
                    Array.Copy(data, pos, modelChunkBytes, 0, modelChunkTotalLen);

                    byte[] mergedFileBytes;
                    List<string> texUsed = new List<string>();

                    if (activeNjtlBytes != null)
                    {
                        mergedFileBytes = new byte[activeNjtlBytes.Length + modelChunkBytes.Length];
                        Array.Copy(activeNjtlBytes, 0, mergedFileBytes, 0, activeNjtlBytes.Length);
                        Array.Copy(modelChunkBytes, 0, mergedFileBytes, activeNjtlBytes.Length, modelChunkBytes.Length);
                        texUsed.AddRange(activeNjtlNames);
                        activeNjtlBytes = null;
                    }
                    else
                    {
                        mergedFileBytes = modelChunkBytes;
                    }

                    NinjaBinaryFile njFile = new NinjaBinaryFile(mergedFileBytes);
                    NJS_OBJECT rootObj = njFile.Models.Count > 0 ? njFile.Models[0] : null;

                    Models.Add(new CgmModelEntry
                    {
                        Index = modelIdx,
                        ChunkTag = tag,
                        ModelName = $"model_{modelIdx:000}",
                        ModelBytes = mergedFileBytes,
                        TexturesUsed = texUsed,
                        RootModel = rootObj
                    });

                    modelIdx++;
                    pos += modelChunkTotalLen;
                    continue;
                }

                pos++;
            }
        }

        private static List<string> ParseNjtlNames(byte[] chunkBytes)
        {
            List<string> names = new List<string>();
            if (chunkBytes.Length < 8) return names;

            int firstEntry = ByteConverter.ToInt32(chunkBytes, 0);
            int numTex = ByteConverter.ToInt32(chunkBytes, 4);

            for (int t = 0; t < numTex; t++)
            {
                int entryOff = firstEntry + t * 12;
                if (entryOff + 4 <= chunkBytes.Length)
                {
                    int strPtr = ByteConverter.ToInt32(chunkBytes, entryOff);
                    if (strPtr >= 0 && strPtr < chunkBytes.Length)
                    {
                        int len = 0;
                        while (strPtr + len < chunkBytes.Length && chunkBytes[strPtr + len] != 0) len++;
                        names.Add(Encoding.ASCII.GetString(chunkBytes, strPtr, len));
                    }
                    else
                    {
                        names.Add($"tex_{t:02d}");
                    }
                }
            }
            return names;
        }
    }
}