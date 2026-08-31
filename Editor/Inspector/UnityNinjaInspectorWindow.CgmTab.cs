using System;
using UnityEngine;
using UnityEditor;
using UnityNinja;

namespace UnityNinja.Editor
{
    public partial class UnityNinjaInspectorWindow
    {
        private int m_CgmModelsPage = 0;
        private int m_CgmTexturesPage = 0;
        private int m_CgmLightsPage = 0;

        private void DrawCgmTab()
        {
            var cgm = m_Context.CgmData;
            if (cgm == null)
            {
                EditorGUILayout.HelpBox("No CGM archive data loaded.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"CGM Archive ({cgm.Models.Count} Models, {cgm.Textures.Count} Textures, {cgm.Lights.Count} Lights, {cgm.UnknownChunks.Count} Unknown Blocks)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Extract CGM Archive to Folder...", GUILayout.Height(24)))
            {
                CgmExporter.ExtractCgmToDirectory(m_Context.AssetPath);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 1. Models
            if (cgm.Models.Count > 0)
            {
                EditorGUILayout.LabelField($"Embedded Models ({cgm.Models.Count})", EditorStyles.boldLabel);
                DrawPaginationControls(ref m_CgmModelsPage, cgm.Models.Count, 10);

                int start = m_CgmModelsPage * 10;
                int end = Math.Min(cgm.Models.Count, (m_CgmModelsPage + 1) * 10);

                for (int i = start; i < end; i++)
                {
                    var mdl = cgm.Models[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"📦 [{i:00}] {mdl.ModelName} (Tag: {mdl.ChunkTag}, Size: {mdl.ModelBytes.Length:N0} bytes)", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    if (mdl.TexturesUsed != null && mdl.TexturesUsed.Count > 0)
                    {
                        EditorGUILayout.LabelField("Textures:", string.Join(", ", mdl.TexturesUsed));
                    }
                    if (mdl.EmbeddedMotions != null && mdl.EmbeddedMotions.Count > 0)
                    {
                        EditorGUILayout.LabelField("Embedded Motions:", $"{mdl.EmbeddedMotions.Count} tracks");
                    }
                    if (mdl.UnknownChunks != null && mdl.UnknownChunks.Count > 0)
                    {
                        EditorGUILayout.LabelField("Unknown Blocks:", $"{mdl.UnknownChunks.Count} blocks");
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.Space(4);
            }

            // 2. Textures
            if (cgm.Textures.Count > 0)
            {
                EditorGUILayout.LabelField($"Embedded PVR Textures ({cgm.Textures.Count})", EditorStyles.boldLabel);
                DrawPaginationControls(ref m_CgmTexturesPage, cgm.Textures.Count, 15);

                int start = m_CgmTexturesPage * 15;
                int end = Math.Min(cgm.Textures.Count, (m_CgmTexturesPage + 1) * 15);

                for (int i = start; i < end; i++)
                {
                    var tex = cgm.Textures[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"🖼 [{tex.Index:00}] {tex.Name}.pvr ({tex.Width}x{tex.Height})", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Format: {tex.ColorFormat} / {tex.DataFormat} | Length: {tex.Length:N0} bytes");
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.Space(4);
            }

            // 3. Dynamic Lights (NJLI)
            if (cgm.Lights.Count > 0)
            {
                EditorGUILayout.LabelField($"Scene Dynamic Lights ({cgm.Lights.Count})", EditorStyles.boldLabel);
                DrawPaginationControls(ref m_CgmLightsPage, cgm.Lights.Count, 10);

                int start = m_CgmLightsPage * 10;
                int end = Math.Min(cgm.Lights.Count, (m_CgmLightsPage + 1) * 10);

                for (int i = start; i < end; i++)
                {
                    var l = cgm.Lights[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"💡 Light [{l.Index:00}] - Offset: 0x{l.Offset:X8}", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Vector3Field("Position", l.Position);
                    EditorGUILayout.Vector3Field("Direction", l.Direction);
                    EditorGUILayout.ColorField("Color", l.Color);
                    EditorGUILayout.LabelField($"Attenuation Near/Far: {l.Near:F1}m / {l.Far:F1}m");
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.Space(4);
            }

            // 4. Unknown Chunks
            if (cgm.UnknownChunks.Count > 0)
            {
                EditorGUILayout.LabelField($"Standalone Unknown Blocks ({cgm.UnknownChunks.Count})", EditorStyles.boldLabel);
                for (int i = 0; i < cgm.UnknownChunks.Count; i++)
                {
                    var unk = cgm.UnknownChunks[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"❓ Block [{unk.Index:00}] Tag: '{unk.Tag}' (Offset: 0x{unk.Offset:X8}, Size: {unk.PayloadSize} bytes)", EditorStyles.miniBoldLabel);
                    EditorGUILayout.EndVertical();
                }
            }
        }
    }
}