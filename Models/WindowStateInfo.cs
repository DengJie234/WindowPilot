namespace WindowPilot.Models;

public enum SavedWindowState
{
    Normal,
    Minimized,
    Maximized
}

public sealed class WindowStateInfo
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string MonitorDeviceName { get; set; } = string.Empty;
    public int MonitorX { get; set; }
    public int MonitorY { get; set; }
    public int MonitorWidth { get; set; }
    public int MonitorHeight { get; set; }
    public uint Dpi { get; set; } = 96;
    public bool IsTopMost { get; set; }
    public int Opacity { get; set; } = 100;
    public bool IsClickThrough { get; set; }
    public SavedWindowState WindowState { get; set; } = SavedWindowState.Normal;
}
