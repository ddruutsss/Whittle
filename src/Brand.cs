using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Whittle
{
    /// <summary>
    /// Name, version and the logo mark. The mark is a rounded block with one corner
    /// cut away; the cut-off sliver sits just beside it. Drawn on a 64-unit grid.
    /// </summary>
    public static class Brand
    {
        public const string Name = "Whittle";
        public const string Version = "1.0.0";

        public static readonly Color Accent = Color.FromRgb(0xF3, 0x87, 0x3A);

        // Main block: 48x48 rounded square (radius 11) with the top-right corner cut at 45 degrees.
        // Both shapes together span 5.25..58.75 on each axis, so the mark is centered.
        public const string BodyPath =
            "M 16.25,10.75 L 32.25,10.75 L 53.25,31.75 L 53.25,47.75 A 11,11 0 0 1 42.25,58.75 " +
            "L 16.25,58.75 A 11,11 0 0 1 5.25,47.75 L 5.25,21.75 A 11,11 0 0 1 16.25,10.75 Z";

        // The piece that was cut off, moved up and right so a clean gap shows the cut line.
        public const string SliverPath =
            "M 37.75,5.25 L 51.75,5.25 A 7,7 0 0 1 58.75,12.25 L 58.75,26.25 Z";

        public static Geometry Body()
        {
            Geometry g = Geometry.Parse(BodyPath);
            g.Freeze();
            return g;
        }

        public static Geometry Sliver()
        {
            Geometry g = Geometry.Parse(SliverPath);
            g.Freeze();
            return g;
        }

        /// <summary>Vector mark at any size, for use inside the UI.</summary>
        public static FrameworkElement Mark(double size)
        {
            Brush fill = new SolidColorBrush(Accent);
            fill.Freeze();

            Canvas canvas = new Canvas();
            canvas.Width = 64;
            canvas.Height = 64;
            Path body = new Path();
            body.Data = Body();
            body.Fill = fill;
            Path sliver = new Path();
            sliver.Data = Sliver();
            sliver.Fill = fill;
            canvas.Children.Add(body);
            canvas.Children.Add(sliver);

            Viewbox box = new Viewbox();
            box.Width = size;
            box.Height = size;
            box.Stretch = Stretch.Uniform;
            box.Child = canvas;
            box.SnapsToDevicePixels = true;
            return box;
        }
    }
}
