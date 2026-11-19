using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;

namespace Whittle.Core
{
    public sealed class ChangePlan
    {
        public readonly List<Tweak> Apply = new List<Tweak>();
        public readonly List<Tweak> Revert = new List<Tweak>();
        public readonly List<AppEntry> RemoveApps = new List<AppEntry>();
        public bool CreateRestorePoint;

        public int Count { get { return Apply.Count + Revert.Count + RemoveApps.Count; } }

        public bool NeedsRestart
        {
            get
            {
                foreach (Tweak t in Apply) if (t.NeedsRestart) return true;
                foreach (Tweak t in Revert) if (t.NeedsRestart) return true;
                return false;
            }
        }
    }

    public sealed class RunResult
    {
        public int Succeeded;
        public int Failed;
        public readonly List<string> Failures = new List<string>();
        public bool NeedsRestart;
        public bool Cancelled;
        public string RestorePointProblem;
        public bool RestorePointCreated;
        public bool ExplorerRestarted;
    }

    /// <summary>Runs a change plan: restore point, apply, undo, app removal, then refreshes Windows.</summary>
    public sealed class Engine
    {
        readonly Journal journal;
        readonly AppSettings settings;

        public event Action<int, int, string> Progress;

        /// <summary>Asked when the restore point fails. Return true to continue without one.</summary>
        public Func<string, bool> ContinueWithoutRestorePoint;

        public Engine(Journal journal, AppSettings settings)
        {
            this.journal = journal;
            this.settings = settings;
        }

        void Report(int done, int total, string label)
        {
            Action<int, int, string> h = Progress;
            if (h != null) h(done, total, label);
        }

        public RunResult Run(ChangePlan plan)
        {
            RunResult result = new RunResult();
            int total = plan.Count + (plan.CreateRestorePoint ? 1 : 0) + 1;
            int step = 0;
            Log.Info("---- " + plan.Apply.Count + " to apply, " + plan.Revert.Count + " to undo, " + plan.RemoveApps.Count + " apps to remove ----");

            if (plan.CreateRestorePoint)
            {
                Report(step, total, "Creating a restore point");
                Log.Info("Creating a system restore point (this can take a minute)");
                string problem = SysRestore.Create("Whittle: before applying changes");
                if (problem == null)
                {
                    result.RestorePointCreated = true;
                    Log.Ok("Restore point created");
                }
                else
                {
                    result.RestorePointProblem = problem;
                    Log.Warn("No restore point: " + problem);
                    Func<string, bool> ask = ContinueWithoutRestorePoint;
                    if (ask != null && !ask(problem))
                    {
                        result.Cancelled = true;
                        Log.Info("Stopped before making any changes.");
                        return result;
                    }
                }
                step++;
            }

            foreach (Tweak t in plan.Apply)
            {
                Report(step, total, t.Title);
                ApplyTweak(t, result);
                step++;
            }

            foreach (Tweak t in plan.Revert)
            {
                Report(step, total, "Undo: " + t.Title);
                RevertTweak(t, result);
                step++;
            }

            if (plan.RemoveApps.Count > 0)
            {
                Report(step, total, "Removing " + plan.RemoveApps[0].Title);
                Log.Info("Removing " + plan.RemoveApps.Count + " app(s)");
                int appIndex = 0;
                Appx.Remove(plan.RemoveApps, delegate(AppEntry app, bool ok, string error)
                {
                    appIndex++;
                    step++;
                    if (ok)
                    {
                        result.Succeeded++;
                        foreach (string n in app.PackageNames) journal.AddRemovedApp(n, app.Family, app.Title);
                        Log.Ok("Removed " + app.Title);
                    }
                    else
                    {
                        result.Failed++;
                        result.Failures.Add(app.Title + ": " + error);
                        Log.Error("Couldn't remove " + app.Title + ": " + error);
                    }
                    string next = appIndex < plan.RemoveApps.Count ? "Removing " + plan.RemoveApps[appIndex].Title : "Finishing up";
                    Report(step, total, next);
                });
                SaveJournal();
            }

            Report(step, total, "Finishing up");
            result.NeedsRestart = plan.NeedsRestart;
            RefreshWindows(plan, result);
            SaveJournal();
            Report(total, total, "Done");
            Log.Info("---- Finished: " + result.Succeeded + " succeeded, " + result.Failed + " failed ----");
            return result;
        }

