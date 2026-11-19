using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;

namespace Whittle.Core
{
    public sealed class CleanupTarget
    {
        public string Id;
        public string Title;
        public string Description;
        public bool DefaultOn = true;
        public long Size = -1;
        internal string[] Folders = new string[0];
        internal string[] Files = new string[0];
        internal TimeSpan MinAge = TimeSpan.Zero;
    }

    /// <summary>Finds and deletes files Windows can do without. Never follows junctions or symlinks.</summary>
    public static class Cleaner
    {
        static string Win { get { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); } }
        static string Local { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
        static string ProgramData { get { return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); } }

        public static List<CleanupTarget> Targets()
        {
            List<CleanupTarget> list = new List<CleanupTarget>();

            CleanupTarget temp = new CleanupTarget();
            temp.Id = "temp";
            temp.Title = "Temporary files";
            temp.Description = "Leftovers from installers and apps in your Temp folders. Files used in the last 24 hours are kept.";
            temp.Folders = new[] { Path.GetTempPath(), Path.Combine(Win, "Temp") };
            temp.MinAge = TimeSpan.FromHours(24);
            list.Add(temp);

            CleanupTarget wu = new CleanupTarget();
            wu.Id = "update-cache";
            wu.Title = "Windows Update downloads";
            wu.Description = "Update files that were already installed. Windows downloads them again if it ever needs them.";
            wu.Folders = new[] { Path.Combine(Win, @"SoftwareDistribution\Download") };
            list.Add(wu);

            CleanupTarget dlo = new CleanupTarget();
            dlo.Id = "delivery-cache";
            dlo.Title = "Delivery Optimization cache";
            dlo.Description = "Copies of updates kept to share with other PCs.";
            dlo.Folders = new[] { Path.Combine(Win, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache") };
            list.Add(dlo);

            CleanupTarget dumps = new CleanupTarget();
            dumps.Id = "crash-dumps";
            dumps.Title = "Crash dumps and error reports";
            dumps.Description = "Memory dumps and reports written when apps or Windows crashed.";
            dumps.Folders = new[]
            {
                Path.Combine(Win, "Minidump"),
                Path.Combine(Local, "CrashDumps"),
                Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportArchive"),
                Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportQueue"),
                Path.Combine(Local, @"Microsoft\Windows\WER\ReportArchive"),
                Path.Combine(Local, @"Microsoft\Windows\WER\ReportQueue")
            };
            dumps.Files = new[] { Path.Combine(Win, "MEMORY.DMP") };
            list.Add(dumps);

            CleanupTarget bin = new CleanupTarget();
            bin.Id = "recycle-bin";
            bin.Title = "Recycle Bin";
            bin.Description = "Files you've already deleted, on every drive.";
            list.Add(bin);

            CleanupTarget shaders = new CleanupTarget();
            shaders.Id = "shader-cache";
            shaders.Title = "GPU shader caches";
            shaders.Description = "Compiled shaders from DirectX, NVIDIA, AMD and Intel. Worth clearing after a driver update; games rebuild them, which can cause brief stutter the first time you play.";
            shaders.DefaultOn = false;
            string localLow = Path.Combine(Path.GetDirectoryName(Local.TrimEnd('\\')), "LocalLow");
            shaders.Folders = new[]
            {
                Path.Combine(Local, "D3DSCache"),
                Path.Combine(Local, @"NVIDIA\DXCache"),
                Path.Combine(Local, @"NVIDIA\GLCache"),
                Path.Combine(localLow, @"NVIDIA\PerDriverVersion\DXCache"),
                Path.Combine(localLow, @"NVIDIA\PerDriverVersion\GLCache"),
                Path.Combine(Local, @"AMD\DxCache"),
                Path.Combine(Local, @"AMD\DxcCache"),
                Path.Combine(Local, @"AMD\VkCache"),
                Path.Combine(Local, @"AMD\GLCache"),
                Path.Combine(localLow, @"Intel\ShaderCache")
            };
            list.Add(shaders);

            return list;
        }

        public static void Measure(CleanupTarget t)
        {
            if (t.Id == "recycle-bin")
            {
                try
                {
                    Native.SHQUERYRBINFO info = new Native.SHQUERYRBINFO();
                    info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.SHQUERYRBINFO));
                    t.Size = Native.SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : -1;
                }
                catch
                {
                    t.Size = -1;
                }
                return;
            }
            long total = 0;
            bool any = false;
            DateTime cutoff = DateTime.Now - t.MinAge;
            foreach (string f in t.Folders)
            {
                long s = FolderSize(f, cutoff);
                if (s >= 0) { total += s; any = true; }
            }
            foreach (string f in t.Files)
            {
                try
                {
                    FileInfo fi = new FileInfo(f);
                    if (fi.Exists) { total += fi.Length; }
                    any = true;
                }
                catch
                {
                }
            }
            t.Size = any ? total : -1;
        }

