using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace Bridge.Core;

/// <summary>A virtual Xbox 360 controller backed by the ViGEmBus driver.</summary>
public sealed class XboxOutput : IDisposable
{
    static readonly (Buttons From, Xbox360Button To)[] ButtonMap =
    [
        (Buttons.South, Xbox360Button.A),
        (Buttons.East, Xbox360Button.B),
        (Buttons.West, Xbox360Button.X),
        (Buttons.North, Xbox360Button.Y),
        (Buttons.LeftShoulder, Xbox360Button.LeftShoulder),
        (Buttons.RightShoulder, Xbox360Button.RightShoulder),
        (Buttons.Back, Xbox360Button.Back),
        (Buttons.Start, Xbox360Button.Start),
        (Buttons.Guide, Xbox360Button.Guide),
        (Buttons.LeftStick, Xbox360Button.LeftThumb),
        (Buttons.RightStick, Xbox360Button.RightThumb),
        (Buttons.DpadUp, Xbox360Button.Up),
        (Buttons.DpadDown, Xbox360Button.Down),
        (Buttons.DpadLeft, Xbox360Button.Left),
        (Buttons.DpadRight, Xbox360Button.Right),
    ];

    readonly ViGEmClient _client;
    readonly IXbox360Controller _pad;

    XboxOutput(ViGEmClient client, IXbox360Controller pad)
    {
        _client = client;
        _pad = pad;
    }

    public static XboxOutput? Create(out string? error)
    {
        error = null;
        try
        {
            var client = new ViGEmClient();
            var pad = client.CreateXbox360Controller();
            pad.AutoSubmitReport = false;
            pad.Connect();
            return new XboxOutput(client, pad);
        }
        catch (Nefarius.ViGEm.Client.Exceptions.VigemBusNotFoundException)
        {
            error = "ViGEmBus driver is not installed.";
        }
        catch (Exception e)
        {
            error = $"Could not create virtual Xbox controller: {e.Message}";
        }
        return null;
    }

    public void Send(in GamepadState s)
    {
        foreach (var (from, to) in ButtonMap)
            _pad.SetButtonState(to, s.Has(from));
        _pad.SetSliderValue(Xbox360Slider.LeftTrigger, s.Has(Buttons.LeftTrigger) ? byte.MaxValue : (byte)0);
        _pad.SetSliderValue(Xbox360Slider.RightTrigger, s.Has(Buttons.RightTrigger) ? byte.MaxValue : (byte)0);
        _pad.SetAxisValue(Xbox360Axis.LeftThumbX, ToShort(s.LeftX));
        _pad.SetAxisValue(Xbox360Axis.LeftThumbY, ToShort(s.LeftY));
        _pad.SetAxisValue(Xbox360Axis.RightThumbX, ToShort(s.RightX));
        _pad.SetAxisValue(Xbox360Axis.RightThumbY, ToShort(s.RightY));
        _pad.SubmitReport();
    }

    /// <summary>Releases everything, so games don't see a stuck input when the controller unplugs.</summary>
    public void Reset() => Send(default);

    static short ToShort(float v) => (short)Math.Clamp(v * short.MaxValue, short.MinValue, short.MaxValue);

    public void Dispose()
    {
        try { _pad.Disconnect(); } catch { }
        _client.Dispose();
    }
}
