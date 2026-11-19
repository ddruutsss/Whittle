using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Whittle.Core;
using Whittle.Shell;

namespace Whittle.Pages
{
    public sealed class CleanupPage : PageBase
    {
        readonly List<CleanupTarget> targets = Cleaner.Targets();
        readonly Dictionary<string, bool> selected = new Dictionary<string, bool>();
        readonly Dictionary<string, TextBlock> sizeLabels = new Dictionary<string, TextBlock>();
        TextBlock total;
        Button cleanButton;
        Button scanButton;
        bool scanning, cleaning, scannedOnce;

        public CleanupPage(MainWindow win) : base(win)
        {
            foreach (CleanupTarget t in targets) selected[t.Id] = t.DefaultOn;
        }

        protected override FrameworkElement Build()
        {
            StackPanel page = new StackPanel();
            scanButton = Ui.IconButton("", "Scan again", "Btn", delegate { Scan(); });
            page.Children.Add(Ui.PageHeader("Cleanup",
                "Free up space by deleting files Windows doesn't need. Deleted files can't be brought back, so only things that are safe to remove are listed here.",
                scanButton));

            StackPanel body = new StackPanel();
            for (int i = 0; i < targets.Count; i++)
            {
                if (i > 0) body.Children.Add(Ui.Divider());
                body.Children.Add(Row(targets[i]));
            }
            page.Children.Add(Ui.Card(body));

            Grid foot = new Grid();
            foot.Margin = new Thickness(2, 18, 0, 0);
            foot.ColumnDefinitions.Add(new ColumnDefinition());
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            total = Ui.Text("", 14, "Text1", FontWeights.SemiBold);
            total.VerticalAlignment = VerticalAlignment.Center;
            foot.Children.Add(total);
            cleanButton = Ui.IconButton("", "Clean up", "Btn.Primary", delegate { Clean(); });
            if (Win.ReadOnly)
            {
                cleanButton.IsEnabled = false;
                ToolTipService.SetShowOnDisabled(cleanButton, true);
                cleanButton.ToolTip = "Run Whittle as administrator to clean up.";
            }
            Grid.SetColumn(cleanButton, 1);
            foot.Children.Add(cleanButton);
            page.Children.Add(foot);
            UpdateTotal();
            return Scroller(page);
        }

        public override void OnShown()
        {
            if (!scannedOnce) Scan();
        }

        FrameworkElement Row(CleanupTarget t)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            CheckBox cb = Ui.Check(selected[t.Id]);
            cb.VerticalAlignment = VerticalAlignment.Top;
            cb.Margin = new Thickness(0, 0, 12, 0);
            cb.Checked += delegate { selected[t.Id] = true; UpdateTotal(); };
            cb.Unchecked += delegate { selected[t.Id] = false; UpdateTotal(); };
            System.Windows.Automation.AutomationProperties.SetName(cb, t.Title);
            g.Children.Add(cb);

            StackPanel text = new StackPanel();
            text.Children.Add(Ui.Text(t.Title, 14, "Text1"));
            TextBlock d = Ui.Para(t.Description, 12.5, "Text2");
            d.Margin = new Thickness(0, 3, 0, 0);
            d.MaxWidth = 640;
            d.HorizontalAlignment = HorizontalAlignment.Left;
            text.Children.Add(d);
            Grid.SetColumn(text, 1);
            g.Children.Add(text);

            TextBlock size = Ui.Text("—", 14, "Text2", FontWeights.SemiBold);
            size.Margin = new Thickness(20, 1, 0, 0);
            size.VerticalAlignment = VerticalAlignment.Top;
            size.MinWidth = 70;
            size.TextAlignment = TextAlignment.Right;
            sizeLabels[t.Id] = size;
            Grid.SetColumn(size, 2);
            g.Children.Add(size);

            Border row = new Border();
            row.Padding = new Thickness(16, 13, 18, 13);
            row.Background = Brushes.Transparent;
            row.Cursor = Cursors.Hand;
            row.Child = g;
            row.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                if (Ui.IsWithin(e.OriginalSource, cb)) return;
                cb.IsChecked = cb.IsChecked != true;
            };
            return row;
        }

        async void Scan()
        {
            if (scanning || cleaning) return;
            scanning = true;
            scannedOnce = true;
            scanButton.IsEnabled = false;
            foreach (TextBlock l in sizeLabels.Values) { l.Text = "Measuring…"; l.Foreground = Ui.Br("Text3"); }
            total.Text = "Measuring…";
            foreach (CleanupTarget t in targets)
            {
                CleanupTarget target = t;
                await Task.Run(new Action(delegate { Cleaner.Measure(target); }));
                TextBlock l = sizeLabels[t.Id];
                l.Text = SizeText(t.Size);
                l.Foreground = Ui.Br(t.Size > 50L * 1024 * 1024 ? "Text1" : "Text2");
            }
            scanning = false;
            scanButton.IsEnabled = true;
            UpdateTotal();
        }

        static string SizeText(long size)
        {
            if (size == 0) return "Empty";
            if (size < 0) return "Unknown";
            return Cleaner.FormatBytes(size);
        }

        void UpdateTotal()
        {
            if (total == null || scanning) return;
            long sum = 0;
            int count = 0;
            foreach (CleanupTarget t in targets)
            {
                if (!selected[t.Id]) continue;
                count++;
                if (t.Size > 0) sum += t.Size;
            }
            total.Text = count == 0 ? "Nothing selected" : "Selected: " + Cleaner.FormatBytes(sum);
            if (cleanButton != null && !Win.ReadOnly) cleanButton.IsEnabled = count > 0 && !cleaning && scannedOnce;
        }

        async void Clean()
        {
            if (cleaning || scanning) return;
            List<CleanupTarget> chosen = new List<CleanupTarget>();
            foreach (CleanupTarget t in targets) if (selected[t.Id]) chosen.Add(t);
            if (chosen.Count == 0) return;
            bool ok = await Win.Confirm("Delete these files?",
                "This permanently deletes " + Ui.Plural(chosen.Count, "category", "categories") +
                " of files. Undo can't bring them back, but Windows doesn't need them.", "Delete", true);
            if (!ok) return;

            cleaning = true;
            cleanButton.IsEnabled = false;
            scanButton.IsEnabled = false;
            long freed = 0;
            foreach (CleanupTarget t in chosen)
            {
                TextBlock l = sizeLabels[t.Id];
                l.Text = "Cleaning…";
                l.Foreground = Ui.Br("Accent");
                CleanupTarget target = t;
                long f = 0;
                try
                {
                    f = await Task.Run(new Func<long>(delegate { return Cleaner.Clean(target); }));
                }
                catch (Exception ex)
                {
                    Log.Warn("Cleanup of " + t.Title + " failed: " + ex.Message);
                }
                freed += f;
                l.Text = SizeText(t.Size);
                l.Foreground = Ui.Br("Text2");
            }
            cleaning = false;
            scanButton.IsEnabled = true;
            UpdateTotal();
            Win.Toast(freed > 0 ? "Freed " + Cleaner.FormatBytes(freed) + "." : "Done. Some files were in use and were left alone.", "");
        }
    }
}
