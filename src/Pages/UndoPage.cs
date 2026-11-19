using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Whittle.Core;
using Whittle.Shell;

namespace Whittle.Pages
{
    public sealed class UndoPage : PageBase
    {
        StackPanel changesHost;
        StackPanel appsHost;
        TextBlock restoreLine;
        Button createPoint;
        bool creating;

        public UndoPage(MainWindow win) : base(win)
        {
            State.StatesChanged += delegate { if (IsBuilt) RenderChanges(); };
            State.AppsChanged += delegate { if (IsBuilt) RenderApps(); };
        }

        protected override FrameworkElement Build()
        {
            StackPanel page = new StackPanel();
            page.Children.Add(Ui.PageHeader("Undo & restore",
                "Whittle saves the original value of everything it changes, so any change can be put back exactly as it was."));

            // System Restore
            Grid rp = new Grid();
            rp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rp.ColumnDefinitions.Add(new ColumnDefinition());
            rp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border ic = new Border();
            ic.Width = 36;
            ic.Height = 36;
            ic.CornerRadius = new CornerRadius(8);
            ic.Background = Ui.Br("AccentSoft");
            ic.VerticalAlignment = VerticalAlignment.Top;
            TextBlock ig = Ui.Icon("", 16, "Accent");
            ig.HorizontalAlignment = HorizontalAlignment.Center;
            ic.Child = ig;
            rp.Children.Add(ic);
            StackPanel rpText = new StackPanel();
            rpText.Margin = new Thickness(16, 0, 16, 0);
            rpText.Children.Add(Ui.Text("System Restore", 14.5, "Text1", FontWeights.SemiBold));
            TextBlock rpDesc = Ui.Para("A restore point lets you roll back the whole system if something ever goes wrong, not just Whittle's changes.", 13, "Text2");
            rpDesc.Margin = new Thickness(0, 3, 0, 6);
            rpDesc.MaxWidth = 560;
            rpDesc.HorizontalAlignment = HorizontalAlignment.Left;
            rpText.Children.Add(rpDesc);
            restoreLine = Ui.Text("Checking restore points…", 12.5, "Text3");
            rpText.Children.Add(restoreLine);
            Grid.SetColumn(rpText, 1);
            rp.Children.Add(rpText);
            StackPanel rpButtons = new StackPanel();
            rpButtons.Orientation = Orientation.Horizontal;
            rpButtons.VerticalAlignment = VerticalAlignment.Center;
            Button open = Ui.IconButton("", "Open System Restore", "Btn", OpenSystemRestore);
            createPoint = Ui.Button("Create restore point", "Btn", CreateRestorePoint);
            createPoint.Margin = new Thickness(8, 0, 0, 0);
            if (Win.ReadOnly) createPoint.IsEnabled = false;
            rpButtons.Children.Add(open);
            rpButtons.Children.Add(createPoint);
            Grid.SetColumn(rpButtons, 2);
            rp.Children.Add(rpButtons);
            page.Children.Add(Ui.Card(rp, new Thickness(18, 18, 18, 18)));

            changesHost = new StackPanel();
            page.Children.Add(changesHost);
            appsHost = new StackPanel();
            page.Children.Add(appsHost);

            FrameworkElement logHeader = Ui.SectionHeader("Activity log", null, null);
            page.Children.Add(logHeader);
            Grid logRow = new Grid();
            logRow.ColumnDefinitions.Add(new ColumnDefinition());
            logRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock logText = Ui.Para("Every run is written to a daily log file, including the exact registry values, services and tasks that changed.", 13, "Text2");
            logText.VerticalAlignment = VerticalAlignment.Center;
            logRow.Children.Add(logText);
            Button openLog = Ui.IconButton("", "Open log folder", "Btn", delegate { OpenFolder(AppPaths.Logs); });
            openLog.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(openLog, 1);
            logRow.Children.Add(openLog);
            page.Children.Add(Ui.Card(logRow, new Thickness(18, 14, 14, 14)));

            RenderChanges();
            RenderApps();
            return Scroller(page);
        }

