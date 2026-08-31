using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace UnityNinja.Editor
{
    public static class NinjaReflectionDrawer
    {
        public static void DrawObjectReflectively(object target, string label = "", int depth = 0, int maxDepth = 5)
        {
            if (target == null)
            {
                if (!string.IsNullOrEmpty(label)) EditorGUILayout.LabelField(label, "null");
                return;
            }

            if (depth > maxDepth)
            {
                if (!string.IsNullOrEmpty(label)) EditorGUILayout.LabelField(label, target.ToString());
                return;
            }

            Type type = target.GetType();

            if (type.IsPrimitive || type.IsEnum || target is string)
            {
                EditorGUILayout.LabelField(label, target.ToString());
                return;
            }

            if (target is Vector3 v3)
            {
                EditorGUILayout.Vector3Field(label, v3);
                return;
            }

            if (target is Color32 col)
            {
                EditorGUILayout.ColorField(label, col);
                return;
            }

            if (target is IEnumerable list && !(target is string))
            {
                if (!string.IsNullOrEmpty(label)) EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                int idx = 0;
                foreach (var item in list)
                {
                    DrawObjectReflectively(item, $"[{idx++}]", depth + 1, maxDepth);
                    if (idx > 50) { EditorGUILayout.LabelField("... [truncated]"); break; }
                }
                EditorGUI.indentLevel--;
                return;
            }

            if (!string.IsNullOrEmpty(label)) EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                object val = null;
                try { val = prop.GetValue(target, null); } catch { continue; }
                DrawObjectReflectively(val, prop.Name, depth + 1, maxDepth);
            }

            EditorGUI.indentLevel--;
        }
    }
}