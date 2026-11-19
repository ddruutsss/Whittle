using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security.Principal;
using System.ServiceProcess;

namespace Whittle.Core
{
    public sealed class LiveStats
    {
        public int Processes;
        public int ServicesRunning;
        public ulong MemTotal;
        public ulong MemUsed;
        public int MemLoad;
        public long DiskFree = -1;
        public long DiskTotal = -1;
        public TimeSpan Uptime;
    }

    public static class SysInfo
    {
        const string CurrentVersion = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        static string gpu;
        static int build = -1;

        /// <summary>Set by the test build's screenshot mode so published images don't show the real PC name.</summary>
        public static string MachineNameOverride;

        public static string MachineName
        {
            get { return MachineNameOverride ?? Environment.MachineName; }
        }

        public static int Build
        {
            get
            {
                if (build < 0)
                {
                    int b;
                    object v = Reg.ReadOrNull("HKLM", CurrentVersion, "CurrentBuildNumber") ?? Reg.ReadOrNull("HKLM", CurrentVersion, "CurrentBuild");
                    build = v != null && int.TryParse(Convert.ToString(v), out b) ? b : Environment.OSVersion.Version.Build;
                }
                return build;
            }
        }

        public static string WindowsName
        {
            get
            {
                string product = Convert.ToString(Reg.ReadOrNull("HKLM", CurrentVersion, "ProductName") ?? "Windows");
                // Windows 11 still reports "Windows 10" in ProductName.
                if (Build >= 22000) product = product.Replace("Windows 10", "Windows 11");
                return product;
            }
        }

        public static string VersionLine
        {
            get
            {
                string display = Convert.ToString(Reg.ReadOrNull("HKLM", CurrentVersion, "DisplayVersion") ?? "");
                object ubr = Reg.ReadOrNull("HKLM", CurrentVersion, "UBR");
                string b = Build + (ubr != null ? "." + Reg.ToUInt32(ubr) : "");
                return (display.Length > 0 ? display + " · " : "") + "build " + b;
            }
        }

        public static string Cpu
        {
            get
            {
                object v = Reg.ReadOrNull("HKLM", @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString");
                string s = v == null ? "Unknown processor" : Convert.ToString(v).Trim();
                s = s.Replace("(R)", "").Replace("(TM)", "").Replace(" CPU", "");
                while (s.Contains("  ")) s = s.Replace("  ", " ");
                return s;
            }
        }

        public static string Gpu
        {
            get
            {
                if (gpu != null) return gpu;
                List<string> names = new List<string>();
                try
                {
                    using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                    {
                        foreach (ManagementObject o in s.Get())
                        {
                            string n = Convert.ToString(o["Name"]);
                            if (!string.IsNullOrEmpty(n) && n.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) < 0 &&
                                n.IndexOf("Remote", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                names.Add(n.Replace("(R)", "").Replace("(TM)", "").Trim());
                            }
                        }
                    }
                }
                catch
                {
                }
                string best = null;
                foreach (string n in names)
                {
                    string l = n.ToLowerInvariant();
                    if (l.Contains("nvidia") || l.Contains("radeon") || l.Contains(" arc")) { best = n; break; }
                }
                gpu = best ?? (names.Count > 0 ? names[0] : "Unknown graphics");
                return gpu;
            }
        }

        public static bool HasBattery
        {
            get
            {
                Native.SYSTEM_POWER_STATUS s;
                if (!Native.GetSystemPowerStatus(out s)) return false;
                return s.BatteryFlag != 128 && s.BatteryFlag != 255;
            }
        }

        public static bool IsAdmin
        {
            get
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
        }

        /// <summary>
        /// When Whittle was elevated with a different admin account than the one signed in, returns
        /// both account names (elevated, desktop). Per-user settings would then go to the wrong account.
        /// Returns null when they match or it can't be told.
        /// </summary>
        public static string[] AccountMismatch()
        {
            try
            {
                string mine;
                using (WindowsIdentity id = WindowsIdentity.GetCurrent()) mine = id.User.Value;
                int session = Process.GetCurrentProcess().SessionId;
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Handle, SessionId FROM Win32_Process WHERE Name = 'explorer.exe'"))
                {
                    foreach (ManagementObject o in s.Get())
                    {
                        if (Convert.ToInt32(o["SessionId"]) != session) continue;
                        ManagementBaseObject result = o.InvokeMethod("GetOwnerSid", null, null);
                        if (result == null || Convert.ToInt32(result["ReturnValue"]) != 0) continue;
                        string shell = Convert.ToString(result["Sid"]);
                        if (string.IsNullOrEmpty(shell) || string.Equals(shell, mine, StringComparison.OrdinalIgnoreCase)) return null;
                        return new[] { AccountName(mine), AccountName(shell) };
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        static string AccountName(string sid)
        {
            try { return new SecurityIdentifier(sid).Translate(typeof(NTAccount)).Value; }
            catch { return sid; }
        }

        public static LiveStats Measure()
        {
            LiveStats s = new LiveStats();
            try { s.Processes = Process.GetProcesses().Length; } catch { }
            try
            {
                int running = 0;
                foreach (ServiceController sc in ServiceController.GetServices())
                {
                    try { if (sc.Status == ServiceControllerStatus.Running) running++; } catch { }
                    sc.Dispose();
                }
                s.ServicesRunning = running;
            }
            catch
            {
            }
            Native.MEMORYSTATUSEX m = new Native.MEMORYSTATUSEX();
            m.dwLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MEMORYSTATUSEX));
            if (Native.GlobalMemoryStatusEx(ref m))
            {
                s.MemTotal = m.ullTotalPhys;
                s.MemUsed = m.ullTotalPhys - m.ullAvailPhys;
                s.MemLoad = m.dwMemoryLoad;
            }
            try
            {
                DriveInfo d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                s.DiskFree = d.AvailableFreeSpace;
                s.DiskTotal = d.TotalSize;
            }
            catch
            {
            }
            s.Uptime = TimeSpan.FromMilliseconds(Native.GetTickCount64());
            return s;
        }

        public static string FormatGB(ulong bytes)
        {
            double gb = bytes / 1073741824.0;
            return gb.ToString(gb >= 100 ? "0" : "0.0", System.Globalization.CultureInfo.CurrentCulture) + " GB";
        }

        public static string FormatUptime(TimeSpan t)
        {
            if (t.TotalDays >= 1) return (int)t.TotalDays + " d " + t.Hours + " h";
            if (t.TotalHours >= 1) return (int)t.TotalHours + " h " + t.Minutes + " min";
            return Math.Max(1, t.Minutes) + " min";
        }
    }

    public sealed class AppSettings
    {
        public bool CreateRestorePoint = true;
        public bool RestartExplorer = true;
        public bool ShowAdvanced = true;

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                if (File.Exists(AppPaths.Settings))
                {
                    Dictionary<string, object> d = Json.ReadFile(AppPaths.Settings);
                    s.CreateRestorePoint = Json.Bool(d, "createRestorePoint", true);
                    s.RestartExplorer = Json.Bool(d, "restartExplorer", true);
                    s.ShowAdvanced = Json.Bool(d, "showAdvanced", true);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Settings couldn't be read, using defaults: " + ex.Message);
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["createRestorePoint"] = CreateRestorePoint;
                d["restartExplorer"] = RestartExplorer;
                d["showAdvanced"] = ShowAdvanced;
                Json.WriteFile(AppPaths.Settings, d);
            }
            catch (Exception ex)
            {
                Log.Warn("Settings couldn't be saved: " + ex.Message);
            }
        }
    }
}
