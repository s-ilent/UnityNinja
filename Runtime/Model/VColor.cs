using System;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    public static class NinjaColor
    {
        public static Color32 FromBytes(byte[] file, int address, bool is16Bit = false)
        {
            if (address >= file.Length) return new Color32(255, 255, 255, 255);

            if (is16Bit)
            {
                if (address + 4 > file.Length) return new Color32(255, 255, 255, 255);
                ushort low = ByteConverter.ToUInt16(file, address);
                ushort high = ByteConverter.ToUInt16(file, address + 2);
                uint val = (uint)((high << 16) | low);
                return FromArgb32(val);
            }

            if (address + 4 > file.Length) return new Color32(255, 255, 255, 255);
            uint raw = ByteConverter.ToUInt32(file, address);
            return FromArgb32(raw);
        }

        public static Color32 FromArgb32(uint argb)
        {
            byte a = (byte)((argb >> 24) & 0xFF);
            byte r = (byte)((argb >> 16) & 0xFF);
            byte g = (byte)((argb >> 8) & 0xFF);
            byte b = (byte)(argb & 0xFF);
            return new Color32(r, g, b, a);
        }
    }
}