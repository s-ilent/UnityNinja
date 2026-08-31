using System;

namespace UnityNinja.Editor
{
    [Serializable]
    public class NinjaImportSettings
    {
        public float Scale = 0.10f;
        public bool GenerateMeshColliders = false;
        public bool ImportAnimation = true;
        public static NinjaImportSettings Default => new NinjaImportSettings();
    }
}