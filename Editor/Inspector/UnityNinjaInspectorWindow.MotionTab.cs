using UnityEngine;
using UnityEditor;
using UnityNinja;

namespace UnityNinja.Editor
{
    public partial class UnityNinjaInspectorWindow
    {
        private void DrawMotionTab()
        {
            var mot = m_Context.MainMotion;
            if (mot == null)
            {
                EditorGUILayout.HelpBox("No motion/animation data in target asset.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Motion: {mot.Name} ({mot.Frames} Frames, {mot.ModelParts} Nodes)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Interpolation: {mot.InterpolationMode} | ShortRot: {mot.ShortRot} | Flags: {mot.Flags}");

            EditorGUILayout.Space(4);

            foreach (var kvp in mot.Models)
            {
                int nodeIdx = kvp.Key;
                var data = kvp.Value;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Node [{nodeIdx:00}] - Pos: {data.Position.Count}, Rot: {data.Rotation.Count}, Scl: {data.Scale.Count}", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                if (data.Position.Count > 0)
                    EditorGUILayout.LabelField($"Position Frames: {string.Join(", ", data.Position.Keys)}");
                if (data.Rotation.Count > 0)
                    EditorGUILayout.LabelField($"Rotation Frames: {string.Join(", ", data.Rotation.Keys)}");
                if (data.Scale.Count > 0)
                    EditorGUILayout.LabelField($"Scale Frames: {string.Join(", ", data.Scale.Keys)}");

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }
    }
}