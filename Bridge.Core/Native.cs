using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Bridge.Core;

internal static class Native
{
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint FILE_SHARE_READ = 0x1;
    public const uint FILE_SHARE_WRITE = 0x2;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFile(string fileName, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    // cfgmgr32: device interface enumeration
    public const uint CM_GET_DEVICE_INTERFACE_LIST_PRESENT = 0;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_Interface_List_Size(out uint len, ref Guid classGuid, string? deviceId, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_Interface_List(ref Guid classGuid, string? deviceId, char[] buffer, uint len, uint flags);

    // hid.dll
    [DllImport("hid.dll")]
    public static extern void HidD_GetHidGuid(out Guid guid);

    [DllImport("hid.dll", SetLastError = true)]
    public static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsed);

    [DllImport("hid.dll")]
    public static extern bool HidD_FreePreparsedData(IntPtr preparsed);

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
            NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")]
    public static extern int HidP_GetCaps(IntPtr preparsed, out HIDP_CAPS caps);

    // winusb.dll
    public const byte PIPE_TRANSFER_TIMEOUT = 0x03;

    public enum UsbdPipeType { Control, Isochronous, Bulk, Interrupt }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINUSB_PIPE_INFORMATION
    {
        public UsbdPipeType PipeType;
        public byte PipeId;
        public ushort MaximumPacketSize;
        public byte Interval;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct USB_INTERFACE_DESCRIPTOR
    {
        public byte bLength, bDescriptorType, bInterfaceNumber, bAlternateSetting, bNumEndpoints,
            bInterfaceClass, bInterfaceSubClass, bInterfaceProtocol, iInterface;
    }

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_Initialize(SafeFileHandle device, out IntPtr iface);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_Free(IntPtr iface);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_QueryInterfaceSettings(IntPtr iface, byte altIndex, out USB_INTERFACE_DESCRIPTOR desc);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_QueryPipe(IntPtr iface, byte altIndex, byte pipeIndex, out WINUSB_PIPE_INFORMATION info);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_SetPipePolicy(IntPtr iface, byte pipeId, uint policy, uint len, ref uint value);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_WritePipe(IntPtr iface, byte pipeId, byte[] buffer, uint len, out uint transferred, IntPtr overlapped);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_ReadPipe(IntPtr iface, byte pipeId, byte[] buffer, uint len, out uint transferred, IntPtr overlapped);

    public static List<string> GetInterfacePaths(Guid classGuid)
    {
        var result = new List<string>();
        if (CM_Get_Device_Interface_List_Size(out var len, ref classGuid, null, CM_GET_DEVICE_INTERFACE_LIST_PRESENT) != 0 || len <= 1)
            return result;
        var buf = new char[len];
        if (CM_Get_Device_Interface_List(ref classGuid, null, buf, len, CM_GET_DEVICE_INTERFACE_LIST_PRESENT) != 0)
            return result;
        foreach (var s in new string(buf).Split('\0', StringSplitOptions.RemoveEmptyEntries))
            result.Add(s);
        return result;
    }
}
