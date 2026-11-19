using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Whittle.Core;
using Whittle.Pages;

namespace Whittle.Shell
{
    public sealed partial class MainWindow : Window
    {
        public readonly AppState State;
        public readonly bool ReadOnly;

        Grid root;
        ContentControl host;
        Border pendingBar;
        TextBlock pendingTitle;
        TextBlock pendingMeta;
        Button pendingApply;
        Grid overlay;
        Grid dialogLayer;
        StackPanel toasts;
        Button maxButton;
        TextBlock statusLine;

        readonly Dictionary<string, PageBase> pages = new Dictionary<string, PageBase>();
        readonly Dictionary<string, RadioButton> nav = new Dictionary<string, RadioButton>();
        readonly List<string> order = new List<string>();
        public string CurrentPage { get; private set; }
        public bool AppsScanning { get; private set; }

        public MainWindow(AppState state, bool readOnly)
        {
            State = state;
            ReadOnly = readOnly;

            Title = Brand.Name;
            Width = 1200;
            Height = 790;
            MinWidth = 1000;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Br("Bg0");
            Foreground = Ui.Br("Text1");
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 40;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.GlassFrameThickness = new Thickness(1);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            pages["home"] = new HomePage(this);
            pages["tweaks"] = new TweaksPage(this);
            pages["apps"] = new AppsPage(this);
            pages["startup"] = new StartupPage(this);
            pages["cleanup"] = new CleanupPage(this);
            pages["profiles"] = new ProfilesPage(this);
            pages["undo"] = new UndoPage(this);
            pages["settings"] = new SettingsPage(this);

            Content = BuildLayout();

            State.SelectionChanged += UpdatePendingBar;
            SourceInitialized += OnSourceInitialized;
            StateChanged += delegate { UpdateMaximized(); };
            SizeChanged += delegate { if (WindowState == WindowState.Maximized) UpdateMaximized(); };
            Closing += OnClosing;
            PreviewKeyDown += OnPreviewKeyDown;

            Navigate("home");
            UpdatePendingBar();
        }

        // ------------------------------------------------------------------ Layout

