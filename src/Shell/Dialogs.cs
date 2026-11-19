using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Whittle.Core;

namespace Whittle.Shell
{
    public sealed partial class MainWindow
    {
        readonly List<Action> escapeHandlers = new List<Action>();

        // ------------------------------------------------------------------ Dialog stack

        public static Border DialogCard(double width)
        {
            Border card = new Border();
            card.Width = width;
            card.Background = Ui.Br("Elevated");
            card.BorderBrush = Ui.Br("LineStrong");
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(12);
            card.Padding = new Thickness(26, 24, 26, 22);
            card.HorizontalAlignment = HorizontalAlignment.Center;
            card.VerticalAlignment = VerticalAlignment.Center;
            card.Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 10, Direction = 270, Opacity = 0.5, Color = Colors.Black };
            return card;
        }

        public void PushDialog(FrameworkElement card, Action onEscape)
        {
            // Toasts sit above everything; clear them so they never cover a dialog's buttons.
            toasts.Children.Clear();
            foreach (UIElement e in dialogLayer.Children) e.Visibility = Visibility.Hidden;
            dialogLayer.Children.Add(card);
            escapeHandlers.Add(onEscape);
            if (overlay.Visibility != Visibility.Visible)
            {
                overlay.Visibility = Visibility.Visible;
                overlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
            }
            ScaleTransform st = new ScaleTransform(0.97, 0.97);
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            card.RenderTransform = st;
            DoubleAnimation grow = new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(170));
            grow.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
            Dispatcher.BeginInvoke(new Action(delegate
            {
                IInputElement first = FindDefault(card);
                if (first != null) Keyboard.Focus(first);
            }), DispatcherPriority.Input);
        }

        static IInputElement FindDefault(DependencyObject d)
        {
            Button b = d as Button;
            if (b != null && b.IsDefault && b.IsEnabled) return b;
            TextBox t = d as TextBox;
            if (t != null) return t;
            int n = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < n; i++)
            {
                IInputElement r = FindDefault(VisualTreeHelper.GetChild(d, i));
                if (r != null) return r;
            }
            return null;
        }

        public void PopDialog()
        {
            if (dialogLayer.Children.Count == 0) return;
            dialogLayer.Children.RemoveAt(dialogLayer.Children.Count - 1);
            if (escapeHandlers.Count > 0) escapeHandlers.RemoveAt(escapeHandlers.Count - 1);
            if (dialogLayer.Children.Count > 0)
            {
                dialogLayer.Children[dialogLayer.Children.Count - 1].Visibility = Visibility.Visible;
            }
            else
            {
                overlay.Visibility = Visibility.Collapsed;
            }
        }

        public bool DialogOpen { get { return dialogLayer.Children.Count > 0; } }

        static StackPanel DialogHeader(string glyph, string brush, string title, string body)
        {
            StackPanel sp = new StackPanel();
            if (glyph != null)
            {
                TextBlock ic = Ui.Icon(glyph, 22, brush);
                ic.HorizontalAlignment = HorizontalAlignment.Left;
                ic.Margin = new Thickness(0, 0, 0, 14);
                sp.Children.Add(ic);
            }
            TextBlock t = Ui.Text(title, 18, "Text1", FontWeights.SemiBold);
            t.TextWrapping = TextWrapping.Wrap;
            sp.Children.Add(t);
            if (!string.IsNullOrEmpty(body))
            {
                TextBlock b = Ui.Para(body, 13.5, "Text2");
                b.Margin = new Thickness(0, 8, 0, 0);
                sp.Children.Add(b);
            }
            return sp;
        }

        static StackPanel DialogButtons(params Button[] buttons)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.HorizontalAlignment = HorizontalAlignment.Right;
            sp.Margin = new Thickness(0, 24, 0, 0);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (i > 0) buttons[i].Margin = new Thickness(8, 0, 0, 0);
                sp.Children.Add(buttons[i]);
            }
            return sp;
        }

        public Task<bool> Confirm(string title, string body, string okText, bool danger)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            Border card = DialogCard(460);
            StackPanel sp = DialogHeader(danger ? "" : null, danger ? "Warn" : "Text2", title, body);
            Button cancel = Ui.Button("Cancel", "Btn", delegate { PopDialog(); tcs.TrySetResult(false); });
            Button ok = Ui.Button(okText, danger ? "Btn.Danger" : "Btn.Primary", delegate { PopDialog(); tcs.TrySetResult(true); });
            ok.IsDefault = true;
            cancel.IsCancel = false;
            sp.Children.Add(DialogButtons(cancel, ok));
            card.Child = sp;
            PushDialog(card, delegate { PopDialog(); tcs.TrySetResult(false); });
            return tcs.Task;
        }

        public Task<string> Prompt(string title, string body, string initial, string okText)
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            Border card = DialogCard(460);
            StackPanel sp = DialogHeader(null, null, title, body);
            TextBox box = Ui.Field("");
            box.Text = initial ?? "";
            box.Margin = new Thickness(0, 16, 0, 0);
            box.SelectAll();
            sp.Children.Add(box);
            Button cancel = Ui.Button("Cancel", "Btn", delegate { PopDialog(); tcs.TrySetResult(null); });
            Button ok = Ui.Button(okText, "Btn.Primary", null);
            ok.Click += delegate
            {
                string v = box.Text.Trim();
                if (v.Length == 0) { box.Focus(); return; }
                PopDialog();
                tcs.TrySetResult(v);
            };
            ok.IsDefault = true;
            box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { ok.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
            };
            sp.Children.Add(DialogButtons(cancel, ok));
            card.Child = sp;
            PushDialog(card, delegate { PopDialog(); tcs.TrySetResult(null); });
            Dispatcher.BeginInvoke(new Action(delegate { box.Focus(); box.SelectAll(); }), DispatcherPriority.Input);
            return tcs.Task;
        }

        public void Toast(string message, string glyph)
        {
            Border t = new Border();
            t.Background = Ui.Br("Elevated");
            t.BorderBrush = Ui.Br("LineStrong");
            t.BorderThickness = new Thickness(1);
            t.CornerRadius = new CornerRadius(8);
            t.Padding = new Thickness(14, 10, 16, 11);
            t.Margin = new Thickness(0, 8, 0, 0);
            t.MaxWidth = 420;
            t.HorizontalAlignment = HorizontalAlignment.Right;
            t.Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = 0.45, Color = Colors.Black };
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            if (glyph != null)
            {
                TextBlock ic = Ui.Icon(glyph, 14, glyph == "" ? "Warn" : "Accent");
                ic.Margin = new Thickness(0, 1, 10, 0);
                ic.VerticalAlignment = VerticalAlignment.Top;
                sp.Children.Add(ic);
            }
            TextBlock tx = Ui.Para(message, 13, "Text1");
            tx.MaxWidth = 360;
            sp.Children.Add(tx);
            t.Child = sp;
            toasts.Children.Add(t);
            Ui.FadeIn(t);
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(Math.Min(7, 2.8 + message.Length / 40.0));
            timer.Tick += delegate
            {
                timer.Stop();
                DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                fade.Completed += delegate { toasts.Children.Remove(t); };
                t.BeginAnimation(UIElement.OpacityProperty, fade);
            };
            timer.Start();
        }

        // ------------------------------------------------------------------ Review and apply

        public void ReviewAndApply()
        {
            if (ReadOnly)
            {
                Toast("Run Whittle as administrator to apply changes.", "");
                return;
            }
            ChangePlan plan = State.BuildPlan();
            if (plan.Count == 0) return;

            Border card = DialogCard(560);
            StackPanel sp = new StackPanel();
            sp.Children.Add(DialogHeader(null, null, "Review changes",
                "Nothing has changed yet. Every change is recorded so you can undo it later from Undo & restore."));

            StackPanel list = new StackPanel();
            if (plan.Apply.Count > 0)
            {
                list.Children.Add(ReviewGroup("Turn on", plan.Apply.Count));
                foreach (Tweak t in plan.Apply) list.Children.Add(ReviewRow("", "Accent", t.Title, t.NeedsRestart, t.Risk));
            }
            if (plan.Revert.Count > 0)
            {
                list.Children.Add(ReviewGroup("Undo", plan.Revert.Count));
                foreach (Tweak t in plan.Revert) list.Children.Add(ReviewRow("", "Text2", t.Title, t.NeedsRestart, Risk.Safe));
            }
            if (plan.RemoveApps.Count > 0)
            {
                list.Children.Add(ReviewGroup("Remove apps", plan.RemoveApps.Count));
                foreach (AppEntry a in plan.RemoveApps)
                {
                    list.Children.Add(ReviewRow("", "Bad", a.Title, false, a.Group == AppGroup.Caution ? Risk.Advanced : Risk.Safe));
                }
            }
            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.MaxHeight = 330;
            sv.Content = list;
            Border box = new Border();
            box.Margin = new Thickness(0, 18, 0, 0);
            box.BorderBrush = Ui.Br("Line");
            box.BorderThickness = new Thickness(1);
            box.CornerRadius = new CornerRadius(8);
            box.Background = Ui.Br("Bg1");
            box.Padding = new Thickness(14, 4, 6, 10);
            box.Child = sv;
            sp.Children.Add(box);

            CheckBox restore = Ui.Check(State.Settings.CreateRestorePoint);
            StackPanel rp = Ui.HStack(restore, Ui.Margin(Ui.Text("Create a restore point first", 13.5, "Text1"), 8, 0, 0, 1));
            ((FrameworkElement)rp.Children[1]).VerticalAlignment = VerticalAlignment.Center;
            rp.Margin = new Thickness(0, 16, 0, 0);
            rp.Cursor = Cursors.Hand;
            rp.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.OriginalSource is TextBlock) restore.IsChecked = restore.IsChecked != true;
            };
            sp.Children.Add(rp);
            TextBlock rpHint = Ui.Para("Recommended. Adds about a minute, and turns on System Restore for your system drive if it's off.", 12, "Text3");
            rpHint.Margin = new Thickness(30, 2, 0, 0);
            sp.Children.Add(rpHint);

            if (plan.NeedsRestart)
            {
                TextBlock note = Ui.Para("Some of these changes take effect after you restart your PC.", 12.5, "Text2");
                note.Margin = new Thickness(0, 14, 0, 0);
                sp.Children.Add(note);
            }

            Button cancel = Ui.Button("Cancel", "Btn", delegate { PopDialog(); });
            Button go = Ui.Button("Apply " + Ui.Plural(plan.Count, "change", "changes"), "Btn.Primary", null);
            go.IsDefault = true;
            go.Click += delegate
            {
                plan.CreateRestorePoint = restore.IsChecked == true;
                PopDialog();
                RunPlan(plan, "Applying changes");
            };
            sp.Children.Add(DialogButtons(cancel, go));
            card.Child = sp;
            PushDialog(card, delegate { PopDialog(); });
        }

        static FrameworkElement ReviewGroup(string title, int count)
        {
            TextBlock t = Ui.Text(title.ToUpperInvariant() + "  ·  " + count, 11, "Text3", FontWeights.SemiBold);
            t.Margin = new Thickness(0, 12, 0, 6);
            return t;
        }

        static FrameworkElement ReviewRow(string glyph, string brush, string text, bool restart, Risk risk)
        {
            DockPanel row = new DockPanel();
            row.Margin = new Thickness(0, 3, 0, 3);
            TextBlock ic = Ui.Icon(glyph, 12, brush);
            ic.Width = 22;
            row.Children.Add(ic);
            if (restart)
            {
                Border b = Ui.Badge("Restart", Tone.Neutral);
                b.Margin = new Thickness(8, 0, 0, 0);
                DockPanel.SetDock(b, Dock.Right);
                row.Children.Add(b);
            }
            if (risk == Risk.Advanced)
            {
                Border b = Ui.Badge("Advanced", Tone.Bad);
                b.Margin = new Thickness(8, 0, 0, 0);
                DockPanel.SetDock(b, Dock.Right);
                row.Children.Add(b);
            }
            row.Children.Add(Ui.Text(text, 13.5, "Text1"));
            return row;
        }

        /// <summary>Runs a plan with a live progress dialog, then refreshes everything.</summary>
        public async void RunPlan(ChangePlan plan, string title)
        {
            State.Busy = true;
            UpdatePendingBar();
            ProgressDialog dlg = new ProgressDialog(title);
            PushDialog(dlg.Card, null);

            Action<string, LogLevel> onLog = delegate(string m, LogLevel l)
            {
                Dispatcher.BeginInvoke(new Action(delegate { dlg.AddLine(m, l); }));
            };
            Log.Written += onLog;
            Engine engine = new Engine(State.Journal, State.Settings);
            engine.Progress += delegate(int done, int total, string label)
            {
                Dispatcher.BeginInvoke(new Action(delegate { dlg.SetProgress(done, total, label); }));
            };
            engine.ContinueWithoutRestorePoint = delegate(string problem)
            {
                Task<bool> answer = (Task<bool>)Dispatcher.Invoke(new Func<Task<bool>>(delegate
                {
                    return Confirm("Continue without a restore point?",
                        "Windows couldn't create one: " + problem.TrimEnd('.') + ". Whittle still records every change so you can undo it.",
                        "Continue", false);
                }));
                return answer.Result;
            };

            RunResult result = null;
            Exception error = null;
            try
            {
                result = await Task.Run(new Func<RunResult>(delegate { return engine.Run(plan); }));
            }
            catch (Exception ex)
            {
                error = ex;
                Log.Error("Unexpected error: " + ex);
            }
            Log.Written -= onLog;

            Dictionary<string, TweakState> states = await Task.Run(new Func<Dictionary<string, TweakState>>(State.DetectAll));
            State.Busy = false;
            State.SetStates(states);
            if (plan.RemoveApps.Count > 0)
            {
                State.AppSelection.Clear();
                ScanApps();
            }
            dlg.Finish(result, error,
                delegate { PopDialog(); UpdatePendingBar(); },
                delegate { RestartPc(); });
        }

