using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public abstract class PolyChunk
    {
        public ushort Header;
        public ChunkType Type => (ChunkType)(Header & 0xFF);
        public byte Flags => (byte)(Header >> 8);
        public abstract int ByteSize { get; }

        public static PolyChunk Load(byte[] file, int address)
        {
            if (address + 2 > file.Length) return new PolyChunkNull();
            ChunkType type = (ChunkType)(ByteConverter.ToUInt16(file, address) & 0xFF);

            return type switch
            {
                ChunkType.Null => new PolyChunkNull(),
                ChunkType.End => new PolyChunkEnd(),
                ChunkType.Bits_BlendAlpha => new PolyChunkBitsBlendAlpha(file, address),
                ChunkType.Tiny_TextureID or ChunkType.Tiny_TextureID2 => new PolyChunkTinyTextureID(file, address),
                ChunkType.Material_Diffuse or ChunkType.Material_Ambient or ChunkType.Material_DiffuseAmbient or
                ChunkType.Material_Specular or ChunkType.Material_DiffuseSpecular or ChunkType.Material_AmbientSpecular or
                ChunkType.Material_DiffuseAmbientSpecular => new PolyChunkMaterial(file, address),
                ChunkType.Strip_Strip or ChunkType.Strip_StripUVN or ChunkType.Strip_StripUVH or
                ChunkType.Strip_StripNormal or ChunkType.Strip_StripUVNNormal or ChunkType.Strip_StripUVHNormal or
                ChunkType.Strip_StripColor or ChunkType.Strip_StripUVNColor or ChunkType.Strip_StripUVHColor or
                ChunkType.Strip_Strip2 or ChunkType.Strip_StripUVN2 or ChunkType.Strip_StripUVH2 => new PolyChunkStrip(file, address),
                _ => new PolyChunkGeneric(file, address)
            };
        }
    }

    [Serializable] public class PolyChunkNull : PolyChunk { public override int ByteSize => 2; }
    [Serializable] public class PolyChunkEnd : PolyChunk { public override int ByteSize => 2; }

    [Serializable]
    public class PolyChunkGeneric : PolyChunk
    {
        private readonly int m_Size;
        public override int ByteSize => m_Size;
        public PolyChunkGeneric(byte[] file, int address)
        {
            Header = ByteConverter.ToUInt16(file, address);
            ushort sz = (address + 4 <= file.Length) ? ByteConverter.ToUInt16(file, address + 2) : (ushort)0;
            m_Size = Math.Max(2, (sz * 2) + 4);
        }
    }

    [Serializable]
    public class PolyChunkBitsBlendAlpha : PolyChunk
    {
        public AlphaInstruction SourceAlpha => (AlphaInstruction)((Flags >> 3) & 7);
        public AlphaInstruction DestinationAlpha => (AlphaInstruction)(Flags & 7);
        public override int ByteSize => 2;

        public PolyChunkBitsBlendAlpha(byte[] file, int address)
        {
            Header = ByteConverter.ToUInt16(file, address);
        }
    }

    [Serializable]
    public class PolyChunkTinyTextureID : PolyChunk
    {
        public ushort Data;
        public ushort TextureID => (ushort)(Data & 0x1FFF);
        public bool ClampV => (Flags & 0x10) != 0;
        public bool ClampU => (Flags & 0x20) != 0;
        public bool FlipV => (Flags & 0x40) != 0;
        public bool FlipU => (Flags & 0x80) != 0;
        public override int ByteSize => 4;

        public PolyChunkTinyTextureID(byte[] file, int address)
        {
            Header = ByteConverter.ToUInt16(file, address);
            Data = ByteConverter.ToUInt16(file, address + 2);
        }
    }

    [Serializable]
    public class PolyChunkMaterial : PolyChunk
    {
        public Color32? Diffuse;
        public Color32? Ambient;
        public Color32? Specular;
        public byte SpecularExponent;
        public ushort Size;
        public override int ByteSize => (Size * 2) + 4;

        public PolyChunkMaterial(byte[] file, int address)
        {
            Header = ByteConverter.ToUInt16(file, address);
            Size = ByteConverter.ToUInt16(file, address + 2);
            address += 4;

            if ((Type >= ChunkType.Material_Diffuse && Type <= ChunkType.Material_DiffuseAmbientSpecular) ||
                (Type >= ChunkType.Material_Diffuse2 && Type <= ChunkType.Material_DiffuseAmbientSpecular2))
            {
                Diffuse = NinjaColor.FromBytes(file, address, true);
                address += 4;
            }
        }
    }

    [Serializable]
    public class PolyChunkStrip : PolyChunk
    {
        public class ChunkStripData
        {
            public bool Reversed;
            public ushort[] Indexes;
            public Vector2[] UVs;
            public Color32[] Colors;
        }

        public ushort Size;
        public List<ChunkStripData> Strips = new List<ChunkStripData>();
        public override int ByteSize => (Size * 2) + 4;

        public bool DoubleSided => (Flags & 0x10) != 0;
        public bool IgnoreLighting => (Flags & 0x01) != 0;
        public bool UseAlpha => (Flags & 0x08) != 0;

        public PolyChunkStrip(byte[] file, int address)
        {
            Header = ByteConverter.ToUInt16(file, address);
            Size = ByteConverter.ToUInt16(file, address + 2);
            ushort header2 = ByteConverter.ToUInt16(file, address + 4);
            int stripCount = header2 & 0x3FFF;
            byte userFlags = (byte)(header2 >> 14);

            address += 6;

            bool hasUV = Type is ChunkType.Strip_StripUVN or ChunkType.Strip_StripUVH or
                               ChunkType.Strip_StripUVNColor or ChunkType.Strip_StripUVHColor or
                               ChunkType.Strip_StripUVNNormal or ChunkType.Strip_StripUVHNormal or
                               ChunkType.Strip_StripUVN2 or ChunkType.Strip_StripUVH2;

            bool isUVH = Type is ChunkType.Strip_StripUVH or ChunkType.Strip_StripUVHColor or
                                ChunkType.Strip_StripUVHNormal or ChunkType.Strip_StripUVH2;

            bool hasColor = Type is ChunkType.Strip_StripColor or ChunkType.Strip_StripUVNColor or ChunkType.Strip_StripUVHColor;
            double uvDiv = isUVH ? 1024.0 : 256.0;

            for (int s = 0; s < stripCount && address < file.Length; s++)
            {
                short rawCount = ByteConverter.ToInt16(file, address);
                int count = Math.Abs(rawCount);
                bool reversed = (ByteConverter.ToUInt16(file, address) & 0x8000) != 0;
                address += 2;

                ChunkStripData strip = new ChunkStripData
                {
                    Reversed = reversed,
                    Indexes = new ushort[count],
                    UVs = hasUV ? new Vector2[count] : null,
                    Colors = hasColor ? new Color32[count] : null
                };

                for (int i = 0; i < count && address < file.Length; i++)
                {
                    strip.Indexes[i] = ByteConverter.ToUInt16(file, address);
                    address += 2;

                    if (hasUV && address + 4 <= file.Length)
                    {
                        float u = (float)(ByteConverter.ToInt16(file, address) / uvDiv);
                        float v = (float)(ByteConverter.ToInt16(file, address + 2) / uvDiv);
                        strip.UVs[i] = new Vector2(u, v);
                        address += 4;
                    }

                    if (hasColor && address + 4 <= file.Length)
                    {
                        strip.Colors[i] = NinjaColor.FromBytes(file, address, true);
                        address += 4;
                    }

                    if (i > 1 && userFlags > 0)
                    {
                        address += userFlags * 2;
                    }
                }

                Strips.Add(strip);
            }
        }
    }
}