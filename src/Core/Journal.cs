using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace Whittle.Core
{
    public sealed class RemovedApp
    {
        public string Name;
        public string Family;
        public string Title;
        public DateTime When;
    }

    /// <summary>
    /// Persistent record of every original value Whittle replaced, which tweaks it applied,
    /// and which apps it removed. Undo reads from here, so it restores exactly what was there.
    /// </summary>
    public sealed class Journal
    {
        readonly string path;
        readonly object gate = new object();
        Dictionary<string, object> originals = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object> applied = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        List<object> removedApps = new List<object>();
        Dictionary<string, object> baseline;

        public Journal(string path)
        {
            this.path = path;
        }

        public string FilePath { get { return path; } }

        public static Journal Load(string path)
        {
            Journal j = new Journal(path);
            if (!File.Exists(path)) return j;
            try
            {
                Dictionary<string, object> d = Json.ReadFile(path);
                Dictionary<string, object> o = Json.Obj(d, "originals");
                if (o != null) foreach (KeyValuePair<string, object> kv in o) j.originals[kv.Key] = kv.Value;
                Dictionary<string, object> a = Json.Obj(d, "applied");
                if (a != null) foreach (KeyValuePair<string, object> kv in a) j.applied[kv.Key] = kv.Value;
                foreach (Dictionary<string, object> r in Json.ObjList(d, "removedApps")) j.removedApps.Add(r);
                j.baseline = Json.Obj(d, "baseline");
            }
            catch (Exception ex)
            {
                // Keep the unreadable file so nothing is lost, and start a fresh journal.
                try { File.Copy(path, path + ".unreadable-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } catch { }
                Log.Error("Undo journal could not be read and was set aside: " + ex.Message);
            }
            return j;
        }

        public void Save()
        {
            lock (gate)
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["app"] = Brand.Name;
                d["format"] = 1;
                d["saved"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
                d["applied"] = applied;
                d["removedApps"] = removedApps;
                d["baseline"] = baseline;
                d["originals"] = originals;
                Json.WriteFile(path, d);
            }
        }

        // ---- Original values ----

        public bool HasOriginal(string key)
        {
            lock (gate) return originals.ContainsKey(key);
        }

        public Dictionary<string, object> GetOriginal(string key)
        {
            lock (gate)
            {
                object v;
                return originals.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
            }
        }

        public void SetOriginal(string key, Dictionary<string, object> snapshot)
        {
            lock (gate) originals[key] = snapshot;
        }

        public void RemoveOriginal(string key)
        {
            lock (gate) originals.Remove(key);
        }

        public int OriginalCount
        {
            get { lock (gate) return originals.Count; }
        }

        // ---- Applied tweaks ----

        public void MarkApplied(string tweakId)
        {
            lock (gate) applied[tweakId] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
        }

        public void MarkReverted(string tweakId)
        {
            lock (gate) applied.Remove(tweakId);
        }

        public Dictionary<string, DateTime> AppliedTweaks()
        {
            Dictionary<string, DateTime> r = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            lock (gate)
            {
                foreach (KeyValuePair<string, object> kv in applied)
                {
                    DateTime t;
                    r[kv.Key] = DateTime.TryParse(Convert.ToString(kv.Value), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t) ? t : DateTime.MinValue;
                }
            }
            return r;
        }

        // ---- Removed apps ----

        public void AddRemovedApp(string name, string family, string title)
        {
            lock (gate)
            {
                for (int i = removedApps.Count - 1; i >= 0; i--)
                {
                    Dictionary<string, object> e = removedApps[i] as Dictionary<string, object>;
                    if (e != null && string.Equals(Json.Str(e, "name", ""), name, StringComparison.OrdinalIgnoreCase)) removedApps.RemoveAt(i);
                }
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["name"] = name;
                d["family"] = family ?? "";
                d["title"] = title ?? name;
                d["time"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
                removedApps.Add(d);
            }
        }

        public void ForgetRemovedApp(string name)
        {
            lock (gate)
            {
                for (int i = removedApps.Count - 1; i >= 0; i--)
                {
                    Dictionary<string, object> e = removedApps[i] as Dictionary<string, object>;
                    if (e != null && string.Equals(Json.Str(e, "name", ""), name, StringComparison.OrdinalIgnoreCase)) removedApps.RemoveAt(i);
                }
            }
        }

        public List<RemovedApp> RemovedApps()
        {
            List<RemovedApp> r = new List<RemovedApp>();
            lock (gate)
            {
                foreach (object o in removedApps)
                {
                    Dictionary<string, object> e = o as Dictionary<string, object>;
                    if (e == null) continue;
                    RemovedApp a = new RemovedApp();
                    a.Name = Json.Str(e, "name", "");
                    a.Family = Json.Str(e, "family", "");
                    a.Title = Json.Str(e, "title", a.Name);
                    DateTime t;
                    a.When = DateTime.TryParse(Json.Str(e, "time", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t) ? t : DateTime.MinValue;
                    r.Add(a);
                }
            }
            return r;
        }

        // ---- First-run baseline (shown on the Home page for comparison) ----

        public Dictionary<string, object> Baseline
        {
            get { lock (gate) return baseline; }
            set { lock (gate) baseline = value; }
        }
    }

    /// <summary>Encodes a registry value so it survives a round trip through JSON.</summary>
    public static class RegSnapshot
    {
        public static Dictionary<string, object> Capture(bool existed, RegistryValueKind kind, object data, List<string> createdKeys)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["existed"] = existed;
            if (existed)
            {
                d["kind"] = kind.ToString();
                d["data"] = Encode(kind, data);
            }
            d["created"] = createdKeys != null ? createdKeys.ToArray() : new string[0];
            return d;
        }

        public static object Encode(RegistryValueKind kind, object data)
        {
            switch (kind)
            {
                case RegistryValueKind.DWord: return (long)Reg.ToUInt32(data);
                case RegistryValueKind.QWord: return Convert.ToString(unchecked((ulong)Convert.ToInt64(data)), CultureInfo.InvariantCulture);
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString: return Convert.ToString(data);
                case RegistryValueKind.MultiString: return data as string[] ?? new string[0];
                default:
                    byte[] b = data as byte[];
                    return b != null ? Convert.ToBase64String(b) : "";
            }
        }

        public static bool Existed(Dictionary<string, object> snap)
        {
            return Json.Bool(snap, "existed", false);
        }

        public static RegistryValueKind Kind(Dictionary<string, object> snap)
        {
            string k = Json.Str(snap, "kind", "String");
            try { return (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), k, true); }
            catch { return RegistryValueKind.String; }
        }

        public static object Data(Dictionary<string, object> snap)
        {
            object v;
            snap.TryGetValue("data", out v);
            return v;
        }

        public static List<string> Created(Dictionary<string, object> snap)
        {
            return Json.StrList(snap, "created");
        }

        public static void AddCreated(Dictionary<string, object> snap, List<string> more)
        {
            if (more == null || more.Count == 0) return;
            List<string> all = Created(snap);
            foreach (string s in more) if (!all.Contains(s)) all.Add(s);
            snap["created"] = all.ToArray();
        }
    }
}
