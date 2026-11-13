// Turns a transparent PNG into a multi-size .ico: crops to the visible pixels, centers on a square, scales each size.
// Usage: IconGen <source.png> <output.ico> [preview.png]
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

int[] icoSizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

if (args.Length < 2)
{
    Console.WriteLine("Usage: IconGen <source.png> <output.ico> [preview.png]");
    return 1;
}

var source = LoadCropped(args[0]);
WriteIco(args[1], icoSizes.Select(s => (s, RenderPng(source, s))).ToList());
if (args.Length > 2) File.WriteAllBytes(args[2], RenderPreview(source));
Console.WriteLine($"Wrote {args[1]}");
return 0;

static BitmapSource LoadCropped(string path)
{
    var img = new FormatConvertedBitmap(new BitmapImage(new Uri(Path.GetFullPath(path))), PixelFormats.Bgra32, null, 0);
    int w = img.PixelWidth, h = img.PixelHeight;
    var px = new byte[w * h * 4];
    img.CopyPixels(px, w * 4, 0);

    int minX = w, minY = h, maxX = -1, maxY = -1;
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            if (px[(y * w + x) * 4 + 3] > 8)
            {
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
    if (maxX < 0) throw new InvalidOperationException("Image is fully transparent.");
    return new CroppedBitmap(img, new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
}

static DrawingVisual Compose(BitmapSource src, int px, Color? background = null)
{
    var visual = new DrawingVisual();
    RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.Fant);
    using var dc = visual.RenderOpen();
    if (background is { } bg) dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, px, px));
    // Fill the width (controllers are wide) and center vertically.
    var scale = (double)px / Math.Max(src.PixelWidth, src.PixelHeight);
    double w = src.PixelWidth * scale, h = src.PixelHeight * scale;
    var rect = new Rect((px - w) / 2, (px - h) / 2, w, h);

    // A dark controller vanishes on a dark taskbar at small sizes, so give those a thin light rim:
    // the silhouette, filled light grey, stamped at small offsets behind the image.
    if (px <= 64)
    {
        var rim = new SolidColorBrush(Color.FromArgb(0xB0, 0x9A, 0xA0, 0xA8));
        var mask = new ImageBrush(src);
        var d = Math.Max(1, px / 48.0);
        for (int i = 0; i < 8; i++)
        {
            var a = i * Math.PI / 4;
            var shifted = rect;
            shifted.Offset(Math.Round(Math.Cos(a) * d, 2), Math.Round(Math.Sin(a) * d, 2));
            dc.PushOpacityMask(mask);
            dc.DrawRectangle(rim, null, shifted);
            dc.Pop();
        }
    }
    dc.DrawImage(src, rect);
    return visual;
}

static byte[] Encode(Visual visual, int width, int height)
{
    var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bmp.Render(visual);
    var enc = new PngBitmapEncoder();
    enc.Frames.Add(BitmapFrame.Create(bmp));
    using var ms = new MemoryStream();
    enc.Save(ms);
    return ms.ToArray();
}

static byte[] RenderPng(BitmapSource src, int px) => Encode(Compose(src, px), px, px);

// Common icon sizes, shown at 1:1 on a dark and a light strip, to judge legibility.
static byte[] RenderPreview(BitmapSource src)
{
    int[] sizes = [16, 24, 32, 48, 64, 128];
    int width = sizes.Sum() + 16 * (sizes.Length + 1), rowH = 128 + 32;
    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x20, 0x22, 0x26)), null, new Rect(0, 0, width, rowH));
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)), null, new Rect(0, rowH, width, rowH));
        double x = 16;
        foreach (var s in sizes)
        {
            var icon = BitmapFrame.Create(new MemoryStream(RenderPng(src, s)));
            dc.DrawImage(icon, new Rect(x, 16, s, s));
            dc.DrawImage(icon, new Rect(x, rowH + 16, s, s));
            x += s + 16;
        }
    }
    return Encode(visual, width, rowH * 2);
}

static void WriteIco(string path, List<(int Size, byte[] Png)> images)
{
    using var w = new BinaryWriter(File.Create(path));
    w.Write((ushort)0); // reserved
    w.Write((ushort)1); // type: icon
    w.Write((ushort)images.Count);
    var offset = 6 + 16 * images.Count;
    foreach (var (size, png) in images)
    {
        w.Write((byte)(size >= 256 ? 0 : size));
        w.Write((byte)(size >= 256 ? 0 : size));
        w.Write((byte)0); // palette
        w.Write((byte)0); // reserved
        w.Write((ushort)1); // planes
        w.Write((ushort)32); // bpp
        w.Write(png.Length);
        w.Write(offset);
        offset += png.Length;
    }
    foreach (var (_, png) in images) w.Write(png);
}
