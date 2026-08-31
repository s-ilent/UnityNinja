using System;
using UnityEngine;

namespace UnityNinja.IO
{
    public enum PVRColorFormat : byte
    {
        ARGB1555 = 0x00,
        RGB565   = 0x01,
        ARGB4444 = 0x02,
        YUV422   = 0x03,
        BumpMap  = 0x04,
        RGB555   = 0x05,
        ARGB8888 = 0x06
    }

    public enum PVRDataFormat : byte
    {
        SquareTwiddled            = 0x01,
        SquareTwiddledMipmapped   = 0x02,
        VQ                        = 0x03,
        VQ_Mipmapped              = 0x04,
        Rectangle                 = 0x09,
        Stride                    = 0x0D,
        SmallVQ                   = 0x10
    }

    public static class PVRTextureDecoder
    {
        public static Texture2D DecodePVR(byte[] pvrBytes, string textureName = "pvr_texture")
        {
            if (pvrBytes == null || pvrBytes.Length < 16) return null;

            int pvrtOffset = 0;
            if (pvrBytes[0] == 'G' && pvrBytes[1] == 'B' && pvrBytes[2] == 'I' && pvrBytes[3] == 'X')
            {
                uint gbixLen = ByteConverter.ToUInt32(pvrBytes, 4);
                pvrtOffset = 8 + (int)gbixLen;
            }

            if (pvrtOffset + 16 > pvrBytes.Length ||
                pvrBytes[pvrtOffset] != 'P' || pvrBytes[pvrtOffset + 1] != 'V' ||
                pvrBytes[pvrtOffset + 2] != 'R' || pvrBytes[pvrtOffset + 3] != 'T')
            {
                return null;
            }

            PVRColorFormat colorFormat = (PVRColorFormat)pvrBytes[pvrtOffset + 8];
            PVRDataFormat dataFormat = (PVRDataFormat)pvrBytes[pvrtOffset + 9];
            int width = ByteConverter.ToUInt16(pvrBytes, pvrtOffset + 12);
            int height = ByteConverter.ToUInt16(pvrBytes, pvrtOffset + 14);

            int pixelDataOffset = pvrtOffset + 16;
            if (width <= 0 || height <= 0 || pixelDataOffset >= pvrBytes.Length)
                return null;

            Color32[] pixels = new Color32[width * height];

            switch (dataFormat)
            {
                case PVRDataFormat.Rectangle:
                case PVRDataFormat.Stride:
                    DecodeLinear(pvrBytes, pixelDataOffset, width, height, colorFormat, pixels);
                    break;

                case PVRDataFormat.SquareTwiddled:
                case PVRDataFormat.SquareTwiddledMipmapped:
                    DecodeTwiddled(pvrBytes, pixelDataOffset, width, height, colorFormat, pixels);
                    break;

                case PVRDataFormat.VQ:
                case PVRDataFormat.VQ_Mipmapped:
                case PVRDataFormat.SmallVQ:
                    DecodeVQ(pvrBytes, pixelDataOffset, width, height, colorFormat, pixels);
                    break;

                default:
                    DecodeTwiddled(pvrBytes, pixelDataOffset, width, height, colorFormat, pixels);
                    break;
            }

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = UnityEngine.FilterMode.Bilinear
            };

            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            return tex;
        }

        private static void DecodeLinear(byte[] data, int offset, int width, int height, PVRColorFormat colorFormat, Color32[] output)
        {
            int cursor = offset;
            for (int y = 0; y < height; y++)
            {
                int rowStart = (height - 1 - y) * width; // Direct vertical inversion for Unity UV origin
                for (int x = 0; x < width; x++)
                {
                    if (cursor + 2 > data.Length) return;
                    ushort raw = (ushort)(data[cursor] | (data[cursor + 1] << 8));
                    cursor += 2;
                    output[rowStart + x] = DecodePixel(raw, colorFormat);
                }
            }
        }

