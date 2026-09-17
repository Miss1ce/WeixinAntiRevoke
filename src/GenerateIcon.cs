using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Reproducible vector drawing: no downloaded images or external tools required.
internal static class GenerateIcon
{
    private static GraphicsPath Rounded(float x, float y, float w, float h, float radius)
    {
        GraphicsPath p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    private static Bitmap Draw(int size)
    {
        using (Bitmap large = new Bitmap(size * 4, size * 4, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(large))
            {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(large.Width / 256F, large.Height / 256F);
                using (GraphicsPath tile = Rounded(5, 5, 246, 246, 54))
                using (LinearGradientBrush fill = new LinearGradientBrush(new PointF(20, 0), new PointF(240, 256), Color.FromArgb(27, 59, 82), Color.FromArgb(9, 18, 34)))
                using (Pen border = new Pen(Color.FromArgb(66, 123, 148), 3))
                {
                    g.FillPath(fill, tile); g.DrawPath(border, tile);
                }
                using (GraphicsPath shield = new GraphicsPath())
                {
                    shield.AddLines(new[] { new PointF(128, 40), new PointF(202, 66), new PointF(194, 138) });
                    shield.AddBezier(194, 138, 185, 171, 157, 200, 128, 218);
                    shield.AddBezier(128, 218, 99, 200, 71, 171, 62, 138);
                    shield.AddLine(62, 138, 54, 66); shield.CloseFigure();
                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(12, 35, 51))) g.FillPath(fill, shield);
                    using (LinearGradientBrush accent = new LinearGradientBrush(new PointF(54, 50), new PointF(202, 205), Color.FromArgb(87, 242, 220), Color.FromArgb(74, 162, 255)))
                    using (Pen border = new Pen(accent, size <= 24 ? 14 : 11)) { border.LineJoin = LineJoin.Round; g.DrawPath(border, shield); }
                }
                using (Pen check = new Pen(Color.FromArgb(157, 255, 245), size <= 24 ? 17 : 14))
                {
                    check.StartCap = check.EndCap = LineCap.Round; check.LineJoin = LineJoin.Round;
                    g.DrawLines(check, new[] { new PointF(92, 127), new PointF(116, 151), new PointF(164, 100) });
                }
            }
            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(large, new Rectangle(0, 0, size, size), 0, 0, large.Width, large.Height, GraphicsUnit.Pixel);
            }
            return result;
        }
    }

    private static byte[] EncodeDib(Bitmap bitmap)
    {
        int size = bitmap.Width, maskStride = ((size + 31) / 32) * 4;
        using (MemoryStream data = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(data))
        {
            w.Write(40); w.Write(size); w.Write(size * 2); w.Write((ushort)1); w.Write((ushort)32);
            w.Write(0); w.Write(size * size * 4); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = size - 1; y >= 0; y--)
                for (int x = 0; x < size; x++)
                {
                    Color c = bitmap.GetPixel(x, y); w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
                }
            for (int y = size - 1; y >= 0; y--)
            {
                byte[] mask = new byte[maskStride];
                for (int x = 0; x < size; x++) if (bitmap.GetPixel(x, y).A == 0) mask[x / 8] |= (byte)(128 >> (x % 8));
                w.Write(mask);
            }
            return data.ToArray();
        }
    }

    private static int Main(string[] args)
    {
        if (args.Length != 2) return 2;
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        byte[][] frames = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++) using (Bitmap bitmap = Draw(sizes[i])) frames[i] = EncodeDib(bitmap);
        using (BinaryWriter w = new BinaryWriter(File.Create(args[0])))
        {
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
            int offset = 6 + sizes.Length * 16;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0); w.Write((ushort)1); w.Write((ushort)32);
                w.Write(frames[i].Length); w.Write(offset); offset += frames[i].Length;
            }
            foreach (byte[] frame in frames) w.Write(frame);
        }
        using (Bitmap preview = Draw(256)) preview.Save(args[1], ImageFormat.Png);
        Console.WriteLine("Generated 9 icon sizes (16 through 256 px)."); return 0;
    }
}
