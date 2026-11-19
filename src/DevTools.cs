#if DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Whittle.Core;
using Whittle.Pages;
using Whittle.Shell;

namespace Whittle
{
    /// <summary>Test-build only: screenshots of every page and a sandboxed self-test of the tweak engine.</summary>
    public static class DevTools
    {
        // ------------------------------------------------------------------ Screenshots

        public static void AttachScreenshots(MainWindow w, string dir)
        {
            Directory.CreateDirectory(dir);
            w.ShowInTaskbar = false;
            w.ShowActivated = false;
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Width = 1280;
            w.Height = 820;
            w.Left = SystemParameters.VirtualScreenLeft - 4000;
            w.Top = 40;
            bool started = false;
            w.ContentRendered += async delegate
            {
                if (started) return;
                started = true;
                await Shoot(w, dir);
            };
        }

        static async Task Shoot(MainWindow w, string dir)
        {
            for (int i = 0; i < 240 && (!AppState.Current.StatesReady || AppState.Current.Apps == null); i++) await Task.Delay(250);
            string[] ids = { "home", "tweaks", "apps", "startup", "cleanup", "profiles", "undo", "settings" };
            foreach (string id in ids)
            {
                w.Navigate(id);
                await Task.Delay(id == "cleanup" ? 9000 : id == "home" ? 2500 : 1500);
                Save(w, Path.Combine(dir, id + ".png"));
                if (id == "tweaks" || id == "apps") await ScrollShots(w, id, dir, 8);
            }
            w.Navigate("tweaks");
            await Task.Delay(800);
            Button more = FindButton((DependencyObject)w.Content, "What it changes");
            if (more != null) more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(500);
            Save(w, Path.Combine(dir, "tweaks-details.png"));
            // The largest profile gives the review dialog something to show on an already-tuned PC.
            HomePage.UsePreset(w, Presets.All[Presets.All.Count - 1]);
            await Task.Delay(1500);
            Save(w, Path.Combine(dir, "tweaks-pending.png"));
            w.ReviewAndApply();
            await Task.Delay(900);
            Save(w, Path.Combine(dir, "review.png"));
            w.PopDialog();
            w.DemoProgress(false);
            await Task.Delay(900);
            Save(w, Path.Combine(dir, "progress.png"));
            w.PopDialog();
            w.DemoProgress(true);
            await Task.Delay(900);
            Save(w, Path.Combine(dir, "progress-done.png"));
            w.PopDialog();
            w.Navigate("profiles");
            await Task.Delay(600);
            w.Confirm("Delete “My setup”?", "This removes the saved config. It doesn't change anything on your PC.", "Delete", true);
            await Task.Delay(700);
            Save(w, Path.Combine(dir, "confirm.png"));
            w.PopDialog();
            w.Toast("Gaming profile selected. Look over the changes, then choose Review and apply.", "");
            await Task.Delay(800);
            Save(w, Path.Combine(dir, "toast.png"));
            AppState.Current.Discard();
            await Task.Delay(300);
            w.Close();
        }

