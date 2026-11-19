using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using Whittle.Core;
using Whittle.Shell;

namespace Whittle.Pages
{
    public sealed class ProfilesPage : PageBase
    {
        StackPanel savedHost;

        public ProfilesPage(MainWindow win) : base(win) { }

        protected override FrameworkElement Build()
        {
            StackPanel page = new StackPanel();
            page.Children.Add(Ui.PageHeader("Profiles",
                "A profile turns on a set of tweaks and picks apps to remove. Save your own setup as a config to reuse it later or on another PC.",
                Ui.IconButton("", "Import…", "Btn", Import),
                Ui.IconButton("", "Save current setup", "Btn.Primary", SaveCurrent)));

            FrameworkElement h1 = Ui.SectionHeader("Built in", null, null);
            h1.Margin = new Thickness(2, 4, 0, 10);
            page.Children.Add(h1);
            UniformGrid grid = new UniformGrid();
            grid.Columns = 2;
            grid.Margin = new Thickness(-6, -6, -6, 0);
            foreach (Preset p in Presets.All) grid.Children.Add(PresetCard(p));
            page.Children.Add(grid);

            page.Children.Add(Ui.SectionHeader("Your configs", null, null));
            savedHost = new StackPanel();
            page.Children.Add(savedHost);
            return Scroller(page);
        }

        public override void OnShown()
        {
            RenderSaved();
        }

        FrameworkElement PresetCard(Preset p)
        {
            Border card = (Border)HomePage.PresetCard(Win, p);
            Grid g = (Grid)card.Child;
            Grid foot = (Grid)g.Children[g.Children.Count - 1];
            Button details = Ui.Button("See what's included", "Btn.Link", delegate { ShowIncluded(p); });
            details.FontSize = 12;
            details.VerticalAlignment = VerticalAlignment.Center;
            details.Margin = new Thickness(12, 0, 0, 0);
            TextBlock meta = (TextBlock)foot.Children[0];
            foot.Children.RemoveAt(0);
            StackPanel left = Ui.HStack(meta, details);
            left.VerticalAlignment = VerticalAlignment.Center;
            foot.Children.Insert(0, left);
            return card;
        }

        void ShowIncluded(Preset p)
        {
            Border card = MainWindow.DialogCard(520);
            StackPanel sp = new StackPanel();
            sp.Children.Add(Ui.Text(p.Title + " profile", 18, "Text1", FontWeights.SemiBold));
            TextBlock sum = Ui.Para(p.Summary, 13.5, "Text2");
            sum.Margin = new Thickness(0, 6, 0, 14);
            sp.Children.Add(sum);

            StackPanel list = new StackPanel();
            foreach (string cat in Categories.Order)
            {
                List<Tweak> inCat = new List<Tweak>();
                foreach (string id in p.Tweaks)
                {
                    Tweak t = State.Find(id);
                    if (t != null && t.Category == cat) inCat.Add(t);
                }
                if (inCat.Count == 0) continue;
                TextBlock head = Ui.Text(cat.ToUpperInvariant(), 11, "Text3", FontWeights.SemiBold);
                head.Margin = new Thickness(0, list.Children.Count == 0 ? 0 : 12, 0, 4);
                list.Children.Add(head);
                foreach (Tweak t in inCat)
                {
                    bool on = State.StateOf(t) == TweakState.Applied;
                    StackPanel row = Ui.HStack(
                        Ui.Margin(Ui.Icon(on ? "" : "", 11, on ? "Good" : "Text3"), 0, 0, 10, 0),
                        Ui.Text(t.Title, 13.5, on ? "Text2" : "Text1"));
                    row.Margin = new Thickness(0, 2, 0, 2);
                    row.ToolTip = on ? "Already on" : null;
                    list.Children.Add(row);
                }
            }
            if (p.RemoveRecommendedApps)
            {
                TextBlock head = Ui.Text("APPS", 11, "Text3", FontWeights.SemiBold);
                head.Margin = new Thickness(0, 12, 0, 4);
                list.Children.Add(head);
                list.Children.Add(Ui.Para("Selects the apps under “Recommended to remove” on the Apps page. Optional apps are left alone.", 13.5, "Text1"));
            }
            ScrollViewer sv = new ScrollViewer();
            sv.MaxHeight = 380;
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.Content = list;
            Border box = new Border();
            box.Background = Ui.Br("Bg1");
            box.BorderBrush = Ui.Br("Line");
            box.BorderThickness = new Thickness(1);
            box.CornerRadius = new CornerRadius(8);
            box.Padding = new Thickness(14, 12, 6, 12);
            box.Child = sv;
            sp.Children.Add(box);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 22, 0, 0);
            buttons.Children.Add(Ui.Button("Close", "Btn", delegate { Win.PopDialog(); }));
            Button use = Ui.Button("Use profile", "Btn.Primary", delegate { Win.PopDialog(); HomePage.UsePreset(Win, p); });
            use.Margin = new Thickness(8, 0, 0, 0);
            use.IsDefault = true;
            buttons.Children.Add(use);
            sp.Children.Add(buttons);
            card.Child = sp;
            Win.PushDialog(card, delegate { Win.PopDialog(); });
        }

