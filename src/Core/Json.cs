using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Whittle.Core
{
    /// <summary>
    /// JSON helpers. Parsing produces Dictionary&lt;string, object&gt;, ArrayList, string,
    /// bool, int/long/decimal. Writing pretty-prints so config files stay readable.
    /// </summary>
    public static class Json
    {
        public static Dictionary<string, object> ParseObject(string text)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = int.MaxValue;
            object o = js.DeserializeObject(text);
            Dictionary<string, object> d = o as Dictionary<string, object>;
            if (d == null) throw new FormatException("Expected a JSON object.");
            return d;
        }

        public static Dictionary<string, object> ReadFile(string path)
        {
            return ParseObject(File.ReadAllText(path, Encoding.UTF8));
        }

        public static void WriteFile(string path, object value)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, Write(value), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Replace(tmp, path, null);
            }
            else
            {
                File.Move(tmp, path);
            }
        }

        public static string Write(object value)
        {
            StringBuilder sb = new StringBuilder();
            WriteValue(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v, int indent)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is DateTime) { WriteString(sb, ((DateTime)v).ToString("o", CultureInfo.InvariantCulture)); return; }
            if (v is int || v is long || v is uint || v is short || v is byte || v is ulong)
            {
                sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                return;
            }
            if (v is double || v is float || v is decimal)
            {
                sb.Append(Convert.ToDecimal(v).ToString(CultureInfo.InvariantCulture));
                return;
            }
            IDictionary dict = v as IDictionary;
            if (dict != null)
            {
                if (dict.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{\n");
                int i = 0;
                foreach (DictionaryEntry e in dict)
                {
                    Indent(sb, indent + 1);
                    WriteString(sb, Convert.ToString(e.Key));
                    sb.Append(": ");
                    WriteValue(sb, e.Value, indent + 1);
                    if (++i < dict.Count) sb.Append(',');
                    sb.Append('\n');
                }
                Indent(sb, indent);
                sb.Append('}');
                return;
            }
            IEnumerable list = v as IEnumerable;
            if (list != null)
            {
                List<object> items = new List<object>();
                foreach (object o in list) items.Add(o);
                if (items.Count == 0) { sb.Append("[]"); return; }
                sb.Append("[\n");
                for (int i = 0; i < items.Count; i++)
                {
                    Indent(sb, indent + 1);
                    WriteValue(sb, items[i], indent + 1);
                    if (i < items.Count - 1) sb.Append(',');
                    sb.Append('\n');
                }
                Indent(sb, indent);
                sb.Append(']');
                return;
            }
            WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        static void Indent(StringBuilder sb, int n)
        {
            sb.Append(' ', n * 2);
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---- Typed accessors for parsed data ----

        public static string Str(IDictionary<string, object> d, string key, string fallback)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null) return Convert.ToString(v, CultureInfo.InvariantCulture);
            return fallback;
        }

        public static bool Bool(IDictionary<string, object> d, string key, bool fallback)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is bool) return (bool)v;
            return fallback;
        }

        public static long Long(IDictionary<string, object> d, string key, long fallback)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null)
            {
                try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { }
            }
            return fallback;
        }

        public static List<string> StrList(IDictionary<string, object> d, string key)
        {
            List<string> r = new List<string>();
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is IEnumerable && !(v is string))
            {
                foreach (object o in (IEnumerable)v) if (o != null) r.Add(Convert.ToString(o, CultureInfo.InvariantCulture));
            }
            return r;
        }

        public static Dictionary<string, object> Obj(IDictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v as Dictionary<string, object>;
            return null;
        }

        public static List<Dictionary<string, object>> ObjList(IDictionary<string, object> d, string key)
        {
            List<Dictionary<string, object>> r = new List<Dictionary<string, object>>();
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is IEnumerable && !(v is string))
            {
                foreach (object o in (IEnumerable)v)
                {
                    Dictionary<string, object> od = o as Dictionary<string, object>;
                    if (od != null) r.Add(od);
                }
            }
            return r;
        }
    }
}
