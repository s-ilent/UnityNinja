using System;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja.GC
{
    public static class GCColorReader
    {
        public static Color32 Read(byte[] file, int address, GCDataType dataType, out int nextAddress)
        {
            nextAddress = address;
            if (address >= file.Length) return new Color32(255, 255, 255, 255);

            switch (dataType)
            {
                case GCDataType.RGB565:
                {
                    ushort s = ByteConverter.ToUInt16(file, address);
                    byte r = (byte)(((s >> 11) & 0x1F) * 255 / 31);
                    byte g = (byte)(((s >> 5) & 0x3F) * 255 / 63);
                    byte b = (byte)((s & 0x1F) * 255 / 31);
                    nextAddress = address + 2;
                    return new Color32(r, g, b, 255);
                }
                case GCDataType.RGBA4:
                {
                    ushort s = ByteConverter.ToUInt16(file, address);
                    byte r = (byte)(((s >> 12) & 0x0F) * 0x11);
                    byte g = (byte)(((s >> 8) & 0x0F) * 0x11);
                    byte b = (byte)(((s >> 4) & 0x0F) * 0x11);
                    byte a = (byte)((s & 0x0F) * 0x11);
                    nextAddress = address + 2;
                    return new Color32(r, g, b, a);
                }
                case GCDataType.RGB8 or GCDataType.RGBX8:
                {
                    byte r = file[address];
                    byte g = file[address + 1];
                    byte b = file[address + 2];
                    nextAddress = address + 4;
                    return new Color32(r, g, b, 255);
                }
                case GCDataType.RGBA8:
                default:
                {
                    byte r = file[address];
                    byte g = file[address + 1];
                    byte b = file[address + 2];
                    byte a = (address + 3 < file.Length) ? file[address + 3] : (byte)255;
                    nextAddress = address + 4;
                    return new Color32(r, g, b, a);
                }
            }
        }
    }
}