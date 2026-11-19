using System;
using System.Windows;
using System.Windows.Controls;
using Whittle.Core;
using Whittle.Shell;

namespace Whittle.Pages
{
    public sealed class SettingsPage : PageBase
    {
        public SettingsPage(MainWindow win) : base(win) { }

        protected override FrameworkElement Build()
        {
            StackPanel page = new StackPanel();
            page.Children.Add(Ui.PageHeader("Settings", null));

            StackPanel prefs = new StackPanel();
            prefs.Children.Add(SwitchRow("Create a restore point before applying",
                "Recommended. Adds about a minute to each run and turns on System Restore for your system drive if it's off.",
                State.Settings.CreateRestorePoint, delegate(bool v) { State.Settings.CreateRestorePoint = v; }));
            prefs.Children.Add(Ui.Divider());
            prefs.Children.Add(SwitchRow("Restart Explorer when needed",
                "Taskbar, Start and right-click menu changes show up straight away. Open File Explorer windows close and the taskbar blinks once.",
                State.Settings.RestartExplorer, delegate(bool v) { State.Settings.RestartExplorer = v; }));
            prefs.Children.Add(Ui.Divider());
            prefs.Children.Add(SwitchRow("Show advanced tweaks",
                "Tweaks with a security trade-off, such as turning off Memory Integrity.",
                State.Settings.ShowAdvanced, delegate(bool v)
                {
                    State.Settings.ShowAdvanced = v;
                    TweaksPage tp = Win.Page<TweaksPage>("tweaks");
                    if (tp != null) tp.Rebuild();
                }));
            FrameworkElement h = Ui.SectionHeader("Applying changes", null, null);
            h.Margin = new Thickness(2, 4, 0, 10);
            page.Children.Add(h);
            page.Children.Add(Ui.Card(prefs));

            page.Children.Add(Ui.SectionHeader("Your data", null, null));
            Grid data = new Grid();
            data.ColumnDefinitions.Add(new ColumnDefinition());
            data.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel dt = new StackPanel();
            dt.Children.Add(Ui.Text("Undo history, configs and logs", 14, "Text1"));
            TextBlock path = Ui.Text(AppPaths.DataDir, 12, "Text3");
            path.FontFamily = Ui.MonoFont;
            path.Margin = new Thickness(0, 3, 0, 0);
            dt.Children.Add(path);
            TextBlock warn = Ui.Para("Keep this folder: the undo history inside it is what lets Whittle restore your original settings.", 12.5, "Text2");
            warn.Margin = new Thickness(0, 6, 0, 0);
            dt.Children.Add(warn);
            data.Children.Add(dt);
            Button open = Ui.IconButton("", "Open folder", "Btn", delegate
            {
                UndoPage up = Win.Page<UndoPage>("undo");
                if (up != null) up.OpenFolder(AppPaths.DataDir);
            });
            open.VerticalAlignment = VerticalAlignment.Top;
            open.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(open, 1);
            data.Children.Add(open);
            page.Children.Add(Ui.Card(data, new Thickness(18, 15, 14, 15)));

            page.Children.Add(Ui.SectionHeader("About", null, null));
            Grid about = new Grid();
            about.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            about.ColumnDefinitions.Add(new ColumnDefinition());
            FrameworkElement mark = Brand.Mark(44);
            mark.VerticalAlignment = VerticalAlignment.Top;
            about.Children.Add(mark);
            StackPanel at = new StackPanel();
            at.Margin = new Thickness(18, 0, 0, 0);
            at.Children.Add(Ui.Text(Brand.Name + " " + Brand.Version, 16, "Text1", FontWeights.SemiBold));
            TextBlock line = Ui.Para("A careful debloater and tuner for Windows 11. Every change is recorded and can be undone.", 13, "Text2");
            line.Margin = new Thickness(0, 3, 0, 10);
            at.Children.Add(line);
            at.Children.Add(Ui.Text("Running on " + SysInfo.WindowsName + " · " + SysInfo.VersionLine, 12.5, "Text3"));
            TextBlock credit = Ui.Para("Tweak values and app list informed by Win11Debloat by Raphire (MIT License).", 12.5, "Text3");
            credit.Margin = new Thickness(0, 4, 0, 0);
            at.Children.Add(credit);
            Grid.SetColumn(at, 1);
            about.Children.Add(at);
            page.Children.Add(Ui.Card(about, new Thickness(20, 18, 20, 18)));

            return Scroller(page);
        }

        FrameworkElement SwitchRow(string title, string description, bool value, Action<bool> onChange)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel text = new StackPanel();
            text.Margin = new Thickness(0, 0, 24, 0);
            text.Children.Add(Ui.Text(title, 14, "Text1"));
            TextBlock d = Ui.Para(description, 13, "Text2");
            d.Margin = new Thickness(0, 3, 0, 0);
            d.MaxWidth = 660;
            d.HorizontalAlignment = HorizontalAlignment.Left;
            text.Children.Add(d);
            g.Children.Add(text);
            CheckBox sw = Ui.Switch(value);
            System.Windows.Automation.AutomationProperties.SetName(sw, title);
            sw.Checked += delegate { onChange(true); State.Settings.Save(); };
            sw.Unchecked += delegate { onChange(false); State.Settings.Save(); };
            Grid.SetColumn(sw, 1);
            g.Children.Add(sw);
            Border row = new Border();
            row.Padding = new Thickness(18, 14, 18, 14);
            row.Child = g;
            return row;
        }
    }
}
