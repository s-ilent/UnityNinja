using System;
using UnityEngine;
using UnityEditor;
using UnityNinja;

namespace UnityNinja.Editor
{
    public partial class UnityNinjaInspectorWindow
    {
        private int m_CgmModelsPage = 0;
        private int m_CgmMapObjPage = 0;
        private int m_CgmTexturesPage = 0;
        private int m_CgmLightsPage = 0;
        private int m_CgmColPage = 0;
        private int m_CgmSoundPage = 0;

        private void DrawCgmTab()
        {
            var cgm = m_Context.CgmData;
            if (cgm == null)
            {
                EditorGUILayout.HelpBox("No CGM archive data loaded.", MessageType.Info);
                return;
            }

            // 1. Archive Overview Card
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"CGM Stage & Archive Structure", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Models: {cgm.Models.Count} | Map Placements (CGMP): {cgm.MapObjects.Count} | Textures: {cgm.Textures.Count}");
            EditorGUILayout.LabelField($"• Dynamic Lights: {cgm.Lights.Count} | Collisions: {cgm.Collisions.Count} | Sound Cues: {cgm.SoundCues.Count} | Cameras: {cgm.Cameras.Count}");
            EditorGUILayout.Space(2);
            if (GUILayout.Button("Extract CGM Archive Contents to Folder...", GUILayout.Height(26)))
            {
                CgmExporter.ExtractCgmToDirectory(m_Context.AssetPath);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 2. Stage Map Objects (CGMP)
            if (cgm.MapObjects.Count > 0)
            {
                EditorGUILayout.LabelField($"Stage Map Placements [CGMP] ({cgm.MapObjects.Count} Instances)", EditorStyles.boldLabel);
                DrawPaginationControls(ref m_CgmMapObjPage, cgm.MapObjects.Count, 10);

                int start = m_CgmMapObjPage * 10;
                int end = Math.Min(cgm.MapObjects.Count, (m_CgmMapObjPage + 1) * 10);

                for (int i = start; i < end; i++)
                {
                    var obj = cgm.MapObjects[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"📍 [Instance {i:000}] Model ID: {obj.ObjectID} | Flags: 0x{obj.Flags:X8}", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Vector3Field("Position", obj.Position);
                    EditorGUILayout.Vector3Field("Rotation (Deg)", obj.Rotation);
                    EditorGUILayout.Vector3Field("Scale", obj.Scale);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.Space(4);
            }

            // 3. Models
            if (cgm.Models.Count > 0)
            {
                EditorGUILayout.LabelField($"Embedded 3D Models ({cgm.Models.Count})", EditorStyles.boldLabel);
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
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.Space(4);
            }

            // 4. Textures
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

            // 5. Collisions (CGCL / CGLC)
            if (cgm.Collisions.Count > 0)
            {
                EditorGUILayout.LabelField($"Stage Collisions [CGCL/CGLC] ({cgm.Collisions.Count})", EditorStyles.boldLabel);
                DrawPaginationControls(ref m_CgmColPage, cgm.Collisions.Count, 10);

                int start = m_CgmColPage * 10;
                int end = Math.Min(cgm.Collisions.Count, (m_CgmColPage + 1) * 10);

                for (int i = start; i < end; i++)
                {
                    var col = cgm.Collisions[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"🛡 [Col {i:000}] Shape: {(col.Shape == 0 ? "Box" : "Sphere")} | Flags: 0x{col.Flags:X8}", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Vector3Field("Center", col.Center);
                    EditorGUILayout.Vector3Field("Size", col.Size);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.Space(4);
            }

            // 6. Dynamic Lights (NJLI / CGAL)
            if (cgm.Lights.Count > 0)
            {
                EditorGUILayout.LabelField($"Dynamic Lights [NJLI/CGAL] ({cgm.Lights.Count})", EditorStyles.boldLabel);
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

            // 7. Sound Cues (CGSP)
            if (cgm.SoundCues.Count > 0)
            {
                EditorGUILayout.LabelField($"Sound Cues [CGSP] ({cgm.SoundCues.Count})", EditorStyles.boldLabel);
                DrawPaginationControls(ref m_CgmSoundPage, cgm.SoundCues.Count, 10);

                int start = m_CgmSoundPage * 10;
                int end = Math.Min(cgm.SoundCues.Count, (m_CgmSoundPage + 1) * 10);

                for (int i = start; i < end; i++)
                {
                    var sc = cgm.SoundCues[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"🔊 Sound [{sc.Index:000}] ID: {sc.SoundID} | Radius: {sc.Radius:F1}m", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Vector3Field("Position", sc.Position);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
            }
        }
    }
}