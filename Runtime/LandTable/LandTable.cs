using System;
using System.Collections.Generic;
using System.Text;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class LandTable
    {
        public const ulong SA1LVL = 0x4C564C314153u;
        public const ulong SA2LVL = 0x4C564C324153u;
        public const ulong SA2BLVL = 0x4C564C42324153u;

        public string Name { get; set; } = "landtable_00000000";
        public List<COL> COLList { get; } = new List<COL>();
        public List<GeoAnimData> AnimList { get; } = new List<GeoAnimData>();
        public float FarClipping { get; set; } = 10000.0f;
        public string TextureFileName { get; set; } = "";

        public LandTable(byte[] file, int address, uint imageBase, ModelFormat format, Dictionary<int, string> labels = null)
        {
            if (address + 32 > file.Length) return;

            if (labels != null && labels.TryGetValue(address, out string lbl))
                Name = lbl;
            else
                Name = $"landtable_{address:X8}";

            short colCount = ByteConverter.ToInt16(file, address);
            short animCount = ByteConverter.ToInt16(file, address + 2);
            FarClipping = ByteConverter.ToSingle(file, address + 8);

            int colAddr = (int)(ByteConverter.ToUInt32(file, address + 12) - imageBase);
            int animAddr = (int)(ByteConverter.ToUInt32(file, address + 16) - imageBase);
            int texNameAddr = (int)(ByteConverter.ToUInt32(file, address + 20) - imageBase);

            if (colAddr > 0 && colAddr < file.Length)
            {
                for (int i = 0; i < colCount; i++)
                {
                    COLList.Add(new COL(file, colAddr + i * 36, imageBase, format, labels));
                }
            }

            if (animAddr > 0 && animAddr < file.Length)
            {
                for (int i = 0; i < animCount; i++)
                {
                    AnimList.Add(new GeoAnimData(file, animAddr + i * 24, imageBase, format, labels));
                }
            }

            if (texNameAddr > 0 && texNameAddr < file.Length)
            {
                int len = 0;
                while (texNameAddr + len < file.Length && file[texNameAddr + len] != 0) len++;
                TextureFileName = Encoding.ASCII.GetString(file, texNameAddr, len);
            }
        }
    }
}