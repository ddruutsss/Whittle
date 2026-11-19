using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using Microsoft.Win32;

namespace Whittle.Core
{
    public enum Risk { Safe, Tradeoff, Advanced }
    public enum ActionState { Applied, NotApplied, NotApplicable }
    public enum TweakState { NotApplied, Partial, Applied, Unavailable }

    /// <summary>One concrete change. Every action can detect, apply (saving the original first) and revert.</summary>
    public abstract class TweakAction
    {
        /// <summary>Stable identity used as the journal key.</summary>
        public abstract string Key { get; }
        public abstract string Describe();
        public abstract ActionState Detect();
        public abstract void Apply(Journal journal);
        public abstract void Revert(Journal journal);
        /// <summary>True when the action only touches the registry and can run in the test sandbox.</summary>
        public virtual bool Sandboxable { get { return false; } }
    }

    public sealed class Tweak
    {
        public string Id;
        public string Category;
        public string Title;
        public string Description;
        public string Note;
        public Risk Risk = Risk.Safe;
        public bool NeedsRestart;
        public bool RestartsExplorer;
        public int MinBuild;
        public int MaxBuild = int.MaxValue;
        public string RefreshHint;
        public readonly List<TweakAction> Actions = new List<TweakAction>();

        public Tweak(string id, string category, string title, string description)
        {
            Id = id;
            Category = category;
            Title = title;
            Description = description;
        }

        public TweakState Detect()
        {
            int on = 0, off = 0;
            foreach (TweakAction a in Actions)
            {
                ActionState s;
                try { s = a.Detect(); } catch { s = ActionState.NotApplied; }
                if (s == ActionState.Applied) on++;
                else if (s == ActionState.NotApplied) off++;
            }
            if (on == 0 && off == 0) return TweakState.Unavailable;
            if (off == 0) return TweakState.Applied;
            if (on == 0) return TweakState.NotApplied;
            return TweakState.Partial;
        }

        public bool SupportsBuild(int build)
        {
            return build >= MinBuild && build <= MaxBuild;
        }

        // ---- Fluent builders used by the catalog ----

        RegValueAction Last
        {
            get
            {
                RegValueAction r = Actions.Count > 0 ? Actions[Actions.Count - 1] as RegValueAction : null;
                if (r == null) throw new InvalidOperationException("Last action of " + Id + " is not a registry value.");
                return r;
            }
        }

        public Tweak Dword(string hive, string path, string name, uint value, object revertTo)
        {
            Actions.Add(new RegValueAction(hive, path, name, RegistryValueKind.DWord, value, revertTo));
            return this;
        }

        public Tweak Str(string hive, string path, string name, string value, object revertTo)
        {
            Actions.Add(new RegValueAction(hive, path, name, RegistryValueKind.String, value, revertTo));
            return this;
        }

        public Tweak Bin(string hive, string path, string name, byte[] value, byte[] revertTo)
        {
            Actions.Add(new RegValueAction(hive, path, name, RegistryValueKind.Binary, value, revertTo));
            return this;
        }

        public Tweak Service(string service, uint start, uint revertTo)
        {
            Actions.Add(new ServiceStartAction(service, start, revertTo));
            return this;
        }

        public Tweak Task(string folder, string name)
        {
            Actions.Add(new TaskAction(folder, name));
            return this;
        }

        public Tweak Custom(TweakAction action)
        {
            Actions.Add(action);
            return this;
        }

        /// <summary>Points a URL protocol at a do-nothing program so Windows stops offering to find an app for it.</summary>
        public Tweak NullProtocol(string scheme)
        {
            string path = @"SOFTWARE\Classes\" + scheme;
            string noop = "\"" + Path.Combine(Proc.System32, "systray.exe") + "\"";
            Actions.Add(new RegValueAction("HKLM", path, "", RegistryValueKind.String, "URL:" + scheme, null));
            Actions.Add(new RegValueAction("HKLM", path, "URL Protocol", RegistryValueKind.String, "", null));
            Actions.Add(new RegValueAction("HKLM", path, "NoOpenWith", RegistryValueKind.String, "", null));
            Actions.Add(new RegValueAction("HKLM", path + @"\shell\open\command", "", RegistryValueKind.String, noop, null));
            return this;
        }

