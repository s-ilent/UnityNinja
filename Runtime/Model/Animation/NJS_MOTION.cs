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

        public NJS_MOTION(
            byte[] file,
            int address,
            uint imageBase,
            int numModels,
            Dictionary<int, string> labels = null,
            bool shortRot = false,
            int[] numVerts = null)
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

            // Resolve Sega Ninja channel order for NJS_MDATA
            List<string> activeChannels = GetActiveChannels(Flags);
            int channelCount = activeChannels.Count;

            for (int i = 0; i < ModelParts && mdataAddr < file.Length; i++)
            {
                AnimModelData data = new AnimModelData();

                // 1. Read Pointer Offsets
                Dictionary<string, uint> offsets = new Dictionary<string, uint>();
                for (int c = 0; c < channelCount && mdataAddr + 4 <= file.Length; c++)
                {
                    uint off = ByteConverter.ToUInt32(file, mdataAddr);
                    mdataAddr += 4;
                    offsets[activeChannels[c]] = off > 0 ? off - imageBase : 0;
                }

                // 2. Read Keyframe Counts
                Dictionary<string, int> counts = new Dictionary<string, int>();
                for (int c = 0; c < channelCount && mdataAddr + 4 <= file.Length; c++)
                {
                    counts[activeChannels[c]] = ByteConverter.ToInt32(file, mdataAddr);
                    mdataAddr += 4;
                }

                // 3. Read Keyframe Data
                // Position (16 bytes/frame)
                if (offsets.TryGetValue("Position", out uint posOff) && posOff > 0 && counts.TryGetValue("Position", out int posCount) && posCount > 0)
                {
                    int pAddr = (int)posOff;
                    for (int k = 0; k < posCount && pAddr + 16 <= file.Length; k++)
                    {
                        int f = ByteConverter.ToInt32(file, pAddr);
                        Vector3 v = new Vector3(ByteConverter.ToSingle(file, pAddr + 4), ByteConverter.ToSingle(file, pAddr + 8), ByteConverter.ToSingle(file, pAddr + 12));
                        data.Position[f] = v;
                        pAddr += 16;
                    }
                }

                // Rotation (Euler 16 bytes/frame or ShortRot 8 bytes/frame)
                if (offsets.TryGetValue("Rotation", out uint rotOff) && rotOff > 0 && counts.TryGetValue("Rotation", out int rotCount) && rotCount > 0)
                {
                    int rAddr = (int)rotOff;
                    for (int k = 0; k < rotCount && rAddr < file.Length; k++)
                    {
                        if (ShortRot && rAddr + 8 <= file.Length)
                        {
                            int f = ByteConverter.ToInt16(file, rAddr);
                            short rx = ByteConverter.ToInt16(file, rAddr + 2);
                            short ry = ByteConverter.ToInt16(file, rAddr + 4);
                            short rz = ByteConverter.ToInt16(file, rAddr + 6);
                            data.Rotation[f] = new NinjaRotation(rx, ry, rz);
                            rAddr += 8;
                        }
                        else if (rAddr + 16 <= file.Length)
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

                // Quaternion (20 bytes/frame: f, w, x, y, z)
                if (offsets.TryGetValue("Quaternion", out uint quatOff) && quatOff > 0 && counts.TryGetValue("Quaternion", out int quatCount) && quatCount > 0)
                {
                    int qAddr = (int)quatOff;
                    for (int k = 0; k < quatCount && qAddr + 20 <= file.Length; k++)
                    {
                        int f = ByteConverter.ToInt32(file, qAddr);
                        float w = ByteConverter.ToSingle(file, qAddr + 4);
                        float x = ByteConverter.ToSingle(file, qAddr + 8);
                        float y = ByteConverter.ToSingle(file, qAddr + 12);
                        float z = ByteConverter.ToSingle(file, qAddr + 16);
                        data.Quaternion[f] = new Quaternion(x, y, z, w);
                        qAddr += 20;
                    }
                }

                // Scale (16 bytes/frame)
                if (offsets.TryGetValue("Scale", out uint sclOff) && sclOff > 0 && counts.TryGetValue("Scale", out int sclCount) && sclCount > 0)
                {
                    int sAddr = (int)sclOff;
                    for (int k = 0; k < sclCount && sAddr + 16 <= file.Length; k++)
                    {
                        int f = ByteConverter.ToInt32(file, sAddr);
                        Vector3 s = new Vector3(ByteConverter.ToSingle(file, sAddr + 4), ByteConverter.ToSingle(file, sAddr + 8), ByteConverter.ToSingle(file, sAddr + 12));
                        data.Scale[f] = s;
                        sAddr += 16;
                    }
                }

                // Vector / Target (16 bytes/frame)
                if (offsets.TryGetValue("Vector", out uint vecOff) && vecOff > 0 && counts.TryGetValue("Vector", out int vecCount) && vecCount > 0)
                {
                    int vAddr = (int)vecOff;
                    for (int k = 0; k < vecCount && vAddr + 16 <= file.Length; k++)
                    {
                        int f = ByteConverter.ToInt32(file, vAddr);
                        data.Vector[f] = new Vector3(ByteConverter.ToSingle(file, vAddr + 4), ByteConverter.ToSingle(file, vAddr + 8), ByteConverter.ToSingle(file, vAddr + 12));
                        vAddr += 16;
                    }
                }

                if (offsets.TryGetValue("Target", out uint targOff) && targOff > 0 && counts.TryGetValue("Target", out int targCount) && targCount > 0)
                {
                    int tAddr = (int)targOff;
                    for (int k = 0; k < targCount && tAddr + 16 <= file.Length; k++)
                    {
                        int f = ByteConverter.ToInt32(file, tAddr);
                        data.Target[f] = new Vector3(ByteConverter.ToSingle(file, tAddr + 4), ByteConverter.ToSingle(file, tAddr + 8), ByteConverter.ToSingle(file, tAddr + 12));
                        tAddr += 16;
                    }
                }

                // Roll / Angle (8 bytes/frame)
                if (offsets.TryGetValue("Roll", out uint rollOff) && rollOff > 0 && counts.TryGetValue("Roll", out int rollCount) && rollCount > 0)
                {
                    int rlAddr = (int)rollOff;
                    for (int k = 0; k < rollCount && rlAddr + 8 <= file.Length; k++)
                    {
                        int f = ByteConverter.ToInt32(file, rlAddr);
                        data.Roll[f] = ByteConverter.ToInt32(file, rlAddr + 4);
                        rlAddr += 8;
                    }
                }

                if (offsets.TryGetValue("Angle", out uint angOff) && angOff > 0 && counts.TryGetValue("Angle", out int angCount) && angCount > 0)
                {
                    int aAddr = (int)angOff;
                    for (int k = 0; k < angCount && aAddr + 8 <= file.Length; k++)
                    {
                        int f = ByteConverter.ToInt32(file, aAddr);
                        data.Angle[f] = ByteConverter.ToInt32(file, aAddr + 4);
                        aAddr += 8;
                    }
                }

                // Color / Intensity (8 bytes/frame)
                if (offsets.TryGetValue("Color", out uint colOff) && colOff > 0 && counts.TryGetValue("Color", out int colCount) && colCount > 0)
                {
                    int cAddr = (int)colOff;
                    for (int k = 0; k < colCount && cAddr + 8 <= file.Length; k++)
                    {
                        int f = ByteConverter.ToInt32(file, cAddr);
                        data.Color[f] = NinjaColor.FromArgb32(ByteConverter.ToUInt32(file, cAddr + 4));
                        cAddr += 8;
                    }
                }

                if (offsets.TryGetValue("Intensity", out uint intOff) && intOff > 0 && counts.TryGetValue("Intensity", out int intCount) && intCount > 0)
                {
                    int inAddr = (int)intOff;
                    for (int k = 0; k < intCount && inAddr + 8 <= file.Length; k++)
                    {
                        int f = ByteConverter.ToInt32(file, inAddr);
                        data.Intensity[f] = ByteConverter.ToSingle(file, inAddr + 4);
                        inAddr += 8;
                    }
                }

                // Vertex / Normal Morph Tracks
                if (offsets.TryGetValue("Vertex", out uint vertOff) && vertOff > 0 && counts.TryGetValue("Vertex", out int vertCount) && vertCount > 0)
                {
                    int vAddr = (int)vertOff;
                    List<int> ptrs = new List<int>();
                    for (int k = 0; k < vertCount && vAddr + 8 <= file.Length; k++)
                    {
                        ptrs.Add((int)(ByteConverter.ToUInt32(file, vAddr + 4) - imageBase));
                        vAddr += 8;
                    }

                    int vtxCount = (numVerts != null && i < numVerts.Length) ? numVerts[i] : -1;
                    if (vtxCount < 0 && ptrs.Count > 1) vtxCount = Math.Max(1, (ptrs[1] - ptrs[0]) / 12);
                    else if (vtxCount < 0 && ptrs.Count > 0) vtxCount = Math.Max(1, (int)(vertOff - ptrs[0]) / 12);

                    vAddr = (int)vertOff;
                    for (int k = 0; k < vertCount && vAddr + 8 <= file.Length; k++)
                    {
                        int f = ByteConverter.ToInt32(file, vAddr);
                        int dataPtr = (int)(ByteConverter.ToUInt32(file, vAddr + 4) - imageBase);
                        vAddr += 8;

                        if (dataPtr >= 0 && dataPtr + (vtxCount * 12) <= file.Length)
                        {
                            Vector3[] verts = new Vector3[vtxCount];
                            for (int v = 0; v < vtxCount; v++)
                            {
                                verts[v] = new Vector3(
                                    ByteConverter.ToSingle(file, dataPtr + v * 12),
                                    ByteConverter.ToSingle(file, dataPtr + v * 12 + 4),
                                    ByteConverter.ToSingle(file, dataPtr + v * 12 + 8)
                                );
                            }
                            data.Vertex[f] = verts;
                        }
                    }
                }

                if (data.HasData)
                {
                    Models[i] = data;
                }
            }
        }

        private static List<string> GetActiveChannels(AnimFlags flags)
        {
            List<string> channels = new List<string>();
            if (flags.HasFlag(AnimFlags.Position)) channels.Add("Position");
            if (flags.HasFlag(AnimFlags.Rotation)) channels.Add("Rotation");
            else if (flags.HasFlag(AnimFlags.Quaternion)) channels.Add("Quaternion");
            else if (flags.HasFlag(AnimFlags.Angle)) channels.Add("Angle");
            else if (flags.HasFlag(AnimFlags.Roll)) channels.Add("Roll");

            if (flags.HasFlag(AnimFlags.Scale)) channels.Add("Scale");
            if (flags.HasFlag(AnimFlags.Vector)) channels.Add("Vector");
            else if (flags.HasFlag(AnimFlags.Target)) channels.Add("Target");

            if (flags.HasFlag(AnimFlags.Vertex)) channels.Add("Vertex");
            if (flags.HasFlag(AnimFlags.Normal)) channels.Add("Normal");
            if (flags.HasFlag(AnimFlags.Color)) channels.Add("Color");
            if (flags.HasFlag(AnimFlags.Intensity)) channels.Add("Intensity");
            if (flags.HasFlag(AnimFlags.Spot)) channels.Add("Spot");
            if (flags.HasFlag(AnimFlags.Point)) channels.Add("Point");

            return channels;
        }

        private static int CalculateModelParts(byte[] file, int address, uint imageBase, AnimFlags flags)
        {
            int mdataPtr = (int)(ByteConverter.ToUInt32(file, address) - imageBase);
            if (mdataPtr <= 0 || mdataPtr >= file.Length) return 1;

            int channelCount = GetActiveChannels(flags).Count;
            int stride = channelCount * 8;
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