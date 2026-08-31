using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    [Flags]
    public enum AnimFlags : ushort
    {
        Position   = 0x1,
        Rotation   = 0x2,
        Scale      = 0x4,
        Vector     = 0x8,
        Vertex     = 0x10,
        Normal     = 0x20,
        Target     = 0x40,
        Roll       = 0x80,
        Angle      = 0x100,
        Color      = 0x200,
        Intensity  = 0x400,
        Spot       = 0x800,
        Point      = 0x1000,
        Quaternion = 0x2000,
        ShapeID    = 0x4000,
        Event      = 0x8000
    }

    public enum InterpolationMode
    {
        Linear,
        Spline,
        User
    }

    [Serializable]
    public class NJS_MOTION
    {
        public string Name { get; set; } = "animation_00000000";
        public int Frames { get; set; }
        public int ModelParts { get; set; }
        public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Linear;
        public bool ShortRot { get; set; }
        public AnimFlags Flags { get; set; }

        public Dictionary<int, AnimModelData> Models { get; } = new Dictionary<int, AnimModelData>();

        public NJS_MOTION() { }

        public NJS_MOTION(byte[] file, int address, uint imageBase, int numModels, Dictionary<int, string> labels = null, bool shortRot = false)
        {
            if (address + 12 > file.Length) return;

            if (labels != null && labels.TryGetValue(address, out string lbl))
                Name = lbl;
            else
                Name = $"animation_{address:X8}";

            Frames = ByteConverter.ToInt32(file, address + 4);
            Flags = (AnimFlags)ByteConverter.ToUInt16(file, address + 8);
            ushort fn = ByteConverter.ToUInt16(file, address + 10);

            InterpolationMode = (fn & 0xC0) switch
            {
                0x40 => InterpolationMode.Spline,
                0x80 => InterpolationMode.User,
                _ => InterpolationMode.Linear
            };

            ShortRot = shortRot;
            ModelParts = numModels > 0 ? numModels : CalculateModelParts(file, address, imageBase, Flags);

            int mdataAddr = (int)(ByteConverter.ToUInt32(file, address) - imageBase);
            if (mdataAddr < 0 || mdataAddr >= file.Length) return;

            for (int i = 0; i < ModelParts && mdataAddr < file.Length; i++)
            {
                AnimModelData data = new AnimModelData();

                uint posOff = Flags.HasFlag(AnimFlags.Position) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;
                uint rotOff = Flags.HasFlag(AnimFlags.Rotation) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;
                uint sclOff = Flags.HasFlag(AnimFlags.Scale) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;
                uint vecOff = Flags.HasFlag(AnimFlags.Vector) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;
                uint vertOff = Flags.HasFlag(AnimFlags.Vertex) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;
                uint normOff = Flags.HasFlag(AnimFlags.Normal) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;
                uint targOff = Flags.HasFlag(AnimFlags.Target) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;
                uint rollOff = Flags.HasFlag(AnimFlags.Roll) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;
                uint angOff = Flags.HasFlag(AnimFlags.Angle) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;
                uint colOff = Flags.HasFlag(AnimFlags.Color) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;
                uint intOff = Flags.HasFlag(AnimFlags.Intensity) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;
                uint quatOff = Flags.HasFlag(AnimFlags.Quaternion) ? ReadOffset(file, ref mdataAddr, imageBase) : 0;

                // 1. Position Tracks
                if (Flags.HasFlag(AnimFlags.Position))
                {
                    int kfCount = ByteConverter.ToInt32(file, mdataAddr); mdataAddr += 4;
                    if (posOff > 0 && kfCount > 0 && posOff + kfCount * 16 <= file.Length)
                    {
                        int pAddr = (int)posOff;
                        for (int k = 0; k < kfCount; k++)
                        {
                            int f = ByteConverter.ToInt32(file, pAddr);
                            Vector3 v = new Vector3(ByteConverter.ToSingle(file, pAddr + 4), ByteConverter.ToSingle(file, pAddr + 8), ByteConverter.ToSingle(file, pAddr + 12));
                            data.Position[f] = v;
                            pAddr += 16;
                        }
                    }
                }

                // 2. Rotation Tracks
                if (Flags.HasFlag(AnimFlags.Rotation))
                {
                    int kfCount = ByteConverter.ToInt32(file, mdataAddr); mdataAddr += 4;
                    if (rotOff > 0 && kfCount > 0)
                    {
                        int rAddr = (int)rotOff;
                        for (int k = 0; k < kfCount && rAddr < file.Length; k++)
                        {
                            if (ShortRot)
                            {
                                int f = ByteConverter.ToInt16(file, rAddr);
                                short rx = ByteConverter.ToInt16(file, rAddr + 2);
                                short ry = ByteConverter.ToInt16(file, rAddr + 4);
                                short rz = ByteConverter.ToInt16(file, rAddr + 6);
                                data.Rotation[f] = new NinjaRotation(rx, ry, rz);
                                rAddr += 8;
                            }
                            else
                            {
                                int f = ByteConverter.ToInt32(file, rAddr);
                                int rx = ByteConverter.ToInt32(file, rAddr + 4);
                                int ry = ByteConverter.ToInt32(file, rAddr + 8);
                                int rz = ByteConverter.ToInt32(file, rAddr + 12);
                                data.Rotation[f] = new NinjaRotation(rx, ry, rz);
                                rAddr += 16;
                            }
                        }
                    }
                }

                // 3. Scale Tracks
                if (Flags.HasFlag(AnimFlags.Scale))
                {
                    int kfCount = ByteConverter.ToInt32(file, mdataAddr); mdataAddr += 4;
                    if (sclOff > 0 && kfCount > 0 && sclOff + kfCount * 16 <= file.Length)
                    {
                        int sAddr = (int)sclOff;
                        for (int k = 0; k < kfCount; k++)
                        {
                            int f = ByteConverter.ToInt32(file, sAddr);
                            Vector3 s = new Vector3(ByteConverter.ToSingle(file, sAddr + 4), ByteConverter.ToSingle(file, sAddr + 8), ByteConverter.ToSingle(file, sAddr + 12));
                            data.Scale[f] = s;
                            sAddr += 16;
                        }
                    }
                }

                // 4. Vector Tracks
                if (Flags.HasFlag(AnimFlags.Vector))
                {
                    int kfCount = ByteConverter.ToInt32(file, mdataAddr); mdataAddr += 4;
                    if (vecOff > 0 && kfCount > 0 && vecOff + kfCount * 16 <= file.Length)
                    {
                        int vAddr = (int)vecOff;
                        for (int k = 0; k < kfCount; k++)
                        {
                            int f = ByteConverter.ToInt32(file, vAddr);
                            Vector3 vec = new Vector3(ByteConverter.ToSingle(file, vAddr + 4), ByteConverter.ToSingle(file, vAddr + 8), ByteConverter.ToSingle(file, vAddr + 12));
                            data.Vector[f] = vec;
                            vAddr += 16;
                        }
                    }
                }

                if (data.HasData)
                {
                    Models[i] = data;
                }
            }
        }

        private static uint ReadOffset(byte[] file, ref int cursor, uint imageBase)
        {
            uint off = ByteConverter.ToUInt32(file, cursor);
            cursor += 4;
            return off > 0 ? off - imageBase : 0;
        }

        private static int CalculateModelParts(byte[] file, int address, uint imageBase, AnimFlags flags)
        {
            int mdataPtr = (int)(ByteConverter.ToUInt32(file, address) - imageBase);
            if (mdataPtr <= 0 || mdataPtr >= file.Length) return 1;

            int channelCount = 0;
            if (flags.HasFlag(AnimFlags.Position)) channelCount++;
            if (flags.HasFlag(AnimFlags.Rotation)) channelCount++;
            if (flags.HasFlag(AnimFlags.Scale)) channelCount++;
            if (flags.HasFlag(AnimFlags.Vector)) channelCount++;

            int stride = channelCount * 8; // pointer + count per active channel
            if (stride == 0) return 1;

            int count = 0;
            while (mdataPtr + stride <= file.Length && count < 256)
            {
                uint probe = ByteConverter.ToUInt32(file, mdataPtr);
                if (probe != 0 && (probe < imageBase || probe - imageBase >= file.Length)) break;
                count++;
                mdataPtr += stride;
            }

            return Math.Max(1, count);
        }
    }
}