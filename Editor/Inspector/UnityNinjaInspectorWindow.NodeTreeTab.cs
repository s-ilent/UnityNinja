using UnityEngine;
using UnityEditor;
using UnityNinja;

namespace UnityNinja.Editor
{
    public partial class UnityNinjaInspectorWindow
    {
        private string m_NodeFilter = "";
        private bool m_ExpandAll = false;

        private void DrawNodeTreeTab()
        {
            if (m_Context.RootModel == null)
            {
                EditorGUILayout.HelpBox("No NJS_OBJECT hierarchy nodes in target asset.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Node Hierarchy Tree", EditorStyles.boldLabel);
            m_NodeFilter = EditorGUILayout.TextField("Filter:", m_NodeFilter);
            if (GUILayout.Button(m_ExpandAll ? "Collapse" : "Expand", GUILayout.Width(70))) m_ExpandAll = !m_ExpandAll;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            foreach (var node in m_Context.RootModel.EnumerateNodes())
            {
                if (!string.IsNullOrEmpty(m_NodeFilter) && !node.Name.ToLower().Contains(m_NodeFilter.ToLower()))
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"📦 [{node.Name}]", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField($"Attach Type:", node.Attach != null ? node.Attach.GetType().Name : "None (Null Node)");
                EditorGUILayout.LabelField($"Eval Flags:", $"0x{(int)node.Flags:X4} ({(node.Animate ? "Animate" : "Static")}, {(node.SkipDraw ? "Hidden" : "Visible")})");
                EditorGUILayout.Vector3Field("Local Position:", node.Position);
                EditorGUILayout.Vector3Field("Rotation (Deg):", node.Rotation);
                EditorGUILayout.Vector3Field("Local Scale:", node.Scale);

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }
    }
}