        void ApplyTweak(Tweak t, RunResult result)
        {
            Log.Info("Applying: " + t.Title);
            int failed = 0;
            string first = null;
            foreach (TweakAction a in t.Actions)
            {
                try
                {
                    a.Apply(journal);
                }
                catch (Exception ex)
                {
                    failed++;
                    if (first == null) first = ex.Message;
                    Log.Warn("   " + a.Describe() + " \u2014 " + ex.Message);
                }
            }
            if (failed < t.Actions.Count) journal.MarkApplied(t.Id);
            SaveJournal();
            if (failed == 0)
            {
                result.Succeeded++;
                Log.Ok(t.Title);
            }
            else
            {
                result.Failed++;
                result.Failures.Add(t.Title + ": " + first);
                Log.Error(t.Title + " was only partly applied");
            }
        }

        void RevertTweak(Tweak t, RunResult result)
        {
            Log.Info("Undoing: " + t.Title);
            int failed = 0;
            string first = null;
            for (int i = t.Actions.Count - 1; i >= 0; i--)
            {
                try
                {
                    t.Actions[i].Revert(journal);
                }
                catch (Exception ex)
                {
                    failed++;
                    if (first == null) first = ex.Message;
                    Log.Warn("   " + t.Actions[i].Describe() + " \u2014 " + ex.Message);
                }
            }
            if (failed == 0) journal.MarkReverted(t.Id);
            SaveJournal();
            if (failed == 0)
            {
                result.Succeeded++;
                Log.Ok("Undone: " + t.Title);
            }
            else
            {
                result.Failed++;
                result.Failures.Add("Undo " + t.Title + ": " + first);
                Log.Error(t.Title + " was only partly undone");
            }
        }

        void SaveJournal()
        {
            try
            {
                journal.Save();
            }
            catch (Exception ex)
            {
                Log.Error("Couldn't save the undo journal: " + ex.Message);
            }
        }

        void RefreshWindows(ChangePlan plan, RunResult result)
        {
            bool mouse = false, menu = false, theme = false, explorer = false;
            List<Tweak> touched = new List<Tweak>(plan.Apply);
            touched.AddRange(plan.Revert);
            foreach (Tweak t in touched)
            {
                if (t.RefreshHint == "mouse") mouse = true;
                if (t.RefreshHint == "menu") menu = true;
                if (t.RefreshHint == "theme") theme = true;
                if (t.RestartsExplorer) explorer = true;
            }
            try
            {
                if (mouse)
                {
                    int[] p = new int[3];
                    p[0] = (int)Reg.ToUInt32(Reg.ReadOrNull("HKCU", @"Control Panel\Mouse", "MouseThreshold1") ?? "6");
                    p[1] = (int)Reg.ToUInt32(Reg.ReadOrNull("HKCU", @"Control Panel\Mouse", "MouseThreshold2") ?? "10");
                    p[2] = (int)Reg.ToUInt32(Reg.ReadOrNull("HKCU", @"Control Panel\Mouse", "MouseSpeed") ?? "1");
                    Native.SystemParametersInfo(Native.SPI_SETMOUSE, 0, p, Native.SPIF_SENDCHANGE);
                }
                if (menu)
                {
                    int delay = (int)Reg.ToUInt32(Reg.ReadOrNull("HKCU", @"Control Panel\Desktop", "MenuShowDelay") ?? "400");
                    Native.SystemParametersInfo(Native.SPI_SETMENUSHOWDELAY, delay, IntPtr.Zero, Native.SPIF_SENDCHANGE);
                }
                if (theme) Native.BroadcastSettingChange("ImmersiveColorSet");
                Native.BroadcastSettingChange("Policy");
                Native.BroadcastSettingChange("TraySettings");
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't notify Windows about the changes: " + ex.Message);
            }

            if (explorer)
            {
                if (settings.RestartExplorer)
                {
                    RestartExplorer();
                    result.ExplorerRestarted = true;
                }
                else
                {
                    result.NeedsRestart = true;
                }
            }
        }

