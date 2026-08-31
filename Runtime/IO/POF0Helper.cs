using System;
using System.Collections.Generic;

namespace UnityNinja.IO
{
    public static class POF0Helper
    {
        private enum POFOffsetType : byte
        {
            Padding = 0x00,
            Char = 0x40,
            Short = 0x80,
            Long = 0xC0,
            TypeMask = 0xC0,
            DataMask = 0x3F
        }

        public static List<int> GetPointerListFromPOF(byte[] pofData)
        {
            List<int> offsets = new List<int>();
            int currentOffset = 0;

            while (currentOffset < pofData.Length)
            {
                byte first = (byte)(pofData[currentOffset] & (byte)POFOffsetType.DataMask);
                POFOffsetType type = (POFOffsetType)(pofData[currentOffset] & (byte)POFOffsetType.TypeMask);
                currentOffset++;

                switch (type)
                {
                    case POFOffsetType.Padding:
                        break;
                    case POFOffsetType.Char:
                        offsets.Add(4 * first);
                        break;
                    case POFOffsetType.Short:
                        if (currentOffset >= pofData.Length) break;
                        byte second = pofData[currentOffset++];
                        offsets.Add(4 * ((first << 8) | second));
                        break;
                    case POFOffsetType.Long:
                        if (currentOffset + 2 >= pofData.Length) break;
                        byte s2 = pofData[currentOffset++];
                        byte s3 = pofData[currentOffset++];
                        byte s4 = pofData[currentOffset++];
                        offsets.Add(4 * ((first << 24) | (s2 << 16) | (s3 << 8) | s4));
                        break;
                }
            }

            return offsets;
        }

        public static void FixPointersWithPOF(byte[] data, List<int> pointerList, int imgBase)
        {
            int currentPos = 0;
            foreach (int pointer in pointerList)
            {
                currentPos += pointer;
                if (currentPos + 4 > data.Length) break;

                int oldPointer = ByteConverter.ToInt32(data, currentPos);
                if (oldPointer != 0)
                {
                    oldPointer += imgBase;
                    byte[] newBytes = ByteConverter.GetBytes(oldPointer);
                    Array.Copy(newBytes, 0, data, currentPos, 4);
                }
            }
        }
    }
}