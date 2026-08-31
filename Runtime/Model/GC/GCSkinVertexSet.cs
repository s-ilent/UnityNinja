using System;
using System.Collections.Generic;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja.GC
{
    [Serializable]
    public class GCSkinVertexSet
    {
        public GCSkinAttribute ElementType;
        public ushort TotalVertIndices;
        public ushort StartingIndex;
        public ushort IndexCount;

        public List<Vector3> Positions = new List<Vector3>();
        public List<Vector3> Normals = new List<Vector3>();
        public List<Vector2Int> WeightData = new List<Vector2Int>(); // x: vertIndex, y: weight (0-255)

        public GCSkinVertexSet(byte[] file, int address, uint imageBase, Dictionary<int, string> labels = null)
        {
            if (address + 16 > file.Length) return;

            ElementType = (GCSkinAttribute)ByteConverter.ToUInt16(file, address);
            TotalVertIndices = ByteConverter.ToUInt16(file, address + 2);
            StartingIndex = ByteConverter.ToUInt16(file, address + 4);
            IndexCount = ByteConverter.ToUInt16(file, address + 6);

            int posNormAddr = (int)(ByteConverter.ToUInt32(file, address + 8) - imageBase);
            int weightAddr = (int)(ByteConverter.ToUInt32(file, address + 12) - imageBase);

            if (ElementType == GCSkinAttribute.WeightStructEndMarker) return;

            if (posNormAddr > 0 && posNormAddr < file.Length)
            {
                for (int i = 0; i < IndexCount && posNormAddr + 12 <= file.Length; i++)
                {
                    short px = ByteConverter.ToInt16(file, posNormAddr);
                    short py = ByteConverter.ToInt16(file, posNormAddr + 2);
                    short pz = ByteConverter.ToInt16(file, posNormAddr + 4);

                    short nx = ByteConverter.ToInt16(file, posNormAddr + 6);
                    short ny = ByteConverter.ToInt16(file, posNormAddr + 8);
                    short nz = ByteConverter.ToInt16(file, posNormAddr + 10);

                    Positions.Add(new Vector3(px / 255.0f, py / 255.0f, pz / 255.0f));
                    Normals.Add(new Vector3(nx / 255.0f, ny / 255.0f, nz / 255.0f));
                    posNormAddr += 12;
                }
            }

            if (ElementType is GCSkinAttribute.PartialWeightStart or GCSkinAttribute.PartialWeight)
            {
                if (weightAddr > 0 && weightAddr < file.Length)
                {
                    for (int i = 0; i < IndexCount && weightAddr + 4 <= file.Length; i++)
                    {
                        ushort vIdx = ByteConverter.ToUInt16(file, weightAddr);
                        ushort w = ByteConverter.ToUInt16(file, weightAddr + 2);
                        WeightData.Add(new Vector2Int(vIdx, w));
                        weightAddr += 4;
                    }
                }
            }
        }
    }
}