        public override void OnShown()
        {
            RenderChanges();
            RenderApps();
            LoadRestorePoints();
        }

        async void LoadRestorePoints()
        {
            List<RestorePointInfo> points = await Task.Run(new Func<List<RestorePointInfo>>(SysRestore.List));
            if (points == null)
            {
                restoreLine.Text = Win.ReadOnly ? "Restore points can only be listed when Whittle runs as administrator." : "Couldn't read restore points.";
            }
            else if (points.Count == 0)
            {
                restoreLine.Text = "No restore points yet.";
            }
            else
            {
                restoreLine.Text = "Latest: " + Ui.When(points[0].Created) + " — " + points[0].Description + "  ·  " + Ui.Plural(points.Count, "restore point", "restore points") + " in total";
            }
        }

        void RenderChanges()
        {
            if (changesHost == null) return;
            changesHost.Children.Clear();
            Dictionary<string, DateTime> applied = State.Journal.AppliedTweaks();
            List<KeyValuePair<Tweak, DateTime>> list = new List<KeyValuePair<Tweak, DateTime>>();
            foreach (KeyValuePair<string, DateTime> kv in applied)
            {
                Tweak t = State.Find(kv.Key) ?? Catalog.Find(kv.Key);
                if (t != null) list.Add(new KeyValuePair<Tweak, DateTime>(t, kv.Value));
            }
            list.Sort(delegate(KeyValuePair<Tweak, DateTime> a, KeyValuePair<Tweak, DateTime> b) { return b.Value.CompareTo(a.Value); });

            Button undoAll = null;
            if (list.Count > 1)
            {
                undoAll = Ui.Button("Undo all", "Btn.Danger", delegate { UndoAll(list); });
                undoAll.Height = 30;
                undoAll.IsEnabled = !Win.ReadOnly;
            }
            changesHost.Children.Add(Ui.SectionHeader("Changes made by Whittle", list.Count > 0 ? list.Count.ToString() : null, undoAll));

            if (list.Count == 0)
            {
                changesHost.Children.Add(Ui.Card(Ui.Para("Nothing yet. Tweaks you apply will appear here, ready to undo.", 13.5, "Text2"), new Thickness(18, 15, 18, 15)));
                return;
            }
            StackPanel body = new StackPanel();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) body.Children.Add(Ui.Divider());
                Tweak t = list[i].Key;
                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                StackPanel text = new StackPanel();
                text.VerticalAlignment = VerticalAlignment.Center;
                text.Children.Add(Ui.Text(t.Title, 14, "Text1"));
                string sub = t.Category + " · applied " + Ui.When(list[i].Value);
                TweakState s = State.StateOf(t);
                if (State.StatesReady && s != TweakState.Applied) sub += " · changed since then";
                TextBlock m = Ui.Text(sub, 12, "Text3");
                m.Margin = new Thickness(0, 2, 0, 0);
                text.Children.Add(m);
                g.Children.Add(text);
                Tweak captured = t;
                Button undo = Ui.IconButton("", "Undo", "Btn", delegate { UndoOne(captured); });
                undo.IsEnabled = !Win.ReadOnly && !State.Busy;
                Grid.SetColumn(undo, 1);
                g.Children.Add(undo);
                Border row = new Border();
                row.Padding = new Thickness(18, 11, 14, 11);
                row.Child = g;
                body.Children.Add(row);
            }
            changesHost.Children.Add(Ui.Card(body));
        }

        void RenderApps()
        {
            if (appsHost == null) return;
            appsHost.Children.Clear();
            List<RemovedApp> removed = State.Journal.RemovedApps();
            // Several packages can belong to one app; show each app title once.
            Dictionary<string, RemovedApp> byTitle = new Dictionary<string, RemovedApp>(StringComparer.CurrentCultureIgnoreCase);
            foreach (RemovedApp r in removed)
            {
                RemovedApp existing;
                if (!byTitle.TryGetValue(r.Title, out existing) || r.When > existing.When) byTitle[r.Title] = r;
            }
            List<RemovedApp> list = new List<RemovedApp>(byTitle.Values);
            list.Sort(delegate(RemovedApp a, RemovedApp b) { return b.When.CompareTo(a.When); });

            appsHost.Children.Add(Ui.SectionHeader("Removed apps", list.Count > 0 ? list.Count.ToString() : null, null));
            if (list.Count == 0)
            {
                appsHost.Children.Add(Ui.Card(Ui.Para("No apps removed yet. Apps you remove are listed here with a link back to the Microsoft Store.", 13.5, "Text2"), new Thickness(18, 15, 18, 15)));
                return;
            }
            StackPanel body = new StackPanel();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) body.Children.Add(Ui.Divider());
                RemovedApp a = list[i];
                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                StackPanel text = new StackPanel();
                text.VerticalAlignment = VerticalAlignment.Center;
                text.Children.Add(Ui.Text(a.Title, 14, "Text1"));
                TextBlock m = Ui.Text("Removed " + Ui.When(a.When) + " · " + a.Name, 12, "Text3");
                m.Margin = new Thickness(0, 2, 0, 0);
                text.Children.Add(m);
                g.Children.Add(text);
                RemovedApp captured = a;
                Button get = Ui.IconButton("", "Get it back", "Btn", delegate { OpenStore(captured); });
                get.ToolTip = "Opens the app's page in the Microsoft Store";
                Grid.SetColumn(get, 1);
                g.Children.Add(get);
                Border row = new Border();
                row.Padding = new Thickness(18, 11, 14, 11);
                row.Child = g;
                body.Children.Add(row);
            }
            appsHost.Children.Add(Ui.Card(body));
        }

        async void UndoOne(Tweak t)
        {
            bool ok = await Win.Confirm("Undo “" + t.Title + "”?",
                "Whittle puts back the values it saved before making this change." + (t.NeedsRestart ? " A restart finishes the job." : ""),
                "Undo", false);
            if (!ok) return;
            ChangePlan plan = new ChangePlan();
            plan.Revert.Add(t);
            plan.CreateRestorePoint = false;
            Win.RunPlan(plan, "Undoing change");
        }

        async void UndoAll(List<KeyValuePair<Tweak, DateTime>> list)
        {
            bool ok = await Win.Confirm("Undo all " + list.Count + " changes?",
                "Whittle restores every value it saved, putting these settings back the way they were before you used it. Removed apps aren't reinstalled.",
                "Undo all", true);
            if (!ok) return;
            ChangePlan plan = new ChangePlan();
            foreach (KeyValuePair<Tweak, DateTime> kv in list) plan.Revert.Add(kv.Key);
            plan.CreateRestorePoint = false;
            Win.RunPlan(plan, "Undoing changes");
        }

        async void CreateRestorePoint()
        {
            if (creating) return;
            creating = true;
            createPoint.IsEnabled = false;
            createPoint.Content = "Creating…";
            string problem = await Task.Run(new Func<string>(delegate { return SysRestore.Create("Whittle: manual restore point"); }));
            creating = false;
            createPoint.IsEnabled = true;
            createPoint.Content = "Create restore point";
            if (problem == null) Win.Toast("Restore point created.", "");
            else Win.Toast("Windows couldn't create a restore point: " + problem, "");
            LoadRestorePoints();
        }

        void OpenSystemRestore()
        {
            try { Process.Start(Path.Combine(Proc.System32, "rstrui.exe")); }
            catch (Exception ex) { Win.Toast("Couldn't open System Restore: " + ex.Message, ""); }
        }

        void OpenStore(RemovedApp a)
        {
            string uri = a.Family.Length > 0
                ? "ms-windows-store://pdp/?PFN=" + Uri.EscapeDataString(a.Family)
                : "ms-windows-store://search/?query=" + Uri.EscapeDataString(a.Title);
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
            catch (Exception ex) { Win.Toast("Couldn't open the Microsoft Store: " + ex.Message, ""); }
        }

        public void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Win.Toast("Couldn't open the folder: " + ex.Message, "");
            }
        }
    }
}
