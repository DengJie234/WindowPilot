using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WindowPilot.Models;
using WindowPilot.Native;

namespace WindowPilot.Services;

public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message = "完成") => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}

public sealed class WindowService
{
    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "csrss", "wininit", "winlogon", "services", "smss", "lsass", "dwm", "fontdrvhost", "sihost", "system", "idle"
    };

    private readonly Dictionary<nint, WindowSnapshot> _snapshots = [];
    private readonly int _ownProcessId = Environment.ProcessId;
    private readonly byte _minOpacity;
    private readonly byte _maxOpacity;
    private readonly byte _opacityStep;
    private readonly BlacklistService? _blacklistService;

    public WindowService(byte minOpacity, byte maxOpacity, byte opacityStep, BlacklistService? blacklistService = null)
    {
        _minOpacity = minOpacity == 0 ? (byte)51 : minOpacity;
        _maxOpacity = maxOpacity == 0 ? (byte)255 : maxOpacity;
        _opacityStep = opacityStep == 0 ? (byte)25 : opacityStep;
        _blacklistService = blacklistService;
    }

    public IReadOnlyCollection<WindowSnapshot> TrackedSnapshots => _snapshots.Values;

    public bool HasManagedVisualState(WindowInfo window)
    {
        CleanupInvalidHandles();
        if (!_snapshots.TryGetValue(window.Handle, out var snapshot) || !NativeMethods.IsWindow(window.Handle))
        {
            return false;
        }

        var style = GetExtendedStyle(window.Handle);
        var isTopMost = (style & NativeMethods.WS_EX_TOPMOST) != 0;
        var isClickThrough = (style & NativeMethods.WS_EX_TRANSPARENT) != 0;
        var wasClickThrough = (snapshot.ExtendedStyle & NativeMethods.WS_EX_TRANSPARENT) != 0;
        var alpha = GetCurrentAlpha(window.Handle);

        var topMostChanged = isTopMost != snapshot.WasTopMost;
        var clickThroughChanged = isClickThrough != wasClickThrough;
        var opacityChanged = snapshot.HadAlphaAttribute
            ? alpha != snapshot.OriginalAlpha
            : alpha < _maxOpacity;

        return topMostChanged || clickThroughChanged || opacityChanged;
    }

    public OperationResult RestoreManagedWindow(nint hwnd)
    {
        CleanupInvalidHandles();
        if (!_snapshots.TryGetValue(hwnd, out var snapshot))
        {
            return OperationResult.Ok("目标窗口没有 WindowPilot 管理状态。");
        }

        var result = RestoreSnapshot(snapshot);
        if (result.Success)
        {
            _snapshots.Remove(hwnd);
        }

        return result;
    }

    public WindowInfo? GetForegroundWindowInfo()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == nint.Zero || !IsCandidateWindow(hwnd, allowUntitled: true))
        {
            return null;
        }

        return CreateWindowInfo(hwnd);
    }

    public WindowInfo? GetWindowInfo(nint hwnd)
    {
        CleanupInvalidHandles();
        return NativeMethods.IsWindow(hwnd) ? CreateWindowInfo(hwnd) : null;
    }

    public List<WindowInfo> EnumerateVisibleWindows(bool sort = true)
    {
        CleanupInvalidHandles();
        var windows = new List<WindowInfo>();

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!IsCandidateWindow(hwnd, allowUntitled: false))
            {
                return true;
            }

            var info = CreateWindowInfo(hwnd);
            if (info is not null)
            {
                windows.Add(info);
            }

            return true;
        }, nint.Zero);

        return sort
            ? windows
                .OrderBy(w => w.ProcessName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
            : windows;
    }

    public WindowLayoutItem? CreateLayoutItem(nint hwnd)
    {
        var info = GetWindowInfo(hwnd);
        return info is null ? null : CreateLayoutItem(info);
    }

    public WindowLayoutItem CreateLayoutItem(WindowInfo info)
    {
        var monitor = GetMonitorInfoForWindow(info.Handle);
        return new WindowLayoutItem
        {
            Identity = info.Identity,
            State = new WindowStateInfo
            {
                X = info.X,
                Y = info.Y,
                Width = info.Width,
                Height = info.Height,
                MonitorDeviceName = info.MonitorDeviceName,
                MonitorX = monitor.rcWork.Left,
                MonitorY = monitor.rcWork.Top,
                MonitorWidth = monitor.rcWork.Width,
                MonitorHeight = monitor.rcWork.Height,
                Dpi = info.Dpi,
                IsTopMost = info.IsTopMost,
                Opacity = info.OpacityPercent,
                IsClickThrough = info.IsClickThrough,
                WindowState = info.WindowState
            }
        };
    }

    public OperationResult ToggleTopMost(nint hwnd)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        CaptureSnapshot(hwnd);
        var isTopMost = IsTopMost(hwnd);
        return SetTopMostInternal(hwnd, !isTopMost, isTopMost ? "已取消置顶" : "已置顶");
    }

    public OperationResult SetTopMost(nint hwnd, bool enabled)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        CaptureSnapshot(hwnd);
        return SetTopMostInternal(hwnd, enabled, enabled ? "已置顶" : "已取消置顶");
    }

    public OperationResult IncreaseOpacity(nint hwnd) => AdjustOpacity(hwnd, _opacityStep);

    public OperationResult DecreaseOpacity(nint hwnd) => AdjustOpacity(hwnd, -_opacityStep);

    public OperationResult RestoreOpacity(nint hwnd)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        CaptureSnapshot(hwnd);
        return SetOpacity(hwnd, _maxOpacity);
    }

    public OperationResult SetOpacityPercent(nint hwnd, int percent)
    {
        var alpha = PercentToAlpha(percent);
        return SetOpacity(hwnd, alpha);
    }

    public OperationResult ToggleClickThrough(nint hwnd)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        var style = GetExtendedStyle(hwnd);
        return SetClickThrough(hwnd, (style & NativeMethods.WS_EX_TRANSPARENT) == 0);
    }

    public OperationResult SetClickThrough(nint hwnd, bool enabled)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        CaptureSnapshot(hwnd);
        var style = GetExtendedStyle(hwnd);
        var newStyle = enabled
            ? style | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT
            : style & ~NativeMethods.WS_EX_TRANSPARENT;

        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new nint(newStyle));
        var ok = RefreshFrame(hwnd);
        return ResultFromWin32(ok, enabled ? "已启用点击穿透" : "已取消点击穿透");
    }

    public OperationResult MoveLeftHalf(nint hwnd) => MoveToMonitorArea(hwnd, WindowMovePreset.LeftHalf);

    public OperationResult MoveRightHalf(nint hwnd) => MoveToMonitorArea(hwnd, WindowMovePreset.RightHalf);

    public OperationResult CenterWindow(nint hwnd) => MoveToMonitorArea(hwnd, WindowMovePreset.Center);

    public OperationResult MoveTopLeft(nint hwnd) => MoveToMonitorArea(hwnd, WindowMovePreset.TopLeft);

    public OperationResult MoveTopRight(nint hwnd) => MoveToMonitorArea(hwnd, WindowMovePreset.TopRight);

    public OperationResult MoveBottomLeft(nint hwnd) => MoveToMonitorArea(hwnd, WindowMovePreset.BottomLeft);

    public OperationResult MoveBottomRight(nint hwnd) => MoveToMonitorArea(hwnd, WindowMovePreset.BottomRight);

    public OperationResult SetWindowSize(nint hwnd, int width, int height)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return LastError("无法读取窗口位置");
        }

        var workArea = GetWorkAreaForWindow(hwnd);
        width = Math.Clamp(width, 160, workArea.Width);
        height = Math.Clamp(height, 120, workArea.Height);
        var ok = NativeMethods.MoveWindow(hwnd, rect.Left, rect.Top, width, height, true);
        return ResultFromWin32(ok, $"已设置窗口大小 {width} x {height}");
    }

    public OperationResult MoveResizeWindow(nint hwnd, int x, int y, int width, int height)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        width = Math.Max(80, width);
        height = Math.Max(60, height);
        var ok = NativeMethods.MoveWindow(hwnd, x, y, width, height, true);
        return ResultFromWin32(ok, "已移动窗口");
    }

    public OperationResult MinimizeWindow(nint hwnd)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        return ResultFromWin32(NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE), "已最小化窗口");
    }

    public OperationResult RestoreWindow(nint hwnd)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        return ResultFromWin32(NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE), "已恢复窗口");
    }

    public OperationResult SetSavedWindowState(nint hwnd, SavedWindowState state)
    {
        return state switch
        {
            SavedWindowState.Minimized => MinimizeWindow(hwnd),
            SavedWindowState.Maximized => EnsureCanControl(hwnd, out var message)
                ? ResultFromWin32(NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MAXIMIZE), "已最大化窗口")
                : OperationResult.Fail(message),
            _ => RestoreWindow(hwnd)
        };
    }

    public OperationResult RestoreAll()
    {
        CleanupInvalidHandles();
        var restored = 0;

        foreach (var snapshot in _snapshots.Values.ToList())
        {
            if (RestoreSnapshot(snapshot).Success)
            {
                restored++;
            }
        }

        _snapshots.Clear();
        return OperationResult.Ok($"已恢复 {restored} 个窗口");
    }

    public OperationResult ClearAllTopMost()
    {
        var count = 0;
        foreach (var snapshot in _snapshots.Values.ToList())
        {
            if (!NativeMethods.IsWindow(snapshot.Handle))
            {
                continue;
            }

            if (NativeMethods.SetWindowPos(snapshot.Handle, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
            {
                count++;
            }
        }

        return OperationResult.Ok($"已取消 {count} 个窗口置顶");
    }

    public OperationResult RestoreAllOpacity()
    {
        var count = 0;
        foreach (var snapshot in _snapshots.Values.ToList())
        {
            if (!NativeMethods.IsWindow(snapshot.Handle))
            {
                continue;
            }

            var style = GetExtendedStyle(snapshot.Handle);
            var keepLayeredForClickThrough = (style & NativeMethods.WS_EX_TRANSPARENT) != 0;
            if (snapshot.WasLayered || keepLayeredForClickThrough)
            {
                NativeMethods.SetWindowLongPtr(snapshot.Handle, NativeMethods.GWL_EXSTYLE, new nint(style | NativeMethods.WS_EX_LAYERED));
                NativeMethods.SetLayeredWindowAttributes(snapshot.Handle, 0, snapshot.OriginalAlpha, NativeMethods.LWA_ALPHA);
            }
            else
            {
                NativeMethods.SetWindowLongPtr(snapshot.Handle, NativeMethods.GWL_EXSTYLE, new nint(style & ~NativeMethods.WS_EX_LAYERED));
            }

            RefreshFrame(snapshot.Handle);
            count++;
        }

        return OperationResult.Ok($"已恢复 {count} 个窗口透明度");
    }

    public OperationResult ClearAllClickThrough()
    {
        var count = 0;
        foreach (var snapshot in _snapshots.Values.ToList())
        {
            if (!NativeMethods.IsWindow(snapshot.Handle))
            {
                continue;
            }

            var style = GetExtendedStyle(snapshot.Handle);
            NativeMethods.SetWindowLongPtr(snapshot.Handle, NativeMethods.GWL_EXSTYLE, new nint(style & ~NativeMethods.WS_EX_TRANSPARENT));
            RefreshFrame(snapshot.Handle);
            count++;
        }

        return OperationResult.Ok($"已取消 {count} 个窗口点击穿透");
    }

    public List<PersistedWindowState> CreatePersistedStates()
    {
        CleanupInvalidHandles();
        return _snapshots.Keys
            .Select(CreateWindowInfo)
            .Where(info => info is not null)
            .Select(info => new PersistedWindowState
            {
                HandleHex = info!.HandleHex,
                Title = info.Title,
                ProcessName = info.ProcessName,
                IsTopMost = info.IsTopMost,
                IsClickThrough = info.IsClickThrough,
                OpacityPercent = info.OpacityPercent
            })
            .ToList();
    }

    public NativeMethods.RECT GetWorkAreaForWindow(nint hwnd)
    {
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (monitor != nint.Zero && NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return info.rcWork;
        }

        return new NativeMethods.RECT
        {
            Left = 0,
            Top = 0,
            Right = 1920,
            Bottom = 1080
        };
    }

    public NativeMethods.MONITORINFOEX GetMonitorInfoForWindow(nint hwnd)
    {
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFOEX
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
            szDevice = string.Empty
        };

        if (monitor != nint.Zero && NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return info;
        }

        info.rcWork = GetWorkAreaForWindow(hwnd);
        info.rcMonitor = info.rcWork;
        return info;
    }

    public bool IsFullScreenApplicationActive()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == nint.Zero || !NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        var processName = GetProcessName(GetProcessId(hwnd));
        if (ProtectedProcesses.Contains(processName) || processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var monitor = GetMonitorInfoForWindow(hwnd).rcMonitor;
        var tolerance = 2;
        return Math.Abs(rect.Left - monitor.Left) <= tolerance &&
               Math.Abs(rect.Top - monitor.Top) <= tolerance &&
               Math.Abs(rect.Width - monitor.Width) <= tolerance &&
               Math.Abs(rect.Height - monitor.Height) <= tolerance;
    }

    private OperationResult AdjustOpacity(nint hwnd, int delta)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        CaptureSnapshot(hwnd);
        var alpha = GetCurrentAlpha(hwnd);
        var newAlpha = (byte)Math.Clamp(alpha + delta, _minOpacity, _maxOpacity);
        return SetOpacity(hwnd, newAlpha);
    }

    private OperationResult SetOpacity(nint hwnd, byte alpha)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        CaptureSnapshot(hwnd);
        alpha = (byte)Math.Clamp(alpha, _minOpacity, _maxOpacity);
        var style = GetExtendedStyle(hwnd) | NativeMethods.WS_EX_LAYERED;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new nint(style));
        var ok = NativeMethods.SetLayeredWindowAttributes(hwnd, 0, alpha, NativeMethods.LWA_ALPHA);
        RefreshFrame(hwnd);
        return ResultFromWin32(ok, $"不透明度 {AlphaToPercent(alpha)}%");
    }

    private OperationResult SetTopMostInternal(nint hwnd, bool enabled, string message)
    {
        var insertAfter = enabled ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST;
        var ok = NativeMethods.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        return ResultFromWin32(ok, message);
    }

    private OperationResult MoveToMonitorArea(nint hwnd, WindowMovePreset preset)
    {
        if (!EnsureCanControl(hwnd, out var message))
        {
            return OperationResult.Fail(message);
        }

        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return LastError("无法读取窗口位置");
        }

        var workArea = GetWorkAreaForWindow(hwnd);
        var width = Math.Min(rect.Width, workArea.Width);
        var height = Math.Min(rect.Height, workArea.Height);
        var x = rect.Left;
        var y = rect.Top;
        var label = "已移动窗口";

        switch (preset)
        {
            case WindowMovePreset.LeftHalf:
                x = workArea.Left;
                y = workArea.Top;
                width = workArea.Width / 2;
                height = workArea.Height;
                label = "已移动到左半屏";
                break;
            case WindowMovePreset.RightHalf:
                width = workArea.Width / 2;
                x = workArea.Left + width;
                y = workArea.Top;
                height = workArea.Height;
                label = "已移动到右半屏";
                break;
            case WindowMovePreset.Center:
                x = workArea.Left + (workArea.Width - width) / 2;
                y = workArea.Top + (workArea.Height - height) / 2;
                label = "已居中窗口";
                break;
            case WindowMovePreset.TopLeft:
                x = workArea.Left;
                y = workArea.Top;
                label = "已移动到左上角";
                break;
            case WindowMovePreset.TopRight:
                x = workArea.Right - width;
                y = workArea.Top;
                label = "已移动到右上角";
                break;
            case WindowMovePreset.BottomLeft:
                x = workArea.Left;
                y = workArea.Bottom - height;
                label = "已移动到左下角";
                break;
            case WindowMovePreset.BottomRight:
                x = workArea.Right - width;
                y = workArea.Bottom - height;
                label = "已移动到右下角";
                break;
        }

        x = Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        y = Math.Clamp(y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        var ok = NativeMethods.MoveWindow(hwnd, x, y, width, height, true);
        return ResultFromWin32(ok, label);
    }

    private OperationResult RestoreSnapshot(WindowSnapshot snapshot)
    {
        if (!NativeMethods.IsWindow(snapshot.Handle))
        {
            return OperationResult.Fail("窗口已关闭");
        }

        NativeMethods.SetWindowLongPtr(snapshot.Handle, NativeMethods.GWL_EXSTYLE, new nint(snapshot.ExtendedStyle));
        if (snapshot.WasLayered && snapshot.HadAlphaAttribute)
        {
            NativeMethods.SetLayeredWindowAttributes(snapshot.Handle, 0, snapshot.OriginalAlpha, NativeMethods.LWA_ALPHA);
        }

        var topMost = snapshot.WasTopMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST;
        NativeMethods.SetWindowPos(snapshot.Handle, topMost, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
        return OperationResult.Ok();
    }

    private void CaptureSnapshot(nint hwnd)
    {
        if (_snapshots.ContainsKey(hwnd) || !NativeMethods.IsWindow(hwnd))
        {
            return;
        }

        var style = GetExtendedStyle(hwnd);
        var hadLayered = (style & NativeMethods.WS_EX_LAYERED) != 0;
        var hadAlpha = NativeMethods.GetLayeredWindowAttributes(hwnd, out _, out var alpha, out var flags) &&
                       (flags & NativeMethods.LWA_ALPHA) != 0;
        var info = CreateWindowInfo(hwnd);

        _snapshots[hwnd] = new WindowSnapshot
        {
            Handle = hwnd,
            Title = info?.Title ?? string.Empty,
            ProcessName = info?.ProcessName ?? string.Empty,
            ExtendedStyle = style,
            WasTopMost = (style & NativeMethods.WS_EX_TOPMOST) != 0,
            WasLayered = hadLayered,
            OriginalAlpha = hadAlpha ? alpha : (byte)255,
            HadAlphaAttribute = hadAlpha
        };
    }

    private WindowInfo? CreateWindowInfo(nint hwnd)
    {
        if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        var pid = GetProcessId(hwnd);
        var processName = GetProcessName(pid);
        var style = GetExtendedStyle(hwnd);
        var alpha = GetCurrentAlpha(hwnd);

        return new WindowInfo
        {
            Handle = hwnd,
            Title = GetWindowTitle(hwnd),
            ProcessName = processName,
            ProcessPath = GetProcessPath(pid),
            ClassName = GetClassName(hwnd),
            ProcessId = pid,
            X = rect.Left,
            Y = rect.Top,
            Width = Math.Max(0, rect.Width),
            Height = Math.Max(0, rect.Height),
            MonitorDeviceName = GetMonitorDeviceName(hwnd),
            Dpi = GetDpi(hwnd),
            WindowState = GetSavedWindowState(hwnd),
            IsTopMost = (style & NativeMethods.WS_EX_TOPMOST) != 0,
            IsClickThrough = (style & NativeMethods.WS_EX_TRANSPARENT) != 0,
            OpacityPercent = AlphaToPercent(alpha)
        };
    }

    private bool IsCandidateWindow(nint hwnd, bool allowUntitled)
    {
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
        {
            return false;
        }

        var pid = GetProcessId(hwnd);
        if (pid == _ownProcessId || ProtectedProcesses.Contains(GetProcessName(pid)))
        {
            return false;
        }

        var title = GetWindowTitle(hwnd);
        if (!allowUntitled && string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var style = GetExtendedStyle(hwnd);
        if ((style & NativeMethods.WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        return !IsCloaked(hwnd);
    }

    private bool EnsureCanControl(nint hwnd, out string message)
    {
        CleanupInvalidHandles();
        if (!NativeMethods.IsWindow(hwnd))
        {
            message = "窗口句柄无效，窗口可能已经关闭。";
            return false;
        }

        var info = CreateWindowInfo(hwnd);
        if (info is null)
        {
            message = "无法读取目标窗口信息。";
            return false;
        }

        if (info.ProcessId == _ownProcessId || ProtectedProcesses.Contains(info.ProcessName))
        {
            message = "出于稳定性考虑，WindowPilot 不控制自身或系统关键窗口。";
            return false;
        }

        if (_blacklistService is not null && _blacklistService.IsBlocked(info, out var reason))
        {
            message = reason;
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void CleanupInvalidHandles()
    {
        foreach (var hwnd in _snapshots.Keys.Where(hwnd => !NativeMethods.IsWindow(hwnd)).ToList())
        {
            _snapshots.Remove(hwnd);
        }
    }

    private static int GetExtendedStyle(nint hwnd) => NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt32();

    private static bool IsTopMost(nint hwnd) => (GetExtendedStyle(hwnd) & NativeMethods.WS_EX_TOPMOST) != 0;

    private static int GetProcessId(nint hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    private static string GetWindowTitle(nint hwnd)
    {
        var length = NativeMethods.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetClassName(nint hwnd)
    {
        var builder = new StringBuilder(256);
        return NativeMethods.GetClassName(hwnd, builder, builder.Capacity) > 0 ? builder.ToString() : string.Empty;
    }

    private static string GetProcessName(int processId)
    {
        try
        {
            return Process.GetProcessById(processId).ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetProcessPath(int processId)
    {
        try
        {
            return Process.GetProcessById(processId).MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static byte GetCurrentAlpha(nint hwnd)
    {
        var style = GetExtendedStyle(hwnd);
        if ((style & NativeMethods.WS_EX_LAYERED) == 0)
        {
            return 255;
        }

        return NativeMethods.GetLayeredWindowAttributes(hwnd, out _, out var alpha, out var flags) &&
               (flags & NativeMethods.LWA_ALPHA) != 0
            ? alpha
            : (byte)255;
    }

    private static string GetMonitorDeviceName(nint hwnd)
    {
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFOEX
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
            szDevice = string.Empty
        };

        return monitor != nint.Zero && NativeMethods.GetMonitorInfo(monitor, ref info) ? info.szDevice : string.Empty;
    }

    private static uint GetDpi(nint hwnd)
    {
        try
        {
            var dpi = NativeMethods.GetDpiForWindow(hwnd);
            return dpi == 0 ? 96 : dpi;
        }
        catch
        {
            return 96;
        }
    }

    private static SavedWindowState GetSavedWindowState(nint hwnd)
    {
        if (NativeMethods.IsIconic(hwnd))
        {
            return SavedWindowState.Minimized;
        }

        return NativeMethods.IsZoomed(hwnd) ? SavedWindowState.Maximized : SavedWindowState.Normal;
    }

    private static bool IsCloaked(nint hwnd)
    {
        var hr = NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_CLOAKED, out var cloaked, sizeof(int));
        return hr == 0 && cloaked != 0;
    }

    private static byte PercentToAlpha(int percent) => (byte)Math.Clamp((int)Math.Round(percent / 100.0 * 255), 51, 255);

    private static int AlphaToPercent(byte alpha) => (int)Math.Round(alpha / 255.0 * 100);

    private static bool RefreshFrame(nint hwnd) =>
        NativeMethods.SetWindowPos(hwnd, nint.Zero, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER |
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);

    private static OperationResult ResultFromWin32(bool ok, string successMessage) => ok ? OperationResult.Ok(successMessage) : LastError(successMessage);

    private static OperationResult LastError(string context)
    {
        var error = Marshal.GetLastWin32Error();
        var message = error == 0 ? "可能是权限不足或目标窗口不支持该操作。" : new Win32Exception(error).Message;
        return OperationResult.Fail($"{context}失败：{message} 普通权限程序无法控制管理员权限窗口，全屏游戏或反作弊游戏也可能不支持。");
    }

    private enum WindowMovePreset
    {
        LeftHalf,
        RightHalf,
        Center,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}
