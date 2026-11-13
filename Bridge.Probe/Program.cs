using Bridge.Core;

// Diagnostic tool: starts the controller (unless --hid-only) and prints decoded input.
var skipInit = args.Contains("--hid-only");
var seconds = args.FirstOrDefault(a => int.TryParse(a, out _)) is { } s ? int.Parse(s) : 3;

Switch2Usb? usb = null;
if (!skipInit)
{
    usb = Switch2Usb.Open(out var usbErr);
    if (usb == null) Console.WriteLine($"USB: {usbErr}");
    else if (!usb.Initialize(out usbErr)) Console.WriteLine($"Init: {usbErr}");
    else Console.WriteLine("Init sequence sent.");
}

using var hid = HidInput.Open(out var err);
if (hid == null) { Console.WriteLine(err); return 1; }
Console.WriteLine($"HID opened, report length {hid.ReportLength}.");

var parser = new ProReportParser();
var buf = new byte[hid.ReportLength];
var end = DateTime.UtcNow.AddSeconds(seconds);
int count = 0;
string last = "";
while (DateTime.UtcNow < end)
{
    var n = hid.Read(buf, 1000, CancellationToken.None);
    if (n == 0) { Console.WriteLine("(no report within 1s)"); continue; }
    count++;
    if (!parser.TryParse(buf.AsSpan(0, n), out var st)) continue;
    var line = $"{st.Buttons} L({st.LeftX:+0.00;-0.00}, {st.LeftY:+0.00;-0.00}) R({st.RightX:+0.00;-0.00}, {st.RightY:+0.00;-0.00}) bat={st.Battery} raw={Convert.ToHexString(buf, 0, 12)}";
    if (line != last && !line.StartsWith(last.Split(" raw=")[0] + " raw=")) { Console.WriteLine($"{count,5}: {line}"); }
    last = line;
}
Console.WriteLine($"{count} reports in {seconds}s.");
usb?.Dispose();
return 0;