#if DEV
        /// <summary>Test build only: shows the progress dialog with sample content for screenshots.</summary>
        public void DemoProgress(bool finished)
        {
            ProgressDialog dlg = new ProgressDialog("Applying changes");
            PushDialog(dlg.Card, null);
            dlg.AddLine("Creating a system restore point (this can take a minute)", LogLevel.Info);
            dlg.AddLine("Restore point created", LogLevel.Success);
            dlg.AddLine("Applying: Turn off power throttling", LogLevel.Info);
            dlg.AddLine("Turn off power throttling", LogLevel.Success);
            dlg.AddLine("Applying: Hardware-accelerated GPU scheduling", LogLevel.Info);
            dlg.AddLine("Hardware-accelerated GPU scheduling", LogLevel.Success);
            dlg.AddLine("Applying: Turn off telemetry", LogLevel.Info);
            dlg.AddLine("   Scheduled task \\Microsoft\\Windows\\Autochk\\Proxy › Disabled — Access is denied.", LogLevel.Warning);
            dlg.AddLine("Turn off telemetry was only partly applied", LogLevel.Error);
            dlg.SetProgress(finished ? 27 : 9, 27, finished ? "Done" : "Optimizations for windowed games");
            if (finished)
            {
                RunResult r = new RunResult();
                r.Succeeded = 26;
                r.Failed = 1;
                r.NeedsRestart = true;
                dlg.Finish(r, null, delegate { PopDialog(); }, delegate { });
            }
        }
