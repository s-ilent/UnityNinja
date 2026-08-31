using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityNinja.Editor
{
    [Serializable]
    public class MaterialRemapEntry
    {
        public int slotIndex;
        public string originalName = "";
        public Material overrideMaterial;
    }

    [Serializable]
    public class TextureRemapEntry
    {
        public int textureIndex;
        public string originalFileName = "";
        public Texture2D overrideTexture;
    }

    [Serializable]
    public class NinjaImportSettings
    {
        // Transform
        public float Scale = 0.10f;
        public bool GenerateMeshColliders = false;

        // Materials
        public bool ImportMaterials = true;
        public bool DeduplicateMaterials = true;
        public MaterialLocation MaterialLocation = MaterialLocation.EmbedInPrefab;
        public MaterialNaming MaterialNaming = MaterialNaming.ByMaterialName;
        public MaterialSearch MaterialSearch = MaterialSearch.RecursiveSubFolder;
        public string MaterialSearchPath = "Assets/Materials";
        public string[] TextureSearchPaths = Array.Empty<string>();
        public List<MaterialRemapEntry> MaterialRemaps = new List<MaterialRemapEntry>();
        public List<TextureRemapEntry> TextureRemaps = new List<TextureRemapEntry>();

        // In-Memory Embedded Textures (for CGMs)
        public List<Texture2D> EmbeddedTextures = new List<Texture2D>();
        public Dictionary<string, Texture2D> EmbeddedTextureMap = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        // Transparency & Dreamcast OIT Emulation
        public bool TransparencyAsCoverage = false;

        // Animation
        public bool ImportAnimation = true;

        public static NinjaImportSettings Default => new NinjaImportSettings();
    }
}