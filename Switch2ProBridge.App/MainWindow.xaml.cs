using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using Bridge.Core;

namespace Switch2ProBridge.App;

public partial class MainWindow : Window
{
    static readonly Brush Online = new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50));
    static readonly Brush Offline = new SolidColorBrush(Color.FromRgb(0x6B, 0x70, 0x79));
    static readonly Brush Problem = new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D));
    static readonly Brush ArrowOn = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF));
    static readonly Brush ArrowOff = new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x43));

    readonly BridgeService _bridge = new();
    GamepadState _shown;

    public MainWindow()
    {
        InitializeComponent();
        _bridge.StatusChanged += () => Dispatcher.BeginInvoke(UpdateStatus);
        // Input arrives ~500 times a second; redraw at most once per frame.
        CompositionTarget.Rendering += (_, _) => RedrawIfChanged();
        _bridge.Start();
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        int on = 1;
        DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
    }

    void RedrawIfChanged()
    {
        var state = _bridge.State;
        if (state == _shown) return;
        _shown = state;

        SwitchPad.State = state;
        // The Xbox view only reflects input when the virtual controller is actually receiving it.
        XboxPad.State = _bridge.XboxConnected ? state : default;
        SwitchPad.InvalidateVisual();
        XboxPad.InvalidateVisual();
        Battery.Text = _bridge.ControllerConnected ? $"Battery: {Math.Min(state.Battery * 100 / 9, 100)}%" : "";
    }

    void UpdateStatus()
    {
        SwitchStatus.Text = _bridge.ControllerStatus;
        SwitchDot.Fill = _bridge.ControllerConnected ? Online : Offline;
        SwitchPad.Opacity = _bridge.ControllerConnected ? 1 : 0.45;

        XboxStatus.Text = _bridge.XboxStatus;
        XboxDot.Fill = _bridge.XboxConnected ? Online : Problem;
        XboxPad.Opacity = _bridge.XboxConnected && _bridge.ControllerConnected ? 1 : 0.45;
        VigemHelp.Visibility = _bridge.XboxConnected ? Visibility.Collapsed : Visibility.Visible;

        Arrow.Foreground = _bridge.XboxConnected && _bridge.ControllerConnected ? ArrowOn : ArrowOff;
        if (!_bridge.ControllerConnected) Battery.Text = "";
    }

    void OpenLink(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _bridge.Dispose();
        base.OnClosed(e);
    }
}
