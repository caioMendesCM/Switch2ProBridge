using Microsoft.Win32.SafeHandles;
using static Bridge.Core.Native;

namespace Bridge.Core;

/// <summary>Reads raw input reports from the controller's HID interface.</summary>
public sealed class HidInput : IDisposable
{
    readonly FileStream _stream;
    public int ReportLength { get; }

    HidInput(SafeFileHandle handle, int reportLength)
    {
        ReportLength = reportLength;
        _stream = new FileStream(handle, FileAccess.Read, 0, isAsync: true);
    }

    public static HidInput? Open(out string? error)
    {
        error = null;
        HidD_GetHidGuid(out var hidGuid);
        var match = $"vid_{Switch2Usb.VendorId:x4}&pid_{Switch2Usb.ProductId:x4}";
        var path = GetInterfacePaths(hidGuid).FirstOrDefault(p => p.Contains(match, StringComparison.OrdinalIgnoreCase));
        if (path == null)
        {
            error = "Controller HID interface not found.";
            return null;
        }

        var handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            error = $"Could not open HID interface (error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).";
            return null;
        }

        int length = 64;
        if (HidD_GetPreparsedData(handle, out var preparsed))
        {
            if (HidP_GetCaps(preparsed, out var caps) == 0x00110000 /* HIDP_STATUS_SUCCESS */)
                length = caps.InputReportByteLength;
            HidD_FreePreparsedData(preparsed);
        }
        return new HidInput(handle, length);
    }

    /// <summary>
    /// Waits for the next report (byte 0 is the report ID). Returns 0 on timeout.
    /// Throws IOException when the controller is unplugged.
    /// </summary>
    public int Read(byte[] buffer, int timeoutMs, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            return _stream.ReadAsync(buffer.AsMemory(0, ReportLength), cts.Token).AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return 0;
        }
    }

    public void Dispose() => _stream.Dispose();
}