#endif

        public void RestartPc()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(System.IO.Path.Combine(Proc.System32, "shutdown.exe"), "/r /t 5 /c \"Restarting to finish Whittle changes\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                Process.Start(psi);
                Toast("Restarting in a few seconds…", "");
            }
            catch (Exception ex)
            {
                Toast("Couldn't restart: " + ex.Message, "");
            }
        }

        /// <summary>The live view shown while a plan runs.</summary>
        sealed class ProgressDialog
        {
            public readonly Border Card;
            readonly TextBlock title;
            readonly TextBlock step;
            readonly ProgressBar bar;
            readonly StackPanel log;
            readonly ScrollViewer logScroll;
            readonly StackPanel footer;
            readonly Border iconSlot;

            public ProgressDialog(string heading)
            {
                Card = DialogCard(600);
                StackPanel sp = new StackPanel();
                iconSlot = new Border();
                iconSlot.HorizontalAlignment = HorizontalAlignment.Left;
                iconSlot.Margin = new Thickness(0, 0, 0, 14);
                iconSlot.Height = 24;
                iconSlot.Child = Ui.Spinner(22);
                sp.Children.Add(iconSlot);
                title = Ui.Text(heading, 18, "Text1", FontWeights.SemiBold);
                sp.Children.Add(title);
                step = Ui.Text("Getting ready…", 13.5, "Text2");
                step.Margin = new Thickness(0, 6, 0, 0);
                sp.Children.Add(step);
                bar = new ProgressBar();
                bar.Style = Ui.St("Progress");
                bar.Minimum = 0;
                bar.Maximum = 1;
                bar.Margin = new Thickness(0, 16, 0, 0);
                sp.Children.Add(bar);

                log = new StackPanel();
                logScroll = new ScrollViewer();
                logScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                logScroll.Height = 200;
                logScroll.Content = log;
                Border box = new Border();
                box.Margin = new Thickness(0, 18, 0, 0);
                box.Background = Ui.Br("Bg0");
                box.BorderBrush = Ui.Br("Line");
                box.BorderThickness = new Thickness(1);
                box.CornerRadius = new CornerRadius(8);
                box.Padding = new Thickness(12, 8, 4, 8);
                box.Child = logScroll;
                sp.Children.Add(box);

                footer = new StackPanel();
                footer.Orientation = Orientation.Horizontal;
                footer.HorizontalAlignment = HorizontalAlignment.Right;
                footer.Margin = new Thickness(0, 20, 0, 0);
                footer.Height = 32;
                sp.Children.Add(footer);
                Card.Child = sp;
            }

            public void SetProgress(int done, int total, string label)
            {
                double target = total <= 0 ? 0 : Math.Min(1, (double)done / total);
                DoubleAnimation a = new DoubleAnimation(target, TimeSpan.FromMilliseconds(250));
                a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                bar.BeginAnimation(ProgressBar.ValueProperty, a);
                step.Text = done >= total ? label : label + "  ·  " + Math.Min(done + 1, total) + " of " + total;
            }

            public void AddLine(string message, LogLevel level)
            {
                DockPanel row = new DockPanel();
                row.Margin = new Thickness(0, 1, 0, 1);
                string glyph = level == LogLevel.Success ? "" : level == LogLevel.Error ? "" : level == LogLevel.Warning ? "" : "";
                string brush = level == LogLevel.Success ? "Good" : level == LogLevel.Error ? "Bad" : level == LogLevel.Warning ? "Warn" : "Text3";
                TextBlock ic = Ui.Icon(glyph, 10, brush);
                ic.Width = 18;
                ic.VerticalAlignment = VerticalAlignment.Top;
                ic.Margin = new Thickness(0, 3, 0, 0);
                row.Children.Add(ic);
                TextBlock t = new TextBlock();
                t.Text = message;
                t.FontFamily = Ui.MonoFont;
                t.FontSize = 11.5;
                t.Foreground = Ui.Br(level == LogLevel.Info ? "Text2" : "Text1");
                t.TextWrapping = TextWrapping.Wrap;
                row.Children.Add(t);
                log.Children.Add(row);
                if (log.Children.Count > 600) log.Children.RemoveAt(0);
                logScroll.ScrollToBottom();
            }

            public void Finish(RunResult r, Exception error, Action close, Action restart)
            {
                bar.BeginAnimation(ProgressBar.ValueProperty, null);
                bar.Value = 1;
                bool ok = error == null && r != null && !r.Cancelled && r.Failed == 0;
                if (r != null && r.Cancelled)
                {
                    iconSlot.Child = Ui.Icon("", 22, "Text2");
                    title.Text = "Nothing was changed";
                    step.Text = "You stopped before any changes were made.";
                }
                else if (error != null || r == null)
                {
                    iconSlot.Child = Ui.Icon("", 22, "Bad");
                    title.Text = "Something went wrong";
                    step.Text = error != null ? error.Message : "The run stopped unexpectedly.";
                }
                else if (ok)
                {
                    iconSlot.Child = Ui.Icon("", 22, "Good");
                    title.Text = "All done";
                    step.Text = Ui.Plural(r.Succeeded, "change", "changes") + " made" + (r.NeedsRestart ? ". Restart your PC to finish." : ".");
                }
                else
                {
                    iconSlot.Child = Ui.Icon("", 22, "Warn");
                    title.Text = "Finished with " + Ui.Plural(r.Failed, "problem", "problems");
                    step.Text = r.Succeeded + " succeeded, " + r.Failed + " didn't. Details are in the log below.";
                }
                footer.Children.Clear();
                Button done = Ui.Button(r != null && r.NeedsRestart && ok ? "Later" : "Close", "Btn", close);
                footer.Children.Add(done);
                if (r != null && r.NeedsRestart && !r.Cancelled)
                {
                    Button rs = Ui.IconButton("", "Restart now", "Btn.Primary", restart);
                    rs.Margin = new Thickness(8, 0, 0, 0);
                    footer.Children.Add(rs);
                    rs.IsDefault = true;
                }
                else
                {
                    done.IsDefault = true;
                }
                done.Focus();
            }
        }
    }
}
