using System;
using UnityEngine;
using UnityEditor;

namespace UnityNinja.Editor
{
    public partial class UnityNinjaInspectorWindow
    {
        public static string CleanEnumString(object enumValue)
        {
            if (enumValue == null) return "None";
            string raw = enumValue.ToString();
            return raw
                .Replace("Vertex_", "")
                .Replace("Bits_", "")
                .Replace("Tiny_", "")
                .Replace("Material_", "")
                .Replace("Strip_", "")
                .Replace("Volume_", "");
        }

        public static void DrawPaginationControls(ref int currentPage, int totalItems, int itemsPerPage)
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling((float)totalItems / itemsPerPage));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Prev", GUILayout.Width(50)) && currentPage > 0) currentPage--;
            EditorGUILayout.LabelField($"Page {currentPage + 1}/{totalPages} ({currentPage * itemsPerPage}-{Math.Min(totalItems, (currentPage + 1) * itemsPerPage) - 1})");
            if (GUILayout.Button("Next", GUILayout.Width(50)) && currentPage < totalPages - 1) currentPage++;
            EditorGUILayout.EndHorizontal();
        }
    }
}