using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Whittle.Core;
using Whittle.Shell;

namespace Whittle.Pages
{
    public sealed class TweaksPage : PageBase
    {
        string filter;
        string query = "";
        StackPanel listHost;
        WrapPanel pills;
        readonly Dictionary<string, TweakRow> rows = new Dictionary<string, TweakRow>(StringComparer.OrdinalIgnoreCase);

        public TweaksPage(MainWindow win) : base(win)
        {
            State.StatesChanged += delegate { if (IsBuilt) Rebuild(); };
            State.SelectionChanged += RefreshRows;
        }

        protected override FrameworkElement Build()
        {
            StackPanel page = new StackPanel();
            page.Children.Add(Ui.PageHeader("Tweaks",
                "Turn on what you want changed. Turning a tweak back off restores the setting it replaced.",
                Ui.SearchBox("Search tweaks", 240, delegate(string q) { query = q.Trim(); Rebuild(); })));
            pills = new WrapPanel();
            pills.Margin = new Thickness(0, 0, 0, 4);
            page.Children.Add(pills);
            listHost = new StackPanel();
            page.Children.Add(listHost);
            Rebuild();
            return Scroller(page);
        }

        bool Visible(Tweak t)
        {
            return State.Settings.ShowAdvanced || t.Risk != Risk.Advanced || State.IsDesired(t);
        }

        bool Matches(Tweak t)
        {
            if (query.Length == 0) return true;
            return t.Title.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                   t.Description.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                   t.Category.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        public void Rebuild()
        {
            if (listHost == null) return;
            rows.Clear();
            listHost.Children.Clear();
            BuildPills();
            if (!State.StatesReady)
            {
                listHost.Children.Add(Ui.Loading("Checking your current settings…"));
                return;
            }

            bool first = true;
            foreach (string cat in Categories.Order)
            {
                if (filter != null && filter != cat && query.Length == 0) continue;
                List<Tweak> list = new List<Tweak>();
                foreach (Tweak t in State.Tweaks) if (t.Category == cat && Visible(t) && Matches(t)) list.Add(t);
                if (list.Count == 0) continue;
                int on = 0;
                foreach (Tweak t in list) if (State.StateOf(t) == TweakState.Applied) on++;
                FrameworkElement header = Ui.SectionHeader(cat, on + " of " + list.Count + " on", null);
                if (first) header.Margin = new Thickness(2, 14, 0, 10);
                first = false;
                listHost.Children.Add(header);
                StackPanel body = new StackPanel();
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) body.Children.Add(Ui.Divider());
                    TweakRow row = new TweakRow(list[i]);
                    rows[list[i].Id] = row;
                    body.Children.Add(row.View);
                }
                listHost.Children.Add(Ui.Card(body));
            }
            if (first)
            {
                TextBlock empty = Ui.Para(query.Length > 0 ? "No tweaks match “" + query + "”." : "Nothing to show.", 14, "Text2");
                empty.Margin = new Thickness(2, 24, 0, 0);
                listHost.Children.Add(empty);
            }
        }

        void BuildPills()
        {
            pills.Children.Clear();
            pills.Children.Add(Pill(null, "All", CountVisible(null)));
            foreach (string cat in Categories.Order)
            {
                int n = CountVisible(cat);
                if (n > 0) pills.Children.Add(Pill(cat, cat, n));
            }
        }

        int CountVisible(string cat)
        {
            int n = 0;
            foreach (Tweak t in State.Tweaks) if ((cat == null || t.Category == cat) && Visible(t)) n++;
            return n;
        }

