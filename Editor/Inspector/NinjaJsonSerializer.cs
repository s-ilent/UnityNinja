using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityNinja.Editor
{
    public static class NinjaJsonSerializer
    {
        public static string Serialize(object obj, int indentLevel = 0)
        {
            using (StringWriter sw = new StringWriter())
            {
                HashSet<object> visited = new HashSet<object>(new ReferenceComparer());
                SerializeInternal(obj, sw, indentLevel, visited);
                return sw.ToString();
            }
        }

        private static void SerializeInternal(object obj, TextWriter writer, int indentLevel, HashSet<object> visited)
        {
            if (obj == null) { writer.Write("null"); return; }
            if (indentLevel > 8) { writer.Write("\"...\""); return; }

            Type type = obj.GetType();

            if (obj is string str) { writer.Write($"\"{str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "")}\""); return; }
            if (type.IsPrimitive || obj is decimal)
            {
                writer.Write(obj is bool b ? (b ? "true" : "false") : Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            if (type.IsEnum) { writer.Write($"\"{obj}\""); return; }

            if (obj is Vector3 v3)
            {
                writer.Write($"{{\"x\": {v3.x:F4}, \"y\": {v3.y:F4}, \"z\": {v3.z:F4}}}");
                return;
            }

            if (obj is Color32 c32)
            {
                writer.Write($"{{\"r\": {c32.r}, \"g\": {c32.g}, \"b\": {c32.b}, \"a\": {c32.a}}}");
                return;
            }

            // Detect cycles for complex objects
            if (!type.IsValueType && visited.Contains(obj))
            {
                writer.Write("\"[Circular]\"");
                return;
            }

            if (!type.IsValueType)
            {
                visited.Add(obj);
            }

            if (obj is IDictionary dict)
            {
                writer.WriteLine("{");
                string indent = new string(' ', (indentLevel + 1) * 2);
                string endIndent = new string(' ', indentLevel * 2);
                bool first = true;
                foreach (DictionaryEntry kvp in dict)
                {
                    if (!first) writer.WriteLine(",");
                    first = false;
                    writer.Write($"{indent}\"{kvp.Key}\": ");
                    SerializeInternal(kvp.Value, writer, indentLevel + 1, visited);
                }
                writer.WriteLine();
                writer.Write(endIndent + "}");
                return;
            }

            if (obj is IEnumerable list && !(obj is string))
            {
                writer.WriteLine("[");
                string indent = new string(' ', (indentLevel + 1) * 2);
                string endIndent = new string(' ', indentLevel * 2);
                bool first = true;
                int count = 0;
                foreach (var item in list)
                {
                    if (count++ > 200) { writer.WriteLine($"{indent}\"... [truncated {count} items]\""); break; }
                    if (!first) writer.WriteLine(",");
                    first = false;
                    writer.Write(indent);
                    SerializeInternal(item, writer, indentLevel + 1, visited);
                }
                writer.WriteLine();
                writer.Write(endIndent + "]");
                return;
            }

            writer.WriteLine("{");
            string propIndent = new string(' ', (indentLevel + 1) * 2);
            string closeIndent = new string(' ', indentLevel * 2);
            bool firstProp = true;

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                if (prop.Name is "Parent" or "Sibling" or "CodePath" or "Children") continue; // Avoid hierarchy tree redundancy

                object val = null;
                try { val = prop.GetValue(obj, null); } catch { continue; }

                if (!firstProp) writer.WriteLine(",");
                firstProp = false;
                writer.Write($"{propIndent}\"{prop.Name}\": ");
                SerializeInternal(val, writer, indentLevel + 1, visited);
            }

            writer.WriteLine();
            writer.Write(closeIndent + "}");
        }

        private class ReferenceComparer : IEqualityComparer<object>
        {
            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}