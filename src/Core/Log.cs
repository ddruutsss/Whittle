using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Whittle.Core
{
    public enum LogLevel { Info, Success, Warning, Error }

    public static class AppPaths
    {
        static string dataDir;

        /// <summary>%LocalAppData%\Whittle — journal, settings, configs and logs live here.</summary>
        public static string DataDir
        {
            get
            {
                if (dataDir == null)
                {
                    dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Whittle");
                }
                return dataDir;
            }
            set { dataDir = value; }
        }

        public static string Journal { get { return Path.Combine(DataDir, "journal.json"); } }
        public static string Settings { get { return Path.Combine(DataDir, "settings.json"); } }
        public static string Configs { get { return Path.Combine(DataDir, "Configs"); } }
        public static string Logs { get { return Path.Combine(DataDir, "Logs"); } }
    }

    /// <summary>Daily log file plus an event the UI listens to while changes are applied.</summary>
    public static class Log
    {
        static readonly object gate = new object();
        public static event Action<string, LogLevel> Written;

        public static string CurrentFile
        {
            get { return Path.Combine(AppPaths.Logs, "whittle-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log"); }
        }

        public static void Info(string message) { Write(message, LogLevel.Info); }
        public static void Ok(string message) { Write(message, LogLevel.Success); }
        public static void Warn(string message) { Write(message, LogLevel.Warning); }
        public static void Error(string message) { Write(message, LogLevel.Error); }

        public static void Write(string message, LogLevel level)
        {
            string tag = level == LogLevel.Error ? "ERROR" : level == LogLevel.Warning ? "WARN " : level == LogLevel.Success ? "OK   " : "INFO ";
            string line = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + tag + "  " + message;
            lock (gate)
            {
                try
                {
                    Directory.CreateDirectory(AppPaths.Logs);
                    File.AppendAllText(CurrentFile, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Logging must never break the app.
                }
            }
            Action<string, LogLevel> h = Written;
            if (h != null)
            {
                try { h(message, level); } catch { }
            }
        }
    }
}