        void RenderSaved()
        {
            if (savedHost == null) return;
            savedHost.Children.Clear();
            List<SavedConfig> configs = ConfigStore.List();
            if (configs.Count == 0)
            {
                StackPanel empty = new StackPanel();
                empty.Children.Add(Ui.Text("No saved configs yet", 14, "Text1"));
                TextBlock hint = Ui.Para("Turn on the tweaks you want and pick apps to remove, then choose Save current setup. Configs are plain files you can copy to another PC and import there.", 13, "Text2");
                hint.Margin = new Thickness(0, 4, 0, 0);
                hint.MaxWidth = 620;
                hint.HorizontalAlignment = HorizontalAlignment.Left;
                empty.Children.Add(hint);
                savedHost.Children.Add(Ui.Card(empty, new Thickness(20, 16, 20, 18)));
                return;
            }
            StackPanel body = new StackPanel();
            for (int i = 0; i < configs.Count; i++)
            {
                if (i > 0) body.Children.Add(Ui.Divider());
                body.Children.Add(ConfigRow(configs[i]));
            }
            savedHost.Children.Add(Ui.Card(body));
        }

        FrameworkElement ConfigRow(SavedConfig c)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border icon = new Border();
            icon.Width = 32;
            icon.Height = 32;
            icon.CornerRadius = new CornerRadius(7);
            icon.Background = Ui.Br("SurfacePressed");
            TextBlock ig = Ui.Icon("", 14, "Text2");
            ig.HorizontalAlignment = HorizontalAlignment.Center;
            icon.Child = ig;
            g.Children.Add(icon);

            StackPanel text = new StackPanel();
            text.Margin = new Thickness(14, 0, 16, 0);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Children.Add(Ui.Text(c.Name, 14, "Text1"));
            TextBlock meta = Ui.Text("Saved " + Ui.When(c.Created) + " · " + Ui.Plural(c.Tweaks.Count, "tweak", "tweaks") + " · " + Ui.Plural(c.Apps.Count, "app", "apps"), 12, "Text3");
            meta.Margin = new Thickness(0, 2, 0, 0);
            text.Children.Add(meta);
            Grid.SetColumn(text, 1);
            g.Children.Add(text);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;
            actions.Children.Add(Ui.Button("Load", "Btn", delegate { LoadConfig(c); }));
            Button export = Ui.IconButton("", null, "Btn.Ghost", delegate { Export(c); });
            export.ToolTip = "Export to a file";
            export.Margin = new Thickness(6, 0, 0, 0);
            actions.Children.Add(export);
            Button del = Ui.IconButton("", null, "Btn.Ghost", delegate { Delete(c); });
            del.ToolTip = "Delete";
            actions.Children.Add(del);
            Grid.SetColumn(actions, 2);
            g.Children.Add(actions);

            Border row = new Border();
            row.Padding = new Thickness(16, 12, 12, 12);
            row.Child = g;
            return row;
        }

        void LoadConfig(SavedConfig c)
        {
            if (!State.StatesReady)
            {
                Win.Toast("Still checking your current settings. Try again in a second.", "");
                return;
            }
            State.SelectConfig(c);
            int missing = 0;
            foreach (string key in c.Apps) if (State.FindApp(key) == null) missing++;
            Win.Navigate("tweaks");
            string extra = missing > 0 ? " " + Ui.Plural(missing, "app in it isn't", "apps in it aren't") + " on this PC." : "";
            Win.Toast("Loaded “" + c.Name + "”. Look over the changes, then choose Review and apply." + extra, "");
        }

        async void SaveCurrent()
        {
            if (!State.StatesReady) return;
            string name = await Win.Prompt("Save current setup",
                "Saves the tweaks that are on or selected, and the apps you've removed or selected, as a config.",
                "My setup", "Save");
            if (name == null) return;
            try
            {
                SavedConfig c = State.CaptureConfig(name);
                ConfigStore.SaveToLibrary(c);
                RenderSaved();
                Win.Toast("Saved “" + name + "” with " + Ui.Plural(c.Tweaks.Count, "tweak", "tweaks") + " and " + Ui.Plural(c.Apps.Count, "app", "apps") + ".", "");
            }
            catch (Exception ex)
            {
                Win.Toast("Couldn't save the config: " + ex.Message, "");
            }
        }

        void Export(SavedConfig c)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Title = "Export config";
            dlg.FileName = ConfigStore.SafeFileName(c.Name) + ConfigStore.Extension;
            dlg.Filter = "Whittle config (*.whittle.json)|*.whittle.json|JSON file (*.json)|*.json";
            dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (dlg.ShowDialog(Win) != true) return;
            try
            {
                File.Copy(c.FilePath, dlg.FileName, true);
                Win.Toast("Exported to " + Path.GetFileName(dlg.FileName) + ".", "");
            }
            catch (Exception ex)
            {
                Win.Toast("Couldn't export: " + ex.Message, "");
            }
        }

        void Import()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "Import config";
            dlg.Filter = "Whittle config (*.whittle.json;*.json)|*.whittle.json;*.json|All files (*.*)|*.*";
            if (dlg.ShowDialog(Win) != true) return;
            try
            {
                SavedConfig c = ConfigStore.Read(dlg.FileName);
                c.FilePath = null;
                ConfigStore.SaveToLibrary(c);
                RenderSaved();
                LoadConfig(c);
            }
            catch (Exception ex)
            {
                Win.Toast("That file couldn't be imported: " + ex.Message, "");
            }
        }

        async void Delete(SavedConfig c)
        {
            bool ok = await Win.Confirm("Delete “" + c.Name + "”?", "This removes the saved config. It doesn't change anything on your PC.", "Delete", true);
            if (!ok) return;
            try
            {
                File.Delete(c.FilePath);
            }
            catch (Exception ex)
            {
                Win.Toast("Couldn't delete it: " + ex.Message, "");
            }
            RenderSaved();
        }
    }
}
