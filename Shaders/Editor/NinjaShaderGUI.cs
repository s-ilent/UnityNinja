using UnityEngine;
using UnityEditor;

namespace UnityNinja.Editor
{
    public class NinjaShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            Material targetMat = materialEditor.target as Material;
            if (targetMat == null) return;

            MaterialProperty colorProp = FindProperty("_Color", properties, false);
            MaterialProperty mainTexProp = FindProperty("_MainTex", properties, false);
            MaterialProperty ambientColorProp = FindProperty("_AmbientColor", properties, false);
            MaterialProperty alphaTestProp = FindProperty("_AlphaTest", properties, false);
            MaterialProperty cutoffProp = FindProperty("_Cutoff", properties, false);
            MaterialProperty specColorProp = FindProperty("_SpecColor", properties, false);
            MaterialProperty shininessProp = FindProperty("_Shininess", properties, false);
            MaterialProperty unlitProp = FindProperty("_Unlit", properties, false);
            MaterialProperty cullProp = FindProperty("_Cull", properties, false);

            EditorGUILayout.LabelField("Ninja Surface Parameters", EditorStyles.boldLabel);
            if (mainTexProp != null && colorProp != null)
                materialEditor.TexturePropertySingleLine(new GUIContent("Main Texture"), mainTexProp, colorProp);

            if (ambientColorProp != null) materialEditor.ShaderProperty(ambientColorProp, "Ambient Color");
            if (alphaTestProp != null)
            {
                materialEditor.ShaderProperty(alphaTestProp, "Alpha Cutout");
                if (alphaTestProp.floatValue > 0.5f && cutoffProp != null)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(cutoffProp, "Cutoff Threshold");
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lighting & Specular", EditorStyles.boldLabel);
            if (specColorProp != null) materialEditor.ShaderProperty(specColorProp, "Specular Color");
            if (shininessProp != null) materialEditor.ShaderProperty(shininessProp, "Shininess Exponent");
            if (unlitProp != null) materialEditor.ShaderProperty(unlitProp, "Unlit / Bypass Lighting");
            if (cullProp != null) materialEditor.ShaderProperty(cullProp, "Cull Mode");
        }
    }
}