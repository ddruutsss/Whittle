using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Whittle.Core
{
    /// <summary>
    /// Thin registry layer. Hives are written as "HKCU", "HKLM" or "HKU".
    /// When <see cref="SandboxRoot"/> is set, every read and write is redirected to
    /// HKCU\&lt;SandboxRoot&gt;\&lt;hive&gt;\... so tweaks can be tested without touching real settings.
    /// </summary>
    public static class Reg
    {
        public static string SandboxRoot;

        static RegistryKey BaseKey(string hive)
        {
            switch (hive)
            {
                case "HKCU": return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                case "HKLM": return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                case "HKU": return RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
                default: throw new ArgumentException("Unknown hive " + hive);
            }
        }

        static void Resolve(string hive, string path, out RegistryKey root, out string sub)
        {
            if (SandboxRoot != null)
            {
                root = BaseKey("HKCU");
                sub = SandboxRoot + "\\" + hive + (string.IsNullOrEmpty(path) ? "" : "\\" + path);
            }
            else
            {
                root = BaseKey(hive);
                sub = path;
            }
        }

        public static string Display(string hive, string path)
        {
            return hive + "\\" + path;
        }

        public static RegistryKey Open(string hive, string path, bool writable)
        {
            RegistryKey root; string sub;
            Resolve(hive, path, out root, out sub);
            using (root)
            {
                return root.OpenSubKey(sub, writable);
            }
        }

        public static bool KeyExists(string hive, string path)
        {
            using (RegistryKey k = Open(hive, path, false))
            {
                return k != null;
            }
        }

        public static bool TryRead(string hive, string path, string name, out object value, out RegistryValueKind kind)
        {
            value = null;
            kind = RegistryValueKind.Unknown;
            using (RegistryKey k = Open(hive, path, false))
            {
                if (k == null) return false;
                object v = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (v == null) return false;
                value = v;
                kind = k.GetValueKind(name);
                return true;
            }
        }

        public static object ReadOrNull(string hive, string path, string name)
        {
            object v; RegistryValueKind k;
            try
            {
                return TryRead(hive, path, name, out v, out k) ? v : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Creates any missing keys along the path and returns the ones it created, outermost first.</summary>
        public static List<string> EnsureKey(string hive, string path)
        {
            List<string> created = new List<string>();
            string[] parts = path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string current = "";
            foreach (string part in parts)
            {
                current = current.Length == 0 ? part : current + "\\" + part;
                if (!KeyExists(hive, current))
                {
                    RegistryKey root; string sub;
                    Resolve(hive, current, out root, out sub);
                    using (root)
                    using (RegistryKey nk = root.CreateSubKey(sub))
                    {
                        if (nk == null) throw new InvalidOperationException("Could not create " + Display(hive, current));
                    }
                    created.Add(current);
                }
            }
            return created;
        }

        public static void Write(string hive, string path, string name, object value, RegistryValueKind kind)
        {
            RegistryKey root; string sub;
            Resolve(hive, path, out root, out sub);
            using (root)
            using (RegistryKey k = root.CreateSubKey(sub))
            {
                if (k == null) throw new InvalidOperationException("Could not open " + Display(hive, path));
                k.SetValue(name, ToRegistryObject(value, kind), kind);
            }
        }

        public static void DeleteValue(string hive, string path, string name)
        {
            using (RegistryKey k = Open(hive, path, true))
            {
                if (k == null) return;
                if (k.GetValue(name) != null || Array.IndexOf(k.GetValueNames(), name) >= 0)
                {
                    k.DeleteValue(name, false);
                }
            }
        }

        /// <summary>Deletes a key only when it has no values and no subkeys.</summary>
        public static bool DeleteKeyIfEmpty(string hive, string path)
        {
            using (RegistryKey k = Open(hive, path, false))
            {
                if (k == null) return true;
                if (k.SubKeyCount > 0 || k.ValueCount > 0) return false;
            }
            int slash = path.LastIndexOf('\\');
            string parent = slash < 0 ? "" : path.Substring(0, slash);
            string leaf = slash < 0 ? path : path.Substring(slash + 1);
            RegistryKey root; string sub;
            Resolve(hive, parent, out root, out sub);
            using (root)
            using (RegistryKey p = string.IsNullOrEmpty(sub) ? root : root.OpenSubKey(sub, true))
            {
                if (p == null) return false;
                p.DeleteSubKey(leaf, false);
                return true;
            }
        }

        public static void DeleteSandbox()
        {
            if (SandboxRoot == null) return;
            using (RegistryKey hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                hkcu.DeleteSubKeyTree(SandboxRoot, false);
            }
        }

        /// <summary>Converts our in-memory value representation to what RegistryKey.SetValue expects.</summary>
        public static object ToRegistryObject(object value, RegistryValueKind kind)
        {
            switch (kind)
            {
                case RegistryValueKind.DWord:
                    return unchecked((int)ToUInt32(value));
                case RegistryValueKind.QWord:
                    return unchecked((long)Convert.ToUInt64(value));
                case RegistryValueKind.MultiString:
                    if (value is string[]) return value;
                    if (value is System.Collections.IEnumerable && !(value is string))
                    {
                        List<string> l = new List<string>();
                        foreach (object o in (System.Collections.IEnumerable)value) l.Add(Convert.ToString(o));
                        return l.ToArray();
                    }
                    return new[] { Convert.ToString(value) };
                case RegistryValueKind.Binary:
                case RegistryValueKind.None:
                case RegistryValueKind.Unknown:
                    if (value is byte[]) return value;
                    if (value is string) return Convert.FromBase64String((string)value);
                    return new byte[0];
                default:
                    return Convert.ToString(value);
            }
        }

        public static uint ToUInt32(object value)
        {
            if (value is uint) return (uint)value;
            if (value is int) return unchecked((uint)(int)value);
            if (value is long) return unchecked((uint)(long)value);
            if (value is ulong) return unchecked((uint)(ulong)value);
            if (value is decimal) return unchecked((uint)(long)(decimal)value);
            if (value is string)
            {
                string s = ((string)value).Trim();
                uint r;
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                    uint.TryParse(s.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out r)) return r;
                long l;
                if (long.TryParse(s, out l)) return unchecked((uint)l);
            }
            return unchecked((uint)Convert.ToInt64(value));
        }

        /// <summary>Compares a value read from the registry with a target value.</summary>
        public static bool ValueEquals(object current, RegistryValueKind currentKind, object target, RegistryValueKind targetKind)
        {
            if (current == null || target == null) return current == null && target == null;
            switch (targetKind)
            {
                case RegistryValueKind.DWord:
                    if (currentKind != RegistryValueKind.DWord) return false;
                    return ToUInt32(current) == ToUInt32(target);
                case RegistryValueKind.QWord:
                    if (currentKind != RegistryValueKind.QWord) return false;
                    return Convert.ToUInt64(unchecked((ulong)(long)current)) == Convert.ToUInt64(target);
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    if (currentKind != RegistryValueKind.String && currentKind != RegistryValueKind.ExpandString) return false;
                    return string.Equals(Convert.ToString(current), Convert.ToString(target), StringComparison.Ordinal);
                case RegistryValueKind.Binary:
                    {
                        byte[] a = current as byte[];
                        byte[] b = (byte[])ToRegistryObject(target, RegistryValueKind.Binary);
                        if (a == null || a.Length != b.Length) return false;
                        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
                        return true;
                    }
                case RegistryValueKind.MultiString:
                    {
                        string[] a = current as string[];
                        string[] b = (string[])ToRegistryObject(target, RegistryValueKind.MultiString);
                        if (a == null || a.Length != b.Length) return false;
                        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
                        return true;
                    }
            }
            return false;
        }

        /// <summary>Loose comparison used for "a missing value behaves like X" checks.</summary>
        public static bool LooseEquals(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is string || b is string) return string.Equals(Convert.ToString(a), Convert.ToString(b), StringComparison.Ordinal);
            try
            {
                return ToUInt32(a) == ToUInt32(b);
            }
            catch
            {
                return false;
            }
        }
    }
}
