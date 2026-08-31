using System;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja.GC
{
    public enum ParameterType : uint
    {
        VtxAttrFmt = 0,
        IndexAttributeFlags = 1,
        StripFlags1 = 2,
        StripFlags2 = 3,
        BlendAlpha = 4,
        DiffuseColor = 5,
        AmbientColor = 6,
        SpecularColor = 7,
        Texture = 8,
        TextureTEVMode = 9,
        TexCoordGen = 10
    }

    [Serializable]
    public class GCParameter
    {
        public ParameterType Type;
        public uint Data;

        public GCVertexAttribute VertexAttribute => (GCVertexAttribute)(Data >> 16);
        public GCUVScale UVScale => (GCUVScale)(Data & 0xFF);
        public GCIndexAttributeFlags IndexAttributes => (GCIndexAttributeFlags)Data;

        public ushort TextureID => (ushort)(Data & 0xFFFF);
        public GCTileMode TileMode => (GCTileMode)(Data >> 16);

        public AlphaInstruction SourceAlpha => (AlphaInstruction)((Data >> 11) & 7);
        public AlphaInstruction DestAlpha => (AlphaInstruction)((Data >> 8) & 7);

        public Color32 Color => NinjaColor.FromArgb32(Data);

        public static GCParameter Read(byte[] file, int address)
        {
            if (address + 8 > file.Length) return null;
            return new GCParameter
            {
                Type = (ParameterType)file[address],
                Data = ByteConverter.ToUInt32(file, address + 4)
            };
        }
    }
}