using System;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja
{
    [Serializable]
    public class NinjaBoundingSphere
    {
        public Vector3 center;
        public float radius;

        public NinjaBoundingSphere() { }

        public NinjaBoundingSphere(byte[] file, int address)
        {
            if (address + 16 > file.Length) return;
            center = new Vector3(
                ByteConverter.ToSingle(file, address),
                ByteConverter.ToSingle(file, address + 4),
                ByteConverter.ToSingle(file, address + 8)
            );
            radius = ByteConverter.ToSingle(file, address + 12);
        }
    }
}