        UIElement BuildLayout()
        {
            root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232) });
            root.ColumnDefinitions.Add(new ColumnDefinition());

            root.Children.Add(BuildSidebar());

            // Content panel sits below the caption strip with a rounded top-left corner.
            Border panel = new Border();
            panel.Background = Ui.Br("Bg1");
            panel.BorderBrush = Ui.Br("Line");
            panel.BorderThickness = new Thickness(1, 1, 0, 0);
            panel.CornerRadius = new CornerRadius(10, 0, 0, 0);
            panel.Margin = new Thickness(0, 44, 0, 0);
            Grid.SetColumn(panel, 1);
            Grid panelGrid = new Grid();
            panelGrid.RowDefinitions.Add(new RowDefinition());
            panelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            host = new ContentControl();
            host.Focusable = false;
            panelGrid.Children.Add(host);
            pendingBar = BuildPendingBar();
            Grid.SetRow(pendingBar, 1);
            panelGrid.Children.Add(pendingBar);
            panel.Child = panelGrid;
            root.Children.Add(panel);

            // Caption buttons
            StackPanel caption = new StackPanel();
            caption.Orientation = Orientation.Horizontal;
            caption.HorizontalAlignment = HorizontalAlignment.Right;
            caption.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(caption, 1);
            Button min = CaptionButton("", "Btn.Caption", delegate { WindowState = WindowState.Minimized; });
            maxButton = CaptionButton("", "Btn.Caption", delegate
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            });
            Button close = CaptionButton("", "Btn.Close", delegate { Close(); });
            min.ToolTip = "Minimize";
            maxButton.ToolTip = "Maximize";
            close.ToolTip = "Close";
            caption.Children.Add(min);
            caption.Children.Add(maxButton);
            caption.Children.Add(close);
            root.Children.Add(caption);

            // Dialog overlay
            overlay = new Grid();
            overlay.Background = Ui.Br("Scrim");
            overlay.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(overlay, 2);
            dialogLayer = new Grid();
            overlay.Children.Add(dialogLayer);
            root.Children.Add(overlay);

            toasts = new StackPanel();
            toasts.HorizontalAlignment = HorizontalAlignment.Right;
            toasts.VerticalAlignment = VerticalAlignment.Bottom;
            toasts.Margin = new Thickness(0, 0, 24, 84);
            toasts.IsHitTestVisible = false;
            Grid.SetColumnSpan(toasts, 2);
            root.Children.Add(toasts);

            return root;
        }

        Button CaptionButton(string glyph, string style, Action click)
        {
            Button b = new Button();
            b.Style = Ui.St(style);
            b.Content = glyph;
            b.Click += delegate { click(); };
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            return b;
        }

        UIElement BuildSidebar()
        {
            DockPanel side = new DockPanel();
            side.LastChildFill = false;

            // Brand
            StackPanel brand = new StackPanel();
            brand.Orientation = Orientation.Horizontal;
            brand.Margin = new Thickness(22, 13, 0, 22);
            FrameworkElement mark = Brand.Mark(20);
            mark.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(mark);
            TextBlock name = Ui.Text(Brand.Name, 15, "Text1", FontWeights.SemiBold);
            name.Margin = new Thickness(10, 0, 0, 1);
            name.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(name);
            DockPanel.SetDock(brand, Dock.Top);
            side.Children.Add(brand);

            StackPanel top = new StackPanel();
            DockPanel.SetDock(top, Dock.Top);
            top.Children.Add(NavItem("home", "", "Overview"));
            top.Children.Add(NavItem("tweaks", "", "Tweaks"));
            top.Children.Add(NavItem("apps", "", "Apps"));
            top.Children.Add(NavItem("startup", "", "Startup"));
            top.Children.Add(NavItem("cleanup", "", "Cleanup"));
            TextBlock group = Ui.Text("Manage", 11.5, "Text3", FontWeights.SemiBold);
            group.Margin = new Thickness(24, 18, 0, 6);
            top.Children.Add(group);
            top.Children.Add(NavItem("profiles", "", "Profiles"));
            top.Children.Add(NavItem("undo", "", "Undo & restore"));
            side.Children.Add(top);

            StackPanel bottom = new StackPanel();
            bottom.Margin = new Thickness(0, 0, 0, 14);
            DockPanel.SetDock(bottom, Dock.Bottom);
            bottom.Children.Add(NavItem("settings", "", "Settings"));
            if (ReadOnly)
            {
                statusLine = Ui.Para("Preview mode. Run as administrator to make changes.", 11.5, "Warn");
            }
            else
            {
                statusLine = Ui.Para(SysInfo.WindowsName + " · " + SysInfo.VersionLine.Split('·')[0].Trim(), 11.5, "Text3");
            }
            statusLine.Margin = new Thickness(24, 12, 16, 0);
            bottom.Children.Add(statusLine);
            side.Children.Add(bottom);
            return side;
        }

        RadioButton NavItem(string id, string glyph, string label)
        {
            RadioButton rb = new RadioButton();
            rb.Style = Ui.St("Nav");
            rb.GroupName = "nav";
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            TextBlock ic = new TextBlock();
            ic.Text = glyph;
            ic.FontFamily = Ui.IconFont;
            ic.FontSize = 16;
            ic.VerticalAlignment = VerticalAlignment.Center;
            ic.Width = 18;
            sp.Children.Add(ic);
            TextBlock tx = new TextBlock();
            tx.Text = label;
            tx.FontSize = 13.5;
            tx.Margin = new Thickness(13, 0, 0, 1);
            tx.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(tx);
            rb.Content = sp;
            rb.Checked += delegate { if (CurrentPage != id) Navigate(id); };
            nav[id] = rb;
            order.Add(id);
            return rb;
        }

        Border BuildPendingBar()
        {
            Border bar = new Border();
            bar.Background = Ui.Br("Elevated");
            bar.BorderBrush = Ui.Br("Line");
            bar.BorderThickness = new Thickness(0, 1, 0, 0);
            bar.Padding = new Thickness(40, 12, 24, 12);
            bar.Visibility = Visibility.Collapsed;
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border iconWrap = new Border();
            iconWrap.Width = 30;
            iconWrap.Height = 30;
            iconWrap.CornerRadius = new CornerRadius(15);
            iconWrap.Background = Ui.Br("AccentSoft");
            iconWrap.Child = Ui.Icon("", 14, "Accent");
            ((TextBlock)iconWrap.Child).HorizontalAlignment = HorizontalAlignment.Center;
            g.Children.Add(iconWrap);
            StackPanel text = new StackPanel();
            text.Margin = new Thickness(14, 0, 0, 0);
            text.VerticalAlignment = VerticalAlignment.Center;
            pendingTitle = Ui.Text("", 13.5, "Text1", FontWeights.SemiBold);
            pendingMeta = Ui.Text("", 12, "Text2");
            pendingMeta.Margin = new Thickness(0, 1, 0, 0);
            text.Children.Add(pendingTitle);
            text.Children.Add(pendingMeta);
            Grid.SetColumn(text, 1);
            g.Children.Add(text);
            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            Button discard = Ui.Button("Discard", "Btn.Ghost", delegate { State.Discard(); });
            pendingApply = Ui.Button("Review and apply", "Btn.Primary", ReviewAndApply);
            pendingApply.Margin = new Thickness(8, 0, 0, 0);
            if (ReadOnly)
            {
                pendingApply.IsEnabled = false;
                ToolTipService.SetShowOnDisabled(pendingApply, true);
                pendingApply.ToolTip = "Run Whittle as administrator to apply changes.";
            }
            actions.Children.Add(discard);
            actions.Children.Add(pendingApply);
            Grid.SetColumn(actions, 2);
            g.Children.Add(actions);
            bar.Child = g;
            return bar;
        }

        void UpdatePendingBar()
        {
            ChangePlan plan = State.BuildPlan();
            int n = plan.Count;
            if (n == 0 || State.Busy)
            {
                pendingBar.Visibility = Visibility.Collapsed;
                return;
            }
            bool wasHidden = pendingBar.Visibility != Visibility.Visible;
            pendingBar.Visibility = Visibility.Visible;
            pendingTitle.Text = Ui.Plural(n, "change", "changes") + " ready to apply";
            List<string> parts = new List<string>();
            if (plan.Apply.Count > 0) parts.Add(plan.Apply.Count + " to turn on");
            if (plan.Revert.Count > 0) parts.Add(plan.Revert.Count + " to undo");
            if (plan.RemoveApps.Count > 0) parts.Add(Ui.Plural(plan.RemoveApps.Count, "app", "apps") + " to remove");
            if (plan.NeedsRestart) parts.Add("restart needed");
            pendingMeta.Text = string.Join(" · ", parts.ToArray());
            if (wasHidden) Ui.FadeIn(pendingBar);
        }

        // ------------------------------------------------------------------ Navigation

        public void Navigate(string id)
        {
            PageBase page;
            if (!pages.TryGetValue(id, out page)) return;
            CurrentPage = id;
            RadioButton rb;
            if (nav.TryGetValue(id, out rb) && rb.IsChecked != true) rb.IsChecked = true;
            host.Content = page.View;
            page.OnShown();
            Ui.FadeIn(page.View);
        }

        public T Page<T>(string id) where T : PageBase
        {
            PageBase p;
            return pages.TryGetValue(id, out p) ? p as T : null;
        }

        void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && overlay.Visibility == Visibility.Visible && escapeHandlers.Count > 0)
            {
                Action h = escapeHandlers[escapeHandlers.Count - 1];
                if (h != null) { h(); e.Handled = true; }
                return;
            }
            if (overlay.Visibility == Visibility.Visible) return;
            if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.Tab || e.Key == Key.PageDown || e.Key == Key.PageUp))
            {
                int i = order.IndexOf(CurrentPage);
                bool back = e.Key == Key.PageUp || (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
                i = (i + (back ? order.Count - 1 : 1)) % order.Count;
                Navigate(order[i]);
                e.Handled = true;
            }
        }

        // ------------------------------------------------------------------ Background work

        public async void RefreshStates()
        {
            Dictionary<string, TweakState> states = await Task.Run(new Func<Dictionary<string, TweakState>>(State.DetectAll));
            State.SetStates(states);
        }

        public async void ScanApps()
        {
            if (AppsScanning) return;
            AppsScanning = true;
            AppsPage ap = Page<AppsPage>("apps");
            if (ap != null) ap.ShowScanning();
            List<AppEntry> list = null;
            try
            {
                list = await Task.Run(new Func<List<AppEntry>>(delegate { return Appx.Match(Appx.Scan()); }));
            }
            catch (Exception ex)
            {
                Log.Error("App scan failed: " + ex.Message);
                list = new List<AppEntry>();
            }
            AppsScanning = false;
            State.SetApps(list);
        }

        public async void CheckAccount()
        {
            if (ReadOnly) return;
            string[] names = await Task.Run(new Func<string[]>(SysInfo.AccountMismatch));
            if (names == null) return;
            Log.Warn("Running as " + names[0] + " while " + names[1] + " is signed in.");
            statusLine.Text = "Running as " + names[0] + ". Per-user settings will change for that account, not " + names[1] + ".";
            statusLine.Foreground = Ui.Br("Warn");
            Toast("Whittle is running as " + names[0] + ", but " + names[1] + " is signed in. Settings that belong to a user will change for " +
                names[0] + ". Sign in as " + names[1] + " with admin rights to tune that account.", "");
        }

        // ------------------------------------------------------------------ Window plumbing

        void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            try
            {
                int dark = 1;
                Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
                int round = 2;
                Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
                int border = 0x002F2A2A; // COLORREF 0x00BBGGRR for #2A2A2F
                Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_BORDER_COLOR, ref border, 4);
            }
            catch
            {
                // Older Windows builds don't support these attributes.
            }
        }

        void UpdateMaximized()
        {
            maxButton.Content = WindowState == WindowState.Maximized ? "" : "";
            maxButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
            if (WindowState != WindowState.Maximized)
            {
                root.Margin = new Thickness(0);
                return;
            }
            // A maximized window with custom chrome hangs past the screen edge by its frame size.
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            RECT wr;
            if (!GetWindowRect(hwnd, out wr)) { root.Margin = new Thickness(8); return; }
            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref mi)) { root.Margin = new Thickness(8); return; }
            PresentationSource src = PresentationSource.FromVisual(this);
            double sx = src != null ? src.CompositionTarget.TransformFromDevice.M11 : 1;
            double sy = src != null ? src.CompositionTarget.TransformFromDevice.M22 : 1;
            root.Margin = new Thickness(
                Math.Max(0, mi.rcWork.Left - wr.Left) * sx,
                Math.Max(0, mi.rcWork.Top - wr.Top) * sy,
                Math.Max(0, wr.Right - mi.rcWork.Right) * sx,
                Math.Max(0, wr.Bottom - mi.rcWork.Bottom) * sy);
        }

        void OnClosing(object sender, CancelEventArgs e)
        {
            if (State.Busy)
            {
                e.Cancel = true;
                Toast("Whittle is still applying changes. It'll be done in a moment.", "");
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }

        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    }
}
