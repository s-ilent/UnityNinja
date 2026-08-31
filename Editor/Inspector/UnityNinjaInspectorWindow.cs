using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityNinja;

namespace UnityNinja.Editor
{
    public class InspectedContext
    {
        public string AssetPath = "";
        public NinjaBinaryFile NinjaFile;
        public NJS_OBJECT RootModel;
        public NJS_MOTION MainMotion;
        public LandTable LevelData;
        public CgmArchive CgmData;

        public bool IsModel => RootModel != null;
        public bool IsMotion => MainMotion != null;
        public bool IsLandTable => LevelData != null;
        public bool IsCgm => CgmData != null;
    }

    public partial class UnityNinjaInspectorWindow : EditorWindow
    {
        private UnityEngine.Object m_SelectedAsset;
        private InspectedContext m_Context = new InspectedContext();

        private Vector2 m_MainScrollPosition;
        private Vector2 m_RightPaneScrollPosition;
        private int m_SelectedTab = 0;

        private bool m_UseDebugView = false;
        private string m_DumpedJsonText = "";
        private bool m_ShowJsonOutput = false;

        private readonly string[] m_ModelTabNames = new[] {
            "Node Tree",
            "Meshes & Vertices",
            "Materials & GX",
            "Motion & Keyframes"
        };

        [MenuItem("Window/UnityNinja/Data Inspector")]
        public static void OpenWindow()
        {
            var window = GetWindow<UnityNinjaInspectorWindow>("UnityNinja Data Inspector");
            window.minSize = new Vector2(880, 560);
            window.Show();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject != m_SelectedAsset)
            {
                m_SelectedAsset = Selection.activeObject;
                LoadAsset();
                Repaint();
            }
        }

        private void LoadAsset()
        {
            m_Context = new InspectedContext();
            m_DumpedJsonText = "";
            m_ShowJsonOutput = false;

            if (m_SelectedAsset == null) return;

            string path = AssetDatabase.GetAssetPath(m_SelectedAsset);
            if (string.IsNullOrEmpty(path)) return;

            m_Context.AssetPath = path;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string assetName = Path.GetFileNameWithoutExtension(path);
            string baseDir = Path.GetDirectoryName(path);

            try
            {
                byte[] raw = File.ReadAllBytes(path);

                if (ext == ".cgm")
                {
                    m_Context.CgmData = CgmArchive.Load(raw);
                }
                else if (ext is ".sa1lvl" or ".sa2lvl" or ".sa2blvl" or ".salvl")
                {
                    int headerAddr = (raw.Length >= 16) ? BitConverter.ToInt32(raw, 8) : 0;
                    ModelFormat fmt = ext switch { ".sa2lvl" => ModelFormat.Chunk, ".sa2blvl" => ModelFormat.GC, _ => ModelFormat.Basic };
                    m_Context.LevelData = new LandTable(raw, headerAddr, 0, fmt);
                }
                else if (ext is ".njm" or ".gjm" or ".xjm" or ".nam")
                {
                    m_Context.NinjaFile = new NinjaBinaryFile(raw);
                    if (m_Context.NinjaFile.Motions.Count > 0)
                        m_Context.MainMotion = m_Context.NinjaFile.Motions[0];
                }
                else
                {
                    ModelFormat fmt = ext switch { ".gj" => ModelFormat.GC, ".xj" => ModelFormat.XJ, _ => ModelFormat.Basic };
                    m_Context.NinjaFile = new NinjaBinaryFile(raw, fmt);

                    if (m_Context.NinjaFile.Models.Count > 0)
                        m_Context.RootModel = m_Context.NinjaFile.Models[0];

                    if (m_Context.NinjaFile.Motions.Count > 0)
                        m_Context.MainMotion = m_Context.NinjaFile.Motions[0];

                    if (m_Context.MainMotion == null && !string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
                    {
                        string[] motionExts = { ".njm", ".gjm", ".xjm", ".nam" };
                        foreach (string motExt in motionExts)
                        {
                            string candidate = Path.Combine(baseDir, assetName + motExt);
                            if (File.Exists(candidate))
                            {
                                byte[] motBytes = File.ReadAllBytes(candidate);
                                NinjaBinaryFile companionFile = new NinjaBinaryFile(motBytes);
                                if (companionFile.Motions.Count > 0)
                                {
                                    m_Context.MainMotion = companionFile.Motions[0];
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityNinjaInspector] Could not load {path}: {ex.Message}");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("UnityNinja Data Inspector", EditorStyles.boldLabel, GUILayout.Width(170));
            m_UseDebugView = GUILayout.Toggle(m_UseDebugView, m_UseDebugView ? "Mode: Raw Debug" : "Mode: Normal", EditorStyles.toolbarButton, GUILayout.Width(130));
            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(m_Context.AssetPath))
                GUILayout.Label(m_Context.AssetPath, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            EditorGUI.BeginChangeCheck();
            m_SelectedAsset = EditorGUILayout.ObjectField("Target Ninja Asset", m_SelectedAsset, typeof(UnityEngine.Object), true);
            if (EditorGUI.EndChangeCheck()) LoadAsset();

            if (!m_Context.IsModel && !m_Context.IsMotion && !m_Context.IsLandTable && !m_Context.IsCgm)
            {
                EditorGUILayout.HelpBox("Select a Ninja asset (.nj, .gj, .xj, .cgm, .njm, .gjm, .sa1lvl, .sa2lvl) in the Project window.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();

            // 1. Main Left Content Pane
            EditorGUILayout.BeginVertical();

            // Only draw model category tabs when inspecting a 3D model hierarchy
            if (m_Context.IsModel && !m_UseDebugView)
            {
                m_SelectedTab = GUILayout.Toolbar(Mathf.Clamp(m_SelectedTab, 0, m_ModelTabNames.Length - 1), m_ModelTabNames, EditorStyles.toolbarButton);
                EditorGUILayout.Space(4);
            }

            m_MainScrollPosition = EditorGUILayout.BeginScrollView(m_MainScrollPosition);

            if (m_UseDebugView)
            {
                NinjaReflectionDrawer.DrawObjectReflectively(m_Context.RootModel ?? (object)m_Context.MainMotion ?? (object)m_Context.LevelData ?? m_Context.CgmData, "Raw Object Data");
            }
            else if (m_Context.IsCgm)
            {
                DrawCgmTab();
            }
            else if (m_Context.IsLandTable)
            {
                DrawLandTableTab();
            }
            else if (m_Context.IsMotion && !m_Context.IsModel)
            {
                DrawMotionTab();
            }
            else if (m_Context.IsModel)
            {
                switch (m_SelectedTab)
                {
                    case 0: DrawNodeTreeTab(); break;
                    case 1: DrawMeshesTab(); break;
                    case 2: DrawMaterialsTab(); break;
                    case 3: DrawMotionTab(); break;
                }
            }

            if (m_ShowJsonOutput && !string.IsNullOrEmpty(m_DumpedJsonText))
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Category JSON Output", EditorStyles.boldLabel);
                if (GUILayout.Button("Copy to Clipboard", GUILayout.Width(120))) GUIUtility.systemCopyBuffer = m_DumpedJsonText;
                if (GUILayout.Button("Close", GUILayout.Width(60))) m_ShowJsonOutput = false;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.TextArea(m_DumpedJsonText, GUILayout.MaxHeight(180));
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // 2. Right Persistent Metrics Overview Pane
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(280));
            m_RightPaneScrollPosition = EditorGUILayout.BeginScrollView(m_RightPaneScrollPosition, false, false);
            DrawOverviewPane();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }
    }
}