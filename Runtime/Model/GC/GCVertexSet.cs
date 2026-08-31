using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja.GC
{
    [Serializable]
    public class GCVertexSet
    {
        public GCVertexAttribute Attribute;
        public GCDataType DataType;
        public GCStructType StructType;
        public byte StructSize;

        public List<Vector3> Positions = new List<Vector3>();
        public List<Vector3> Normals = new List<Vector3>();
        public List<Color32> Colors = new List<Color32>();
        public List<Vector2> UVs = new List<Vector2>();

        public GCVertexSet(byte[] file, int address, uint imageBase, Dictionary<int, string> labels = null)
        {
            Attribute = (GCVertexAttribute)file[address];
            if (Attribute == GCVertexAttribute.Null) return;

            StructSize = file[address + 1];
            ushort count = ByteConverter.ToUInt16(file, address + 2);
            uint structure = ByteConverter.ToUInt32(file, address + 4);

            StructType = (GCStructType)(structure & 0x0F);
            DataType = (GCDataType)((structure >> 4) & 0x0F);

            int dataAddr = (int)(ByteConverter.ToUInt32(file, address + 8) - imageBase);
            if (dataAddr < 0 || dataAddr >= file.Length) return;

            switch (Attribute)
            {
                case GCVertexAttribute.Position:
                    for (int i = 0; i < count && dataAddr + 12 <= file.Length; i++)
                    {
                        Positions.Add(new Vector3(ByteConverter.ToSingle(file, dataAddr), ByteConverter.ToSingle(file, dataAddr + 4), ByteConverter.ToSingle(file, dataAddr + 8)));
                        dataAddr += 12;
                    }
                    break;

                case GCVertexAttribute.Normal:
                    for (int i = 0; i < count && dataAddr + 12 <= file.Length; i++)
                    {
                        Normals.Add(new Vector3(ByteConverter.ToSingle(file, dataAddr), ByteConverter.ToSingle(file, dataAddr + 4), ByteConverter.ToSingle(file, dataAddr + 8)));
                        dataAddr += 12;
                    }
                    break;

                case GCVertexAttribute.Color0:
                    for (int i = 0; i < count && dataAddr < file.Length; i++)
                    {
                        Colors.Add(GCColorReader.Read(file, dataAddr, DataType, out dataAddr));
                    }
                    break;

                case GCVertexAttribute.Tex0:
                    for (int i = 0; i < count && dataAddr + 4 <= file.Length; i++)
                    {
                        float u = ByteConverter.ToInt16(file, dataAddr) / 256.0f;
                        float v = ByteConverter.ToInt16(file, dataAddr + 2) / 256.0f;
                        UVs.Add(new Vector2(u, v));
                        dataAddr += 4;
                    }
                    break;
            }
        }
    }
}