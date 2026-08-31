using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace UnityNinja.Editor
{
    [CustomEditor(typeof(NinjaCGMImporter))]
    [CanEditMultipleObjects]
    public class NinjaCGMImporterEditor : ScriptedImporterEditor
    {
        private SerializedProperty m_ScaleProp;
        private SerializedProperty m_GenerateMeshCollidersProp;
        private SerializedProperty m_ImportMaterialsProp;
        private SerializedProperty m_DeduplicateMaterialsProp;
        private SerializedProperty m_TransparencyAsCoverageProp;
        private SerializedProperty m_ImportAnimationProp;

        public override void OnEnable()
        {
            base.OnEnable();
            m_ScaleProp = serializedObject.FindProperty("m_Scale");
            m_GenerateMeshCollidersProp = serializedObject.FindProperty("m_GenerateMeshColliders");
            m_ImportMaterialsProp = serializedObject.FindProperty("m_ImportMaterials");
            m_DeduplicateMaterialsProp = serializedObject.FindProperty("m_DeduplicateMaterials");
            m_TransparencyAsCoverageProp = serializedObject.FindProperty("m_TransparencyAsCoverage");
            m_ImportAnimationProp = serializedObject.FindProperty("m_ImportAnimation");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            string assetPath = ((ScriptedImporter)target).assetPath;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("CGM Archive Utilities", EditorStyles.boldLabel);
            if (GUILayout.Button("Extract CGM Contents to Folder...", GUILayout.Height(28)))
            {
                CgmExporter.ExtractCgmToDirectory(assetPath);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Model & Transform Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ScaleProp, new GUIContent("Scale Factor"));
            EditorGUILayout.PropertyField(m_GenerateMeshCollidersProp, new GUIContent("Generate Mesh Colliders"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Materials & Transparency", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ImportMaterialsProp, new GUIContent("Import Materials"));
            if (m_ImportMaterialsProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_DeduplicateMaterialsProp, new GUIContent("Deduplicate Materials"));
                if (m_TransparencyAsCoverageProp != null)
                {
                    EditorGUILayout.PropertyField(m_TransparencyAsCoverageProp, new GUIContent("Transparency as Coverage (Dreamcast OIT)"));
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ImportAnimationProp, new GUIContent("Import Animation"));

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }
    }
}