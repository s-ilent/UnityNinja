using System;
using System.Collections.Generic;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public abstract class NinjaPoly
    {
        public ushort[] Indexes;
        public abstract Basic_PolyType PolyType { get; }
        public virtual int Size => Indexes != null ? Indexes.Length * 2 : 0;

        public static NinjaPoly CreatePoly(Basic_PolyType type, byte[] file, int address)
        {
            return type switch
            {
                Basic_PolyType.Triangles => new NinjaTriangle(file, address),
                Basic_PolyType.Quads => new NinjaQuad(file, address),
                Basic_PolyType.Strips or Basic_PolyType.NPoly => new NinjaStrip(file, address),
                _ => null
            };
        }
    }

    [Serializable]
    public class NinjaTriangle : NinjaPoly
    {
        public override Basic_PolyType PolyType => Basic_PolyType.Triangles;
        public NinjaTriangle(byte[] file, int address)
        {
            Indexes = new ushort[3];
            Indexes[0] = ByteConverter.ToUInt16(file, address);
            Indexes[1] = ByteConverter.ToUInt16(file, address + 2);
            Indexes[2] = ByteConverter.ToUInt16(file, address + 4);
        }
    }

    [Serializable]
    public class NinjaQuad : NinjaPoly
    {
        public override Basic_PolyType PolyType => Basic_PolyType.Quads;
        public NinjaQuad(byte[] file, int address)
        {
            Indexes = new ushort[4];
            Indexes[0] = ByteConverter.ToUInt16(file, address);
            Indexes[1] = ByteConverter.ToUInt16(file, address + 2);
            Indexes[2] = ByteConverter.ToUInt16(file, address + 4);
            Indexes[3] = ByteConverter.ToUInt16(file, address + 6);
        }
    }

    [Serializable]
    public class NinjaStrip : NinjaPoly
    {
        public bool Reversed { get; private set; }
        public override Basic_PolyType PolyType => Basic_PolyType.Strips;
        public override int Size => (Indexes.Length * 2) + 2;

        public NinjaStrip(byte[] file, int address)
        {
            ushort rawCount = ByteConverter.ToUInt16(file, address);
            int count = rawCount & 0x7FFF;
            Reversed = (rawCount & 0x8000) != 0;

            Indexes = new ushort[count];
            address += 2;
            for (int i = 0; i < count; i++)
            {
                Indexes[i] = ByteConverter.ToUInt16(file, address);
                address += 2;
            }
        }
    }
}