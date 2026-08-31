using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityNinja.IO;

namespace UnityNinja.Editor
{
    public static class CgmExporter
    {
        private const string MENU_EXTRACT = "Assets/UnityNinja/Extract CGM Archive to Folder...";

        [MenuItem(MENU_EXTRACT, false, 20)]
        public static void ExtractSelectedCgms()
        {
            if (Selection.objects == null || Selection.objects.Length == 0) return;

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (IsCgmPath(path))
                {
                    ExtractCgmToDirectory(path);
                }
            }
        }

        [MenuItem(MENU_EXTRACT, true)]
        public static bool ValidateExtractSelectedCgms()
        {
            if (Selection.objects == null || Selection.objects.Length == 0) return false;
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (IsCgmPath(path)) return true;
            }
            return false;
        }

        private static bool IsCgmPath(string path) =>
            !string.IsNullOrEmpty(path) && path.EndsWith(".cgm", StringComparison.OrdinalIgnoreCase);

        public static void ExtractCgmToDirectory(string assetPath)
        {
            string absPath = ResolveAbsolutePath(assetPath);
            if (!File.Exists(absPath)) return;

            string folderName = Path.GetFileNameWithoutExtension(assetPath) + "_extracted";
            string targetDir = Path.Combine(Path.GetDirectoryName(absPath), folderName).Replace('\\', '/');

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            AssetDatabase.StartAssetEditing();

            try
            {
                byte[] rawData = File.ReadAllBytes(absPath);
                CgmArchive archive = CgmArchive.Load(rawData);

                int pngCount = 0;
                int modelCount = 0;

                // 1. Export decoded textures as PNG & raw PVR
                foreach (var texEntry in archive.Textures)
                {
                    string baseName = texEntry.Name;
                    Texture2D tex = PVRTextureDecoder.DecodePVR(texEntry.RawData, baseName);
                    if (tex != null)
                    {
                        byte[] pngBytes = tex.EncodeToPNG();
                        string pngPath = Path.Combine(targetDir, $"{baseName}.png");
                        File.WriteAllBytes(pngPath, pngBytes);
                        pngCount++;
                        UnityEngine.Object.DestroyImmediate(tex);
                    }

                    string pvrPath = Path.Combine(targetDir, $"{baseName}.pvr");
                    File.WriteAllBytes(pvrPath, texEntry.RawData);
                }

                // 2. Export merged standalone .nj / .gj models
                foreach (var mdlEntry in archive.Models)
                {
                    string ext = mdlEntry.ChunkTag == "GJCM" ? "gj" : "nj";
                    string modelFileName = $"{mdlEntry.ModelName}.{ext}";
                    string modelPath = Path.Combine(targetDir, modelFileName);

                    File.WriteAllBytes(modelPath, mdlEntry.ModelBytes);
                    modelCount++;
                }

                // 3. Write manifest.json
                string manifestPath = Path.Combine(targetDir, "manifest.json");
                File.WriteAllText(manifestPath, NinjaJsonSerializer.Serialize(archive));

                EditorUtility.DisplayDialog(
                    "CGM Extraction Complete",
                    $"Successfully extracted {Path.GetFileName(assetPath)}:\n" +
                    $"• Destination: {folderName}/\n" +
                    $"• Textures Converted (PNG + PVR): {pngCount}\n" +
                    $"• Merged Models (.nj/.gj): {modelCount}",
                    "OK"
                );
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        private static string ResolveAbsolutePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return "";
            if (Path.IsPathRooted(assetPath)) return assetPath.Replace('\\', '/');
            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }
    }
}