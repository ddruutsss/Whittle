using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Whittle.Core
{
    public enum StartupSource { UserRun, MachineRun, MachineRun32, UserFolder, CommonFolder, StoreApp }

    public sealed class StartupItem
    {
        public string Name;
        public string Command;
        public string FilePath;
        public string Publisher = "";
        public string Description = "";
        public StartupSource Source;
        public bool Enabled;
        public bool Locked;

        internal string ApprovedHive;
        internal string ApprovedPath;
        internal string ApprovedName;
        internal string StoreKeyPath;

        public string SourceLabel
        {
            get
            {
                switch (Source)
                {
                    case StartupSource.UserRun: return "Registry · you";
                    case StartupSource.MachineRun:
                    case StartupSource.MachineRun32: return "Registry · all users";
                    case StartupSource.UserFolder: return "Startup folder · you";
                    case StartupSource.CommonFolder: return "Startup folder · all users";
                    default: return "Store app";
                }
            }
        }
    }

    /// <summary>
    /// Lists and toggles startup apps through the same StartupApproved keys Task Manager uses,
    /// so nothing is deleted and changes show up in Task Manager too.
    /// </summary>
    public static class StartupManager
    {
        const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string StoreTasks = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";

        public static List<StartupItem> List()
        {
            List<StartupItem> items = new List<StartupItem>();
            ReadRun(items, "HKCU", RunKey, StartupSource.UserRun, "HKCU", Approved + @"\Run");
            ReadRun(items, "HKLM", RunKey, StartupSource.MachineRun, "HKLM", Approved + @"\Run");
            ReadRun(items, "HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", StartupSource.MachineRun32, "HKLM", Approved + @"\Run32");
            ReadFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSource.UserFolder, "HKCU");
            ReadFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSource.CommonFolder, "HKLM");
            ReadStoreTasks(items);
            items.Sort(delegate(StartupItem a, StartupItem b)
            {
                if (a.Enabled != b.Enabled) return a.Enabled ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return items;
        }

        static void ReadRun(List<StartupItem> items, string hive, string path, StartupSource source, string approvedHive, string approvedPath)
        {
            try
            {
                using (RegistryKey k = Reg.Open(hive, path, false))
                {
                    if (k == null) return;
                    foreach (string name in k.GetValueNames())
                    {
                        if (name.Length == 0) continue;
                        string cmd = Convert.ToString(k.GetValue(name, ""));
                        StartupItem it = new StartupItem();
                        it.Name = name;
                        it.Command = cmd;
                        it.Source = source;
                        it.ApprovedHive = approvedHive;
                        it.ApprovedPath = approvedPath;
                        it.ApprovedName = name;
                        it.FilePath = ExtractExe(cmd);
                        it.Enabled = IsApproved(approvedHive, approvedPath, name);
                        Describe(it);
                        items.Add(it);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't read startup entries from " + hive + "\\" + path + ": " + ex.Message);
            }
        }

        static void ReadFolder(List<StartupItem> items, string folder, StartupSource source, string approvedHive)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
                foreach (string file in Directory.GetFiles(folder))
                {
                    string fn = Path.GetFileName(file);
                    if (fn.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    StartupItem it = new StartupItem();
                    it.Name = Path.GetFileNameWithoutExtension(file);
                    it.Command = file;
                    it.Source = source;
                    it.ApprovedHive = approvedHive;
                    it.ApprovedPath = Approved + @"\StartupFolder";
                    it.ApprovedName = fn;
                    it.FilePath = fn.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? ResolveShortcut(file) : file;
                    if (it.FilePath != null && it.FilePath != file) it.Command = it.FilePath;
                    it.Enabled = IsApproved(approvedHive, it.ApprovedPath, fn);
                    Describe(it);
                    items.Add(it);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't read the startup folder " + folder + ": " + ex.Message);
            }
        }

        static void ReadStoreTasks(List<StartupItem> items)
        {
            try
            {
                using (RegistryKey root = Reg.Open("HKCU", StoreTasks, false))
                {
                    if (root == null) return;
                    foreach (string family in root.GetSubKeyNames())
                    {
                        using (RegistryKey fk = root.OpenSubKey(family))
                        {
                            if (fk == null) continue;
                            foreach (string task in fk.GetSubKeyNames())
                            {
                                using (RegistryKey tk = fk.OpenSubKey(task))
                                {
                                    if (tk == null) continue;
                                    object state = tk.GetValue("State");
                                    if (state == null) continue;
                                    uint s = Reg.ToUInt32(state);
                                    StartupItem it = new StartupItem();
                                    string pkg = family.Contains("_") ? family.Substring(0, family.IndexOf('_')) : family;
                                    AppDef def = null;
                                    foreach (AppDef d in AppCatalog.All) if (d.Matches(pkg)) { def = d; break; }
                                    it.Name = def != null ? def.Title : PrettyPackage(pkg);
                                    it.Command = family;
                                    it.Publisher = "Microsoft Store app";
                                    it.Source = StartupSource.StoreApp;
                                    it.StoreKeyPath = StoreTasks + "\\" + family + "\\" + task;
                                    it.Enabled = s == 2 || s == 4;
                                    it.Locked = s == 3 || s == 4;
                                    items.Add(it);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't read Store app startup tasks: " + ex.Message);
            }
        }

        static string PrettyPackage(string pkg)
        {
            int dot = pkg.LastIndexOf('.');
            string s = dot >= 0 && dot < pkg.Length - 1 ? pkg.Substring(dot + 1) : pkg;
            return s.Length > 0 ? s : pkg;
        }

        static bool IsApproved(string hive, string path, string name)
        {
            object v = Reg.ReadOrNull(hive, path, name);
            byte[] b = v as byte[];
            if (b == null || b.Length == 0) return true;
            return (b[0] & 1) == 0;
        }

        public static void SetEnabled(StartupItem item, bool enabled)
        {
            if (item.Locked) throw new InvalidOperationException("This startup task is managed by a policy.");
            if (item.Source == StartupSource.StoreApp)
            {
                Reg.Write("HKCU", item.StoreKeyPath, "State", enabled ? 2u : 1u, RegistryValueKind.DWord);
            }
            else
            {
                byte[] data = new byte[12];
                if (enabled)
                {
                    data[0] = 0x02;
                }
                else
                {
                    data[0] = 0x03;
                    BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
                }
                Reg.Write(item.ApprovedHive, item.ApprovedPath, item.ApprovedName, data, RegistryValueKind.Binary);
            }
            item.Enabled = enabled;
            Log.Info((enabled ? "Startup app turned on: " : "Startup app turned off: ") + item.Name);
        }

        static readonly System.Text.RegularExpressions.Regex IdSuffix =
            new System.Text.RegularExpressions.Regex("[_-][0-9A-Fa-f]{16,}$");

        static void Describe(StartupItem it)
        {
            string description = null;
            if (it.FilePath != null)
            {
                try
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(it.FilePath);
                    if (!string.IsNullOrWhiteSpace(fvi.CompanyName)) it.Publisher = fvi.CompanyName.Trim();
                    if (!string.IsNullOrWhiteSpace(fvi.FileDescription)) description = fvi.FileDescription.Trim();
                }
                catch
                {
                }
            }
            // Names like "MicrosoftEdgeAutoLaunch_712E8038B422E054..." read better as the program's own name.
            if (IdSuffix.IsMatch(it.Name))
            {
                it.Name = description ?? IdSuffix.Replace(it.Name, "");
            }
            if (description != null && !string.Equals(description, it.Name, StringComparison.CurrentCultureIgnoreCase))
            {
                it.Description = description;
            }
        }

        public static string ExtractExe(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            string c = Environment.ExpandEnvironmentVariables(command.Trim());
            string path;
            if (c.StartsWith("\""))
            {
                int end = c.IndexOf('"', 1);
                path = end > 1 ? c.Substring(1, end - 1) : c.Trim('"');
            }
            else
            {
                int idx = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                path = idx > 0 ? c.Substring(0, idx + 4) : c.Split(' ')[0];
            }
            try
            {
                if (!Path.IsPathRooted(path))
                {
                    string sys = Path.Combine(Proc.System32, path);
                    if (File.Exists(sys)) return sys;
                    if (File.Exists(sys + ".exe")) return sys + ".exe";
                    return null;
                }
                return File.Exists(path) ? path : null;
            }
            catch
            {
                return null;
            }
        }

        static string ResolveShortcut(string lnk)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(t);
                dynamic sc = shell.CreateShortcut(lnk);
                string target = sc.TargetPath;
                return string.IsNullOrEmpty(target) ? lnk : target;
            }
            catch
            {
                return lnk;
            }
        }
    }
}
