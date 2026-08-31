using UnityEngine;
using UnityEditor;
using UnityNinja;

namespace UnityNinja.Editor
{
    public partial class UnityNinjaInspectorWindow
    {
        private void DrawMaterialsTab()
        {
            if (m_Context.RootModel == null)
            {
                EditorGUILayout.HelpBox("No materials in target asset.", MessageType.Info);
                return;
            }

            foreach (var node in m_Context.RootModel.EnumerateNodes())
            {
                if (node.Attach is BasicAttach basic && basic.Materials.Count > 0)
                {
                    EditorGUILayout.LabelField($"Materials for [{node.Name}] ({basic.Materials.Count} slots)", EditorStyles.boldLabel);
                    for (int i = 0; i < basic.Materials.Count; i++)
                    {
                        var mat = basic.Materials[i];
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"Slot [{i:00}] - TexID: {mat.TextureID} | Flags: 0x{mat.Flags:X8}", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.ColorField("Diffuse", mat.DiffuseColor);
                        EditorGUILayout.ColorField("Specular", mat.SpecularColor);
                        EditorGUILayout.LabelField($"Exponent: {mat.Exponent:F1} | Alpha: {mat.SourceAlpha} -> {mat.DestinationAlpha}");
                        EditorGUILayout.LabelField($"States: DoubleSided={mat.DoubleSided}, UseAlpha={mat.UseAlpha}, EnvMap={mat.EnvironmentMap}");
                        EditorGUI.indentLevel--;
                        EditorGUILayout.EndVertical();
                    }
                }
            }
        }
    }
}