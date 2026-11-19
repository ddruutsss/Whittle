using System;
using System.Collections.Generic;

namespace Whittle.Core
{
    /// <summary>
    /// Everything the pages share: detected tweak states, the user's selection, scanned apps,
    /// the journal and settings. Mutated on the UI thread only.
    /// </summary>
    public sealed class AppState
    {
        public static AppState Current;

        public readonly Journal Journal;
        public readonly AppSettings Settings;
        public readonly List<Tweak> Tweaks = new List<Tweak>();
        public Dictionary<string, TweakState> States = new Dictionary<string, TweakState>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, bool> Desired = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> AppSelection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public List<AppEntry> Apps;
        public bool StatesReady;
        public bool Busy;
        /// <summary>Set when a profile is chosen before the app scan has finished.</summary>
        public bool SelectRecommendedAppsOnScan;

        /// <summary>Selection or pending changes changed.</summary>
        public event Action SelectionChanged;
        /// <summary>Tweak detection finished.</summary>
        public event Action StatesChanged;
        /// <summary>App scan finished.</summary>
        public event Action AppsChanged;

        public AppState(Journal journal, AppSettings settings)
        {
            Journal = journal;
            Settings = settings;
            int build = SysInfo.Build;
            foreach (Tweak t in Catalog.All)
            {
                if (t.SupportsBuild(build)) Tweaks.Add(t);
            }
        }