        /// <summary>When the value is missing, Windows behaves as if it had this value.</summary>
        public Tweak Missing(object means) { Last.MissingMeans = means; return this; }
        public Tweak Match(Func<object, bool> matcher) { Last.Matcher = matcher; return this; }
        public Tweak WithRisk(Risk risk) { Risk = risk; return this; }
        public Tweak Restart() { NeedsRestart = true; return this; }
        public Tweak Explorer() { RestartsExplorer = true; return this; }
        public Tweak Builds(int min, int max) { MinBuild = min; MaxBuild = max; return this; }
        public Tweak MinimumBuild(int min) { MinBuild = min; return this; }
        public Tweak WithNote(string note) { Note = note; return this; }
        public Tweak Refresh(string hint) { RefreshHint = hint; return this; }
    }

    /// <summary>Sets one registry value. Saves the old value (or its absence) and any keys it had to create.</summary>
    public class RegValueAction : TweakAction
    {
        public readonly string Hive;
        public readonly string Path;
        public readonly string Name;
        public readonly RegistryValueKind Kind;
        public readonly object Target;
        /// <summary>Value written when reverting without a saved original. Null deletes the value.</summary>
        public readonly object RevertTo;
        public object MissingMeans;
        public Func<object, bool> Matcher;
        public bool KeyMustExist;

        public RegValueAction(string hive, string path, string name, RegistryValueKind kind, object target, object revertTo)
        {
            Hive = hive;
            Path = path;
            Name = name;
            Kind = kind;
            Target = target;
            RevertTo = revertTo;
        }

        public override string Key
        {
            get { return "reg:" + Hive + "\\" + Path.ToLowerInvariant() + "|" + Name.ToLowerInvariant(); }
        }

        public override bool Sandboxable { get { return true; } }

        public override string Describe()
        {
            return Reg.Display(Hive, Path) + " › " + (Name.Length == 0 ? "(Default)" : Name) + " = " + FormatValue(Target, Kind);
        }

        public static string FormatValue(object v, RegistryValueKind kind)
        {
            if (v == null) return "(deleted)";
            if (kind == RegistryValueKind.DWord) return Reg.ToUInt32(v).ToString();
            if (kind == RegistryValueKind.Binary)
            {
                byte[] b = (byte[])Reg.ToRegistryObject(v, kind);
                return BitConverter.ToString(b).Replace('-', ' ');
            }
            return "\"" + Convert.ToString(v) + "\"";
        }

        public override ActionState Detect()
        {
            try
            {
                return DetectCore();
            }
            catch (System.Security.SecurityException)
            {
                // Some keys (e.g. the Network Service profile) can only be read as admin.
                // "Can't tell" lets the readable values decide instead of reporting "off".
                return ActionState.NotApplicable;
            }
            catch (UnauthorizedAccessException)
            {
                return ActionState.NotApplicable;
            }
        }

        ActionState DetectCore()
        {
            if (!Reg.KeyExists(Hive, Path))
            {
                if (KeyMustExist) return ActionState.NotApplicable;
                return MissingMatches() ? ActionState.Applied : ActionState.NotApplied;
            }
            object cur; RegistryValueKind k;
            if (!Reg.TryRead(Hive, Path, Name, out cur, out k))
            {
                return MissingMatches() ? ActionState.Applied : ActionState.NotApplied;
            }
            if (Matcher != null)
            {
                try { return Matcher(cur) ? ActionState.Applied : ActionState.NotApplied; }
                catch { return ActionState.NotApplied; }
            }
            return Reg.ValueEquals(cur, k, Target, Kind) ? ActionState.Applied : ActionState.NotApplied;
        }

        bool MissingMatches()
        {
            return MissingMeans != null && Reg.LooseEquals(MissingMeans, Target);
        }

        public override void Apply(Journal journal)
        {
            if (KeyMustExist && !Reg.KeyExists(Hive, Path)) return;
            if (!journal.HasOriginal(Key))
            {
                object cur; RegistryValueKind k;
                bool existed = Reg.TryRead(Hive, Path, Name, out cur, out k);
                List<string> created = Reg.EnsureKey(Hive, Path);
                journal.SetOriginal(Key, RegSnapshot.Capture(existed, k, cur, created));
            }
            else
            {
                List<string> created = Reg.EnsureKey(Hive, Path);
                RegSnapshot.AddCreated(journal.GetOriginal(Key), created);
            }
            Reg.Write(Hive, Path, Name, Target, Kind);
        }

