namespace Bridge.Core;

/// <summary>
/// Background loop: waits for the controller, starts it, and forwards every input report
/// to a virtual Xbox controller. Reconnects automatically when the controller is replugged.
/// </summary>
public sealed class BridgeService : IDisposable
{
    readonly CancellationTokenSource _cts = new();
    Thread? _thread;
    XboxOutput? _xbox;
    DateTime _lastXboxAttempt = DateTime.MinValue;

    /// <summary>Latest decoded input; read by the UI on its own schedule.</summary>
    public GamepadState State { get; private set; }
    public bool ControllerConnected { get; private set; }
    public string ControllerStatus { get; private set; } = "Starting…";
    public bool XboxConnected => _xbox != null;
    public string XboxStatus { get; private set; } = "Starting…";

    public event Action? StatusChanged;

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Bridge" };
        _thread.Start();
    }

    void Run()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            EnsureXbox();
            Session(ct);
            if (!ct.IsCancellationRequested) ct.WaitHandle.WaitOne(1000);
        }
    }

    void Session(CancellationToken ct)
    {
        using var hid = HidInput.Open(out var hidError);
        if (hid == null)
        {
            SetController(false, "Waiting for controller — plug it in with a USB cable.");
            return;
        }

        // Starting the controller needs its vendor USB interface. If another program holds it,
        // that program may have started the controller already, so keep reading HID either way.
        Switch2Usb? usb = null;
        string? usbError = null;
        var parser = new ProReportParser();
        var buffer = new byte[hid.ReportLength];
        bool receiving = false;
        try
        {
            StartController(ref usb, ref usbError);
            while (!ct.IsCancellationRequested)
            {
                var n = hid.Read(buffer, 1000, ct);
                if (n == 0)
                {
                    // Silent: the controller hasn't been started (or was reset). Try again.
                    receiving = false;
                    _xbox?.Reset();
                    StartController(ref usb, ref usbError);
                    SetController(false, usbError != null
                        ? $"Controller found but not sending input. {usbError}"
                        : "Starting controller…");
                    EnsureXbox();
                    continue;
                }
                if (!parser.TryParse(buffer.AsSpan(0, n), out var state)) continue;

                State = state;
                _xbox?.Send(state);
                if (!receiving)
                {
                    receiving = true;
                    SetController(true, "Connected (USB)");
                }
                if (_xbox == null) EnsureXbox();
            }
        }
        catch (IOException)
        {
            // Unplugged.
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            usb?.Dispose();
        }
        State = default;
        _xbox?.Reset();
        SetController(false, "Controller disconnected.");
    }

    static void StartController(ref Switch2Usb? usb, ref string? error)
    {
        usb ??= Switch2Usb.Open(out error);
        if (usb == null)
        {
            error += " Close any other controller tool or browser tab using it.";
            return;
        }
        if (!usb.Initialize(out error))
        {
            // The handle may be stale (e.g. the controller was reset); reopen on the next attempt.
            usb.Dispose();
            usb = null;
        }
    }

    void EnsureXbox()
    {
        if (_xbox != null || DateTime.UtcNow - _lastXboxAttempt < TimeSpan.FromSeconds(3)) return;
        _lastXboxAttempt = DateTime.UtcNow;
        _xbox = XboxOutput.Create(out var error);
        XboxStatus = _xbox != null ? "Virtual Xbox 360 controller active" : error!;
        StatusChanged?.Invoke();
    }

    void SetController(bool connected, string status)
    {
        if (ControllerConnected == connected && ControllerStatus == status) return;
        ControllerConnected = connected;
        ControllerStatus = status;
        StatusChanged?.Invoke();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _thread?.Join(3000);
        _xbox?.Dispose();
        _cts.Dispose();
    }
}