        static void Save(Window w, string path)
        {
            w.UpdateLayout();
            int width = (int)Math.Ceiling(w.ActualWidth);
            int height = (int)Math.Ceiling(w.ActualHeight);
            RenderTargetBitmap raw = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            raw.Render(w);

            // Clip to Windows 11's 8 px window corners and draw the 1 px window border,
            // so the image looks like the real window on a transparent background.
            Rect r = new Rect(0, 0, width, height);
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.PushClip(new RectangleGeometry(r, 8, 8));
                dc.DrawImage(raw, r);
                dc.Pop();
                Pen border = new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x41)), 1);
                dc.DrawRoundedRectangle(null, border, new Rect(0.5, 0.5, width - 1, height - 1), 8, 8);
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(path)) enc.Save(fs);
        }

        static async Task ScrollShots(MainWindow w, string id, string dir, int max)
        {
            ContentControl host = FindHost((DependencyObject)w.Content);
            if (host == null) return;
            ScrollViewer sv = host.Content as ScrollViewer;
            if (sv == null) return;
            double step = Math.Max(200, sv.ViewportHeight - 80);
            for (int i = 1; i <= max; i++)
            {
                double offset = i * step;
                if (offset >= sv.ExtentHeight - 40) break;
                sv.ScrollToVerticalOffset(offset);
                await Task.Delay(300);
                Save(w, Path.Combine(dir, id + "-p" + i + ".png"));
            }
            sv.ScrollToVerticalOffset(0);
        }

        static Button FindButton(DependencyObject d, string tooltip)
        {
            Button b = d as Button;
            if (b != null && Equals(b.ToolTip, tooltip)) return b;
            int n = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < n; i++)
            {
                Button r = FindButton(VisualTreeHelper.GetChild(d, i), tooltip);
                if (r != null) return r;
            }
            return null;
        }

        static ContentControl FindHost(DependencyObject d)
        {
            ContentControl cc = d as ContentControl;
            if (cc != null && !(d is Button) && cc.Content is ScrollViewer) return cc;
            int n = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < n; i++)
            {
                ContentControl r = FindHost(VisualTreeHelper.GetChild(d, i));
                if (r != null) return r;
            }
            return null;
        }

        static void Render(FrameworkElement e, Brush background, string path)
        {
            int width = (int)Math.Ceiling(e.ActualWidth + e.Margin.Left + e.Margin.Right);
            int height = (int)Math.Ceiling(e.ActualHeight + e.Margin.Top + e.Margin.Bottom);
            if (width <= 0 || height <= 0) return;
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(background, null, new Rect(0, 0, width, height));
                // Absolute viewbox: the element's own layout box, not the bounds of whatever it draws
                // (shadows and transforms can reach past its edges and would shift the image).
                VisualBrush vb = new VisualBrush(e);
                vb.ViewboxUnits = BrushMappingMode.Absolute;
                vb.Viewbox = new Rect(0, 0, e.ActualWidth, e.ActualHeight);
                vb.Stretch = Stretch.Fill;
                dc.DrawRectangle(vb, null, new Rect(e.Margin.Left, e.Margin.Top, e.ActualWidth, e.ActualHeight));
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(path)) enc.Save(fs);
        }

        // ------------------------------------------------------------------ Self-test

        static StringBuilder report;
        static int failures;
        static int checks;

        static void Check(bool ok, string message)
        {
            checks++;
            if (!ok)
            {
                failures++;
                report.AppendLine("FAIL  " + message);
            }
        }

        static void Info(string message)
        {
            report.AppendLine("      " + message);
        }

        public static int SelfTest(string outFile)
        {
            report = new StringBuilder();
            failures = 0;
            checks = 0;
            string tempRoot = Path.Combine(Path.GetTempPath(), "WhittleSelfTest-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            AppPaths.DataDir = tempRoot;
            report.AppendLine("Whittle self-test  " + DateTime.Now.ToString("s"));
            report.AppendLine("Windows: " + SysInfo.WindowsName + " " + SysInfo.VersionLine + "   admin: " + SysInfo.IsAdmin);
            report.AppendLine();

            try { CatalogChecks(); }
            catch (Exception ex) { Check(false, "Catalog checks crashed: " + ex); }

            report.AppendLine();
            report.AppendLine("== Sandbox round trips (HKCU\\Software\\WhittleSelfTest) ==");
            Reg.SandboxRoot = @"Software\WhittleSelfTest";
            try
            {
                int tested = 0;
                foreach (Tweak t in Catalog.All)
                {
                    bool sandboxable = true;
                    foreach (TweakAction a in t.Actions) if (!a.Sandboxable) sandboxable = false;
                    if (!sandboxable) { Info("skip (needs the real system): " + t.Id); continue; }
                    RoundTripMissing(t, tempRoot);
                    RoundTripExisting(t, tempRoot);
                    RevertWithoutJournal(t, tempRoot);
                    tested++;
                }
                Info(tested + " tweaks tested in the sandbox");
            }
            catch (Exception ex)
            {
                Check(false, "Sandbox tests crashed: " + ex);
            }
            finally
            {
                try { Reg.DeleteSandbox(); } catch { }
                Reg.SandboxRoot = null;
            }
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\WhittleSelfTest"))
            {
                Check(k == null, "sandbox key was not cleaned up");
            }

            report.AppendLine();
            report.AppendLine("== Journal and config files ==");
            try { JournalRoundTrip(tempRoot); ConfigRoundTrip(); }
            catch (Exception ex) { Check(false, "File round trips crashed: " + ex); }

            report.AppendLine();
            report.AppendLine("== Read-only checks on this PC ==");
            try { ReadOnlyChecks(tempRoot); }
            catch (Exception ex) { Check(false, "Read-only checks crashed: " + ex); }

            report.AppendLine();
            report.AppendLine(checks + " checks, " + failures + " failed");
            try { Directory.Delete(tempRoot, true); } catch { }
            File.WriteAllText(outFile, report.ToString(), Encoding.UTF8);
            return failures;
        }

        static void CatalogChecks()
        {
            report.AppendLine("== Catalog ==");
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Tweak t in Catalog.All)
            {
                Check(ids.Add(t.Id), "duplicate tweak id " + t.Id);
                Check(t.Actions.Count > 0, t.Id + " has no actions");
                Check(Array.IndexOf(Categories.Order, t.Category) >= 0, t.Id + " has unknown category " + t.Category);
                Check(t.Title.Length > 0 && t.Description.Length > 0, t.Id + " is missing text");
                foreach (TweakAction a in t.Actions)
                {
                    string owner;
                    Check(!keys.TryGetValue(a.Key, out owner), "action " + a.Key + " is used by both " + owner + " and " + t.Id);
                    keys[a.Key] = t.Id;
                    RegValueAction r = a as RegValueAction;
                    if (r != null && r.RevertTo != null)
                    {
                        Check(!Reg.LooseEquals(r.RevertTo, r.Target), t.Id + ": revert value equals target for " + r.Name);
                    }
                }
            }
            foreach (Preset p in Presets.All)
            {
                foreach (string id in p.Tweaks) Check(Catalog.Find(id) != null, "preset " + p.Id + " references unknown tweak " + id);
                HashSet<string> seen = new HashSet<string>();
                foreach (string id in p.Tweaks) Check(seen.Add(id), "preset " + p.Id + " lists " + id + " twice");
                foreach (string id in p.Tweaks)
                {
                    Tweak t = Catalog.Find(id);
                    Check(t == null || t.Risk != Risk.Advanced, "preset " + p.Id + " includes advanced tweak " + id);
                }
                Info("preset " + p.Id + ": " + p.Tweaks.Length + " tweaks");
            }
            HashSet<string> appIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AppDef d in AppCatalog.All)
                foreach (string id in d.Ids) Check(appIds.Add(id), "duplicate app id " + id);
            Info(Catalog.All.Count + " tweaks, " + keys.Count + " distinct actions, " + AppCatalog.All.Count + " known apps");
        }

        /// <summary>Empties the sandbox and recreates the hive roots, which always exist in a real registry.</summary>
        static void ResetSandbox()
        {
            Reg.DeleteSandbox();
            foreach (string hive in new[] { "HKCU", "HKLM", "HKU" })
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Reg.SandboxRoot + "\\" + hive)) { }
            }
        }

        static void SeedServices(Tweak t)
        {
            foreach (TweakAction a in t.Actions)
            {
                ServiceStartAction s = a as ServiceStartAction;
                if (s != null) Reg.Write(s.Hive, s.Path, "Start", s.RevertTo, RegistryValueKind.DWord);
            }
        }

        static string Snapshot(Tweak t)
        {
            StringBuilder sb = new StringBuilder();
            foreach (TweakAction a in t.Actions)
            {
                RegValueAction r = a as RegValueAction;
                if (r != null)
                {
                    object v; RegistryValueKind k;
                    if (Reg.TryRead(r.Hive, r.Path, r.Name, out v, out k)) sb.Append(r.Key).Append('=').Append(k).Append(':').Append(Convert.ToString(RegSnapshot.Encode(k, v))).Append(';');
                    else sb.Append(r.Key).Append("=<missing>;");
                }
                else if (a is DxSettingAction)
                {
                    object v = Reg.ReadOrNull("HKCU", @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings");
                    sb.Append("dx=").Append(v == null ? "<missing>" : Convert.ToString(v)).Append(';');
                }
            }
            return sb.ToString();
        }

        static string Tree()
        {
            StringBuilder sb = new StringBuilder();
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Reg.SandboxRoot))
            {
                if (k != null) Walk(k, "", sb);
            }
            return sb.ToString();
        }

        static void Walk(RegistryKey k, string path, StringBuilder sb)
        {
            foreach (string v in k.GetValueNames()) sb.Append(path).Append('|').Append(v).Append('\n');
            foreach (string s in k.GetSubKeyNames())
            {
                sb.Append(path + "\\" + s).Append('\n');
                using (RegistryKey c = k.OpenSubKey(s)) Walk(c, path + "\\" + s, sb);
            }
        }

        static Journal TempJournal(string root)
        {
            return new Journal(Path.Combine(root, "j-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".json"));
        }

        static void ApplyAll(Tweak t, Journal j)
        {
            foreach (TweakAction a in t.Actions) a.Apply(j);
        }

        static void RevertAll(Tweak t, Journal j)
        {
            for (int i = t.Actions.Count - 1; i >= 0; i--) t.Actions[i].Revert(j);
        }

        static void RoundTripMissing(Tweak t, string root)
        {
            ResetSandbox();
            SeedServices(t);
            string snap0 = Snapshot(t);
            string tree0 = Tree();
            TweakState s0 = t.Detect();
            Journal j = TempJournal(root);
            ApplyAll(t, j);
            TweakState s1 = t.Detect();
            Check(s1 == TweakState.Applied, t.Id + " [from missing] detect after apply = " + s1);
            // Save and reload the journal between apply and revert, like a restart of the app would.
            j.Save();
            Journal j2 = Journal.Load(j.FilePath);
            RevertAll(t, j2);
            Check(t.Detect() == s0, t.Id + " [from missing] state after undo = " + t.Detect() + ", expected " + s0);
            Check(Snapshot(t) == snap0, t.Id + " [from missing] values after undo differ:\n        before " + snap0 + "\n        after  " + Snapshot(t));
            Check(Tree() == tree0, t.Id + " [from missing] left keys or values behind:\n" + Tree());
            Check(j2.OriginalCount == 0, t.Id + " [from missing] journal still holds " + j2.OriginalCount + " originals");
        }

        static void RoundTripExisting(Tweak t, string root)
        {
            ResetSandbox();
            SeedServices(t);
            foreach (TweakAction a in t.Actions)
            {
                RegValueAction r = a as RegValueAction;
                if (r != null && !(r is ServiceStartAction))
                {
                    object different;
                    switch (r.Kind)
                    {
                        case RegistryValueKind.DWord: different = Reg.ToUInt32(r.Target) + 1; break;
                        case RegistryValueKind.Binary:
                            byte[] b = (byte[])((byte[])r.Target).Clone();
                            b[0] = (byte)(b[0] ^ 0xFF);
                            different = b;
                            break;
                        default: different = Convert.ToString(r.Target) + "x"; break;
                    }
                    Reg.Write(r.Hive, r.Path, r.Name, different, r.Kind);
                }
                else if (a is DxSettingAction)
                {
                    Reg.Write("HKCU", @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", "VRROptimizeEnable=0;SwapEffectUpgradeEnable=0;", RegistryValueKind.String);
                }
            }
            string snap0 = Snapshot(t);
            TweakState s0 = t.Detect();
            Check(s0 != TweakState.Applied, t.Id + " [from existing] detects as applied before applying");
            Journal j = TempJournal(root);
            ApplyAll(t, j);
            Check(t.Detect() == TweakState.Applied, t.Id + " [from existing] detect after apply = " + t.Detect());
            foreach (TweakAction a in t.Actions)
            {
                if (a is DxSettingAction)
                {
                    string v = Convert.ToString(Reg.ReadOrNull("HKCU", @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings"));
                    Check(v.Contains("VRROptimizeEnable=0"), t.Id + " lost another DirectX setting: " + v);
                }
            }
            j.Save();
            Journal j2 = Journal.Load(j.FilePath);
            RevertAll(t, j2);
            Check(Snapshot(t) == snap0, t.Id + " [from existing] values after undo differ:\n        before " + snap0 + "\n        after  " + Snapshot(t));
            Check(t.Detect() == s0, t.Id + " [from existing] state after undo = " + t.Detect() + ", expected " + s0);
        }

        static void RevertWithoutJournal(Tweak t, string root)
        {
            ResetSandbox();
            SeedServices(t);
            ApplyAll(t, TempJournal(root));
            RevertAll(t, TempJournal(root));
            foreach (TweakAction a in t.Actions)
            {
                RegValueAction r = a as RegValueAction;
                if (r == null) continue;
                object v; RegistryValueKind k;
                bool exists = Reg.TryRead(r.Hive, r.Path, r.Name, out v, out k);
                if (r.RevertTo == null) Check(!exists, t.Id + " [no journal] " + r.Name + " should be deleted");
                else Check(exists && Reg.ValueEquals(v, k, r.RevertTo, r.Kind), t.Id + " [no journal] " + r.Name + " should be " + RegValueAction.FormatValue(r.RevertTo, r.Kind));
            }
            Check(t.Detect() != TweakState.Applied, t.Id + " [no journal] still detects as applied after turning it off");
        }

        static void JournalRoundTrip(string root)
        {
            Journal j = TempJournal(root);
            j.SetOriginal("dword", RegSnapshot.Capture(true, RegistryValueKind.DWord, unchecked((int)0x80000002), null));
            j.SetOriginal("string", RegSnapshot.Capture(true, RegistryValueKind.String, "héllo \"quoted\"\n", new List<string> { @"Software\A", @"Software\A\B" }));
            j.SetOriginal("binary", RegSnapshot.Capture(true, RegistryValueKind.Binary, new byte[] { 0x9E, 0x1E, 0x07, 0x80, 0, 1 }, null));
            j.SetOriginal("multi", RegSnapshot.Capture(true, RegistryValueKind.MultiString, new[] { "a", "b c" }, null));
            j.SetOriginal("qword", RegSnapshot.Capture(true, RegistryValueKind.QWord, -2L, null));
            j.SetOriginal("missing", RegSnapshot.Capture(false, RegistryValueKind.Unknown, null, new List<string> { @"X\Y" }));
            j.MarkApplied("perf.menu-delay");
            j.AddRemovedApp("Clipchamp.Clipchamp", "Clipchamp.Clipchamp_yxz26nhyzhsrt", "Microsoft Clipchamp");
            j.Save();
            Journal k = Journal.Load(j.FilePath);
            Check(Reg.ToUInt32(Reg.ToRegistryObject(RegSnapshot.Data(k.GetOriginal("dword")), RegistryValueKind.DWord)) == 0x80000002, "journal: DWORD did not survive");
            Check((string)Reg.ToRegistryObject(RegSnapshot.Data(k.GetOriginal("string")), RegistryValueKind.String) == "héllo \"quoted\"\n", "journal: string did not survive");
            Check(RegSnapshot.Created(k.GetOriginal("string")).Count == 2, "journal: created keys did not survive");
            byte[] bin = (byte[])Reg.ToRegistryObject(RegSnapshot.Data(k.GetOriginal("binary")), RegistryValueKind.Binary);
            Check(bin.Length == 6 && bin[0] == 0x9E && bin[5] == 1, "journal: binary did not survive");
            string[] multi = (string[])Reg.ToRegistryObject(RegSnapshot.Data(k.GetOriginal("multi")), RegistryValueKind.MultiString);
            Check(multi.Length == 2 && multi[1] == "b c", "journal: multi-string did not survive");
            Check((long)Reg.ToRegistryObject(RegSnapshot.Data(k.GetOriginal("qword")), RegistryValueKind.QWord) == -2L, "journal: QWORD did not survive");
            Check(!RegSnapshot.Existed(k.GetOriginal("missing")), "journal: 'missing' flag did not survive");
            Check(k.AppliedTweaks().ContainsKey("perf.menu-delay"), "journal: applied tweak did not survive");
            Check(k.RemovedApps().Count == 1 && k.RemovedApps()[0].Family.EndsWith("hyzhsrt"), "journal: removed app did not survive");
            Info("journal round trip done");
        }

        static void ConfigRoundTrip()
        {
            SavedConfig c = new SavedConfig();
            c.Name = "Test: gaming / é";
            c.Tweaks.AddRange(new[] { "game.hags", "perf.power-plan" });
            c.Apps.Add("Clipchamp.Clipchamp");
            string path = ConfigStore.SaveToLibrary(c);
            SavedConfig d = ConfigStore.Read(path);
            Check(d.Name == c.Name, "config: name changed to " + d.Name);
            Check(d.Tweaks.Count == 2 && d.Tweaks[1] == "perf.power-plan", "config: tweaks changed");
            Check(d.Apps.Count == 1, "config: apps changed");
            Check(ConfigStore.List().Count == 1, "config: library listing");
            Info("config file: " + Path.GetFileName(path));
        }

        static void ReadOnlyChecks(string root)
        {
            Stopwatch sw = Stopwatch.StartNew();
            AppState st = new AppState(TempJournal(root), new AppSettings());
            Dictionary<string, TweakState> states = st.DetectAll();
            Info("detected " + states.Count + " tweaks in " + sw.ElapsedMilliseconds + " ms");
            foreach (Tweak t in st.Tweaks) Info(string.Format("{0,-30} {1}", t.Id, states[t.Id]));

            sw.Restart();
            List<InstalledPackage> pk = Appx.Scan();
            List<AppEntry> apps = Appx.Match(pk);
            // Informational only: build servers (Windows Server) can have almost no Store packages.
            Info("app scan: " + pk.Count + " packages, " + apps.Count + " entries in " + sw.ElapsedMilliseconds + " ms");
            foreach (AppEntry a in apps) if (a.Group != AppGroup.Other) Info("   [" + a.Group + "] " + a.Title + "  (" + string.Join(", ", a.PackageNames.ToArray()) + ")");
            int other = 0;
            foreach (AppEntry a in apps) if (a.Group == AppGroup.Other) other++;
            Info("   + " + other + " other removable packages");

            sw.Restart();
            List<StartupItem> items = StartupManager.List();
            Info("startup: " + items.Count + " items in " + sw.ElapsedMilliseconds + " ms");
            foreach (StartupItem i in items) Info("   " + (i.Enabled ? "on " : "off") + "  " + i.Name + "  |  " + i.SourceLabel + "  |  " + i.Publisher);

            foreach (CleanupTarget t in Cleaner.Targets())
            {
                sw.Restart();
                Cleaner.Measure(t);
                Info("cleanup: " + t.Title + " = " + Cleaner.FormatBytes(t.Size) + " (" + sw.ElapsedMilliseconds + " ms)");
            }

            List<RestorePointInfo> points = SysRestore.List();
            Info("restore points: " + (points == null ? "unavailable (needs admin)" : points.Count.ToString()));
            Info("cpu: " + SysInfo.Cpu + "   gpu: " + SysInfo.Gpu + "   battery: " + SysInfo.HasBattery);
            LiveStats s = SysInfo.Measure();
            Info("processes " + s.Processes + ", services " + s.ServicesRunning + ", memory " + SysInfo.FormatGB(s.MemUsed) + " / " + SysInfo.FormatGB(s.MemTotal) + ", uptime " + SysInfo.FormatUptime(s.Uptime));
        }
    }
}
#endif
