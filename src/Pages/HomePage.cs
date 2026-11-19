using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Whittle.Core;
using Whittle.Shell;

namespace Whittle.Pages
{
    public sealed class HomePage : PageBase
    {
        TextBlock tweaksValue, tweaksCaption;
        TextBlock procValue, procCaption;
        TextBlock startupValue, startupCaption;
        TextBlock memValue, memCaption;
        TextBlock appsLine, diskLine, uptimeLine;
        DispatcherTimer timer;
        bool measuring;

        public HomePage(MainWindow win) : base(win)
        {
            State.StatesChanged += UpdateTweakTile;
            State.AppsChanged += UpdateAppsLine;
        }

        protected override FrameworkElement Build()
        {
            StackPanel page = new StackPanel();
            page.Children.Add(Ui.PageHeader("Overview",
                SysInfo.WindowsName + " · " + SysInfo.VersionLine + " · " + SysInfo.MachineName));

            UniformGrid tiles = new UniformGrid();
            tiles.Columns = 4;
            tiles.Margin = new Thickness(-6, 0, -6, 0);
            tiles.Children.Add(Tile("Tweaks on", out tweaksValue, out tweaksCaption));
            tiles.Children.Add(Tile("Running processes", out procValue, out procCaption));
            tiles.Children.Add(Tile("Startup apps on", out startupValue, out startupCaption));
            tiles.Children.Add(Tile("Memory in use", out memValue, out memCaption));
            page.Children.Add(tiles);

            Grid two = new Grid();
            two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
            two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star), MinWidth = 290 });

            StackPanel left = new StackPanel();
            left.Children.Add(Ui.SectionHeader("Start with a profile", "Nothing changes until you review and apply", null));
            UniformGrid presets = new UniformGrid();
            presets.Columns = 2;
            presets.Margin = new Thickness(-6, -6, -6, 0);
            foreach (Preset p in Presets.All) presets.Children.Add(PresetCard(Win, p));
            left.Children.Add(presets);
            two.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Children.Add(Ui.SectionHeader("This PC", null, null));
            StackPanel info = new StackPanel();
            string cpu = SysInfo.Cpu;
            FrameworkElement cpuRow = InfoRow("Processor", System.Text.RegularExpressions.Regex.Replace(cpu, @"\s*@.*$", ""), null);
            ((TextBlock)((Grid)cpuRow).Children[1]).ToolTip = cpu;
            info.Children.Add(cpuRow);
            info.Children.Add(InfoRow("Graphics", "Detecting…", "gpu"));
            Native.MEMORYSTATUSEX m = new Native.MEMORYSTATUSEX();
            m.dwLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MEMORYSTATUSEX));
            Native.GlobalMemoryStatusEx(ref m);
            info.Children.Add(InfoRow("Memory", SysInfo.FormatGB(m.ullTotalPhys), null));
            FrameworkElement disk = InfoRow("System drive", "—", null);
            diskLine = (TextBlock)((Grid)disk).Children[1];
            info.Children.Add(disk);
            FrameworkElement up = InfoRow("Up time", "—", null);
            uptimeLine = (TextBlock)((Grid)up).Children[1];
            info.Children.Add(up);
            info.Children.Add(InfoRow("Power", SysInfo.HasBattery ? "Laptop (battery present)" : "Desktop", null));
            right.Children.Add(Ui.Card(info, new Thickness(18, 10, 18, 10)));

            Border apps = Ui.Card(null, new Thickness(18, 16, 18, 16));
            apps.Margin = new Thickness(0, 12, 0, 0);
            StackPanel appsBody = new StackPanel();
            appsBody.Children.Add(Ui.Text("Preinstalled apps", 12.5, "Text2"));
            appsLine = Ui.Para("Looking for apps…", 14, "Text1");
            appsLine.Margin = new Thickness(0, 4, 0, 10);
            appsBody.Children.Add(appsLine);
            Button review = Ui.Button("Review apps", "Btn", delegate { Win.Navigate("apps"); });
            review.HorizontalAlignment = HorizontalAlignment.Left;
            appsBody.Children.Add(review);
            apps.Child = appsBody;
            right.Children.Add(apps);

            Grid.SetColumn(right, 2);
            two.Children.Add(right);
            page.Children.Add(two);

            UpdateTweakTile();
            UpdateAppsLine();
            Task.Run(new Func<string>(delegate { return SysInfo.Gpu; })).ContinueWith(delegate(Task<string> t)
            {
                Win.Dispatcher.BeginInvoke(new Action(delegate
                {
                    TextBlock g = FindValue(info, "gpu");
                    if (g != null) { g.Text = t.Result; g.ToolTip = t.Result; }
                }));
            });

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(4);
            timer.Tick += delegate
            {
                if (Win.CurrentPage == "home" && Win.WindowState != WindowState.Minimized) MeasureLive();
                else timer.Stop();
            };
            return Scroller(page);
        }

        public override void OnShown()
        {
            MeasureLive();
            if (timer != null) timer.Start();
        }

        static FrameworkElement Tile(string label, out TextBlock value, out TextBlock caption)
        {
            StackPanel sp = new StackPanel();
            sp.Children.Add(Ui.Text(label, 12.5, "Text2"));
            value = Ui.Text("—", 28, "Text1", FontWeights.SemiBold);
            value.Margin = new Thickness(0, 6, 0, 2);
            sp.Children.Add(value);
            caption = Ui.Text(" ", 12, "Text3");
            sp.Children.Add(caption);
            Border b = Ui.Card(sp, new Thickness(18, 15, 18, 15));
            b.Margin = new Thickness(6);
            return b;
        }

        static FrameworkElement InfoRow(string label, string value, string tag)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 7, 0, 7);
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(Ui.Text(label, 12.5, "Text3"));
            TextBlock v = Ui.Text(value, 13, "Text1");
            v.Tag = tag;
            v.ToolTip = value;
            Grid.SetColumn(v, 1);
            g.Children.Add(v);
            return g;
        }

        static TextBlock FindValue(Panel p, string tag)
        {
            foreach (UIElement e in p.Children)
            {
                Grid g = e as Grid;
                if (g == null || g.Children.Count < 2) continue;
                TextBlock t = g.Children[1] as TextBlock;
                if (t != null && Equals(t.Tag, tag)) return t;
            }
            return null;
        }

        public static FrameworkElement PresetCard(MainWindow win, Preset p)
        {
            AppState state = AppState.Current;
            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel head = new StackPanel();
            head.Orientation = Orientation.Horizontal;
            Border icon = new Border();
            icon.Width = 32;
            icon.Height = 32;
            icon.CornerRadius = new CornerRadius(8);
            icon.Background = Ui.Br("AccentSoft");
            TextBlock glyph = Ui.Icon(p.Glyph, 15, "Accent");
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            icon.Child = glyph;
            head.Children.Add(icon);
            TextBlock title = Ui.Text(p.Title, 15, "Text1", FontWeights.SemiBold);
            title.Margin = new Thickness(12, 0, 0, 1);
            title.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(title);
            g.Children.Add(head);

            TextBlock summary = Ui.Para(p.Summary, 13, "Text2");
            summary.Margin = new Thickness(0, 12, 0, 14);
            Grid.SetRow(summary, 1);
            g.Children.Add(summary);

            Grid foot = new Grid();
            foot.ColumnDefinitions.Add(new ColumnDefinition());
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int count = 0;
            foreach (string id in p.Tweaks) if (state.Find(id) != null) count++;
            TextBlock meta = Ui.Text(Ui.Plural(count, "tweak", "tweaks") + (p.RemoveRecommendedApps ? " · removes bloat" : ""), 12, "Text3");
            meta.VerticalAlignment = VerticalAlignment.Center;
            foot.Children.Add(meta);
            Button use = Ui.Button("Use profile", "Btn", delegate { UsePreset(win, p); });
            use.Height = 30;
            use.Padding = new Thickness(12, 0, 12, 0);
            Grid.SetColumn(use, 1);
            foot.Children.Add(use);
            Grid.SetRow(foot, 2);
            g.Children.Add(foot);

            Border card = Ui.Card(g, new Thickness(18, 16, 16, 16));
            card.Margin = new Thickness(6);
            return card;
        }

        public static void UsePreset(MainWindow win, Preset p)
        {
            AppState state = AppState.Current;
            if (!state.StatesReady)
            {
                win.Toast("Still checking your current settings. Try again in a second.", "");
                return;
            }
            int added = state.SelectPreset(p);
            if (state.Apps == null) state.SelectRecommendedAppsOnScan = p.RemoveRecommendedApps;
            win.Navigate("tweaks");
            if (added == 0 && state.SelectedApps().Count == 0)
            {
                win.Toast("Everything in the " + p.Title + " profile is already on.", "");
            }
            else
            {
                win.Toast(p.Title + " profile selected. Look over the changes, then choose Review and apply.", "");
            }
        }

        void UpdateTweakTile()
        {
            if (tweaksValue == null) return;
            if (!State.StatesReady) { tweaksValue.Text = "—"; tweaksCaption.Text = "Checking…"; return; }
            tweaksValue.Text = State.CountApplied().ToString(CultureInfo.CurrentCulture);
            tweaksCaption.Text = "of " + State.CountAvailable() + " available";
        }

        void UpdateAppsLine()
        {
            if (appsLine == null) return;
            if (State.Apps == null) { appsLine.Text = "Looking for apps…"; return; }
            int rec = 0, all = 0;
            foreach (AppEntry a in State.Apps)
            {
                if (a.Group == AppGroup.Other) continue;
                all++;
                if (a.Group == AppGroup.Recommended) rec++;
            }
            appsLine.Text = rec > 0
                ? Ui.Plural(rec, "app", "apps") + " worth removing, " + all + " known apps in total"
                : "No bloat found. " + Ui.Plural(all, "known app", "known apps") + " installed.";
        }

        async void MeasureLive()
        {
            if (measuring || procValue == null) return;
            measuring = true;
            LiveStats s = null;
            int startupOn = -1, startupAll = 0;
            try
            {
                s = await Task.Run(new Func<LiveStats>(SysInfo.Measure));
                List<StartupItem> items = await Task.Run(new Func<List<StartupItem>>(StartupManager.List));
                startupOn = 0;
                foreach (StartupItem i in items) if (i.Enabled) startupOn++;
                startupAll = items.Count;
            }
            catch
            {
            }
            measuring = false;
            if (s == null) return;

            Dictionary<string, object> baseline = State.Journal.Baseline;
            if (baseline == null)
            {
                baseline = new Dictionary<string, object>();
                baseline["processes"] = s.Processes;
                baseline["services"] = s.ServicesRunning;
                baseline["startup"] = startupOn;
                baseline["memUsedMB"] = (long)(s.MemUsed / 1048576);
                baseline["time"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
                State.Journal.Baseline = baseline;
                try { State.Journal.Save(); } catch { }
            }

            procValue.Text = s.Processes.ToString(CultureInfo.CurrentCulture);
            long bp = Json.Long(baseline, "processes", -1);
            // Compare with the first time Whittle ran, so the effect of debloating is visible.
            procCaption.Text = bp > 0 && bp != s.Processes ? "was " + bp + " at first run" : s.ServicesRunning + " services running";
            procCaption.ToolTip = s.ServicesRunning + " services running";
            startupValue.Text = startupOn >= 0 ? startupOn.ToString(CultureInfo.CurrentCulture) : "—";
            long bs = Json.Long(baseline, "startup", -1);
            startupCaption.Text = "of " + startupAll + (bs >= 0 && bs != startupOn ? " · was " + bs + " at first run" : "");
            memValue.Text = SysInfo.FormatGB(s.MemUsed);
            memCaption.Text = "of " + SysInfo.FormatGB(s.MemTotal) + " · " + s.MemLoad + "%";
            if (diskLine != null && s.DiskTotal > 0)
            {
                diskLine.Text = SysInfo.FormatGB((ulong)s.DiskFree) + " free of " + SysInfo.FormatGB((ulong)s.DiskTotal);
            }
            if (uptimeLine != null) uptimeLine.Text = SysInfo.FormatUptime(s.Uptime);
        }
    }
}