        public Tweak Find(string id)
        {
            foreach (Tweak t in Tweaks) if (string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        /// <summary>Reads every tweak's current state. Safe to call from a background thread.</summary>
        public Dictionary<string, TweakState> DetectAll()
        {
            Dictionary<string, TweakState> r = new Dictionary<string, TweakState>(StringComparer.OrdinalIgnoreCase);
            foreach (Tweak t in Tweaks)
            {
                TweakState s;
                try { s = t.Detect(); }
                catch (Exception ex)
                {
                    s = TweakState.NotApplied;
                    Log.Warn("Couldn't read the state of " + t.Title + ": " + ex.Message);
                }
                r[t.Id] = s;
            }
            return r;
        }

        /// <summary>Call on the UI thread with the result of <see cref="DetectAll"/>. Resets the selection to match.</summary>
        public void SetStates(Dictionary<string, TweakState> states)
        {
            States = states;
            StatesReady = true;
            Desired.Clear();
            foreach (Tweak t in Tweaks) Desired[t.Id] = StateOf(t) == TweakState.Applied;
            Raise(StatesChanged);
            Raise(SelectionChanged);
        }

        public void SetApps(List<AppEntry> apps)
        {
            Apps = apps;
            List<string> stale = new List<string>();
            foreach (string key in AppSelection) if (FindApp(key) == null) stale.Add(key);
            foreach (string key in stale) AppSelection.Remove(key);
            if (SelectRecommendedAppsOnScan)
            {
                SelectRecommendedAppsOnScan = false;
                foreach (AppEntry a in apps) if (a.Group == AppGroup.Recommended) AppSelection.Add(a.Key);
            }
            // An app that's installed again no longer belongs in the "removed" list.
            bool forgot = false;
            foreach (RemovedApp r in Journal.RemovedApps())
            {
                foreach (AppEntry a in apps)
                {
                    if (a.PackageNames.Contains(r.Name)) { Journal.ForgetRemovedApp(r.Name); forgot = true; break; }
                }
            }
            if (forgot) { try { Journal.Save(); } catch { } }
            Raise(AppsChanged);
            Raise(SelectionChanged);
        }

        public AppEntry FindApp(string key)
        {
            if (Apps == null) return null;
            foreach (AppEntry a in Apps)
            {
                if (string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase)) return a;
                if (a.Def != null)
                {
                    foreach (string id in a.Def.Ids) if (string.Equals(id, key, StringComparison.OrdinalIgnoreCase)) return a;
                }
                foreach (string n in a.PackageNames) if (string.Equals(n, key, StringComparison.OrdinalIgnoreCase)) return a;
            }
            return null;
        }

        public TweakState StateOf(Tweak t)
        {
            TweakState s;
            return States.TryGetValue(t.Id, out s) ? s : TweakState.NotApplied;
        }

        public bool IsDesired(Tweak t)
        {
            bool d;
            return Desired.TryGetValue(t.Id, out d) && d;
        }

        public bool WillApply(Tweak t)
        {
            return IsDesired(t) && StateOf(t) != TweakState.Applied && StateOf(t) != TweakState.Unavailable;
        }

        public bool WillRevert(Tweak t)
        {
            return !IsDesired(t) && StateOf(t) == TweakState.Applied;
        }

        public void SetDesired(Tweak t, bool on)
        {
            if (StateOf(t) == TweakState.Unavailable) return;
            Desired[t.Id] = on;
            Raise(SelectionChanged);
        }

        public void SetAppSelected(AppEntry a, bool on)
        {
            if (on) AppSelection.Add(a.Key);
            else AppSelection.Remove(a.Key);
            Raise(SelectionChanged);
        }

        public int PendingCount
        {
            get
            {
                int n = 0;
                foreach (Tweak t in Tweaks) if (WillApply(t) || WillRevert(t)) n++;
                return n + SelectedApps().Count;
            }
        }

        public List<AppEntry> SelectedApps()
        {
            List<AppEntry> r = new List<AppEntry>();
            if (Apps == null) return r;
            foreach (AppEntry a in Apps) if (AppSelection.Contains(a.Key)) r.Add(a);
            return r;
        }

        public ChangePlan BuildPlan()
        {
            ChangePlan p = new ChangePlan();
            foreach (Tweak t in Tweaks)
            {
                if (WillApply(t)) p.Apply.Add(t);
                else if (WillRevert(t)) p.Revert.Add(t);
            }
            p.RemoveApps.AddRange(SelectedApps());
            p.CreateRestorePoint = Settings.CreateRestorePoint;
            return p;
        }

        /// <summary>Puts every toggle back to the current state of the PC and clears the app selection.</summary>
        public void Discard()
        {
            foreach (Tweak t in Tweaks) Desired[t.Id] = StateOf(t) == TweakState.Applied;
            AppSelection.Clear();
            Raise(SelectionChanged);
        }

        /// <summary>Adds a profile's tweaks and recommended apps to the selection. Returns how many tweaks it turned on.</summary>
        public int SelectPreset(Preset p)
        {
            int added = 0;
            foreach (string id in p.Tweaks)
            {
                Tweak t = Find(id);
                if (t == null || StateOf(t) == TweakState.Unavailable) continue;
                if (!IsDesired(t)) added++;
                Desired[t.Id] = true;
            }
            if (p.RemoveRecommendedApps && Apps != null)
            {
                foreach (AppEntry a in Apps) if (a.Group == AppGroup.Recommended) AppSelection.Add(a.Key);
            }
            Raise(SelectionChanged);
            return added;
        }

        public void SelectConfig(SavedConfig c)
        {
            foreach (string id in c.Tweaks)
            {
                Tweak t = Find(id);
                if (t != null && StateOf(t) != TweakState.Unavailable) Desired[t.Id] = true;
            }
            foreach (string key in c.Apps)
            {
                AppEntry a = FindApp(key);
                if (a != null) AppSelection.Add(a.Key);
            }
            Raise(SelectionChanged);
        }

        /// <summary>A config describing the end state: tweaks that are on (or will be) and apps removed (or selected).</summary>
        public SavedConfig CaptureConfig(string name)
        {
            SavedConfig c = new SavedConfig();
            c.Name = name;
            c.Created = DateTime.Now;
            foreach (Tweak t in Tweaks) if (IsDesired(t)) c.Tweaks.Add(t.Id);
            foreach (string key in AppSelection) if (!c.Apps.Contains(key)) c.Apps.Add(key);
            foreach (RemovedApp r in Journal.RemovedApps())
            {
                AppDef d = null;
                foreach (AppDef def in AppCatalog.All) if (def.Matches(r.Name)) { d = def; break; }
                string key = d != null ? d.PrimaryId : r.Name;
                if (!c.Apps.Contains(key)) c.Apps.Add(key);
            }
            return c;
        }

        public int CountApplied()
        {
            int n = 0;
            foreach (Tweak t in Tweaks) if (StateOf(t) == TweakState.Applied) n++;
            return n;
        }

        public int CountAvailable()
        {
            int n = 0;
            foreach (Tweak t in Tweaks) if (StateOf(t) != TweakState.Unavailable) n++;
            return n;
        }

        static void Raise(Action a)
        {
            if (a != null) a();
        }
    }
}
