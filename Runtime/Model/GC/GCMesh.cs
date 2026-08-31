using System;
using System.Collections.Generic;
using UnityNinja.IO;

namespace UnityNinja.GC
{
    [Serializable]
    public class GCMesh
    {
        public List<GCParameter> Parameters = new List<GCParameter>();
        public List<GCPrimitive> Primitives = new List<GCPrimitive>();

        public GCIndexAttributeFlags? IndexFlags
        {
            get
            {
                var p = Parameters.Find(x => x.Type == ParameterType.IndexAttributeFlags);
                return p?.IndexAttributes;
            }
        }

        public GCMesh(byte[] file, int address, uint imageBase, GCIndexAttributeFlags inheritedFlags)
        {
            if (address + 16 > file.Length) return;

            int paramsOffset = (int)(ByteConverter.ToInt32(file, address) - imageBase);
            int paramsCount = ByteConverter.ToInt32(file, address + 4);

            int primsOffset = (int)(ByteConverter.ToInt32(file, address + 8) - imageBase);
            uint primsSize = ByteConverter.ToUInt32(file, address + 12);

            if (paramsOffset > 0 && paramsOffset < file.Length)
            {
                for (int i = 0; i < paramsCount; i++)
                {
                    GCParameter p = GCParameter.Read(file, paramsOffset + i * 8);
                    if (p != null) Parameters.Add(p);
                }
            }

            GCIndexAttributeFlags activeFlags = IndexFlags ?? inheritedFlags;

            if (primsOffset > 0 && primsOffset < file.Length)
            {
                int endPos = Math.Min(file.Length, primsOffset + (int)primsSize);
                while (primsOffset < endPos && file[primsOffset] != 0)
                {
                    Primitives.Add(new GCPrimitive(file, primsOffset, activeFlags, out primsOffset));
                }
            }
        }
    }
}