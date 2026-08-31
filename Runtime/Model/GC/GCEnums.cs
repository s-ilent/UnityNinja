using System;

namespace UnityNinja.GC
{
    public enum GCBlendModeControl
    {
        Zero = 0,
        One = 1,
        SrcColor = 2,
        InverseSrcColor = 3,
        SrcAlpha = 4,
        InverseSrcAlpha = 5,
        DstAlpha = 6,
        InverseDstAlpha = 7
    }

    public enum GCStructType
    {
        Position_XY = 0,
        Position_XYZ = 1,
        Normal_XYZ = 2,
        Normal_NBT = 3,
        Normal_NBT3 = 4,
        Color_RGB = 5,
        Color_RGBA = 6,
        TexCoord_S = 7,
        TexCoord_ST = 8
    }

    public enum GCDataType
    {
        Unsigned8 = 0,
        Signed8 = 1,
        Unsigned16 = 2,
        Signed16 = 3,
        Float32 = 4,
        RGB565 = 5,
        RGB8 = 6,
        RGBX8 = 7,
        RGBA4 = 8,
        RGBA6 = 9,
        RGBA8 = 10
    }

    public enum GCPrimitiveType
    {
        Triangles = 0x90,
        TriangleStrip = 0x98,
        TriangleFan = 0xA0,
        Lines = 0xA8,
        LineStrip = 0xB0,
        Points = 0xB8
    }

    public enum GCTexCoordID
    {
        TexCoord0 = 0x0,
        TexCoord1 = 0x1,
        TexCoord2 = 0x2,
        TexCoord3 = 0x3,
        TexCoord4 = 0x4,
        TexCoord5 = 0x5,
        TexCoord6 = 0x6,
        TexCoord7 = 0x7,
        TexCoordMax = 0x8,
        TexCoordNull = 0xFF
    }

    public enum GCTexGenType
    {
        Matrix3x4 = 0x0,
        Matrix2x4 = 0x1,
        Bump0 = 0x2,
        Bump1 = 0x3,
        Bump2 = 0x4,
        Bump3 = 0x5,
        Bump4 = 0x6,
        Bump5 = 0x7,
        Bump6 = 0x8,
        Bump7 = 0x9,
        SRTG = 0xA
    }

    public enum GCTexGenSrc
    {
        Position = 0x0,
        Normal = 0x1,
        Binormal = 0x2,
        Tangent = 0x3,
        Tex0 = 0x4,
        Tex1 = 0x5,
        Tex2 = 0x6,
        Tex3 = 0x7,
        Tex4 = 0x8,
        Tex5 = 0x9,
        Tex6 = 0xA,
        Tex7 = 0xB,
        TexCoord0 = 0xC,
        TexCoord1 = 0xD,
        TexCoord2 = 0xE,
        TexCoord3 = 0xF,
        TexCoord4 = 0x10,
        TexCoord5 = 0x11,
        TexCoord6 = 0x12,
        Color0 = 0x13,
        Color1 = 0x14
    }

    public enum GCTexGenMatrix
    {
        Matrix0 = 0, Matrix1 = 1, Matrix2 = 2, Matrix3 = 3, Matrix4 = 4,
        Matrix5 = 5, Matrix6 = 6, Matrix7 = 7, Matrix8 = 8, Matrix9 = 9,
        Identity = 10
    }

    public enum GCVertexAttribute
    {
        PositionMatrixIdx = 0,
        Position = 1,
        Normal = 2,
        Color0 = 3,
        Color1 = 4,
        Tex0 = 5,
        Tex1 = 6,
        Tex2 = 7,
        Tex3 = 8,
        Tex4 = 9,
        Tex5 = 10,
        Tex6 = 11,
        Tex7 = 12,
        Null = 255
    }

    public enum GCSkinAttribute : ushort
    {
        StaticWeight = 0,
        PartialWeightStart = 1,
        PartialWeight = 2,
        WeightStructEndMarker = 3
    }

    [Flags]
    public enum GCIndexAttributeFlags : ushort
    {
        Bit0 = 1 << 0,
        Bit1 = 1 << 1,
        Position16BitIndex = 1 << 2,
        HasPosition = 1 << 3,
        Normal16BitIndex = 1 << 4,
        HasNormal = 1 << 5,
        Color16BitIndex = 1 << 6,
        HasColor = 1 << 7,
        Bit8 = 1 << 8,
        Bit9 = 1 << 9,
        UV16BitIndex = 1 << 10,
        HasUV = 1 << 11
    }

    [Flags]
    public enum GCTileMode
    {
        WrapV = 1 << 0,
        MirrorV = 1 << 1,
        WrapU = 1 << 2,
        MirrorU = 1 << 3,
        Unk_1 = 1 << 4,
        Mask = (1 << 5) - 1
    }

    public enum GCUVScale
    {
        Default = 0,
        NoUV1 = 1,
        Scale1 = 8,   // 1.0x (Divisor 1)
        Scale2 = 9,   // 0.5x (Divisor 2)
        Scale3 = 0xA, // 0.25x (Divisor 4)
        Scale4 = 0xB, // 0.125x (Divisor 8)
        Scale5 = 0xC, // 0.0625x (Divisor 16)
        Scale6 = 0xD, // 0.03125x (Divisor 32)
        Scale7 = 0xE, // 0.015625x (Divisor 64)
        Scale8 = 0xF  // 0.0078125x (Divisor 128)
    }

    public static class GCUVScaleHelper
    {
        public static float GetDivisor(GCUVScale scale) => scale switch
        {
            GCUVScale.Scale2 => 2.0f,
            GCUVScale.Scale3 => 4.0f,
            GCUVScale.Scale4 => 8.0f,
            GCUVScale.Scale5 => 16.0f,
            GCUVScale.Scale6 => 32.0f,
            GCUVScale.Scale7 => 64.0f,
            GCUVScale.Scale8 => 128.0f,
            _ => 1.0f
        };
    }
}