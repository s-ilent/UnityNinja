using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace UnityNinja.Editor
{
    [CustomEditor(typeof(NinjaModelImporter))]
    [CanEditMultipleObjects]
    public class NinjaModelImporterEditor : ScriptedImporterEditor
    {
        private SerializedProperty m_ScaleProp;
        private SerializedProperty m_GenerateMeshCollidersProp;
        private SerializedProperty m_ImportMaterialsProp;
        private SerializedProperty m_DeduplicateMaterialsProp;
        private SerializedProperty m_MaterialLocationProp;
        private SerializedProperty m_MaterialNamingProp;
        private SerializedProperty m_MaterialSearchPathProp;
        private SerializedProperty m_TextureSearchPathsProp;
        private SerializedProperty m_MaterialRemapsProp;
        private SerializedProperty m_TextureRemapsProp;
        private SerializedProperty m_ImportAnimationProp;

        private int m_SelectedTab = 0;
        private readonly string[] m_TabNames = new[] { "Model", "Materials", "Textures", "Animation" };

        public override void OnEnable()
        {
            base.OnEnable();
            m_ScaleProp = serializedObject.FindProperty("m_Scale");
            m_GenerateMeshCollidersProp = serializedObject.FindProperty("m_GenerateMeshColliders");
            m_ImportMaterialsProp = serializedObject.FindProperty("m_ImportMaterials");
            m_DeduplicateMaterialsProp = serializedObject.FindProperty("m_DeduplicateMaterials");
            m_MaterialLocationProp = serializedObject.FindProperty("m_MaterialLocation");
            m_MaterialNamingProp = serializedObject.FindProperty("m_MaterialNaming");
            m_MaterialSearchPathProp = serializedObject.FindProperty("m_MaterialSearchPath");
            m_TextureSearchPathsProp = serializedObject.FindProperty("m_TextureSearchPaths");
            m_MaterialRemapsProp = serializedObject.FindProperty("m_MaterialRemaps");
            m_TextureRemapsProp = serializedObject.FindProperty("m_TextureRemaps");
            m_ImportAnimationProp = serializedObject.FindProperty("m_ImportAnimation");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            string assetPath = ((ScriptedImporter)target).assetPath;

            m_SelectedTab = GUILayout.Toolbar(m_SelectedTab, m_TabNames, EditorStyles.toolbarButton);
            EditorGUILayout.Space(6);

            switch (m_SelectedTab)
            {
                case 0:
                    EditorGUILayout.LabelField("Model & Transform Settings", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(m_ScaleProp, new GUIContent("Scale Factor"));
                    EditorGUILayout.PropertyField(m_GenerateMeshCollidersProp, new GUIContent("Generate Mesh Colliders"));
                    break;

                case 1:
                    EditorGUILayout.LabelField("Material Import & Extraction", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(m_ImportMaterialsProp, new GUIContent("Import Materials"));
                    if (m_ImportMaterialsProp.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(m_DeduplicateMaterialsProp, new GUIContent("Deduplicate Materials"));
                        EditorGUILayout.PropertyField(m_MaterialLocationProp, new GUIContent("Location"));
                        EditorGUILayout.PropertyField(m_MaterialNamingProp, new GUIContent("Naming Mode"));

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(m_MaterialSearchPathProp, new GUIContent("Material Path"));
                        if (GUILayout.Button("Browse", GUILayout.Width(65)))
                        {
                            string folder = EditorUtility.OpenFolderPanel("Select Material Search Directory", "Assets", "");
                            if (!string.IsNullOrEmpty(folder) && folder.StartsWith(Application.dataPath))
                                m_MaterialSearchPathProp.stringValue = "Assets" + folder.Substring(Application.dataPath.Length);
                        }
                        EditorGUILayout.EndHorizontal();

                        if ((MaterialLocation)m_MaterialLocationProp.enumValueIndex == MaterialLocation.EmbedInPrefab)
                        {
                            EditorGUILayout.Space(2);
                            if (GUILayout.Button("Extract Materials to Project...", GUILayout.Height(24)))
                            {
                                NinjaMaterialResolver.ExtractMaterials(assetPath, m_MaterialLocationProp, m_MaterialSearchPathProp);
                            }
                        }
                        EditorGUI.indentLevel--;
                    }
                    break;

                case 2:
                    EditorGUILayout.LabelField("Texture Search Paths (Ordered by Priority)", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Searches top-to-bottom. Textures in these folders override local textures.", MessageType.None);

                    if (m_TextureSearchPathsProp != null)
                    {
                        for (int i = 0; i < m_TextureSearchPathsProp.arraySize; i++)
                        {
                            var elem = m_TextureSearchPathsProp.GetArrayElementAtIndex(i);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"[{i}]", GUILayout.Width(24));
                            EditorGUILayout.PropertyField(elem, GUIContent.none);
                            if (GUILayout.Button("Browse", GUILayout.Width(60)))
                            {
                                string folder = EditorUtility.OpenFolderPanel("Select Texture Folder", "Assets", "");
                                if (!string.IsNullOrEmpty(folder) && folder.StartsWith(Application.dataPath))
                                    elem.stringValue = "Assets" + folder.Substring(Application.dataPath.Length);
                            }
                            if (GUILayout.Button("-", GUILayout.Width(25)))
                            {
                                m_TextureSearchPathsProp.DeleteArrayElementAtIndex(i);
                                break;
                            }
                            EditorGUILayout.EndHorizontal();
                        }

                        if (GUILayout.Button("+ Add Search Path", GUILayout.Width(150)))
                        {
                            int newIdx = m_TextureSearchPathsProp.arraySize;
                            m_TextureSearchPathsProp.InsertArrayElementAtIndex(newIdx);
                            m_TextureSearchPathsProp.GetArrayElementAtIndex(newIdx).stringValue = "Assets";
                        }
                    }
                    break;

                case 3:
                    EditorGUILayout.LabelField("Animation Settings", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(m_ImportAnimationProp, new GUIContent("Import Animation"));
                    break;
            }

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }
    }
}