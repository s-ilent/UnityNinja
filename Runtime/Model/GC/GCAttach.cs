using System;
using System.Collections.Generic;
using UnityNinja.IO;

namespace UnityNinja.GC
{
    [Serializable]
    public class GCAttach : NinjaAttach
    {
        public List<GCVertexSet> VertexData = new List<GCVertexSet>();
        public List<GCSkinVertexSet> VertexSkinData = new List<GCSkinVertexSet>();
        public List<GCMesh> OpaqueMeshes = new List<GCMesh>();
        public List<GCMesh> TranslucentMeshes = new List<GCMesh>();

        public override bool HasWeight => VertexSkinData != null && VertexSkinData.Count > 0;

        public GCAttach(byte[] file, int address, uint imageBase, Dictionary<int, string> labels = null)
        {
            if (labels != null && labels.TryGetValue(address, out string lbl))
                Name = lbl;
            else
                Name = $"attach_{address:X8}";

            int vAddr = ByteConverter.ToInt32(file, address);
            if (vAddr != 0)
            {
                vAddr = (int)((uint)vAddr - imageBase);
                while (vAddr + 16 <= file.Length && file[vAddr] != 255)
                {
                    GCVertexSet vSet = new GCVertexSet(file, vAddr, imageBase, labels);
                    VertexData.Add(vSet);
                    vAddr += 16;
                }
            }

            int skinAddr = ByteConverter.ToInt32(file, address + 4);
            if (skinAddr != 0)
            {
                skinAddr = (int)((uint)skinAddr - imageBase);
                int idx = 0;
                while (skinAddr + (0x10 * idx) + 16 <= file.Length)
                {
                    GCSkinVertexSet skinSet = new GCSkinVertexSet(file, skinAddr + (0x10 * idx), imageBase, labels);
                    VertexSkinData.Add(skinSet);
                    idx++;
                    if (skinSet.ElementType == GCSkinAttribute.WeightStructEndMarker) break;
                }
            }

            int opaqueAddr = (int)(ByteConverter.ToUInt32(file, address + 8) - imageBase);
            int transAddr = (int)(ByteConverter.ToUInt32(file, address + 12) - imageBase);

            short opaqueCount = ByteConverter.ToInt16(file, address + 16);
            short transCount = ByteConverter.ToInt16(file, address + 18);

            Bounds = new NinjaBoundingSphere(file, address + 20);

            GCIndexAttributeFlags indexFlags = GCIndexAttributeFlags.HasPosition;

            if (opaqueAddr > 0 && opaqueAddr < file.Length)
            {
                for (int i = 0; i < opaqueCount; i++)
                {
                    GCMesh m = new GCMesh(file, opaqueAddr + i * 16, imageBase, indexFlags);
                    if (m.IndexFlags.HasValue) indexFlags = m.IndexFlags.Value;
                    OpaqueMeshes.Add(m);
                }
            }

            if (transAddr > 0 && transAddr < file.Length)
            {
                for (int i = 0; i < transCount; i++)
                {
                    GCMesh m = new GCMesh(file, transAddr + i * 16, imageBase, indexFlags);
                    if (m.IndexFlags.HasValue) indexFlags = m.IndexFlags.Value;
                    TranslucentMeshes.Add(m);
                }
            }
        }
    }
}