        private static void DecodeTwiddled(byte[] data, int offset, int width, int height, PVRColorFormat colorFormat, Color32[] output)
        {
            for (int y = 0; y < height; y++)
            {
                int rowStart = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    // PowerVR hardware bit interleaving: Y is even bit, X is odd bit
                    int morton = UntwiddlePVR(x, y);
                    int cursor = offset + morton * 2;
                    if (cursor + 2 <= data.Length)
                    {
                        ushort raw = (ushort)(data[cursor] | (data[cursor + 1] << 8));
                        output[rowStart + x] = DecodePixel(raw, colorFormat);
                    }
                }
            }
        }

        private static void DecodeVQ(byte[] data, int offset, int width, int height, PVRColorFormat colorFormat, Color32[] output)
        {
            int codebookOffset = offset;
            int indicesOffset = offset + 2048;

            Color32[] codebook = new Color32[256 * 4];
            int cbCursor = codebookOffset;

            for (int i = 0; i < 256 * 4 && cbCursor + 2 <= data.Length; i++)
            {
                ushort raw = (ushort)(data[cbCursor] | (data[cbCursor + 1] << 8));
                cbCursor += 2;
                codebook[i] = DecodePixel(raw, colorFormat);
            }

            int blocksW = width / 2;
            int blocksH = height / 2;

            for (int by = 0; by < blocksH; by++)
            {
                for (int bx = 0; bx < blocksW; bx++)
                {
                    int morton = UntwiddlePVR(bx, by);
                    int idxPos = indicesOffset + morton;
                    if (idxPos >= data.Length) continue;

                    int codebookIdx = data[idxPos] * 4;

                    int px = bx * 2;
                    int py = (blocksH - 1 - by) * 2;

                    SetPixelSafe(output, width, height, px + 0, py + 1, codebook[codebookIdx + 0]);
                    SetPixelSafe(output, width, height, px + 0, py + 0, codebook[codebookIdx + 1]);
                    SetPixelSafe(output, width, height, px + 1, py + 1, codebook[codebookIdx + 2]);
                    SetPixelSafe(output, width, height, px + 1, py + 0, codebook[codebookIdx + 3]);
                }
            }
        }

        private static void SetPixelSafe(Color32[] output, int w, int h, int x, int y, Color32 col)
        {
            if (x >= 0 && x < w && y >= 0 && y < h)
            {
                output[y * w + x] = col;
            }
        }

        /// <summary>
        /// Sega Dreamcast CLX2 PowerVR Morton unswizzle:
        /// Bit i of Y -> Bit 2*i
        /// Bit i of X -> Bit 2*i + 1
        /// </summary>
        private static int UntwiddlePVR(int x, int y)
        {
            int res = 0;
            for (int i = 0; i < 16; i++)
            {
                res |= ((y & (1 << i)) << i) | ((x & (1 << i)) << (i + 1));
            }
            return res;
        }

        private static Color32 DecodePixel(ushort val, PVRColorFormat fmt)
        {
            switch (fmt)
            {
                case PVRColorFormat.ARGB1555:
                {
                    byte a = (byte)(((val & 0x8000) != 0) ? 255 : 0);
                    byte r = (byte)(((val >> 10) & 0x1F) * 255 / 31);
                    byte g = (byte)(((val >> 5) & 0x1F) * 255 / 31);
                    byte b = (byte)((val & 0x1F) * 255 / 31);
                    return new Color32(r, g, b, a);
                }
                case PVRColorFormat.RGB565:
                {
                    byte r = (byte)(((val >> 11) & 0x1F) * 255 / 31);
                    byte g = (byte)(((val >> 5) & 0x3F) * 255 / 63);
                    byte b = (byte)((val & 0x1F) * 255 / 31);
                    return new Color32(r, g, b, 255);
                }
                case PVRColorFormat.ARGB4444:
                {
                    byte a = (byte)(((val >> 12) & 0x0F) * 0x11);
                    byte r = (byte)(((val >> 8) & 0x0F) * 0x11);
                    byte g = (byte)(((val >> 4) & 0x0F) * 0x11);
                    byte b = (byte)((val & 0x0F) * 0x11);
                    return new Color32(r, g, b, a);
                }
                default:
                {
                    byte r = (byte)(((val >> 11) & 0x1F) * 255 / 31);
                    byte g = (byte)(((val >> 5) & 0x3F) * 255 / 63);
                    byte b = (byte)((val & 0x1F) * 255 / 31);
                    return new Color32(r, g, b, 255);
                }
            }
        }
    }
}