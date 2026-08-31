using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;
using UnityNinja.GC;
using UnityNinja.XJ;

namespace UnityNinja
{
    [Serializable]
    public class NJS_OBJECT
    {
        public string Name { get; set; } = "object_00000000";
        public NinjaAttach Attach { get; set; }

        public Vector3 Position = Vector3.zero;
        public Vector3 Rotation = Vector3.zero; // Degrees
        public Vector3 Scale = Vector3.one;

        public NJS_OBJECT Parent { get; private set; }
        public List<NJS_OBJECT> Children { get; private set; } = new List<NJS_OBJECT>();
        public NJS_OBJECT Sibling { get; private set; }

        public ObjectFlags Flags { get; set; }

        public bool IgnorePosition => (Flags & ObjectFlags.NoPosition) != 0;
        public bool IgnoreRotation => (Flags & ObjectFlags.NoRotate) != 0;
        public bool IgnoreScale => (Flags & ObjectFlags.NoScale) != 0;
        public bool SkipDraw => (Flags & ObjectFlags.NoDisplay) != 0;
        public bool SkipChildren => (Flags & ObjectFlags.NoChildren) != 0;
        public bool RotateZYX => (Flags & ObjectFlags.RotateZYX) != 0;
        public bool Animate => (Flags & ObjectFlags.NoAnimate) == 0;

        public NJS_OBJECT() { }

        public NJS_OBJECT(byte[] file, int address, uint imageBase, ModelFormat format, Dictionary<int, string> labels = null)
            : this(file, address, imageBase, format, null, labels ?? new Dictionary<int, string>())
        {
        }

        private NJS_OBJECT(byte[] file, int address, uint imageBase, ModelFormat format, NJS_OBJECT parent, Dictionary<int, string> labels)
        {
            if (address + 52 > file.Length) return;

            if (labels.TryGetValue(address, out string lbl))
                Name = lbl;
            else
                Name = $"object_{address:X8}";

            Flags = (ObjectFlags)ByteConverter.ToInt32(file, address);

            int attachAddr = ByteConverter.ToInt32(file, address + 4);
            if (attachAddr != 0)
            {
                attachAddr = (int)((uint)attachAddr - imageBase);
                if (attachAddr >= 0 && attachAddr < file.Length)
                {
                    Attach = NinjaAttach.Load(file, attachAddr, imageBase, format, labels);
                }
            }

            Position = new NinjaVertex(file, address + 8).ToVector3();
            Rotation = new NinjaRotation(file, address + 20).ToDegrees();
            Scale = new NinjaVertex(file, address + 32).ToVector3();
            if (Scale == Vector3.zero) Scale = Vector3.one;

            Parent = parent;

            int childAddr = ByteConverter.ToInt32(file, address + 44);
            if (childAddr != 0)
            {
                childAddr = (int)((uint)childAddr - imageBase);
                if (childAddr >= 0 && childAddr < file.Length)
                {
                    NJS_OBJECT child = new NJS_OBJECT(file, childAddr, imageBase, format, this, labels);
                    while (child != null)
                    {
                        Children.Add(child);
                        child = child.Sibling;
                    }
                }
            }

            int siblingAddr = ByteConverter.ToInt32(file, address + 48);
            if (siblingAddr != 0)
            {
                siblingAddr = (int)((uint)siblingAddr - imageBase);
                if (siblingAddr >= 0 && siblingAddr < file.Length)
                {
                    Sibling = new NJS_OBJECT(file, siblingAddr, imageBase, format, parent, labels);
                }
            }
        }

        public int CountAll()
        {
            int result = 1;
            foreach (var item in Children)
                result += item.CountAll();
            if (Parent == null && Sibling != null)
                result += Sibling.CountAll();
            return result;
        }

        public int CountAnimated()
        {
            int result = Animate ? 1 : 0;
            foreach (var item in Children)
                result += item.CountAnimated();
            if (Parent == null && Sibling != null)
                result += Sibling.CountAnimated();
            return result;
        }

        public int CountAllVertices()
        {
            int total = 0;
            foreach (var obj in EnumerateNodes())
            {
                if (obj.Attach is BasicAttach bs && bs.Vertices != null)
                    total += bs.Vertices.Length;
                else if (obj.Attach is ChunkAttach cnk)
                {
                    foreach (var vc in cnk.VertexChunks)
                    {
                        if (vc.WeightStatus != WeightStatus.Middle)
                            total += vc.VertexCount;
                    }
                }
                else if (obj.Attach is GCAttach gc)
                {
                    var pos = gc.VertexData.Find(x => x.Attribute == GCVertexAttribute.Position)?.Positions;
                    if (pos != null) total += pos.Count;
                }
                else if (obj.Attach is XJAttach xj && xj.VertexSets.Count > 0)
                {
                    total += xj.VertexSets[0].Positions.Count;
                }
            }
            return total;
        }

        public IEnumerable<NJS_OBJECT> EnumerateNodes()
        {
            yield return this;
            foreach (var c in Children)
            {
                foreach (var sub in c.EnumerateNodes())
                    yield return sub;
            }
            if (Parent == null && Sibling != null)
            {
                foreach (var s in Sibling.EnumerateNodes())
                    yield return s;
            }
        }
    }
}