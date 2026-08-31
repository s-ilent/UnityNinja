using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class NJS_MATERIAL
    {
        public Color32 DiffuseColor = new Color32(255, 255, 255, 255);
        public Color32 SpecularColor = new Color32(0, 0, 0, 0);
        public float Exponent = 0.0f;
        public int TextureID = 0;
        public uint Flags = 0;

        public const int Size = 20;

        public bool UseAlpha => (Flags & 0x100000) != 0;
        public bool UseTexture => (Flags & 0x200000) != 0;
        public bool EnvironmentMap => (Flags & 0x400000) != 0;
        public bool DoubleSided => (Flags & 0x800000) != 0;
        public bool IgnoreLighting => (Flags & 0x2000000) != 0;
        public bool ClampU => (Flags & 0x10000) != 0;
        public bool ClampV => (Flags & 0x8000) != 0;
        public bool FlipU => (Flags & 0x40000) != 0;
        public bool FlipV => (Flags & 0x20000) != 0;

        public AlphaInstruction DestinationAlpha => (AlphaInstruction)((Flags >> 26) & 7);
        public AlphaInstruction SourceAlpha => (AlphaInstruction)((Flags >> 29) & 7);

        public NJS_MATERIAL() { }

        public NJS_MATERIAL(byte[] file, int address, Dictionary<int, string> labels = null)
        {
            if (address + Size > file.Length) return;

            DiffuseColor = NinjaColor.FromArgb32(ByteConverter.ToUInt32(file, address));
            SpecularColor = NinjaColor.FromArgb32(ByteConverter.ToUInt32(file, address + 4));
            Exponent = ByteConverter.ToSingle(file, address + 8);
            TextureID = ByteConverter.ToInt32(file, address + 12);
            Flags = ByteConverter.ToUInt32(file, address + 16);
        }
    }
}