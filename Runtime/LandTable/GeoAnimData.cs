using System;
using System.Collections.Generic;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class GeoAnimData
    {
        public float AnimationFrame;
        public float AnimationSpeed;
        public float MaxFrame;
        public NJS_OBJECT Model;
        public NJS_MOTION Animation;

        public GeoAnimData(byte[] file, int address, uint imageBase, ModelFormat format, Dictionary<int, string> labels = null)
        {
            if (address + 24 > file.Length) return;

            AnimationFrame = ByteConverter.ToSingle(file, address);
            AnimationSpeed = ByteConverter.ToSingle(file, address + 4);
            MaxFrame = ByteConverter.ToSingle(file, address + 8);

            int modelAddr = (int)(ByteConverter.ToUInt32(file, address + 12) - imageBase);
            int actionAddr = (int)(ByteConverter.ToUInt32(file, address + 16) - imageBase);

            if (modelAddr >= 0 && modelAddr < file.Length)
            {
                Model = new NJS_OBJECT(file, modelAddr, imageBase, format, labels);
            }

            if (actionAddr >= 0 && actionAddr < file.Length)
            {
                NJS_ACTION act = new NJS_ACTION(file, actionAddr, imageBase, format, labels);
                Animation = act.Motion;
            }
        }
    }
}