using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Whittle.Core;
using Whittle.Shell;

namespace Whittle.Pages
{
    public sealed class StartupPage : PageBase
    {
        StackPanel listHost;
        TextBlock summary;
        bool loading;
        List<StartupItem> items;

        public StartupPage(MainWindow win) : base(win) { }

        protected override FrameworkElement Build()
        {
            StackPanel page = new StackPanel();
            page.Children.Add(Ui.PageHeader("Startup",
                "Apps that open when you sign in. Turning one off takes effect the next time you sign in, and you can turn it back on whenever you like.",
                Ui.IconButton("", "Task Manager", "Btn", delegate { OpenTaskManager(); }),
                Ui.IconButton("", null, "Btn", delegate { Load(); })));
            summary = Ui.Text("", 13, "Text2");
            summary.Margin = new Thickness(2, 0, 0, 12);
            page.Children.Add(summary);
            listHost = new StackPanel();
            page.Children.Add(listHost);
            return Scroller(page);
        }

        public override void OnShown()
        {
            Load();
        }

        async void Load()
        {
            if (loading) return;
            loading = true;
            if (items == null)
            {
                listHost.Children.Clear();
                listHost.Children.Add(Ui.Loading("Reading startup apps…"));
            }
            try
            {
                items = await Task.Run(new Func<List<StartupItem>>(StartupManager.List));
            }
            catch (Exception ex)
            {
                items = new List<StartupItem>();
                Log.Error("Couldn't read startup apps: " + ex.Message);
            }
            loading = false;
            Render();
        }

        void Render()
        {
            listHost.Children.Clear();
            int on = 0;
            foreach (StartupItem i in items) if (i.Enabled) on++;
            summary.Text = items.Count == 0 ? "" : on + " of " + items.Count + " turned on";
            if (items.Count == 0)
            {
                listHost.Children.Add(Ui.Card(Ui.Para("No startup apps found.", 14, "Text2"), new Thickness(20, 18, 20, 18)));
                return;
            }
            StackPanel body = new StackPanel();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) body.Children.Add(Ui.Divider());
                body.Children.Add(Row(items[i]));
            }
            listHost.Children.Add(Ui.Card(body));
        }

        FrameworkElement Row(StartupItem item)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border iconBox = new Border();
            iconBox.Width = 32;
            iconBox.Height = 32;
            iconBox.CornerRadius = new CornerRadius(7);
            iconBox.Background = Ui.Br("SurfacePressed");
            TextBlock fallback = Ui.Icon(item.Source == StartupSource.StoreApp ? "" : "", 14, "Text3");
            fallback.HorizontalAlignment = HorizontalAlignment.Center;
            iconBox.Child = fallback;
            g.Children.Add(iconBox);
            if (item.FilePath != null) LoadIcon(item.FilePath, iconBox);

            StackPanel text = new StackPanel();
            text.Margin = new Thickness(14, 0, 16, 0);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Children.Add(Ui.Text(item.Name, 14, item.Enabled ? "Text1" : "Text2"));
            string sub;
            if (item.Description.Length > 0 && item.Publisher.Length > 0) sub = item.Description + " · " + item.Publisher;
            else if (item.Description.Length > 0) sub = item.Description;
            else if (item.Publisher.Length > 0) sub = item.Publisher;
            else sub = item.Command ?? "";
            TextBlock s = Ui.Text(sub, 12, "Text3");
            s.Margin = new Thickness(0, 2, 0, 0);
            s.ToolTip = item.Command;
            text.Children.Add(s);
            Grid.SetColumn(text, 1);
            g.Children.Add(text);

            Border src = Ui.Badge(item.SourceLabel, Tone.Neutral);
            src.Margin = new Thickness(0, 0, 20, 0);
            Grid.SetColumn(src, 2);
            g.Children.Add(src);

            CheckBox sw = Ui.Switch(item.Enabled);
            sw.IsEnabled = !item.Locked;
            if (item.Locked)
            {
                ToolTipService.SetShowOnDisabled(sw, true);
                sw.ToolTip = "Managed by a policy on this PC.";
            }
            System.Windows.Automation.AutomationProperties.SetName(sw, item.Name);
            RoutedEventHandler changed = null;
            changed = delegate
            {
                bool want = sw.IsChecked == true;
                try
                {
                    StartupManager.SetEnabled(item, want);
                    ((TextBlock)text.Children[0]).Foreground = Ui.Br(want ? "Text1" : "Text2");
                    int on = 0;
                    foreach (StartupItem i in items) if (i.Enabled) on++;
                    summary.Text = on + " of " + items.Count + " turned on";
                }
                catch (Exception ex)
                {
                    sw.Checked -= changed;
                    sw.Unchecked -= changed;
                    sw.IsChecked = !want;
                    sw.Checked += changed;
                    sw.Unchecked += changed;
                    Win.Toast("Couldn't change " + item.Name + ": " + ex.Message, "");
                }
            };
            sw.Checked += changed;
            sw.Unchecked += changed;
            Grid.SetColumn(sw, 3);
            g.Children.Add(sw);

            Border row = new Border();
            row.Padding = new Thickness(16, 11, 18, 11);
            row.Child = g;
            return row;
        }

        static void LoadIcon(string path, Border target)
        {
            Task.Run(new Func<BitmapSource>(delegate
            {
                try
                {
                    using (System.Drawing.Icon ic = System.Drawing.Icon.ExtractAssociatedIcon(path))
                    {
                        if (ic == null) return null;
                        BitmapSource raw = Imaging.CreateBitmapSourceFromHIcon(ic.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        FormatConvertedBitmap conv = new FormatConvertedBitmap(raw, PixelFormats.Pbgra32, null, 0);
                        int w = conv.PixelWidth, h = conv.PixelHeight;
                        byte[] px = new byte[w * h * 4];
                        conv.CopyPixels(px, w * 4, 0);
                        BitmapSource copy = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, px, w * 4);
                        copy.Freeze();
                        return copy;
                    }
                }
                catch
                {
                    return null;
                }
            })).ContinueWith(delegate(Task<BitmapSource> t)
            {
                if (t.Result == null) return;
                target.Dispatcher.BeginInvoke(new Action(delegate
                {
                    Image img = new Image();
                    img.Source = t.Result;
                    img.Width = 22;
                    img.Height = 22;
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    target.Background = Brushes.Transparent;
                    target.Child = img;
                }));
            });
        }

        void OpenTaskManager()
        {
            try
            {
                Process.Start(new ProcessStartInfo("taskmgr.exe", "/0 /startup") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Win.Toast("Couldn't open Task Manager: " + ex.Message, "");
            }
        }
    }
}
