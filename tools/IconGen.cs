// Renders the Whittle mark into a multi-size .ico and a preview sheet.
// Build: see build.ps1 (compiled together with src\Brand.cs).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Whittle.Tools
{
    static class IconGen
    {
        [STAThread]
        static int Main(string[] args)
        {
            string outDir = args.Length > 0 ? args[0] : ".";
            Directory.CreateDirectory(outDir);

            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            List<BitmapSource> images = new List<BitmapSource>();
            foreach (int s in sizes)
            {
                images.Add(RenderMark(s, null));
            }
            WriteIco(Path.Combine(outDir, "whittle.ico"), sizes, images);
            SavePng(RenderMark(512, null), Path.Combine(outDir, "whittle-512.png"));
            SavePng(RenderSheet(), Path.Combine(outDir, "logo-preview.png"));
            Console.WriteLine("Icon written to " + Path.GetFullPath(Path.Combine(outDir, "whittle.ico")));
            return 0;
        }

        static BitmapSource RenderMark(int size, Brush background)
        {
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                if (background != null) dc.DrawRectangle(background, null, new Rect(0, 0, size, size));
                DrawMark(dc, 0, 0, size);
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        static void DrawMark(DrawingContext dc, double x, double y, double size)
        {
            Brush fill = new SolidColorBrush(Brand.Accent);
            double k = size / 64.0;
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new ScaleTransform(k, k));
            dc.DrawGeometry(fill, null, Brand.Body());
            dc.DrawGeometry(fill, null, Brand.Sliver());
            dc.Pop();
            dc.Pop();
        }

        static BitmapSource RenderSheet()
        {
            int w = 1100, h = 560;
            DrawingVisual dv = new DrawingVisual();
            Typeface face = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            using (DrawingContext dc = dv.RenderOpen())
            {
                Brush dark = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x13));
                Brush light = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
                dc.DrawRectangle(dark, null, new Rect(0, 0, w / 2, h));
                dc.DrawRectangle(light, null, new Rect(w / 2, 0, w / 2, h));

                for (int side = 0; side < 2; side++)
                {
                    double ox = side * (w / 2) + 40;
                    Brush text = side == 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1C));
                    DrawMark(dc, ox, 40, 160);
                    // Wordmark lockup
                    DrawMark(dc, ox, 240, 44);
                    FormattedText ft = new FormattedText("Whittle", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 40, text);
                    dc.DrawText(ft, new Point(ox + 56, 240 + (44 - ft.Height) / 2));
                    // Small sizes at 1:1
                    double sx = ox;
                    foreach (int s in new int[] { 16, 20, 24, 32, 48 })
                    {
                        dc.DrawImage(RenderMark(s, null), new Rect(sx, 330, s, s));
                        sx += s + 18;
                    }
                    // 16 px and 24 px zoomed 6x with nearest-neighbour, to judge pixel quality
                    DrawZoom(dc, RenderMark(16, null), ox, 400, 6);
                    DrawZoom(dc, RenderMark(24, null), ox + 120, 400, 6);
                }
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            return rtb;
        }

        static void DrawZoom(DrawingContext dc, BitmapSource src, double x, double y, int zoom)
        {
            WriteableBitmap wb = new WriteableBitmap(new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0));
            int w = wb.PixelWidth, h = wb.PixelHeight;
            byte[] px = new byte[w * h * 4];
            wb.CopyPixels(px, w * 4, 0);
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    int o = (j * w + i) * 4;
                    byte a = px[o + 3];
                    if (a == 0) continue;
                    Color c = Color.FromArgb(a, px[o + 2], px[o + 1], px[o]);
                    dc.DrawRectangle(new SolidColorBrush(c), null, new Rect(x + i * zoom, y + j * zoom, zoom, zoom));
                }
            }
        }

        static void SavePng(BitmapSource src, string path)
        {
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(src));
            using (FileStream fs = File.Create(path)) enc.Save(fs);
        }

        static byte[] PngBytes(BitmapSource src)
        {
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(src));
            using (MemoryStream ms = new MemoryStream())
            {
                enc.Save(ms);
                return ms.ToArray();
            }
        }

        // 32-bit DIB entry with an AND mask, the most compatible icon format.
        static byte[] DibBytes(BitmapSource src)
        {
            BitmapSource bgra = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            int w = bgra.PixelWidth, h = bgra.PixelHeight;
            byte[] px = new byte[w * h * 4];
            bgra.CopyPixels(px, w * 4, 0);
            int maskStride = ((w + 31) / 32) * 4;
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(40); bw.Write(w); bw.Write(h * 2);
                bw.Write((short)1); bw.Write((short)32);
                bw.Write(0); bw.Write(w * h * 4 + maskStride * h);
                bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
                for (int row = h - 1; row >= 0; row--) bw.Write(px, row * w * 4, w * 4);
                for (int row = h - 1; row >= 0; row--)
                {
                    byte[] mask = new byte[maskStride];
                    for (int i = 0; i < w; i++)
                    {
                        if (px[(row * w + i) * 4 + 3] == 0) mask[i / 8] |= (byte)(0x80 >> (i % 8));
                    }
                    bw.Write(mask);
                }
                return ms.ToArray();
            }
        }

        static void WriteIco(string path, int[] sizes, List<BitmapSource> images)
        {
            List<byte[]> blobs = new List<byte[]>();
            for (int i = 0; i < sizes.Length; i++)
            {
                blobs.Add(sizes[i] >= 128 ? PngBytes(images[i]) : DibBytes(images[i]));
            }
            using (FileStream fs = File.Create(path))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((short)0); bw.Write((short)1); bw.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                    bw.Write(dim); bw.Write(dim); bw.Write((byte)0); bw.Write((byte)0);
                    bw.Write((short)1); bw.Write((short)32);
                    bw.Write(blobs[i].Length); bw.Write(offset);
                    offset += blobs[i].Length;
                }
                foreach (byte[] b in blobs) bw.Write(b);
            }
        }
    }
}