        public override void Revert(Journal journal)
        {
            Dictionary<string, object> snap = journal.GetOriginal(Key);
            if (snap != null)
            {
                if (RegSnapshot.Existed(snap))
                {
                    Reg.Write(Hive, Path, Name, RegSnapshot.Data(snap), RegSnapshot.Kind(snap));
                }
                else
                {
                    Reg.DeleteValue(Hive, Path, Name);
                    List<string> created = RegSnapshot.Created(snap);
                    for (int i = created.Count - 1; i >= 0; i--)
                    {
                        if (!Reg.DeleteKeyIfEmpty(Hive, created[i])) break;
                    }
                }
                journal.RemoveOriginal(Key);
                return;
            }
            if (KeyMustExist && !Reg.KeyExists(Hive, Path)) return;
            if (RevertTo == null)
            {
                Reg.DeleteValue(Hive, Path, Name);
            }
            else
            {
                Reg.Write(Hive, Path, Name, RevertTo, Kind);
            }
        }
    }

    /// <summary>Changes a service's startup type. Stops it when disabling; starts it again if undo restores Automatic.</summary>
    public sealed class ServiceStartAction : RegValueAction
    {
        public readonly string ServiceName;

        public ServiceStartAction(string service, uint start, uint revertTo)
            : base("HKLM", @"SYSTEM\CurrentControlSet\Services\" + service, "Start", RegistryValueKind.DWord, start, revertTo)
        {
            ServiceName = service;
            KeyMustExist = true;
        }

        public override string Describe()
        {
            return "Service " + ServiceName + " › " + StartName(Reg.ToUInt32(Target));
        }

        public static string StartName(uint start)
        {
            switch (start)
            {
                case 2: return "Automatic";
                case 3: return "Manual";
                case 4: return "Disabled";
                default: return start.ToString();
            }
        }

        public override void Apply(Journal journal)
        {
            base.Apply(journal);
            if (Reg.SandboxRoot != null) return;
            if (Reg.ToUInt32(Target) == 4) TryStop();
        }

        public override void Revert(Journal journal)
        {
            base.Revert(journal);
            if (Reg.SandboxRoot != null) return;
            object v = Reg.ReadOrNull("HKLM", Path, "Start");
            if (v != null && Reg.ToUInt32(v) == 2) TryStart();
        }

        void TryStop()
        {
            try
            {
                using (ServiceController sc = new ServiceController(ServiceName))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.CanStop)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(12));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn(ServiceName + " is set to Disabled but couldn't be stopped right now (" + ex.Message.Trim() + "). It won't start after the next restart.");
            }
        }

