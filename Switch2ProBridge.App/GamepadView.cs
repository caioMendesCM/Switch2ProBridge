using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Bridge.Core;

namespace Switch2ProBridge.App;

public enum PadStyle { Switch2Pro, Xbox360 }

/// <summary>Draws a stylized controller and lights up whatever is pressed.</summary>
public sealed class GamepadView : FrameworkElement
{
    const double DesignWidth = 440, DesignHeight = 300;

    static readonly Geometry BodyGeometry = Geometry.Parse(
        "M 120,48 C 170,38 270,38 320,48 C 370,56 400,80 412,130 C 428,195 436,255 408,278 " +
        "C 380,300 348,284 332,250 C 322,228 306,214 282,214 L 158,214 C 134,214 118,228 108,250 " +
        "C 92,284 60,300 32,278 C 4,255 12,195 28,130 C 40,80 70,56 120,48 Z");

    static readonly Brush Accent = Freeze(new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)));

    public PadStyle PadStyle { get; set; }
    public GamepadState State { get; set; }

    record Palette(Brush Body, Pen Outline, Brush Part, Brush PartDark, Brush Label);

    static readonly Palette SwitchColors = new(Brush(0x26, 0x28, 0x2C), Freeze(new Pen(Brush(0x4A, 0x4D, 0x55), 2)),
        Brush(0x3A, 0x3D, 0x43), Brush(0x1B, 0x1C, 0x1F), Brush(0xC8, 0xCC, 0xD2));
    static readonly Palette XboxColors = new(Brush(0xDD, 0xE0, 0xE5), Freeze(new Pen(Brush(0xA9, 0xAE, 0xB6), 2)),
        Brush(0x5A, 0x5E, 0x66), Brush(0x2E, 0x31, 0x36), Brush(0xF2, 0xF3, 0xF5));

    Palette Colors => PadStyle == PadStyle.Switch2Pro ? SwitchColors : XboxColors;

    protected override Size MeasureOverride(Size available)
    {
        // Keep the drawing's aspect ratio within whatever space we're given.
        var scale = Math.Min(
            double.IsInfinity(available.Width) ? 1 : available.Width / DesignWidth,
            double.IsInfinity(available.Height) ? 1 : available.Height / DesignHeight);
        return new Size(DesignWidth * scale, DesignHeight * scale);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var scale = Math.Min(ActualWidth / DesignWidth, ActualHeight / DesignHeight);
        dc.PushTransform(new TranslateTransform((ActualWidth - DesignWidth * scale) / 2, (ActualHeight - DesignHeight * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));

        var c = Colors;
        var s = State;
        bool sw = PadStyle == PadStyle.Switch2Pro;

        // Triggers sit behind the shoulders, so draw them first.
        Pill(dc, c, 92, 4, 66, 32, s.Has(Buttons.LeftTrigger), sw ? "ZL" : "LT", textDy: -7);
        Pill(dc, c, 282, 4, 66, 32, s.Has(Buttons.RightTrigger), sw ? "ZR" : "RT", textDy: -7);
        Pill(dc, c, 78, 30, 92, 22, s.Has(Buttons.LeftShoulder), sw ? "L" : "LB", textDy: -1);
        Pill(dc, c, 270, 30, 92, 22, s.Has(Buttons.RightShoulder), sw ? "R" : "RB", textDy: -1);

        dc.DrawGeometry(c.Body, c.Outline, BodyGeometry);

        if (sw)
        {
            // Back paddles, drawn as outlines on the grips.
            Pill(dc, c, 52, 236, 34, 18, s.Has(Buttons.GL), "GL", dashed: true);
            Pill(dc, c, 354, 236, 34, 18, s.Has(Buttons.GR), "GR", dashed: true);
        }

        Stick(dc, c, new Point(120, 112), s.LeftX, s.LeftY, s.Has(Buttons.LeftStick));
        Stick(dc, c, new Point(270, 175), s.RightX, s.RightY, s.Has(Buttons.RightStick));
        Dpad(dc, c, new Point(170, 175), s);

        var face = new Point(322, 112);
        const double off = 27;
        if (sw)
        {
            FaceButton(dc, c, new Point(face.X, face.Y - off), s.Has(Buttons.North), "X", null);
            FaceButton(dc, c, new Point(face.X, face.Y + off), s.Has(Buttons.South), "B", null);
            FaceButton(dc, c, new Point(face.X - off, face.Y), s.Has(Buttons.West), "Y", null);
            FaceButton(dc, c, new Point(face.X + off, face.Y), s.Has(Buttons.East), "A", null);

            Round(dc, c, new Point(186, 88), 8, s.Has(Buttons.Back), "−", 13);
            Round(dc, c, new Point(254, 88), 8, s.Has(Buttons.Start), "+", 13);
            Square(dc, c, new Point(196, 122), 7, s.Has(Buttons.Capture));
            Round(dc, c, new Point(244, 122), 9, s.Has(Buttons.Guide), "⌂", 11);
            Round(dc, c, new Point(220, 150), 7, s.Has(Buttons.C), "C", 9);
        }
        else
        {
            FaceButton(dc, c, new Point(face.X, face.Y - off), s.Has(Buttons.North), "Y", Color.FromRgb(0xF2, 0xC2, 0x00));
            FaceButton(dc, c, new Point(face.X, face.Y + off), s.Has(Buttons.South), "A", Color.FromRgb(0x3F, 0xB9, 0x50));
            FaceButton(dc, c, new Point(face.X - off, face.Y), s.Has(Buttons.West), "X", Color.FromRgb(0x3B, 0x82, 0xF6));
            FaceButton(dc, c, new Point(face.X + off, face.Y), s.Has(Buttons.East), "B", Color.FromRgb(0xE5, 0x48, 0x4D));

            Pill(dc, c, 168, 104, 36, 16, s.Has(Buttons.Back), "BACK", fontSize: 7);
            Pill(dc, c, 236, 104, 36, 16, s.Has(Buttons.Start), "START", fontSize: 7);
            Round(dc, c, new Point(220, 72), 15, s.Has(Buttons.Guide), "✕", 13);
        }

        dc.Pop();
        dc.Pop();
    }

    void Stick(DrawingContext dc, Palette c, Point center, float x, float y, bool clicked)
    {
        dc.DrawEllipse(c.PartDark, clicked ? new Pen(Accent, 3) : null, center, 30, 30);
        var moved = x != 0 || y != 0;
        var cap = new Point(center.X + x * 12, center.Y - y * 12);
        dc.DrawEllipse(clicked ? Accent : c.Part, moved ? new Pen(Accent, 2.5) : null, cap, 21, 21);
        dc.DrawEllipse(null, new Pen(c.PartDark, 1.5), cap, 15, 15);
    }

    void Dpad(DrawingContext dc, Palette c, Point p, GamepadState s)
    {
        const double arm = 19, half = 9;
        dc.DrawRectangle(c.Part, null, new Rect(p.X - half, p.Y - half, half * 2, half * 2));
        Arm(new Rect(p.X - half, p.Y - half - arm, half * 2, arm), s.Has(Buttons.DpadUp));
        Arm(new Rect(p.X - half, p.Y + half, half * 2, arm), s.Has(Buttons.DpadDown));
        Arm(new Rect(p.X - half - arm, p.Y - half, arm, half * 2), s.Has(Buttons.DpadLeft));
        Arm(new Rect(p.X + half, p.Y - half, arm, half * 2), s.Has(Buttons.DpadRight));

        void Arm(Rect r, bool on) => dc.DrawRoundedRectangle(on ? Accent : c.Part, null, r, 3, 3);
    }

    void FaceButton(DrawingContext dc, Palette c, Point p, bool on, string label, Color? tint)
    {
        var tintBrush = tint is { } t ? Freeze(new SolidColorBrush(t)) : null;
        dc.DrawEllipse(on ? (tintBrush ?? Accent) : c.PartDark, null, p, 13, 13);
        Text(dc, label, p, 13, on ? Brushes.White : (tintBrush ?? c.Label), bold: true);
    }

    void Round(DrawingContext dc, Palette c, Point p, double r, bool on, string label, double size)
    {
        dc.DrawEllipse(on ? Accent : c.PartDark, null, p, r, r);
        Text(dc, label, p, size, on ? Brushes.White : c.Label);
    }

    void Square(DrawingContext dc, Palette c, Point p, double half, bool on)
    {
        dc.DrawRoundedRectangle(on ? Accent : c.PartDark, null, new Rect(p.X - half, p.Y - half, half * 2, half * 2), 2, 2);
        dc.DrawEllipse(null, new Pen(on ? Brushes.White : c.Label, 1.2), p, half * 0.5, half * 0.5);
    }

    void Pill(DrawingContext dc, Palette c, double x, double y, double w, double h, bool on, string label,
        double fontSize = 10, double textDy = 0, bool dashed = false)
    {
        var rect = new Rect(x, y, w, h);
        Pen? pen = dashed ? new Pen(c.Outline.Brush, 1.5) { DashStyle = DashStyles.Dash } : null;
        dc.DrawRoundedRectangle(on ? Accent : dashed ? null : c.PartDark, pen, rect, h / 2, h / 2);
        Text(dc, label, new Point(x + w / 2, y + h / 2 + textDy), fontSize, on ? Brushes.White : c.Label, bold: true);
    }

    void Text(DrawingContext dc, string text, Point center, double size, Brush brush, bool bold = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
    }

    static SolidColorBrush Brush(byte r, byte g, byte b) => Freeze(new SolidColorBrush(Color.FromRgb(r, g, b)));

    static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}
