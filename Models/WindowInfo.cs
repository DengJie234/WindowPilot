using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowPilot.Models;

public sealed class WindowInfo : INotifyPropertyChanged
{
    private bool _isTopMost;
    private bool _isClickThrough;
    private int _opacityPercent = 100;

    public nint Handle { get; init; }
    public string HandleHex => $"0x{Handle.ToInt64():X}";
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public string ProcessPath { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string MonitorDeviceName { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public uint Dpi { get; init; } = 96;
    public SavedWindowState WindowState { get; init; } = SavedWindowState.Normal;
    public string Bounds => $"{X}, {Y}, {Width} x {Height}";
    public string DisplayName => string.IsNullOrWhiteSpace(ProcessName) ? Title : $"{Title} ({ProcessName})";
    public WindowIdentity Identity => new()
    {
        Hwnd = Handle.ToInt64(),
        ProcessId = ProcessId,
        ProcessName = ProcessName,
        ProcessPath = ProcessPath,
        WindowTitle = Title,
        ClassName = ClassName
    };

    public bool IsTopMost
    {
        get => _isTopMost;
        set => SetField(ref _isTopMost, value);
    }

    public bool IsClickThrough
    {
        get => _isClickThrough;
        set => SetField(ref _isClickThrough, value);
    }

    public int OpacityPercent
    {
        get => _opacityPercent;
        set => SetField(ref _opacityPercent, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