        RadioButton Pill(string cat, string label, int count)
        {
            RadioButton rb = new RadioButton();
            rb.Style = Ui.St("Pill");
            rb.GroupName = "tweakfilter";
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            TextBlock name = new TextBlock();
            name.Text = label;
            name.FontSize = 12.5;
            sp.Children.Add(name);
            TextBlock c = Ui.Text(count.ToString(), 12, "Text3");
            c.Margin = new Thickness(6, 0, 0, 0);
            sp.Children.Add(c);
            rb.Content = sp;
            rb.IsChecked = filter == cat;
            rb.Checked += delegate
            {
                if (filter == cat) return;
                filter = cat;
                Rebuild();
            };
            return rb;
        }

        void RefreshRows()
        {
            foreach (TweakRow r in rows.Values) r.Refresh();
        }

        public void ShowCategory(string cat)
        {
            filter = cat;
            if (IsBuilt) Rebuild();
        }

        /// <summary>One tweak: text, badges, state and the switch.</summary>
        sealed class TweakRow
        {
            public readonly Tweak T;
            public readonly FrameworkElement View;
            readonly CheckBox toggle;
            readonly Border bar;
            readonly Border status;
            readonly TextBlock statusText;
            readonly StackPanel details;
            bool updating;

            AppState State { get { return AppState.Current; } }

            public TweakRow(Tweak t)
            {
                T = t;

                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                bar = new Border();
                bar.CornerRadius = new CornerRadius(1.5);
                bar.Margin = new Thickness(0, 2, 0, 2);
                g.Children.Add(bar);

                StackPanel text = new StackPanel();
                text.Margin = new Thickness(15, 0, 24, 0);
                WrapPanel titleLine = new WrapPanel();
                TextBlock title = Ui.Text(t.Title, 14, "Text1");
                title.Margin = new Thickness(0, 0, 10, 0);
                title.VerticalAlignment = VerticalAlignment.Center;
                titleLine.Children.Add(title);
                if (t.Risk == Risk.Tradeoff) titleLine.Children.Add(Spaced(Ui.Badge("Trade-off", Tone.Warn)));
                if (t.Risk == Risk.Advanced) titleLine.Children.Add(Spaced(Ui.Badge("Advanced", Tone.Bad)));
                if (t.NeedsRestart) titleLine.Children.Add(Spaced(Ui.Badge("Restart", Tone.Neutral)));
                status = Ui.Badge("", Tone.Accent);
                statusText = (TextBlock)status.Child;
                titleLine.Children.Add(Spaced(status));
                text.Children.Add(titleLine);

                TextBlock desc = Ui.Para(t.Description, 13, "Text2");
                desc.Margin = new Thickness(0, 4, 0, 0);
                desc.MaxWidth = 720;
                desc.HorizontalAlignment = HorizontalAlignment.Left;
                text.Children.Add(desc);

                string note = t.Note;
                if (t.Id == "perf.power-plan" && SysInfo.HasBattery)
                {
                    note = "This PC has a battery. The plan drains it faster, so it's best used while plugged in.";
                }
                if (!string.IsNullOrEmpty(note))
                {
                    DockPanel n = new DockPanel();
                    n.Margin = new Thickness(0, 7, 0, 0);
                    TextBlock ic = Ui.Icon("", 12, t.Risk == Risk.Safe ? "Text3" : "Warn");
                    ic.VerticalAlignment = VerticalAlignment.Top;
                    ic.Margin = new Thickness(0, 2, 8, 0);
                    n.Children.Add(ic);
                    TextBlock nt = Ui.Para(note, 12.5, t.Risk == Risk.Safe ? "Text3" : "Text2");
                    nt.MaxWidth = 700;
                    nt.HorizontalAlignment = HorizontalAlignment.Left;
                    n.Children.Add(nt);
                    text.Children.Add(n);
                }

                details = new StackPanel();
                details.Visibility = Visibility.Collapsed;
                details.Margin = new Thickness(0, 10, 0, 0);
                text.Children.Add(details);
                Grid.SetColumn(text, 1);
                g.Children.Add(text);

                // Expand button that lists exactly what the tweak writes, next to the switch.
                Button more = Ui.IconButton("", null, "Btn.Ghost", null);
                more.Width = 30;
                more.Height = 28;
                more.Padding = new Thickness(0);
                more.Margin = new Thickness(0, -3, 10, 0);
                more.VerticalAlignment = VerticalAlignment.Top;
                more.ToolTip = "What it changes";
                System.Windows.Automation.AutomationProperties.SetName(more, "What " + t.Title + " changes");
                more.Click += delegate
                {
                    if (details.Children.Count == 0) FillDetails();
                    bool show = details.Visibility != Visibility.Visible;
                    details.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                    ((TextBlock)((StackPanel)more.Content).Children[0]).Text = show ? "" : "";
                    more.ToolTip = show ? "Hide details" : "What it changes";
                };

                toggle = Ui.Switch(false);
                toggle.VerticalAlignment = VerticalAlignment.Top;
                toggle.Margin = new Thickness(0, -1, 0, 0);
                toggle.Checked += delegate { if (!updating) State.SetDesired(T, true); };
                toggle.Unchecked += delegate { if (!updating) State.SetDesired(T, false); };
                System.Windows.Automation.AutomationProperties.SetName(toggle, t.Title);
                StackPanel controls = Ui.HStack(more, toggle);
                controls.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(controls, 2);
                g.Children.Add(controls);

                Border wrap = new Border();
                wrap.Padding = new Thickness(6, 14, 18, 14);
                wrap.Background = Brushes.Transparent;
                wrap.Child = g;
                View = wrap;
                Refresh();
            }

