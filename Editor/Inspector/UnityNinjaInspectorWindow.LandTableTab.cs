using UnityEngine;
using UnityEditor;
using UnityNinja;

namespace UnityNinja.Editor
{
    public partial class UnityNinjaInspectorWindow
    {
        private void DrawLandTableTab()
        {
            var lvl = m_Context.LevelData;
            if (lvl == null)
            {
                EditorGUILayout.HelpBox("No LandTable level data loaded.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"LandTable: {lvl.Name} ({lvl.COLList.Count} COLs, {lvl.AnimList.Count} GeoAnims)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Far Clipping: {lvl.FarClipping:F1}m | Texture Archive: {lvl.TextureFileName}");

            EditorGUILayout.Space(4);

            for (int i = 0; i < lvl.COLList.Count; i++)
            {
                var col = lvl.COLList[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"COL [{i:000}] - Flags: 0x{col.Flags:X8} ({col.SA1Flags})", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Model: {col.Model?.Name ?? "<null>"} | Center: ({col.Bounds.center.x:F1}, {col.Bounds.center.y:F1}, {col.Bounds.center.z:F1}) | Radius: {col.Bounds.radius:F1}m");
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }
    }
}