        static long FolderSize(string root, DateTime cutoff)
        {
            if (!Directory.Exists(root)) return 0;
            long total = 0;
            Stack<DirectoryInfo> stack = new Stack<DirectoryInfo>();
            stack.Push(new DirectoryInfo(root));
            bool readAnything = false;
            while (stack.Count > 0)
            {
                DirectoryInfo d = stack.Pop();
                FileSystemInfo[] entries;
                try { entries = d.GetFileSystemInfos(); readAnything = true; }
                catch { continue; }
                foreach (FileSystemInfo e in entries)
                {
                    if ((e.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    DirectoryInfo sub = e as DirectoryInfo;
                    if (sub != null) { stack.Push(sub); continue; }
                    FileInfo fi = (FileInfo)e;
                    if (fi.LastWriteTime > cutoff) continue;
                    total += fi.Length;
                }
            }
            return readAnything ? total : -1;
        }

        /// <summary>Deletes what the target covers and returns the number of bytes freed.</summary>
        public static long Clean(CleanupTarget t)
        {
            long before = t.Size < 0 ? 0 : t.Size;
            if (t.Id == "recycle-bin")
            {
                Native.SHEmptyRecycleBin(IntPtr.Zero, null, Native.SHERB_NOCONFIRMATION | Native.SHERB_NOPROGRESSUI | Native.SHERB_NOSOUND);
                Measure(t);
                return Math.Max(0, before - Math.Max(0, t.Size));
            }

            bool restartUpdate = false;
            if (t.Id == "update-cache") restartUpdate = StopService("wuauserv");
            if (t.Id == "delivery-cache")
            {
                Proc.PowerShell("Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue", 120000, null);
            }

            DateTime cutoff = DateTime.Now - t.MinAge;
            foreach (string f in t.Folders) DeleteContents(f, cutoff);
            foreach (string f in t.Files)
            {
                try
                {
                    if (File.Exists(f))
                    {
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                    }
                }
                catch
                {
                }
            }

            if (restartUpdate) StartService("wuauserv");
            Measure(t);
            long freed = Math.Max(0, before - Math.Max(0, t.Size));
            Log.Info("Cleanup: " + t.Title + " freed " + FormatBytes(freed));
            return freed;
        }

        static void DeleteContents(string root, DateTime cutoff)
        {
            if (!Directory.Exists(root)) return;
            List<DirectoryInfo> dirs = new List<DirectoryInfo>();
            Stack<DirectoryInfo> stack = new Stack<DirectoryInfo>();
            stack.Push(new DirectoryInfo(root));
            while (stack.Count > 0)
            {
                DirectoryInfo d = stack.Pop();
                FileSystemInfo[] entries;
                try { entries = d.GetFileSystemInfos(); }
                catch { continue; }
                foreach (FileSystemInfo e in entries)
                {
                    if ((e.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        // Remove the link itself, never what it points to.
                        try
                        {
                            if (e is DirectoryInfo) Directory.Delete(e.FullName, false);
                            else File.Delete(e.FullName);
                        }
                        catch
                        {
                        }
                        continue;
                    }
                    DirectoryInfo sub = e as DirectoryInfo;
                    if (sub != null)
                    {
                        dirs.Add(sub);
                        stack.Push(sub);
                        continue;
                    }
                    FileInfo fi = (FileInfo)e;
                    if (fi.LastWriteTime > cutoff) continue;
                    try
                    {
                        if ((fi.Attributes & FileAttributes.ReadOnly) != 0) fi.Attributes = FileAttributes.Normal;
                        fi.Delete();
                    }
                    catch
                    {
                        // In use or protected; leave it.
                    }
                }
            }
            for (int i = dirs.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (dirs[i].Exists && dirs[i].GetFileSystemInfos().Length == 0) dirs[i].Delete(false);
                }
                catch
                {
                }
            }
        }

        static bool StopService(string name)
        {
            try
            {
                using (ServiceController sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't stop " + name + ": " + ex.Message);
            }
            return false;
        }

        static void StartService(string name)
        {
            try
            {
                using (ServiceController sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped) sc.Start();
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't restart " + name + ": " + ex.Message);
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "—";
            double b = bytes;
            string[] units = { "bytes", "KB", "MB", "GB", "TB" };
            int u = 0;
            while (b >= 1024 && u < units.Length - 1) { b /= 1024; u++; }
            if (u == 0) return bytes + " bytes";
            return b.ToString(b >= 100 ? "0" : b >= 10 ? "0.0" : "0.00", System.Globalization.CultureInfo.CurrentCulture) + " " + units[u];
        }
    }
}
