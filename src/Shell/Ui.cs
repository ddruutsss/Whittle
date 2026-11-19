using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Whittle.Shell
{
    public enum Tone { Neutral, Accent, Good, Warn, Bad }

    /// <summary>Small factory helpers so every page is built from the same pieces.</summary>
    public static class Ui
    {
        public static Brush Br(string key) { return (Brush)Application.Current.FindResource(key); }
        public static Style St(string key) { return (Style)Application.Current.FindResource(key); }
        public static FontFamily IconFont { get { return (FontFamily)Application.Current.FindResource("IconFont"); } }
        public static FontFamily MonoFont { get { return (FontFamily)Application.Current.FindResource("MonoFont"); } }

        public static TextBlock Text(string s, double size, string brush)
        {
            TextBlock t = new TextBlock();
            t.Text = s;
            t.FontSize = size;
            t.Foreground = Br(brush);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            return t;
        }

        public static TextBlock Text(string s, double size, string brush, FontWeight weight)
        {
            TextBlock t = Text(s, size, brush);
            t.FontWeight = weight;
            return t;
        }

        public static TextBlock Para(string s, double size, string brush)
        {
            TextBlock t = new TextBlock();
            t.Text = s;
            t.FontSize = size;
            t.Foreground = Br(brush);
            t.TextWrapping = TextWrapping.Wrap;
            t.LineHeight = Math.Round(size * 1.45);
            t.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            return t;
        }

        public static TextBlock Icon(string glyph, double size, string brush)
        {
            TextBlock t = new TextBlock();
            t.Text = glyph;
            t.FontFamily = IconFont;
            t.FontSize = size;
            t.Foreground = Br(brush);
            t.VerticalAlignment = VerticalAlignment.Center;
            return t;
        }

        public static Button Button(string text, string style, Action onClick)
        {
            Button b = new Button();
            b.Style = St(style);
            b.Content = text;
            if (onClick != null) b.Click += delegate { onClick(); };
            return b;
        }

        public static Button IconButton(string glyph, string text, string style, Action onClick)
        {
            Button b = Button(null, style, onClick);
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            TextBlock ic = new TextBlock();
            ic.Text = glyph;
            ic.FontFamily = IconFont;
            ic.FontSize = 13;
            ic.VerticalAlignment = VerticalAlignment.Center;
            ic.Margin = new Thickness(0, 1, text == null ? 0 : 8, 0);
            sp.Children.Add(ic);
            if (text != null)
            {
                TextBlock tx = new TextBlock();
                tx.Text = text;
                tx.VerticalAlignment = VerticalAlignment.Center;
                sp.Children.Add(tx);
            }
            b.Content = sp;
            if (text == null) b.Padding = new Thickness(9, 0, 9, 0);
            return b;
        }

        public static Border Card(UIElement child)
        {
            Border b = new Border();
            b.Background = Br("Surface");
            b.BorderBrush = Br("Line");
            b.BorderThickness = new Thickness(1);
            b.CornerRadius = new CornerRadius(8);
            b.Child = child;
            b.SnapsToDevicePixels = true;
            return b;
        }

        public static Border Card(UIElement child, Thickness padding)
        {
            Border b = Card(child);
            b.Padding = padding;
            return b;
        }

        public static Border Badge(string text, Tone tone)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(4);
            b.Padding = new Thickness(6, 1, 6, 2);
            b.VerticalAlignment = VerticalAlignment.Center;
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontSize = 11;
            t.FontWeight = FontWeights.SemiBold;
            switch (tone)
            {
                case Tone.Accent: b.Background = Br("AccentSoft"); t.Foreground = Br("Accent"); break;
                case Tone.Good: b.Background = Br("GoodSoft"); t.Foreground = Br("Good"); break;
                case Tone.Warn: b.Background = Br("WarnSoft"); t.Foreground = Br("Warn"); break;
                case Tone.Bad: b.Background = Br("BadSoft"); t.Foreground = Br("Bad"); break;
                default: b.Background = Br("SurfacePressed"); t.Foreground = Br("Text2"); break;
            }
            b.Child = t;
            return b;
        }

        public static FrameworkElement Dot(string brush)
        {
            Ellipse e = new Ellipse();
            e.Width = 7;
            e.Height = 7;
            e.Fill = Br(brush);
            e.VerticalAlignment = VerticalAlignment.Center;
            return e;
        }

        public static Border Divider()
        {
            Border b = new Border();
            b.Height = 1;
            b.Background = Br("Line");
            b.SnapsToDevicePixels = true;
            return b;
        }

        public static CheckBox Switch(bool on)
        {
            CheckBox c = new CheckBox();
            c.Style = St("Switch");
            c.IsChecked = on;
            return c;
        }

        public static CheckBox Check(bool on)
        {
            CheckBox c = new CheckBox();
            c.Style = St("Check");
            c.IsChecked = on;
            return c;
        }

        public static TextBox Field(string placeholder)
        {
            TextBox t = new TextBox();
            t.Style = St("Field");
            t.Tag = placeholder;
            return t;
        }

        public static FrameworkElement SearchBox(string placeholder, double width, Action<string> onChange)
        {
            Grid g = new Grid();
            g.Width = width;
            TextBox box = Field(placeholder);
            box.Padding = new Thickness(32, 0, 10, 0);
            box.TextChanged += delegate { onChange(box.Text); };
            box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape && box.Text.Length > 0) { box.Text = ""; e.Handled = true; }
            };
            TextBlock icon = Icon("", 13, "Text3");
            icon.Margin = new Thickness(11, 0, 0, 0);
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            icon.IsHitTestVisible = false;
            g.Children.Add(box);
            g.Children.Add(icon);
            return g;
        }

        /// <summary>Title, one-line explanation and right-aligned actions at the top of a page.</summary>
        public static FrameworkElement PageHeader(string title, string subtitle, params UIElement[] actions)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 0, 0, 22);
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel left = new StackPanel();
            TextBlock t = Text(title, 26, "Text1", FontWeights.SemiBold);
            t.Margin = new Thickness(0, 0, 0, 4);
            left.Children.Add(t);
            if (!string.IsNullOrEmpty(subtitle))
            {
                TextBlock s = Para(subtitle, 13.5, "Text2");
                s.MaxWidth = 640;
                s.HorizontalAlignment = HorizontalAlignment.Left;
                left.Children.Add(s);
            }
            g.Children.Add(left);
            if (actions != null && actions.Length > 0)
            {
                StackPanel right = new StackPanel();
                right.Orientation = Orientation.Horizontal;
                right.VerticalAlignment = VerticalAlignment.Bottom;
                right.Margin = new Thickness(24, 0, 0, 2);
                for (int i = 0; i < actions.Length; i++)
                {
                    FrameworkElement fe = actions[i] as FrameworkElement;
                    if (fe != null && i > 0) fe.Margin = new Thickness(8, 0, 0, 0);
                    right.Children.Add(actions[i]);
                }
                Grid.SetColumn(right, 1);
                g.Children.Add(right);
            }
            return g;
        }

        public static FrameworkElement SectionHeader(string title, string meta, UIElement right)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(2, 26, 0, 10);
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Bottom;
            sp.Children.Add(Text(title, 15, "Text1", FontWeights.SemiBold));
            if (!string.IsNullOrEmpty(meta))
            {
                TextBlock m = Text(meta, 13, "Text3");
                m.Margin = new Thickness(10, 1, 0, 0);
                m.VerticalAlignment = VerticalAlignment.Bottom;
                sp.Children.Add(m);
            }
            g.Children.Add(sp);
            if (right != null)
            {
                Grid.SetColumn(right, 1);
                g.Children.Add(right);
            }
            return g;
        }

        public static FrameworkElement Spinner(double size)
        {
            Path p = new Path();
            p.Width = size;
            p.Height = size;
            p.Stroke = Br("Accent");
            p.StrokeThickness = 2;
            p.StrokeStartLineCap = PenLineCap.Round;
            p.StrokeEndLineCap = PenLineCap.Round;
            p.Stretch = Stretch.None;
            double r = size / 2 - 1.5;
            double c = size / 2;
            PathFigure f = new PathFigure();
            f.StartPoint = new Point(c, c - r);
            f.Segments.Add(new ArcSegment(new Point(c + r, c), new Size(r, r), 0, false, SweepDirection.Clockwise, true));
            f.Segments.Add(new ArcSegment(new Point(c, c + r), new Size(r, r), 0, false, SweepDirection.Clockwise, true));
            f.Segments.Add(new ArcSegment(new Point(c - r * 0.7071, c + r * 0.7071), new Size(r, r), 0, false, SweepDirection.Clockwise, true));
            PathGeometry geo = new PathGeometry();
            geo.Figures.Add(f);
            p.Data = geo;
            RotateTransform rot = new RotateTransform(0, c, c);
            p.RenderTransform = rot;
            DoubleAnimation spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.85));
            spin.RepeatBehavior = RepeatBehavior.Forever;
            rot.BeginAnimation(RotateTransform.AngleProperty, spin);
            return p;
        }

        public static FrameworkElement Loading(string message)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(2, 8, 0, 8);
            sp.Children.Add(Spinner(18));
            TextBlock t = Text(message, 13.5, "Text2");
            t.Margin = new Thickness(12, 0, 0, 0);
            t.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(t);
            return sp;
        }

        public static StackPanel HStack(params UIElement[] items)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            foreach (UIElement e in items) if (e != null) sp.Children.Add(e);
            return sp;
        }

        public static T Margin<T>(T e, double l, double t, double r, double b) where T : FrameworkElement
        {
            e.Margin = new Thickness(l, t, r, b);
            return e;
        }

        public static void FadeIn(UIElement e)
        {
            e.Opacity = 0;
            TranslateTransform tt = new TranslateTransform(0, 6);
            e.RenderTransform = tt;
            DoubleAnimation op = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170));
            DoubleAnimation y = new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(200));
            y.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            e.BeginAnimation(UIElement.OpacityProperty, op);
            tt.BeginAnimation(TranslateTransform.YProperty, y);
        }

        /// <summary>True when a click's original source is the given element or inside it.</summary>
        public static bool IsWithin(object source, DependencyObject ancestor)
        {
            DependencyObject d = source as DependencyObject;
            while (d != null)
            {
                if (d == ancestor) return true;
                d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return false;
        }

        public static string Plural(int n, string one, string many)
        {
            return n.ToString(CultureInfo.CurrentCulture) + " " + (n == 1 ? one : many);
        }

        public static string When(DateTime t)
        {
            if (t == DateTime.MinValue) return "";
            DateTime local = t.Kind == DateTimeKind.Utc ? t.ToLocalTime() : t;
            string time = local.ToString("t", CultureInfo.CurrentCulture);
            if (local.Date == DateTime.Today) return "Today, " + time;
            if (local.Date == DateTime.Today.AddDays(-1)) return "Yesterday, " + time;
            string date = local.ToString(local.Year == DateTime.Today.Year ? "d MMM" : "d MMM yyyy", CultureInfo.CurrentCulture);
            return date + ", " + time;
        }
    }
}
