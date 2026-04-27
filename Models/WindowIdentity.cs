namespace WindowPilot.Models;

public sealed class WindowIdentity
{
    public long Hwnd { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ProcessPath { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;

    public string HwndHex => $"0x{Hwnd:X}";
}
