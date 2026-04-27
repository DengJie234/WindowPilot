namespace WindowPilot.Models;

public sealed class WindowLayoutItem
{
    public WindowIdentity Identity { get; set; } = new();
    public WindowStateInfo State { get; set; } = new();

    public long Hwnd
    {
        get => Identity.Hwnd;
        set => Identity.Hwnd = value;
    }

    public int ProcessId
    {
        get => Identity.ProcessId;
        set => Identity.ProcessId = value;
    }

    public string ProcessName
    {
        get => Identity.ProcessName;
        set => Identity.ProcessName = value;
    }

    public string ProcessPath
    {
        get => Identity.ProcessPath;
        set => Identity.ProcessPath = value;
    }

    public string WindowTitle
    {
        get => Identity.WindowTitle;
        set => Identity.WindowTitle = value;
    }

    public string ClassName
    {
        get => Identity.ClassName;
        set => Identity.ClassName = value;
    }

    public int X
    {
        get => State.X;
        set => State.X = value;
    }

    public int Y
    {
        get => State.Y;
        set => State.Y = value;
    }

    public int Width
    {
        get => State.Width;
        set => State.Width = value;
    }

    public int Height
    {
        get => State.Height;
        set => State.Height = value;
    }

    public string MonitorDeviceName
    {
        get => State.MonitorDeviceName;
        set => State.MonitorDeviceName = value;
    }

    public bool IsTopMost
    {
        get => State.IsTopMost;
        set => State.IsTopMost = value;
    }

    public int Opacity
    {
        get => State.Opacity;
        set => State.Opacity = value;
    }

    public bool IsClickThrough
    {
        get => State.IsClickThrough;
        set => State.IsClickThrough = value;
    }

    public SavedWindowState WindowState
    {
        get => State.WindowState;
        set => State.WindowState = value;
    }
}
