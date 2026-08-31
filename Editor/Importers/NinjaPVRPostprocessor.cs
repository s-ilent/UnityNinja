using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace UnityNinja.Editor
{
    public class NinjaPVRPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string assetPath in importedAssets)
            {
                if (!assetPath.EndsWith(".pvr", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    // Check if importer is already overridden
                    Type currentOverride = AssetDatabase.GetImporterOverride(assetPath);
                    if (currentOverride == typeof(NinjaPVRImporter))
                        continue;

                    byte[] data = File.ReadAllBytes(assetPath);
                    if (IsSegaPVR(data))
                    {
                        // Set NinjaPVRImporter as the active importer override for this .pvr asset
                        AssetDatabase.SetImporterOverride<NinjaPVRImporter>(assetPath);
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NinjaPVRPostprocessor] Could not set importer override on {assetPath}: {ex.Message}");
                }
            }
        }

        public static bool IsSegaPVR(byte[] data)
        {
            if (data == null || data.Length < 16) return false;

            // GBIX + PVRT or standalone PVRT magic
            if (data[0] == 'G' && data[1] == 'B' && data[2] == 'I' && data[3] == 'X')
                return true;

            if (data[0] == 'P' && data[1] == 'V' && data[2] == 'R' && data[3] == 'T')
                return true;

            return false;
        }
    }
}