            static FrameworkElement Spaced(FrameworkElement e)
            {
                e.Margin = new Thickness(0, 1, 6, 1);
                return e;
            }

            void FillDetails()
            {
                StackPanel lines = new StackPanel();
                foreach (TweakAction a in T.Actions)
                {
                    TextBlock line = new TextBlock();
                    line.Text = a.Describe();
                    line.FontFamily = Ui.MonoFont;
                    line.FontSize = 11.5;
                    line.Foreground = Ui.Br("Text2");
                    line.TextWrapping = TextWrapping.Wrap;
                    line.Margin = new Thickness(0, 1, 0, 1);
                    lines.Children.Add(line);
                }
                TextBlock foot = Ui.Para("Whittle saves the current value of each of these before changing it.", 11.5, "Text3");
                foot.Margin = new Thickness(0, 8, 0, 0);
                lines.Children.Add(foot);
                Border box = new Border();
                box.Background = Ui.Br("Bg1");
                box.BorderBrush = Ui.Br("Line");
                box.BorderThickness = new Thickness(1);
                box.CornerRadius = new CornerRadius(6);
                box.Padding = new Thickness(12, 9, 12, 10);
                box.Child = lines;
                details.Children.Add(box);
            }

            public void Refresh()
            {
                TweakState s = State.StateOf(T);
                bool desired = State.IsDesired(T);
                updating = true;
                toggle.IsChecked = desired;
                toggle.IsEnabled = s != TweakState.Unavailable;
                updating = false;

                if (State.WillApply(T))
                {
                    Show("Will turn on", Tone.Accent);
                    bar.Background = Ui.Br("Accent");
                }
                else if (State.WillRevert(T))
                {
                    Show("Will undo", Tone.Neutral);
                    bar.Background = Ui.Br("Text3");
                }
                else
                {
                    bar.Background = Brushes.Transparent;
                    if (s == TweakState.Partial) Show("Partly on", Tone.Warn);
                    else if (s == TweakState.Unavailable) Show("Not available on this PC", Tone.Neutral);
                    else status.Visibility = Visibility.Collapsed;
                }
            }

            void Show(string text, Tone tone)
            {
                Border fresh = Ui.Badge(text, tone);
                status.Background = fresh.Background;
                statusText.Foreground = ((TextBlock)fresh.Child).Foreground;
                statusText.Text = text;
                status.Visibility = Visibility.Visible;
            }
        }
    }
}
