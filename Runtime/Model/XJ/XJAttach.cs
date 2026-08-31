using System;
using System.Collections.Generic;
using UnityNinja.IO;

namespace UnityNinja.XJ
{
    [Serializable]
    public class XJAttach : NinjaAttach
    {
        public List<XjVertexSet> VertexSets = new List<XjVertexSet>();
        public List<XJMesh> OpaqueMeshes = new List<XJMesh>();
        public List<XJMesh> TranslucentMeshes = new List<XJMesh>();

        public XJAttach(byte[] file, int address, uint imageBase, Dictionary<int, string> labels = null)
        {
            if (labels != null && labels.TryGetValue(address, out string lbl))
                Name = lbl;
            else
                Name = $"attach_{address:X8}";

            int vSetAddr = (int)(ByteConverter.ToUInt32(file, address + 4) - imageBase);
            uint vSetCount = ByteConverter.ToUInt32(file, address + 8);

            int opAddr = (int)(ByteConverter.ToUInt32(file, address + 12) - imageBase);
            uint opCount = ByteConverter.ToUInt32(file, address + 16);

            int trAddr = (int)(ByteConverter.ToUInt32(file, address + 20) - imageBase);
            uint trCount = ByteConverter.ToUInt32(file, address + 24);

            Bounds = new NinjaBoundingSphere(file, address + 28);

            if (vSetAddr > 0 && vSetAddr < file.Length)
            {
                for (int i = 0; i < (int)vSetCount; i++)
                    VertexSets.Add(new XjVertexSet(file, vSetAddr + i * 16, imageBase));
            }

            if (opAddr > 0 && opAddr < file.Length)
            {
                for (int i = 0; i < (int)opCount; i++)
                    OpaqueMeshes.Add(new XJMesh(file, opAddr + i * 20, imageBase));
            }

            if (trAddr > 0 && trAddr < file.Length)
            {
                for (int i = 0; i < (int)trCount; i++)
                    TranslucentMeshes.Add(new XJMesh(file, trAddr + i * 20, imageBase));
            }
        }
    }
}