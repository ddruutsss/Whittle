using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Whittle.Core
{
    public sealed class Preset
    {
        public string Id;
        public string Title;
        public string Summary;
        public string Glyph;
        public string[] Tweaks;
        public bool RemoveRecommendedApps;
    }

    /// <summary>Built-in starting points. None of them touch security settings like Memory Integrity.</summary>
    public static class Presets
    {
        static readonly string[] Recommended =
        {
            "privacy.telemetry", "privacy.advertising-id", "privacy.tailored", "privacy.activity", "privacy.feedback",
            "ads.suggestions", "ads.lock-screen", "ads.start-recommended", "ads.web-search", "ads.edge", "ads.settings",
            "ai.copilot", "ai.recall", "ai.click-to-do", "ai.service",
            "perf.startup-delay", "perf.menu-delay", "perf.edge-background",
            "game.dvr",
            "update.no-auto-restart", "update.delivery-optimization", "update.device-apps",
            "ui.widgets", "ui.end-task", "explorer.extensions"
        };

        static readonly string[] Gaming =
        {
            "perf.power-plan", "perf.power-throttling", "perf.startup-delay", "perf.menu-delay", "perf.edge-background",
            "perf.foreground-priority",
            "game.game-mode", "game.hags", "game.windowed", "game.dvr", "game.mouse", "game.key-shortcuts",
            "game.mmcss", "game.network-throttling",
            "privacy.telemetry", "ads.suggestions", "ai.recall", "ai.service",
            "update.no-auto-restart", "update.no-drivers", "update.delivery-optimization",
            "ui.widgets", "ui.end-task"
        };

        static readonly string[] PrivacyExtra =
        {
            "privacy.app-tracking", "privacy.typing", "privacy.speech", "privacy.location", "privacy.find-device",
            "privacy.error-reporting", "privacy.edge-data", "ads.phone-link",
            "ai.edge", "ai.chrome", "ai.paint", "ai.notepad", "update.not-asap"
        };

        static readonly string[] MinimalExtra =
        {
            "perf.animations", "perf.transparency", "perf.background-apps", "perf.fast-startup", "perf.hibernation",
            "perf.last-access", "game.game-bar", "svc.sysmain", "svc.rarely-used", "ui.classic-menu", "explorer.this-pc"
        };

        static List<Preset> all;

        public static List<Preset> All
        {
            get
            {
                if (all != null) return all;
                all = new List<Preset>();
                all.Add(Make("gaming", "Gaming", "",
                    "Lower input latency and fewer interruptions while you play. Keeps Xbox apps and Game Bar.",
                    Gaming, true));
                all.Add(Make("recommended", "Recommended", "",
                    "Removes ads, telemetry and clutter without changing anything you'd miss.",
                    Recommended, true));
                all.Add(Make("privacy", "Privacy", "",
                    "Everything in Recommended, plus location, typing data, error reports and AI features turned off.",
                    Merge(Recommended, PrivacyExtra), true));
                all.Add(Make("minimal", "Minimal", "",
                    "Gaming and Privacy combined, with animations, hibernation and rarely used services off. For experienced users.",
                    Merge(Merge(Recommended, PrivacyExtra), Merge(Gaming, MinimalExtra)), true));
                return all;
            }
        }

        static Preset Make(string id, string title, string glyph, string summary, string[] tweaks, bool apps)
        {
            Preset p = new Preset();
            p.Id = id;
            p.Title = title;
            p.Glyph = glyph;
            p.Summary = summary;
            p.Tweaks = tweaks;
            p.RemoveRecommendedApps = apps;
            return p;
        }

        static string[] Merge(string[] a, string[] b)
        {
            List<string> r = new List<string>(a);
            foreach (string s in b) if (!r.Contains(s)) r.Add(s);
            return r.ToArray();
        }
    }

    public sealed class SavedConfig
    {
        public string Name = "";
        public DateTime Created = DateTime.Now;
        public List<string> Tweaks = new List<string>();
        public List<string> Apps = new List<string>();
        public string FilePath;
    }

    /// <summary>Saves and loads configs as small, readable JSON files.</summary>
    public static class ConfigStore
    {
        public const string Extension = ".whittle.json";

        public static List<SavedConfig> List()
        {
            List<SavedConfig> r = new List<SavedConfig>();
            try
            {
                if (!Directory.Exists(AppPaths.Configs)) return r;
                foreach (string f in Directory.GetFiles(AppPaths.Configs, "*.json"))
                {
                    try { r.Add(Read(f)); }
                    catch (Exception ex) { Log.Warn("Skipped unreadable config " + Path.GetFileName(f) + ": " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't list configs: " + ex.Message);
            }
            r.Sort(delegate(SavedConfig a, SavedConfig b) { return b.Created.CompareTo(a.Created); });
            return r;
        }

        public static SavedConfig Read(string path)
        {
            Dictionary<string, object> d = Json.ReadFile(path);
            if (!d.ContainsKey("tweaks") && !d.ContainsKey("apps"))
            {
                throw new FormatException("This file isn't a Whittle config.");
            }
            SavedConfig c = new SavedConfig();
            c.Name = Json.Str(d, "name", Path.GetFileNameWithoutExtension(path));
            DateTime t;
            if (DateTime.TryParse(Json.Str(d, "created", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t)) c.Created = t;
            c.Tweaks = Json.StrList(d, "tweaks");
            c.Apps = Json.StrList(d, "apps");
            c.FilePath = path;
            return c;
        }

        public static void Write(SavedConfig c, string path)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["whittle"] = 1;
            d["name"] = c.Name;
            d["created"] = c.Created.ToString("s", CultureInfo.InvariantCulture);
            d["tweaks"] = c.Tweaks;
            d["apps"] = c.Apps;
            Json.WriteFile(path, d);
            c.FilePath = path;
        }

        public static string SaveToLibrary(SavedConfig c)
        {
            Directory.CreateDirectory(AppPaths.Configs);
            string baseName = SafeFileName(c.Name);
            string path = Path.Combine(AppPaths.Configs, baseName + Extension);
            int n = 2;
            while (File.Exists(path) && !string.Equals(path, c.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                path = Path.Combine(AppPaths.Configs, baseName + " (" + n++ + ")" + Extension);
            }
            Write(c, path);
            return path;
        }

        public static string SafeFileName(string name)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in (name ?? "").Trim())
            {
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '-' : ch);
            }
            string s = sb.ToString().Trim('.', ' ');
            return s.Length == 0 ? "Config" : (s.Length > 60 ? s.Substring(0, 60) : s);
        }
    }
}
