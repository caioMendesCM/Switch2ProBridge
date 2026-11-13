using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using static Bridge.Core.Native;

namespace Bridge.Core;

/// <summary>
/// Talks to the vendor (WinUSB) interface of the Switch 2 Pro Controller.
/// The controller only starts streaming HID input after this init sequence.
/// Sequence taken from HandHeldLegend's procon2tool (handheldlegend.github.io/procon2tool),
/// which leaves the controller sending the 0x09 report that ProReportParser decodes.
/// </summary>
public sealed class Switch2Usb : IDisposable
{
    public const ushort VendorId = 0x057E;
    public const ushort ProductId = 0x2069;

    // Sent verbatim, including the two oddly short packets near the end, since that's what is known to work.
    static readonly byte[][] InitSequence =
    [
        [0x03, 0x91, 0x00, 0x0d, 0x00, 0x08, 0x00, 0x00, 0x01, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff], // start HID output
        [0x07, 0x91, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00],
        [0x16, 0x91, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00],
        [0x15, 0x91, 0x00, 0x01, 0x00, 0x0e, 0x00, 0x00, 0x00, 0x02, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
         0xff, 0xff, 0xff, 0xff, 0xff, 0xff], // request controller MAC
        [0x15, 0x91, 0x00, 0x02, 0x00, 0x11, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
         0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff], // LTK request
        [0x15, 0x91, 0x00, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00],
        [0x09, 0x91, 0x00, 0x07, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
        [0x0c, 0x91, 0x00, 0x02, 0x00, 0x04, 0x00, 0x00, 0x27, 0x00, 0x00, 0x00], // IMU
        [0x11, 0x91, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00],
        [0x0a, 0x91, 0x00, 0x08, 0x00, 0x14, 0x00, 0x00, 0x01, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
         0x35, 0x00, 0x46, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
        [0x0c, 0x91, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00, 0x27, 0x00, 0x00, 0x00], // IMU
        [0x03, 0x91, 0x00, 0x0a, 0x00, 0x04, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00], // enable haptics / report 0x09
        [0x10, 0x91, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00],
        [0x01, 0x91, 0x00, 0x0c, 0x00, 0x00, 0x00, 0x00],
        [0x03, 0x91, 0x00, 0x01, 0x00, 0x00, 0x00],
        [0x0a, 0x91, 0x00, 0x02, 0x00, 0x04, 0x00, 0x00, 0x03, 0x00, 0x00],
    ];

    readonly SafeFileHandle _file;
    readonly IntPtr _iface;
    readonly byte _pipeOut, _pipeIn;

    Switch2Usb(SafeFileHandle file, IntPtr iface, byte pipeOut, byte pipeIn)
    {
        _file = file;
        _iface = iface;
        _pipeOut = pipeOut;
        _pipeIn = pipeIn;
    }

    /// <summary>Opens the controller's vendor interface, or returns null if it isn't connected.</summary>
    public static Switch2Usb? Open(out string? error)
    {
        error = null;
        var path = FindDevicePath();
        if (path == null)
        {
            error = "Controller not found (or WinUSB driver not installed on its vendor interface).";
            return null;
        }

        var file = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        if (file.IsInvalid)
        {
            error = $"Could not open USB interface (error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}). Is another app using it?";
            return null;
        }
        if (!WinUsb_Initialize(file, out var iface))
        {
            error = $"WinUsb_Initialize failed (error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).";
            file.Dispose();
            return null;
        }

        byte pipeOut = 0, pipeIn = 0;
        if (WinUsb_QueryInterfaceSettings(iface, 0, out var desc))
        {
            for (byte i = 0; i < desc.bNumEndpoints; i++)
            {
                if (!WinUsb_QueryPipe(iface, 0, i, out var pipe) || pipe.PipeType != UsbdPipeType.Bulk) continue;
                if ((pipe.PipeId & 0x80) != 0) pipeIn = pipe.PipeId; else pipeOut = pipe.PipeId;
            }
        }
        if (pipeOut == 0 || pipeIn == 0)
        {
            error = "Bulk endpoints not found on vendor interface.";
            WinUsb_Free(iface);
            file.Dispose();
            return null;
        }

        uint writeTimeout = 1000, readTimeout = 250;
        WinUsb_SetPipePolicy(iface, pipeOut, PIPE_TRANSFER_TIMEOUT, 4, ref writeTimeout);
        WinUsb_SetPipePolicy(iface, pipeIn, PIPE_TRANSFER_TIMEOUT, 4, ref readTimeout);
        return new Switch2Usb(file, iface, pipeOut, pipeIn);
    }

    static string? FindDevicePath()
    {
        var match = $"vid_{VendorId:x4}&pid_{ProductId:x4}&mi_01";
        foreach (var guid in GetRegisteredInterfaceGuids())
            foreach (var path in GetInterfacePaths(guid))
                if (path.Contains(match, StringComparison.OrdinalIgnoreCase))
                    return path;
        return null;
    }

    // WinUSB installs (Zadig, Steam, etc.) each register their own interface GUID, so read it from the device node.
    static IEnumerable<Guid> GetRegisteredInterfaceGuids()
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\USB\VID_{VendorId:X4}&PID_{ProductId:X4}&MI_01");
        if (key == null) yield break;
        foreach (var instance in key.GetSubKeyNames())
        {
            using var parameters = key.OpenSubKey($@"{instance}\Device Parameters");
            var values = new List<string>();
            if (parameters?.GetValue("DeviceInterfaceGUIDs") is string[] many) values.AddRange(many);
            if (parameters?.GetValue("DeviceInterfaceGUID") is string one) values.Add(one);
            foreach (var v in values)
                if (Guid.TryParse(v, out var g)) yield return g;
        }
    }

    public bool Initialize(out string? error)
    {
        foreach (var cmd in InitSequence)
        {
            if (!SendCommand(cmd, out error)) return false;
        }
        return SetPlayerLed(1, out error);
    }

    public bool SetPlayerLed(int player, out string? error)
    {
        byte[] patterns = [0x1, 0x3, 0x7, 0xf, 0x9, 0x5, 0xd, 0x6];
        byte[] cmd = [0x09, 0x91, 0x00, 0x07, 0x00, 0x08, 0x00, 0x00, patterns[(player - 1) % 8], 0, 0, 0, 0, 0, 0, 0];
        return SendCommand(cmd, out error);
    }

    bool SendCommand(byte[] cmd, out string? error)
    {
        error = null;
        if (!WinUsb_WritePipe(_iface, _pipeOut, cmd, (uint)cmd.Length, out _, IntPtr.Zero))
        {
            error = $"USB write failed for command 0x{cmd[0]:x2} (error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).";
            return false;
        }
        // Some commands don't acknowledge; a missing reply just times out and isn't an error.
        var reply = new byte[64];
        WinUsb_ReadPipe(_iface, _pipeIn, reply, (uint)reply.Length, out _, IntPtr.Zero);
        return true;
    }

    public void Dispose()
    {
        WinUsb_Free(_iface);
        _file.Dispose();
    }
}