        static bool ExplorerRunning(int session)
        {
            foreach (Process p in Process.GetProcessesByName("explorer"))
            {
                bool mine = false;
                try { mine = p.SessionId == session; } catch { }
                p.Dispose();
                if (mine) return true;
            }
            return false;
        }

        public static void RestartExplorer()
        {
            Log.Info("Restarting Explorer so taskbar and menu changes show up");
            int session = Process.GetCurrentProcess().SessionId;
            foreach (Process p in Process.GetProcessesByName("explorer"))
            {
                try
                {
                    // Only this desktop session; other signed-in users keep their Explorer.
                    if (p.SessionId != session) continue;
                    p.Kill();
                    p.WaitForExit(5000);
                }
                catch
                {
                }
                finally
                {
                    p.Dispose();
                }
            }
            // Windows normally brings the shell back by itself within a second or two.
            for (int i = 0; i < 30; i++)
            {
                Thread.Sleep(200);
                if (ExplorerRunning(session)) return;
            }
            string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            if (!TaskSched.RunUnelevated(exe))
            {
                try { Process.Start(exe); } catch (Exception ex) { Log.Error("Couldn't restart Explorer: " + ex.Message); }
            }
        }
    }

    public sealed class RestorePointInfo
    {
        public string Description;
        public DateTime Created;
    }

    public static class SysRestore
    {
        /// <summary>Creates a restore point. Returns null on success or a short explanation.</summary>
        public static string Create(string description)
        {
            string script =
                "$k='HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore'\n" +
                "$old=$null; try { $old=(Get-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -ErrorAction Stop).SystemRestorePointCreationFrequency } catch {}\n" +
                "try {\n" +
                "  try { Enable-ComputerRestore -Drive \"$env:SystemDrive\\\" -ErrorAction Stop } catch {}\n" +
                "  New-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value 0 -PropertyType DWord -Force | Out-Null\n" +
                "  Checkpoint-Computer -Description " + Proc.Quote(description) + " -RestorePointType MODIFY_SETTINGS -ErrorAction Stop -WarningAction SilentlyContinue\n" +
                "  'RESULT|OK'\n" +
                "} catch { 'RESULT|' + ($_.Exception.Message -replace \"[`r`n]+\", ' ') }\n" +
                "finally {\n" +
                "  if ($null -eq $old) { Remove-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue }\n" +
                "  else { Set-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value $old }\n" +
                "}\n";
            ProcResult r = Proc.PowerShell(script, 240000, null);
            if (r.TimedOut) return "Windows took too long to create the restore point.";
            foreach (string raw in r.Output.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("RESULT|"))
                {
                    string msg = line.Substring(7);
                    return msg == "OK" ? null : msg;
                }
            }
            string err = r.Error.Trim();
            return err.Length > 0 ? err : "Windows didn't report whether the restore point was created.";
        }

        /// <summary>Newest first. Returns null when the list can't be read (for example without admin rights).</summary>
        public static List<RestorePointInfo> List()
        {
            try
            {
                List<RestorePointInfo> r = new List<RestorePointInfo>();
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\default"), new ObjectQuery("SELECT Description, CreationTime FROM SystemRestore")))
                {
                    foreach (ManagementObject o in s.Get())
                    {
                        RestorePointInfo p = new RestorePointInfo();
                        p.Description = Convert.ToString(o["Description"]);
                        try { p.Created = ManagementDateTimeConverter.ToDateTime(Convert.ToString(o["CreationTime"])); }
                        catch { p.Created = DateTime.MinValue; }
                        r.Add(p);
                    }
                }
                r.Sort(delegate(RestorePointInfo a, RestorePointInfo b) { return b.Created.CompareTo(a.Created); });
                return r;
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't list restore points: " + ex.Message);
                return null;
            }
        }
    }
}
