using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Whittle.Core
{
    public sealed class ProcResult
    {
        public int ExitCode = -1;
        public string Output = "";
        public string Error = "";
        public bool TimedOut;
        public bool Ok { get { return !TimedOut && ExitCode == 0; } }
    }

    /// <summary>Runs console tools (powercfg, PowerShell) hidden, with a timeout.</summary>
    public static class Proc
    {
        public static string System32
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.System); }
        }

        public static ProcResult Run(string file, string args, int timeoutMs)
        {
            return Run(file, args, timeoutMs, null, Encoding.Default);
        }

        public static ProcResult Run(string file, string args, int timeoutMs, Action<string> onLine, Encoding outputEncoding)
        {
            ProcResult r = new ProcResult();
            ProcessStartInfo psi = new ProcessStartInfo(file, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = outputEncoding;
            psi.StandardErrorEncoding = outputEncoding;
            psi.WorkingDirectory = System32;

            StringBuilder outSb = new StringBuilder();
            StringBuilder errSb = new StringBuilder();
            using (Process p = new Process())
            using (ManualResetEvent outDone = new ManualResetEvent(false))
            using (ManualResetEvent errDone = new ManualResetEvent(false))
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) { outDone.Set(); return; }
                    lock (outSb) outSb.AppendLine(e.Data);
                    if (onLine != null) { try { onLine(e.Data); } catch { } }
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) { errDone.Set(); return; }
                    lock (errSb) errSb.AppendLine(e.Data);
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutMs))
                {
                    r.TimedOut = true;
                    try { p.Kill(); } catch { }
                }
                else
                {
                    outDone.WaitOne(3000);
                    errDone.WaitOne(3000);
                    r.ExitCode = p.ExitCode;
                }
            }
            lock (outSb) r.Output = outSb.ToString();
            lock (errSb) r.Error = errSb.ToString();
            return r;
        }

        public static ProcResult PowerCfg(string args)
        {
            return Run(Path.Combine(System32, "powercfg.exe"), args, 30000);
        }

        /// <summary>
        /// Runs a Windows PowerShell 5.1 script passed as -EncodedCommand, so no quoting issues.
        /// Output is UTF-8. Lines are streamed to <paramref name="onLine"/> as they arrive.
        /// </summary>
        public static ProcResult PowerShell(string script, int timeoutMs, Action<string> onLine)
        {
            string prefix =
                "$ProgressPreference='SilentlyContinue';" +
                "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8;" +
                "$OutputEncoding=[System.Text.Encoding]::UTF8;\n";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(prefix + script));
            string exe = Path.Combine(System32, @"WindowsPowerShell\v1.0\powershell.exe");
            return Run(exe, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, timeoutMs, onLine, new UTF8Encoding(false));
        }

        public static string Quote(string s)
        {
            return "'" + (s ?? "").Replace("'", "''") + "'";
        }
    }

    /// <summary>Task Scheduler access through its COM API (works the same on every Windows language).</summary>
    public static class TaskSched
    {
        public static dynamic Connect()
        {
            Type t = Type.GetTypeFromProgID("Schedule.Service");
            dynamic svc = Activator.CreateInstance(t);
            svc.Connect();
            return svc;
        }

        /// <summary>Returns the task, or null when it doesn't exist.</summary>
        public static dynamic GetTask(string folder, string name)
        {
            try
            {
                dynamic svc = Connect();
                dynamic f = svc.GetFolder(folder);
                return f.GetTask(name);
            }
            catch (COMException ex)
            {
                // 0x80070002 file not found, 0x80070003 path not found
                if (ex.ErrorCode == unchecked((int)0x80070002) || ex.ErrorCode == unchecked((int)0x80070003)) return null;
                throw;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// Starts a program as the signed-in user without admin rights, by registering a
        /// one-off task with an interactive token. Used to bring Explorer back if needed.
        /// </summary>
        public static bool RunUnelevated(string exe)
        {
            try
            {
                dynamic svc = Connect();
                dynamic root = svc.GetFolder("\\");
                dynamic def = svc.NewTask(0);
                def.RegistrationInfo.Description = "Whittle: restart Explorer";
                def.Principal.LogonType = 3; // TASK_LOGON_INTERACTIVE_TOKEN
                def.Principal.RunLevel = 0;  // TASK_RUNLEVEL_LUA
                def.Settings.Enabled = true;
                def.Settings.AllowDemandStart = true;
                def.Settings.DisallowStartIfOnBatteries = false;
                def.Settings.StopIfGoingOnBatteries = false;
                dynamic action = def.Actions.Create(0); // TASK_ACTION_EXEC
                action.Path = exe;
                string name = "Whittle-Explorer-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                dynamic task = root.RegisterTaskDefinition(name, def, 6, null, null, 3, null);
                task.Run(null);
                Thread.Sleep(1500);
                try { root.DeleteTask(name, 0); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not start " + exe + " through Task Scheduler: " + ex.Message);
                return false;
            }
        }
    }
}
