using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Whittle.Core;
using Whittle.Shell;

namespace Whittle.Pages
{
    public sealed class AppsPage : PageBase
    {
        StackPanel listHost;
        string query = "";
        bool showOthers;
        readonly List<AppRow> rows = new List<AppRow>();

        public AppsPage(MainWindow win) : base(win)
        {
            State.AppsChanged += delegate { if (IsBuilt) Rebuild(); };
            State.SelectionChanged += delegate { foreach (AppRow r in rows) r.Refresh(); };
        }

        protected override FrameworkElement Build()
        {
            StackPanel page = new StackPanel();
            page.Children.Add(Ui.PageHeader("Apps",
                "Remove preinstalled apps you don't use. Almost all of them can be reinstalled from the Microsoft Store.",
                Ui.SearchBox("Search apps", 220, delegate(string q) { query = q.Trim(); Rebuild(); }),
                Ui.IconButton("", null, "Btn", delegate { Win.ScanApps(); })));
            listHost = new StackPanel();
            page.Children.Add(listHost);
            Rebuild();
            return Scroller(page);
        }

        public void ShowScanning()
        {
            if (listHost == null) return;
            rows.Clear();
            listHost.Children.Clear();
            listHost.Children.Add(Ui.Loading("Looking for installed apps…"));
        }