        void TryStart()
        {
            try
            {
                using (ServiceController sc = new ServiceController(ServiceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped) sc.Start();
                }
            }
            catch (Exception ex)
            {
                Log.Warn(ServiceName + " will start after the next restart (" + ex.Message.Trim() + ").");
            }
        }
    }

    /// <summary>Disables a scheduled task; undo restores its previous enabled state.</summary>
    public sealed class TaskAction : TweakAction
    {
        public readonly string Folder;
        public readonly string Name;

        public TaskAction(string folder, string name)
        {
            Folder = folder;
            Name = name;
        }

        public override string Key { get { return "task:" + Folder.ToLowerInvariant() + "\\" + Name.ToLowerInvariant(); } }

        public override string Describe() { return "Scheduled task " + Folder + "\\" + Name + " › Disabled"; }

        public override ActionState Detect()
        {
            dynamic task = TaskSched.GetTask(Folder, Name);
            if (task == null) return ActionState.NotApplicable;
            bool enabled = task.Enabled;
            return enabled ? ActionState.NotApplied : ActionState.Applied;
        }

        public override void Apply(Journal journal)
        {
            dynamic task = TaskSched.GetTask(Folder, Name);
            if (task == null) return;
            if (!journal.HasOriginal(Key))
            {
                Dictionary<string, object> snap = new Dictionary<string, object>();
                bool enabled = task.Enabled;
                snap["enabled"] = enabled;
                journal.SetOriginal(Key, snap);
            }
            task.Enabled = false;
        }

        public override void Revert(Journal journal)
        {
            dynamic task = TaskSched.GetTask(Folder, Name);
            Dictionary<string, object> snap = journal.GetOriginal(Key);
            bool enable = snap == null || Json.Bool(snap, "enabled", true);
            if (task != null) task.Enabled = enable;
            journal.RemoveOriginal(Key);
        }
    }

    /// <summary>
    /// Activates an Ultimate Performance plan, creating one from Windows' hidden template if needed.
    /// Detection uses the plan's resource name (powrprof.dll,-19), which is the same in every language.
    /// </summary>
    public sealed class PowerPlanAction : TweakAction
    {
        const string SchemesKey = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";
        const string UltimateTemplate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        const string WhittlePlan = "5b1e7a52-6a3f-4f0e-9d53-1c4a7d2e8f61";
        public const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";

        public override string Key { get { return "power:scheme"; } }
        public override string Describe() { return "Power plan › Ultimate Performance"; }

        static string ActiveScheme()
        {
            object v = Reg.ReadOrNull("HKLM", SchemesKey, "ActivePowerScheme");
            return v == null ? "" : Convert.ToString(v).ToLowerInvariant();
        }

        static bool IsUltimate(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return false;
            if (guid == UltimateTemplate || guid == WhittlePlan) return true;
            object name = Reg.ReadOrNull("HKLM", SchemesKey + "\\" + guid, "FriendlyName");
            return name != null && Convert.ToString(name).IndexOf("powrprof.dll,-19,", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool SchemeExists(string guid)
        {
            return Reg.KeyExists("HKLM", SchemesKey + "\\" + guid);
        }

        static string FindExistingUltimate()
        {
            using (RegistryKey k = Reg.Open("HKLM", SchemesKey, false))
            {
                if (k == null) return null;
                foreach (string sub in k.GetSubKeyNames())
                {
                    string g = sub.ToLowerInvariant();
                    if (g == UltimateTemplate) continue;
                    if (IsUltimate(g)) return g;
                }
            }
            return null;
        }

        public override ActionState Detect()
        {
            return IsUltimate(ActiveScheme()) ? ActionState.Applied : ActionState.NotApplied;
        }

        public override void Apply(Journal journal)
        {
            string active = ActiveScheme();
            if (!journal.HasOriginal(Key))
            {
                Dictionary<string, object> snap = new Dictionary<string, object>();
                snap["previous"] = active;
                snap["created"] = false;
                journal.SetOriginal(Key, snap);
            }
            if (IsUltimate(active)) return;

            string target = FindExistingUltimate();
            if (target == null)
            {
                ProcResult dup = Proc.PowerCfg("/duplicatescheme " + UltimateTemplate + " " + WhittlePlan);
                if (!dup.Ok || !SchemeExists(WhittlePlan))
                {
                    throw new InvalidOperationException("Windows refused to create the Ultimate Performance plan. " + (dup.Output + " " + dup.Error).Trim());
                }
                target = WhittlePlan;
                journal.GetOriginal(Key)["created"] = true;
            }
            ProcResult set = Proc.PowerCfg("/setactive " + target);
            if (!set.Ok || ActiveScheme() != target)
            {
                throw new InvalidOperationException("Couldn't switch to the Ultimate Performance plan. " + (set.Output + " " + set.Error).Trim());
            }
        }

        public override void Revert(Journal journal)
        {
            Dictionary<string, object> snap = journal.GetOriginal(Key);
            string previous = snap != null ? Json.Str(snap, "previous", Balanced) : Balanced;
            if (string.IsNullOrEmpty(previous) || !SchemeExists(previous) || IsUltimate(previous)) previous = Balanced;
            ProcResult set = Proc.PowerCfg("/setactive " + previous);
            if (!set.Ok) throw new InvalidOperationException("Couldn't switch back to the previous power plan. " + (set.Output + " " + set.Error).Trim());
            if (snap != null && Json.Bool(snap, "created", false) && SchemeExists(WhittlePlan))
            {
                Proc.PowerCfg("/delete " + WhittlePlan);
            }
            journal.RemoveOriginal(Key);
        }
    }

    /// <summary>Turns hibernation off with powercfg (which also removes hiberfil.sys).</summary>
    public sealed class HibernateAction : TweakAction
    {
        const string PowerKey = @"SYSTEM\CurrentControlSet\Control\Power";

        public override string Key { get { return "power:hibernate"; } }
        public override string Describe() { return "powercfg /hibernate off"; }

        static bool Enabled()
        {
            object v = Reg.ReadOrNull("HKLM", PowerKey, "HibernateEnabled");
            return v == null || Reg.ToUInt32(v) != 0;
        }

        public override ActionState Detect()
        {
            return Enabled() ? ActionState.NotApplied : ActionState.Applied;
        }

        public override void Apply(Journal journal)
        {
            if (!journal.HasOriginal(Key))
            {
                Dictionary<string, object> snap = new Dictionary<string, object>();
                snap["enabled"] = Enabled();
                journal.SetOriginal(Key, snap);
            }
            ProcResult r = Proc.PowerCfg("/hibernate off");
            if (!r.Ok) throw new InvalidOperationException("powercfg couldn't turn off hibernation. " + (r.Output + " " + r.Error).Trim());
        }

        public override void Revert(Journal journal)
        {
            Dictionary<string, object> snap = journal.GetOriginal(Key);
            if (snap == null || Json.Bool(snap, "enabled", true))
            {
                ProcResult r = Proc.PowerCfg("/hibernate on");
                if (!r.Ok) throw new InvalidOperationException("powercfg couldn't turn hibernation back on. " + (r.Output + " " + r.Error).Trim());
            }
            journal.RemoveOriginal(Key);
        }
    }

    /// <summary>
    /// Edits one entry inside DirectX's per-user settings string ("A=1;B=0;") without
    /// disturbing the others, e.g. SwapEffectUpgradeEnable for windowed-game optimizations.
    /// </summary>
    public sealed class DxSettingAction : TweakAction
    {
        const string Hive = "HKCU";
        const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
        const string ValueName = "DirectXUserGlobalSettings";
        readonly string setting;
        readonly string onValue;
        readonly string offValue;

        public DxSettingAction(string setting, string onValue, string offValue)
        {
            this.setting = setting;
            this.onValue = onValue;
            this.offValue = offValue;
        }

        public override string Key { get { return "dx:" + setting.ToLowerInvariant(); } }
        public override bool Sandboxable { get { return true; } }
        public override string Describe() { return "DirectX setting " + setting + " = " + onValue; }

        static List<KeyValuePair<string, string>> Parse(string s)
        {
            List<KeyValuePair<string, string>> r = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(s)) return r;
            foreach (string part in s.Split(';'))
            {
                string p = part.Trim();
                if (p.Length == 0) continue;
                int eq = p.IndexOf('=');
                if (eq < 0) r.Add(new KeyValuePair<string, string>(p, ""));
                else r.Add(new KeyValuePair<string, string>(p.Substring(0, eq), p.Substring(eq + 1)));
            }
            return r;
        }

        static string Join(List<KeyValuePair<string, string>> items)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, string> kv in items) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
            return sb.ToString();
        }

        static string Get(List<KeyValuePair<string, string>> items, string key)
        {
            foreach (KeyValuePair<string, string> kv in items)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        static void Set(List<KeyValuePair<string, string>> items, string key, string value)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    if (value == null) items.RemoveAt(i);
                    else items[i] = new KeyValuePair<string, string>(items[i].Key, value);
                    return;
                }
            }
            if (value != null) items.Add(new KeyValuePair<string, string>(key, value));
        }

        string Current(out bool existed)
        {
            object v = Reg.ReadOrNull(Hive, KeyPath, ValueName);
            existed = v != null;
            return v == null ? "" : Convert.ToString(v);
        }

        public override ActionState Detect()
        {
            bool existed;
            string val = Get(Parse(Current(out existed)), setting);
            return val == onValue ? ActionState.Applied : ActionState.NotApplied;
        }

        public override void Apply(Journal journal)
        {
            bool existed;
            List<KeyValuePair<string, string>> items = Parse(Current(out existed));
            if (!journal.HasOriginal(Key))
            {
                Dictionary<string, object> snap = new Dictionary<string, object>();
                snap["valueExisted"] = existed;
                string old = Get(items, setting);
                snap["hadSetting"] = old != null;
                snap["oldSetting"] = old ?? "";
                snap["created"] = Reg.EnsureKey(Hive, KeyPath).ToArray();
                journal.SetOriginal(Key, snap);
            }
            Set(items, setting, onValue);
            Reg.Write(Hive, KeyPath, ValueName, Join(items), RegistryValueKind.String);
        }

        public override void Revert(Journal journal)
        {
            bool existed;
            List<KeyValuePair<string, string>> items = Parse(Current(out existed));
            Dictionary<string, object> snap = journal.GetOriginal(Key);
            if (snap == null)
            {
                Set(items, setting, offValue);
                Reg.Write(Hive, KeyPath, ValueName, Join(items), RegistryValueKind.String);
                return;
            }
            Set(items, setting, Json.Bool(snap, "hadSetting", false) ? Json.Str(snap, "oldSetting", "") : null);
            if (items.Count == 0 && !Json.Bool(snap, "valueExisted", false))
            {
                Reg.DeleteValue(Hive, KeyPath, ValueName);
                List<string> created = Json.StrList(snap, "created");
                for (int i = created.Count - 1; i >= 0; i--)
                {
                    if (!Reg.DeleteKeyIfEmpty(Hive, created[i])) break;
                }
            }
            else
            {
                Reg.Write(Hive, KeyPath, ValueName, Join(items), RegistryValueKind.String);
            }
            journal.RemoveOriginal(Key);
        }
    }
}
