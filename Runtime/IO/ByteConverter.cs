using System;
using System.Collections.Generic;

namespace UnityNinja.IO
{
    public static class ByteConverter
    {
        public static bool BigEndian { get; set; } = false;

        private static readonly List<bool> s_EndianStack = new List<bool>();

        public static void SetBigEndian(bool value)
        {
            BackupBigEndian();
            BigEndian = value;
        }

        public static void BackupBigEndian()
        {
            s_EndianStack.Add(BigEndian);
        }

        public static void RestoreBigEndian()
        {
            if (s_EndianStack.Count == 0) return;
            BigEndian = s_EndianStack[s_EndianStack.Count - 1];
            s_EndianStack.RemoveAt(s_EndianStack.Count - 1);
        }

        public static ushort ToUInt16(byte[] value, int startIndex)
        {
            if (startIndex + 2 > value.Length) return 0;
            return BigEndian
                ? (ushort)((value[startIndex] << 8) | value[startIndex + 1])
                : (ushort)(value[startIndex] | (value[startIndex + 1] << 8));
        }

        public static short ToInt16(byte[] value, int startIndex)
        {
            return (short)ToUInt16(value, startIndex);
        }

        public static uint ToUInt32(byte[] value, int startIndex)
        {
            if (startIndex + 4 > value.Length) return 0;
            return BigEndian
                ? (uint)((value[startIndex] << 24) | (value[startIndex + 1] << 16) | (value[startIndex + 2] << 8) | value[startIndex + 3])
                : (uint)(value[startIndex] | (value[startIndex + 1] << 8) | (value[startIndex + 2] << 16) | (value[startIndex + 3] << 24));
        }

        public static int ToInt32(byte[] value, int startIndex)
        {
            return (int)ToUInt32(value, startIndex);
        }

        public static float ToSingle(byte[] value, int startIndex)
        {
            uint raw = ToUInt32(value, startIndex);
            unsafe
            {
                float f = *(float*)&raw;
                return float.IsNaN(f) || float.IsInfinity(f) ? 0.0f : f;
            }
        }

        public static ushort ToUInt16BE(byte[] value, int startIndex)
        {
            if (startIndex + 2 > value.Length) return 0;
            return (ushort)((value[startIndex] << 8) | value[startIndex + 1]);
        }

        public static uint ToUInt32BE(byte[] value, int startIndex)
        {
            if (startIndex + 4 > value.Length) return 0;
            return (uint)((value[startIndex] << 24) | (value[startIndex + 1] << 16) | (value[startIndex + 2] << 8) | value[startIndex + 3]);
        }

        public static int ToInt32BE(byte[] value, int startIndex)
        {
            return (int)ToUInt32BE(value, startIndex);
        }

        public static byte[] GetBytes(ushort value)
        {
            byte[] b = BitConverter.GetBytes(value);
            if (BigEndian) Array.Reverse(b);
            return b;
        }

        public static byte[] GetBytes(short value) => GetBytes((ushort)value);

        public static byte[] GetBytes(uint value)
        {
            byte[] b = BitConverter.GetBytes(value);
            if (BigEndian) Array.Reverse(b);
            return b;
        }

        public static byte[] GetBytes(int value) => GetBytes((uint)value);

        public static byte[] GetBytes(float value)
        {
            byte[] b = BitConverter.GetBytes(value);
            if (BigEndian) Array.Reverse(b);
            return b;
        }
    }
}