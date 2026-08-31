using System;
using System.Collections.Generic;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class NJS_ACTION
    {
        public string Name { get; set; } = "action_00000000";
        public NJS_OBJECT Model { get; set; }
        public NJS_MOTION Motion { get; set; }

        public NJS_ACTION(byte[] file, int address, uint imageBase, ModelFormat format, Dictionary<int, string> labels)
        {
            if (address + 8 > file.Length) return;

            if (labels != null && labels.TryGetValue(address, out string lbl))
                Name = lbl;
            else
                Name = $"action_{address:X8}";

            int modelAddr = (int)(ByteConverter.ToUInt32(file, address) - imageBase);
            int motionAddr = (int)(ByteConverter.ToUInt32(file, address + 4) - imageBase);

            if (modelAddr >= 0 && modelAddr < file.Length)
            {
                Model = new NJS_OBJECT(file, modelAddr, imageBase, format, labels);
            }

            int partCount = Model != null ? Model.CountAll() : 0;

            if (motionAddr >= 0 && motionAddr < file.Length)
            {
                Motion = new NJS_MOTION(file, motionAddr, imageBase, partCount, labels);
            }
        }
    }
}