        bool Matches(AppEntry a)
        {
            if (query.Length == 0) return true;
            if (a.Title.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            if (a.Description != null && a.Description.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            foreach (string n in a.PackageNames) if (n.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        void Rebuild()
        {
            if (listHost == null) return;
            rows.Clear();
            listHost.Children.Clear();
            if (State.Apps == null)
            {
                listHost.Children.Add(Ui.Loading("Looking for installed apps…"));
                return;
            }

            bool any = false;
            any |= Group(AppGroup.Recommended, "Recommended to remove", "Ads, trials and apps most people never open", true);
            any |= Group(AppGroup.Optional, "Optional", "Useful to some people. Only remove what you know you don't use", true);
            any |= Group(AppGroup.Caution, "Keep unless you're sure", "Other parts of Windows or games depend on these", false);

            int others = 0;
            foreach (AppEntry a in State.Apps) if (a.Group == AppGroup.Other) others++;
            if (others > 0)
            {
                CheckBox cb = Ui.Check(showOthers);
                TextBlock label = Ui.Text("Show " + Ui.Plural(others, "other installed app", "other installed apps") + " Whittle doesn't know", 13.5, "Text2");
                label.Margin = new Thickness(8, 0, 0, 1);
                label.VerticalAlignment = VerticalAlignment.Center;
                StackPanel toggleRow = Ui.HStack(cb, label);
                toggleRow.Margin = new Thickness(2, 26, 0, 0);
                toggleRow.Cursor = Cursors.Hand;
                cb.Checked += delegate { showOthers = true; Rebuild(); };
                cb.Unchecked += delegate { showOthers = false; Rebuild(); };
                label.MouseLeftButtonUp += delegate { cb.IsChecked = cb.IsChecked != true; };
                listHost.Children.Add(toggleRow);
                if (showOthers) any |= Group(AppGroup.Other, "Other apps", "Installed from the Store or by your PC's maker. Only remove ones you recognize", false);
            }

            if (!any && query.Length > 0)
            {
                TextBlock empty = Ui.Para("No apps match “" + query + "”.", 14, "Text2");
                empty.Margin = new Thickness(2, 12, 0, 0);
                listHost.Children.Insert(0, empty);
            }
            else if (!any)
            {
                Border done = Ui.Card(Ui.HStack(
                    Ui.Margin(Ui.Icon("", 18, "Good"), 0, 0, 14, 0),
                    Ui.Para("Nothing to remove. None of the preinstalled apps Whittle knows about are on this PC.", 14, "Text1")),
                    new Thickness(20, 18, 20, 18));
                listHost.Children.Insert(0, done);
            }
        }

        bool Group(AppGroup group, string title, string hint, bool allowSelectAll)
        {
            List<AppEntry> list = new List<AppEntry>();
            foreach (AppEntry a in State.Apps) if (a.Group == group && Matches(a)) list.Add(a);
            if (list.Count == 0) return false;

            Button all = null;
            if (allowSelectAll)
            {
                all = Ui.Button("Select all", "Btn.Link", null);
                all.FontSize = 12.5;
                all.VerticalAlignment = VerticalAlignment.Bottom;
                all.Click += delegate
                {
                    bool every = true;
                    foreach (AppEntry a in list) if (!State.AppSelection.Contains(a.Key)) every = false;
                    foreach (AppEntry a in list) State.SetAppSelected(a, !every);
                };
            }
            FrameworkElement header = Ui.SectionHeader(title, Ui.Plural(list.Count, "app", "apps"), all);
            if (listHost.Children.Count == 0) header.Margin = new Thickness(2, 4, 0, 10);
            listHost.Children.Add(header);
            TextBlock h = Ui.Para(hint + ".", 12.5, "Text3");
            h.Margin = new Thickness(2, -6, 0, 10);
            listHost.Children.Add(h);

            StackPanel body = new StackPanel();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) body.Children.Add(Ui.Divider());
                AppRow row = new AppRow(list[i]);
                rows.Add(row);
                body.Children.Add(row.View);
            }
            listHost.Children.Add(Ui.Card(body));
            return true;
        }

        sealed class AppRow
        {
            public readonly AppEntry App;
            public readonly Border View;
            readonly CheckBox check;
            readonly Border bar;
            bool updating;

            public AppRow(AppEntry app)
            {
                App = app;
                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                bar = new Border();
                bar.CornerRadius = new CornerRadius(1.5);
                bar.Margin = new Thickness(0, 1, 0, 1);
                g.Children.Add(bar);

                check = Ui.Check(false);
                check.Margin = new Thickness(13, 0, 12, 0);
                check.VerticalAlignment = VerticalAlignment.Center;
                check.Checked += delegate { if (!updating) AppState.Current.SetAppSelected(App, true); };
                check.Unchecked += delegate { if (!updating) AppState.Current.SetAppSelected(App, false); };
                System.Windows.Automation.AutomationProperties.SetName(check, app.Title);
                Grid.SetColumn(check, 1);
                g.Children.Add(check);

                StackPanel text = new StackPanel();
                text.VerticalAlignment = VerticalAlignment.Center;
                text.Children.Add(Ui.Text(app.Title, 14, "Text1"));
                if (!string.IsNullOrEmpty(app.Description) && app.Group != AppGroup.Other)
                {
                    TextBlock d = Ui.Text(app.Description, 12.5, "Text2");
                    d.Margin = new Thickness(0, 2, 0, 0);
                    text.Children.Add(d);
                }
                Grid.SetColumn(text, 2);
                g.Children.Add(text);

                TextBlock pkg = Ui.Text(string.Join(", ", app.PackageNames.ToArray()), 11.5, "Text3");
                pkg.FontFamily = Ui.MonoFont;
                pkg.MaxWidth = 300;
                pkg.Margin = new Thickness(16, 0, 0, 0);
                pkg.VerticalAlignment = VerticalAlignment.Center;
                pkg.ToolTip = string.Join("\n", app.PackageNames.ToArray());
                Grid.SetColumn(pkg, 3);
                g.Children.Add(pkg);

                View = new Border();
                View.Padding = new Thickness(6, 11, 18, 11);
                View.Background = Brushes.Transparent;
                View.Cursor = Cursors.Hand;
                View.Child = g;
                View.MouseEnter += delegate { View.Background = Ui.Br("SurfaceHover"); };
                View.MouseLeave += delegate { View.Background = Brushes.Transparent; };
                View.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    if (Ui.IsWithin(e.OriginalSource, check)) return;
                    check.IsChecked = check.IsChecked != true;
                };
                Refresh();
            }

            public void Refresh()
            {
                bool on = AppState.Current.AppSelection.Contains(App.Key);
                updating = true;
                check.IsChecked = on;
                updating = false;
                bar.Background = on ? Ui.Br("Bad") : Brushes.Transparent;
            }
        }
    }
}
