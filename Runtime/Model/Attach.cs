using System;
using System.Collections.Generic;
using UnityNinja.GC;
using UnityNinja.XJ;

namespace UnityNinja
{
    [Serializable]
    public abstract class NinjaAttach
    {
        public string Name { get; set; } = "";
        public NinjaBoundingSphere Bounds { get; set; } = new NinjaBoundingSphere();
        public virtual bool HasWeight => false;

        public static NinjaAttach Load(byte[] file, int address, uint imageBase, ModelFormat format, Dictionary<int, string> labels)
        {
            return format switch
            {
                ModelFormat.Basic or ModelFormat.BasicDX => new BasicAttach(file, address, imageBase, format == ModelFormat.BasicDX, labels),
                ModelFormat.Chunk or ModelFormat.ChaoChunk => new ChunkAttach(file, address, imageBase, labels),
                ModelFormat.GC => new GCAttach(file, address, imageBase, labels),
                ModelFormat.XJ => new XJAttach(file, address, imageBase, labels),
                _ => null
            };
        }
    }
}