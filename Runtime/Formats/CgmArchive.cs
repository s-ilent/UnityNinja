using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class CgmTextureEntry
    {
        public int Index;
        public string Name;
        public int Width;
        public int Height;
        public PVRColorFormat ColorFormat;
        public PVRDataFormat DataFormat;
        public int Offset;
        public int Length;
        public byte[] RawData;
    }

    [Serializable]
    public class CgmLightEntry
    {
        public int Index;
        public Vector3 Position;
        public Vector3 Direction;
        public float Near;
        public float Far;
        public Color Color = Color.white;
        public int Offset;
    }

    [Serializable]
    public class CgmUnknownChunkEntry
    {
        public int Index;
        public string Tag;
        public int Offset;
        public int PayloadSize;
        public byte[] RawData;
    }

    [Serializable]
    public class CgmModelEntry
    {
        public int Index;
        public string ChunkTag;
        public string ModelName;
        public byte[] ModelBytes;
        public List<string> TexturesUsed = new List<string>();
        public List<NJS_MOTION> EmbeddedMotions = new List<NJS_MOTION>();
        public NJS_OBJECT RootModel;
    }

    public class CgmArchive
    {
        public List<CgmTextureEntry> Textures { get; } = new List<CgmTextureEntry>();
        public List<CgmModelEntry> Models { get; } = new List<CgmModelEntry>();
        public List<CgmLightEntry> Lights { get; } = new List<CgmLightEntry>();
        public List<CgmUnknownChunkEntry> UnknownChunks { get; } = new List<CgmUnknownChunkEntry>();

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
                            Index = pvrIndex,
                            Name = texName,
                            Width = w,
                            Height = h,
                            ColorFormat = colFmt,
                            DataFormat = dataFmt,
                            Offset = pos,
                            Length = totalLen,
                            RawData = pvrSlice
                        };

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

            // 3. Scan & Process Main Chunk Stream
            pos = 0;
            int modelIdx = 0;
            int lightIdx = 0;
            int unknownIdx = 0;

            byte[] activeNjtlBytes = null;
            List<string> activeNjtlNames = new List<string>();

            while (pos < data.Length - 8)
            {
                var chunkInfo = GetChunkAt(data, pos);
                if (chunkInfo == null)
                {
                    pos++;
                    continue;
                }

                string tag = chunkInfo.Tag;
                int nextPos = chunkInfo.NextOffset;

                // NJTL / GJTL
                if (tag is "NJTL" or "GJTL")
                {
                    byte[] payload = new byte[chunkInfo.PayloadLen];
                    Array.Copy(chunkInfo.FullBytes, 8, payload, 0, chunkInfo.PayloadLen);
                    activeNjtlNames = ParseNjtlNames(payload);
                    activeNjtlBytes = chunkInfo.FullBytes;
                    pos = nextPos;
                    continue;
                }

                // Canonical 3D Model Chunks ONLY (NJCM / GJCM / NJBM / XJCM)
                if (tag is "NJCM" or "GJCM" or "NJBM" or "XJCM")
                {
                    List<byte> combinedModelBytes = new List<byte>();
                    List<string> texUsed = new List<string>();

                    if (activeNjtlBytes != null)
                    {
                        combinedModelBytes.AddRange(activeNjtlBytes);
                        texUsed.AddRange(activeNjtlNames);
                        activeNjtlBytes = null;
                    }

                    combinedModelBytes.AddRange(chunkInfo.FullBytes);

                    // Look ahead for trailing companion animation chunks (NMDM/NSSM)
                    int lookPos = nextPos;

                    while (lookPos < data.Length - 8)
                    {
                        var nextInfo = GetChunkAt(data, lookPos);
                        if (nextInfo == null) break;

                        string nextTag = nextInfo.Tag;

                        if (nextTag is "NMDM" or "NSSM" or "NLIM" or "NJCA")
                        {
                            combinedModelBytes.AddRange(nextInfo.FullBytes);
                            lookPos = nextInfo.NextOffset;
                        }
                        else
                        {
                            break;
                        }
                    }

                    nextPos = lookPos;

                    byte[] mergedBytes = combinedModelBytes.ToArray();
                    NinjaBinaryFile njFile = new NinjaBinaryFile(mergedBytes);
                    NJS_OBJECT rootObj = njFile.Models.Count > 0 ? njFile.Models[0] : null;

                    Models.Add(new CgmModelEntry
                    {
                        Index = modelIdx,
                        ChunkTag = tag,
                        ModelName = $"model_{modelIdx:000}",
                        ModelBytes = mergedBytes,
                        TexturesUsed = texUsed,
                        EmbeddedMotions = njFile.Motions,
                        RootModel = rootObj
                    });

                    modelIdx++;
                    pos = nextPos;
                    continue;
                }

                // Dynamic Light Chunk (NJLI)
                if (tag == "NJLI")
                {
                    var light = ParseNjliLight(chunkInfo.FullBytes, pos, lightIdx++);
                    if (light != null)
                    {
                        Lights.Add(light);
                    }
                    pos = nextPos;
                    continue;
                }

                // Ignored parent sub-chunks
                if (tag is "POF0" or "GBIX" or "PVRT" or "CGLC")
                {
                    pos = nextPos;
                    continue;
                }

                // Standalone Unknown Blocks (e.g. CGMP, CGCL, CGSP, CGAL, CGAM, NCAM, CMCK)
                if (IsAlphanumericTag(tag))
                {
                    UnknownChunks.Add(new CgmUnknownChunkEntry
                    {
                        Index = unknownIdx++,
                        Tag = tag,
                        Offset = pos,
                        PayloadSize = chunkInfo.PayloadLen,
                        RawData = chunkInfo.FullBytes
                    });
                    pos = nextPos;
                    continue;
                }

                pos++;
            }
        }

        private class RawChunkHeader
        {
            public string Tag;
            public int PayloadLen;
            public byte[] FullBytes;
            public int NextOffset;
        }

        private static RawChunkHeader GetChunkAt(byte[] data, int offset)
        {
            if (offset > data.Length - 8) return null;

            string tag = Encoding.ASCII.GetString(data, offset, 4);
            uint payloadLen = ByteConverter.ToUInt32(data, offset + 4);

            if (payloadLen > data.Length - (offset + 8)) return null;

            int totalChunkLen = 8 + (int)payloadLen;
            int pofPos = offset + totalChunkLen;
            int pofTotalLen = 0;

            if (pofPos + 8 <= data.Length && Encoding.ASCII.GetString(data, pofPos, 4) == "POF0")
            {
                uint pofLen = ByteConverter.ToUInt32(data, pofPos + 4);
                if (pofLen <= data.Length - (pofPos + 8))
                {
                    pofTotalLen = 8 + (int)pofLen;
                }
            }

            int fullLen = totalChunkLen + pofTotalLen;
            byte[] fullBytes = new byte[fullLen];
            Array.Copy(data, offset, fullBytes, 0, fullLen);

            return new RawChunkHeader
            {
                Tag = tag,
                PayloadLen = (int)payloadLen,
                FullBytes = fullBytes,
                NextOffset = offset + fullLen
            };
        }

        private static CgmLightEntry ParseNjliLight(byte[] chunkBytes, int offset, int index)
        {
            if (chunkBytes.Length < 0x158) return null;

            float px = ByteConverter.ToSingle(chunkBytes, 0x40 + 8);
            float py = ByteConverter.ToSingle(chunkBytes, 0x44 + 8);
            float pz = ByteConverter.ToSingle(chunkBytes, 0x48 + 8);

            float vx = ByteConverter.ToSingle(chunkBytes, 0x4C + 8);
            float vy = ByteConverter.ToSingle(chunkBytes, 0x50 + 8);
            float vz = ByteConverter.ToSingle(chunkBytes, 0x54 + 8);

            float nearVal = ByteConverter.ToSingle(chunkBytes, 0x134 + 8);
            float farVal = ByteConverter.ToSingle(chunkBytes, 0x138 + 8);

            float r = ByteConverter.ToSingle(chunkBytes, 0x14C + 8);
            float g = ByteConverter.ToSingle(chunkBytes, 0x150 + 8);
            float b = ByteConverter.ToSingle(chunkBytes, 0x154 + 8);

            return new CgmLightEntry
            {
                Index = index,
                Position = new Vector3(px, py, pz),
                Direction = new Vector3(vx, vy, vz),
                Near = nearVal,
                Far = farVal,
                Color = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1.0f),
                Offset = offset
            };
        }

        private static bool IsAlphanumericTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag.Length != 4) return false;
            foreach (char c in tag)
            {
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            }
            return true;
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