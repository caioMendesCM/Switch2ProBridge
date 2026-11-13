// Renders the app icon: a gamepad split diagonally, Switch 2 Pro style on the left, Xbox Series style on the right.
// Usage: IconGen <output.ico> [preview.png]
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

const double Size = 256;
int[] icoSizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

var icoPath = args.Length > 0 ? args[0] : "app.ico";
var previewPath = args.Length > 1 ? args[1] : null;

var pngs = icoSizes.Select(s => (Size: s, Png: RenderPng(s))).ToList();
WriteIco(icoPath, pngs);
if (previewPath != null) File.WriteAllBytes(previewPath, RenderPng(512, background: Color.FromRgb(0x20, 0x22, 0x26)));
Console.WriteLine($"Wrote {icoPath}");

static byte[] RenderPng(int px, Color? background = null)
{
    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        if (background is { } bg) dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, px, px));
        dc.PushTransform(new ScaleTransform(px / Size, px / Size));
        Draw(dc);
        dc.Pop();
    }
    var bmp = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
    bmp.Render(visual);
    var enc = new PngBitmapEncoder();
    enc.Frames.Add(BitmapFrame.Create(bmp));
    using var ms = new MemoryStream();
    enc.Save(ms);
    return ms.ToArray();
}

static void Draw(DrawingContext dc)
{
    // Same silhouette as the app's GamepadView (440x300 design space), fitted to the icon.
    var body = Geometry.Parse(
        "M 120,48 C 170,38 270,38 320,48 C 370,56 400,80 412,130 C 428,195 436,255 408,278 " +
        "C 380,300 348,284 332,250 C 322,228 306,214 282,214 L 158,214 C 134,214 118,228 108,250 " +
        "C 92,284 60,300 32,278 C 4,255 12,195 28,130 C 40,80 70,56 120,48 Z");
    const double scale = 244.0 / 432;
    var toIcon = new TransformGroup();
    toIcon.Children.Add(new TranslateTransform(-220, -169));
    toIcon.Children.Add(new ScaleTransform(scale, scale));
    toIcon.Children.Add(new TranslateTransform(128, 128));

    // Diagonal split, bottom-left to top-right, with a small gap between the halves.
    const double gap = 5, topX = 163, bottomX = 93;
    var left = Polygon((0, 0), (topX - gap, 0), (bottomX - gap, Size), (0, Size));
    var right = Polygon((topX + gap, 0), (Size, 0), (Size, Size), (bottomX + gap, Size));

    var outline = new Pen(new SolidColorBrush(Color.FromRgb(0x7C, 0x82, 0x8C)), 7);

    // Left: Switch 2 Pro style — charcoal body, left stick and D-pad.
    dc.PushClip(left);
    dc.PushTransform(toIcon);
    dc.DrawGeometry(Brush(0x2A, 0x2D, 0x33), outline, body);
    Stick(dc, new Point(120, 112), Brush(0x16, 0x17, 0x1A), Brush(0x45, 0x49, 0x51));
    var dpad = Brush(0x45, 0x49, 0x51);
    var c = new Point(170, 178);
    const double arm = 22, half = 10;
    dc.DrawRoundedRectangle(dpad, null, new Rect(c.X - half, c.Y - half - arm, half * 2, arm * 2 + half * 2), 4, 4);
    dc.DrawRoundedRectangle(dpad, null, new Rect(c.X - half - arm, c.Y - half, arm * 2 + half * 2, half * 2), 4, 4);
    dc.Pop();
    dc.Pop();

    // Right: Xbox Series style — white body, colored face buttons, right stick.
    dc.PushClip(right);
    dc.PushTransform(toIcon);
    dc.DrawGeometry(Brush(0xEC, 0xEE, 0xF1), outline, body);
    Stick(dc, new Point(270, 178), Brush(0x1E, 0x20, 0x24), Brush(0x34, 0x37, 0x3D));
    var face = new Point(326, 110);
    const double off = 30, r = 15;
    dc.DrawEllipse(Brush(0xF2, 0xC2, 0x00), null, new Point(face.X, face.Y - off), r, r); // Y
    dc.DrawEllipse(Brush(0x3F, 0xB9, 0x50), null, new Point(face.X, face.Y + off), r, r); // A
    dc.DrawEllipse(Brush(0x3B, 0x82, 0xF6), null, new Point(face.X - off, face.Y), r, r); // X
    dc.DrawEllipse(Brush(0xE5, 0x48, 0x4D), null, new Point(face.X + off, face.Y), r, r); // B
    dc.Pop();
    dc.Pop();

    // The slash itself, running past the body like in the sketch.
    var slash = new Pen(Brush(0xE6, 0x2B, 0x2B), 7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
    dc.DrawLine(slash, Lerp(bottomX, topX, 0.10), Lerp(bottomX, topX, 0.90));
}

static Point Lerp(double bottomX, double topX, double t) => new(bottomX + (topX - bottomX) * t, Size * (1 - t));

static void Stick(DrawingContext dc, Point p, Brush well, Brush cap)
{
    dc.DrawEllipse(well, null, p, 34, 34);
    dc.DrawEllipse(cap, null, p, 24, 24);
}

static Geometry Polygon(params (double X, double Y)[] pts)
{
    var fig = new PathFigure { StartPoint = new Point(pts[0].X, pts[0].Y), IsClosed = true };
    foreach (var (x, y) in pts.Skip(1)) fig.Segments.Add(new LineSegment(new Point(x, y), false));
    return new PathGeometry([fig]);
}

static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

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
