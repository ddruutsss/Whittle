using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Whittle.Core;
using Whittle.Shell;

[assembly: AssemblyTitle("Whittle")]
[assembly: AssemblyProduct("Whittle")]
[assembly: AssemblyDescription("Whittle - Windows 11 debloater and tuner")]
[assembly: AssemblyCompany("Whittle")]
[assembly: AssemblyCopyright("Copyright © 2026")]
// The version lives in one place: Brand.Version.
[assembly: AssemblyVersion(Whittle.Brand.Version + ".0")]
[assembly: AssemblyFileVersion(Whittle.Brand.Version + ".0")]
[assembly: AssemblyInformationalVersion(Whittle.Brand.Version)]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: System.Runtime.InteropServices.ComVisible(false)]

namespace Whittle
{
    public static class Program
    {
        static Mutex single;

        [STAThread]
        public static int Main(string[] args)
        {
#if DEV
            if (args.Length >= 2 && args[0] == "--selftest") return DevTools.SelfTest(args[1]);
            string shotDir = args.Length >= 2 && args[0] == "--shot" ? args[1] : null;
            if (shotDir != null)
            {
                AppPaths.DataDir = Path.Combine(Path.GetTempPath(), "WhittleShot");
                SysInfo.MachineNameOverride = "DESKTOP-PC";
            }
            int dataArg = Array.IndexOf(args, "--data");
            if (dataArg >= 0 && dataArg + 1 < args.Length) AppPaths.DataDir = args[dataArg + 1];
#else
            string shotDir = null;
#endif
            bool created;
            single = new Mutex(true, "Local\\Whittle-SingleInstance-5b1e7a52", out created);
            if (!created && shotDir == null)
            {
                BringExistingToFront();
                return 0;
            }

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            // The window starts async work while it's being built, before Application.Run installs this.
            // Without it, code after an await would resume on a thread-pool thread and touch the UI.
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Whittle.Theme.xaml"))
                {
                    app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(s));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Whittle couldn't load its interface: " + ex.Message, Brand.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            app.DispatcherUnhandledException += OnUiException;
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Log.Error("Fatal: " + e.ExceptionObject);
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += delegate(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
            {
                Log.Error("Background task failed: " + e.Exception);
                e.SetObserved();
            };

            bool admin = SysInfo.IsAdmin;
            Log.Info(Brand.Name + " " + Brand.Version + " started on " + SysInfo.WindowsName + " " + SysInfo.VersionLine + (admin ? "" : " (not elevated)"));

            Journal journal = Journal.Load(AppPaths.Journal);
            AppSettings settings = AppSettings.Load();
            AppState.Current = new AppState(journal, settings);

            bool readOnly = !admin;
#if DEV
            if (shotDir != null) readOnly = false;
#endif
            MainWindow window = new MainWindow(AppState.Current, readOnly);
            window.Loaded += delegate
            {
                window.RefreshStates();
                window.ScanApps();
                window.CheckAccount();
            };
#if DEV
            if (shotDir != null) DevTools.AttachScreenshots(window, shotDir);
#endif
            app.Run(window);
            GC.KeepAlive(single);
            return 0;
        }

        static void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error("Unexpected error: " + e.Exception);
            e.Handled = true;
            MessageBox.Show("Something unexpected happened:\n\n" + e.Exception.Message +
                "\n\nThe details were written to the log in " + AppPaths.Logs + ". Whittle will keep running.",
                Brand.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        static void BringExistingToFront()
        {
            try
            {
                Process me = Process.GetCurrentProcess();
                foreach (Process p in Process.GetProcessesByName(me.ProcessName))
                {
                    if (p.Id == me.Id || p.MainWindowHandle == IntPtr.Zero) continue;
                    if (Native.IsIconic(p.MainWindowHandle)) Native.ShowWindow(p.MainWindowHandle, 9);
                    Native.SetForegroundWindow(p.MainWindowHandle);
                    break;
                }
            }
            catch
            {
            }
        }
    }
}
