namespace Bridge.Core;

/// <summary>
/// Decodes the Pro Controller's 0x09 USB input report.
/// Layout from github.com/ndeadly/switch2_controller_research, shifted by one for the USB report ID byte.
/// </summary>
public sealed class ProReportParser
{
    public const byte ReportId = 0x09;

    // (byte index, bit mask, button)
    static readonly (int Index, byte Mask, Buttons Button)[] ButtonMap =
    [
        (3, 0x01, Buttons.South),   // B
        (3, 0x02, Buttons.East),    // A
        (3, 0x04, Buttons.West),    // Y
        (3, 0x08, Buttons.North),   // X
        (3, 0x10, Buttons.RightShoulder),
        (3, 0x20, Buttons.RightTrigger),
        (3, 0x40, Buttons.Start),   // Plus
        (3, 0x80, Buttons.RightStick),
        (4, 0x01, Buttons.DpadDown),
        (4, 0x02, Buttons.DpadRight),
        (4, 0x04, Buttons.DpadLeft),
        (4, 0x08, Buttons.DpadUp),
        (4, 0x10, Buttons.LeftShoulder),
        (4, 0x20, Buttons.LeftTrigger),
        (4, 0x40, Buttons.Back),    // Minus
        (4, 0x80, Buttons.LeftStick),
        (5, 0x01, Buttons.Guide),   // Home
        (5, 0x02, Buttons.Capture),
        (5, 0x04, Buttons.GR),
        (5, 0x08, Buttons.GL),
        (5, 0x10, Buttons.C),
    ];

    readonly StickAxis _lx = new(), _ly = new(), _rx = new(), _ry = new();

    public bool TryParse(ReadOnlySpan<byte> r, out GamepadState state)
    {
        state = default;
        if (r.Length < 12 || r[0] != ReportId) return false;

        var buttons = Buttons.None;
        foreach (var (index, mask, button) in ButtonMap)
            if ((r[index] & mask) != 0) buttons |= button;

        state = new GamepadState(
            buttons,
            _lx.Map(r[6] | ((r[7] & 0x0F) << 8)),
            _ly.Map((r[7] >> 4) | (r[8] << 4)),
            _rx.Map(r[9] | ((r[10] & 0x0F) << 8)),
            _ry.Map((r[10] >> 4) | (r[11] << 4)),
            (r[2] >> 2) & 0x0F);
        return true;
    }

    /// <summary>
    /// Self-calibrating 12-bit axis: the first sample is taken as center (sticks at rest on connect),
    /// and the range grows as the stick reaches further out.
    /// </summary>
    sealed class StickAxis
    {
        const float InitialRange = 1200f; // conservative guess; widens as the stick is moved
        const float Deadzone = 0.08f;

        float _center = -1, _min, _max;

        public float Map(int raw)
        {
            if (_center < 0)
            {
                _center = raw;
                _min = raw - InitialRange;
                _max = raw + InitialRange;
            }
            _min = Math.Min(_min, raw);
            _max = Math.Max(_max, raw);

            var v = raw >= _center ? (raw - _center) / (_max - _center) : (raw - _center) / (_center - _min);
            if (Math.Abs(v) < Deadzone) return 0;
            // Rescale so output starts from 0 at the deadzone edge instead of jumping.
            return Math.Sign(v) * (Math.Abs(v) - Deadzone) / (1 - Deadzone);
        }
    }
}
