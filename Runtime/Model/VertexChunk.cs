using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class VertexChunk
    {
        public ChunkType Type;
        public byte Flags;
        public WeightStatus WeightStatus => (WeightStatus)(Flags & 3);
        public ushort Size;
        public ushort IndexOffset;
        public int VertexCount;

        public List<Vector3> Vertices = new List<Vector3>();
        public List<Vector3> Normals = new List<Vector3>();
        public List<Color32> Diffuse = new List<Color32>();
        public List<uint> NinjaFlags = new List<uint>();

        public VertexChunk(byte[] file, int address)
        {
            uint h1 = ByteConverter.ToUInt32(file, address);
            uint h2 = ByteConverter.ToUInt32(file, address + 4);

            Type = (ChunkType)(h1 & 0xFF);
            Flags = (byte)((h1 >> 8) & 0xFF);
            Size = (ushort)(h1 >> 16);

            IndexOffset = (ushort)(h2 & 0xFFFF);
            VertexCount = (int)(h2 >> 16);

            address += 8;

            for (int i = 0; i < VertexCount && address < file.Length; i++)
            {
                switch (Type)
                {
                    case ChunkType.Vertex_Vertex:
                        Vertices.Add(new Vector3(ByteConverter.ToSingle(file, address), ByteConverter.ToSingle(file, address + 4), ByteConverter.ToSingle(file, address + 8)));
                        address += 12;
                        break;

                    case ChunkType.Vertex_VertexNormal:
                        Vertices.Add(new Vector3(ByteConverter.ToSingle(file, address), ByteConverter.ToSingle(file, address + 4), ByteConverter.ToSingle(file, address + 8)));
                        Normals.Add(new Vector3(ByteConverter.ToSingle(file, address + 12), ByteConverter.ToSingle(file, address + 16), ByteConverter.ToSingle(file, address + 20)));
                        address += 24;
                        break;

                    case ChunkType.Vertex_VertexDiffuse8:
                        Vertices.Add(new Vector3(ByteConverter.ToSingle(file, address), ByteConverter.ToSingle(file, address + 4), ByteConverter.ToSingle(file, address + 8)));
                        Diffuse.Add(NinjaColor.FromBytes(file, address + 12));
                        address += 16;
                        break;

                    case ChunkType.Vertex_VertexNormalDiffuse8:
                        Vertices.Add(new Vector3(ByteConverter.ToSingle(file, address), ByteConverter.ToSingle(file, address + 4), ByteConverter.ToSingle(file, address + 8)));
                        Normals.Add(new Vector3(ByteConverter.ToSingle(file, address + 12), ByteConverter.ToSingle(file, address + 16), ByteConverter.ToSingle(file, address + 20)));
                        Diffuse.Add(NinjaColor.FromBytes(file, address + 24));
                        address += 28;
                        break;

                    case ChunkType.Vertex_VertexNinjaFlags:
                        Vertices.Add(new Vector3(ByteConverter.ToSingle(file, address), ByteConverter.ToSingle(file, address + 4), ByteConverter.ToSingle(file, address + 8)));
                        NinjaFlags.Add(ByteConverter.ToUInt32(file, address + 12));
                        address += 16;
                        break;

                    case ChunkType.Vertex_VertexNormalNinjaFlags:
                        Vertices.Add(new Vector3(ByteConverter.ToSingle(file, address), ByteConverter.ToSingle(file, address + 4), ByteConverter.ToSingle(file, address + 8)));
                        Normals.Add(new Vector3(ByteConverter.ToSingle(file, address + 12), ByteConverter.ToSingle(file, address + 16), ByteConverter.ToSingle(file, address + 20)));
                        NinjaFlags.Add(ByteConverter.ToUInt32(file, address + 24));
                        address += 28;
                        break;

                    default:
                        // Advance general 12 bytes minimum if unrecognized
                        address += 12;
                        break;
                }
            }
        }
    }
}