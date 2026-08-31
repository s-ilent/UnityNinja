using System;
using System.Collections.Generic;
using UnityNinja.IO;

namespace UnityNinja.GC
{
    [Serializable]
    public struct GCLoop
    {
        public ushort PositionIndex;
        public ushort NormalIndex;
        public ushort Color0Index;
        public ushort UV0Index;
    }

    [Serializable]
    public class GCPrimitive
    {
        public GCPrimitiveType PrimitiveType;
        public List<GCLoop> Loops = new List<GCLoop>();

        public GCPrimitive(byte[] file, int address, GCIndexAttributeFlags indexFlags, out int endAddress)
        {
            PrimitiveType = (GCPrimitiveType)file[address];
            ushort vtxCount = ByteConverter.ToUInt16BE(file, address + 1);

            bool hasNormal = (indexFlags & GCIndexAttributeFlags.HasNormal) != 0;
            bool hasColor = (indexFlags & GCIndexAttributeFlags.HasColor) != 0;
            bool hasUV = (indexFlags & GCIndexAttributeFlags.HasUV) != 0;

            bool pos16 = (indexFlags & GCIndexAttributeFlags.Position16BitIndex) != 0;
            bool nrm16 = (indexFlags & GCIndexAttributeFlags.Normal16BitIndex) != 0;
            bool col16 = (indexFlags & GCIndexAttributeFlags.Color16BitIndex) != 0;
            bool uv16 = (indexFlags & GCIndexAttributeFlags.UV16BitIndex) != 0;

            int cursor = address + 3;

            for (ushort i = 0; i < vtxCount && cursor < file.Length; i++)
            {
                GCLoop loop = new GCLoop();

                if (pos16) { loop.PositionIndex = ByteConverter.ToUInt16BE(file, cursor); cursor += 2; }
                else { loop.PositionIndex = file[cursor++]; }

                if (hasNormal)
                {
                    if (nrm16) { loop.NormalIndex = ByteConverter.ToUInt16BE(file, cursor); cursor += 2; }
                    else { loop.NormalIndex = file[cursor++]; }
                }

                if (hasColor)
                {
                    if (col16) { loop.Color0Index = ByteConverter.ToUInt16BE(file, cursor); cursor += 2; }
                    else { loop.Color0Index = file[cursor++]; }
                }

                if (hasUV)
                {
                    if (uv16) { loop.UV0Index = ByteConverter.ToUInt16BE(file, cursor); cursor += 2; }
                    else { loop.UV0Index = file[cursor++]; }
                }

                Loops.Add(loop);
            }

            endAddress = cursor;
        }

        public List<GCLoop> ToTriangles()
        {
            List<GCLoop> triangles = new List<GCLoop>();

            if (PrimitiveType == GCPrimitiveType.Triangles)
            {
                return Loops;
            }
            if (PrimitiveType == GCPrimitiveType.TriangleStrip)
            {
                bool isEven = false;
                for (int v = 2; v < Loops.Count; v++)
                {
                    GCLoop a = Loops[v - 2];
                    GCLoop b = isEven ? Loops[v] : Loops[v - 1];
                    GCLoop c = isEven ? Loops[v - 1] : Loops[v];
                    isEven = !isEven;

                    if (a.PositionIndex != b.PositionIndex && b.PositionIndex != c.PositionIndex && a.PositionIndex != c.PositionIndex)
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                    }
                }
            }
            else if (PrimitiveType == GCPrimitiveType.TriangleFan)
            {
                for (int v = 1; v < Loops.Count - 1; v++)
                {
                    GCLoop a = Loops[0];
                    GCLoop b = Loops[v];
                    GCLoop c = Loops[v + 1];

                    if (a.PositionIndex != b.PositionIndex && b.PositionIndex != c.PositionIndex && a.PositionIndex != c.PositionIndex)
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                    }
                }
            }

            return triangles;
        }
    }
}