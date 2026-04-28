using System.Runtime.InteropServices;
using System.Windows.Interop;
using WindowPilot.Native;
using WindowPilot.ViewModels;
using WindowPilot.Views.Tray;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WindowPilot.Services;

public sealed class TrayFlyoutService : IDisposable
{
    private const int GapPixels = 10;
    private const int WorkAreaMarginPixels = 12;
    private const int FallbackAnchorSizePixels = 18;
    private const int OutsideClickIgnoreMilliseconds = 180;
    private const int TrayIconHitPaddingPixels = 18;
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;

    private TrayFlyout? _flyout;
    private nint _flyoutHwnd;
    private nint _mouseHook;
    private LowLevelMouseProc? _mouseProc;
    private DateTime _ignoreMouseUntilUtc;
    private Drawing.Rectangle _lastTrayIconBounds = Drawing.Rectangle.Empty;

    public TrayFlyoutViewModel? ViewModel { get; set; }
    public bool IsOpen => _flyout?.IsVisible == true;

    public void Toggle() => Toggle(null, Forms.Cursor.Position);

    public void Show() => Show(null, Forms.Cursor.Position);

    public void Toggle(Drawing.Rectangle? trayIconBounds, Drawing.Point cursorPosition)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (IsOpen)
            {
                Hide();
                return;
            }

            Show(trayIconBounds, cursorPosition);
        });
    }

    public void Hide() => Close();

    public void CloseAfterCommand(Action action)
    {
        Hide();
        action();
    }

    public void Close()
    {
        StopGlobalMouseHook();

        if (_flyout is null)
        {
            _flyoutHwnd = nint.Zero;
            return;
        }

        var flyout = _flyout;
        _flyout = null;
        _flyoutHwnd = nint.Zero;
        flyout.CloseOnDeactivate = false;
        flyout.Close();
    }

    public void Dispose() => Close();

    public void Show(Drawing.Rectangle? trayIconBounds, Drawing.Point cursorPosition)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (IsOpen)
        {
            Hide();
        }

        var flyout = new TrayFlyout
        {
            DataContext = ViewModel,
            Left = -100000,
            Top = -100000
        };
        _flyout = flyout;

        var hwnd = new WindowInteropHelper(flyout).EnsureHandle();
        _flyoutHwnd = hwnd;
        flyout.Closed += (_, _) =>
        {
            StopGlobalMouseHook();
            if (ReferenceEquals(_flyout, flyout))
            {
                _flyout = null;
            }

            if (_flyoutHwnd == hwnd)
            {
                _flyoutHwnd = nint.Zero;
            }
        };

        flyout.Show();
        flyout.UpdateLayout();

        var flyoutSize = GetFlyoutPixelSize(hwnd);
        var anchor = CreateAnchor(trayIconBounds, cursorPosition);
        _lastTrayIconBounds = Inflate(anchor, TrayIconHitPaddingPixels);
        _ignoreMouseUntilUtc = DateTime.UtcNow.AddMilliseconds(OutsideClickIgnoreMilliseconds);
        var screen = Forms.Screen.FromPoint(GetAnchorCenter(anchor));
        var edge = GetTaskbarEdge(screen);
        var position = PositionTrayFlyout(anchor, screen.WorkingArea, edge, flyoutSize.Width, flyoutSize.Height);

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            position.Left,
            position.Top,
            position.Width,
            position.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        StartGlobalMouseHook();
    }

    private void StartGlobalMouseHook()
    {
        if (_mouseHook != nint.Zero)
        {
            return;
        }

        _mouseProc = OnGlobalMouseHook;
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(null), 0);
    }

    private void StopGlobalMouseHook()
    {
        if (_mouseHook == nint.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_mouseHook);
        _mouseHook = nint.Zero;
        _mouseProc = null;
    }

    private nint OnGlobalMouseHook(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && IsMouseDownMessage(wParam))
        {
            var hook = Marshal.PtrToStructure<MouseHookStruct>(lParam);
            var point = new Drawing.Point(hook.Point.X, hook.Point.Y);
            if (DateTime.UtcNow >= _ignoreMouseUntilUtc && IsOutsideFlyoutAndTrayIcon(point))
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(Hide));
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private bool IsOutsideFlyoutAndTrayIcon(Drawing.Point point)
    {
        if (_lastTrayIconBounds.Contains(point))
        {
            return false;
        }

        return !TryGetFlyoutBounds(out var flyoutBounds) || !flyoutBounds.Contains(point);
    }

    private bool TryGetFlyoutBounds(out Drawing.Rectangle bounds)
    {
        bounds = Drawing.Rectangle.Empty;
        if (_flyoutHwnd == nint.Zero || !NativeMethods.GetWindowRect(_flyoutHwnd, out var rect))
        {
            return false;
        }

        bounds = Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static bool IsMouseDownMessage(nint message)
    {
        var value = message.ToInt32();
        return value is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN;
    }

    private static Drawing.Size GetFlyoutPixelSize(nint hwnd)
    {
        if (NativeMethods.GetWindowRect(hwnd, out var rect) && rect.Width > 0 && rect.Height > 0)
        {
            return new Drawing.Size(rect.Width, rect.Height);
        }

        return new Drawing.Size(340, 560);
    }

    private static Drawing.Rectangle CreateAnchor(Drawing.Rectangle? trayIconBounds, Drawing.Point cursorPosition)
    {
        if (trayIconBounds is { Width: > 0, Height: > 0 } bounds)
        {
            return bounds;
        }

        var half = FallbackAnchorSizePixels / 2;
        return new Drawing.Rectangle(
            cursorPosition.X - half,
            cursorPosition.Y - half,
            FallbackAnchorSizePixels,
            FallbackAnchorSizePixels);
    }

    private static Drawing.Point GetAnchorCenter(Drawing.Rectangle anchor) =>
        new(anchor.Left + anchor.Width / 2, anchor.Top + anchor.Height / 2);

    private static Drawing.Rectangle Inflate(Drawing.Rectangle rectangle, int padding)
    {
        rectangle.Inflate(padding, padding);
        return rectangle;
    }

    internal static Drawing.Rectangle PositionTrayFlyout(
        Drawing.Rectangle trayIconRect,
        Drawing.Rectangle workArea,
        TaskbarEdge edge,
        int flyoutWidth,
        int flyoutHeight)
    {
        var size = LimitSizeToWorkArea(flyoutWidth, flyoutHeight, workArea);
        var candidates = BuildCandidates(trayIconRect, edge, size.Width, size.Height);

        foreach (var candidate in candidates)
        {
            if (FitsInWorkArea(candidate, workArea))
            {
                return candidate;
            }
        }

        var bestCandidate = candidates
            .OrderBy(candidate => OverflowScore(candidate, workArea))
            .First();
        return ClampToWorkArea(bestCandidate, workArea);
    }

    private static IReadOnlyList<Drawing.Rectangle> BuildCandidates(
        Drawing.Rectangle icon,
        TaskbarEdge edge,
        int width,
        int height)
    {
        Drawing.Rectangle AboveRight() => new(icon.Right - width, icon.Top - GapPixels - height, width, height);
        Drawing.Rectangle AboveLeft() => new(icon.Left, icon.Top - GapPixels - height, width, height);
        Drawing.Rectangle BelowRight() => new(icon.Right - width, icon.Bottom + GapPixels, width, height);
        Drawing.Rectangle BelowLeft() => new(icon.Left, icon.Bottom + GapPixels, width, height);
        Drawing.Rectangle LeftBottom() => new(icon.Left - GapPixels - width, icon.Bottom - height, width, height);
        Drawing.Rectangle LeftTop() => new(icon.Left - GapPixels - width, icon.Top, width, height);
        Drawing.Rectangle RightBottom() => new(icon.Right + GapPixels, icon.Bottom - height, width, height);
        Drawing.Rectangle RightTop() => new(icon.Right + GapPixels, icon.Top, width, height);

        return edge switch
        {
            TaskbarEdge.Top =>
            [
                BelowRight(),
                BelowLeft(),
                AboveRight(),
                AboveLeft(),
                LeftTop(),
                RightTop()
            ],
            TaskbarEdge.Left =>
            [
                RightBottom(),
                RightTop(),
                LeftBottom(),
                LeftTop(),
                AboveLeft(),
                BelowLeft()
            ],
            TaskbarEdge.Right =>
            [
                LeftBottom(),
                LeftTop(),
                RightBottom(),
                RightTop(),
                AboveRight(),
                BelowRight()
            ],
            _ =>
            [
                AboveRight(),
                AboveLeft(),
                BelowRight(),
                BelowLeft(),
                LeftBottom(),
                RightBottom()
            ]
        };
    }

    private static Drawing.Size LimitSizeToWorkArea(int width, int height, Drawing.Rectangle workArea)
    {
        var maxWidth = Math.Max(120, workArea.Width - WorkAreaMarginPixels * 2);
        var maxHeight = Math.Max(160, workArea.Height - WorkAreaMarginPixels * 2);
        return new Drawing.Size(
            Math.Min(Math.Max(width, 1), maxWidth),
            Math.Min(Math.Max(height, 1), maxHeight));
    }

    private static bool FitsInWorkArea(Drawing.Rectangle rect, Drawing.Rectangle workArea) =>
        rect.Left >= workArea.Left + WorkAreaMarginPixels &&
        rect.Top >= workArea.Top + WorkAreaMarginPixels &&
        rect.Right <= workArea.Right - WorkAreaMarginPixels &&
        rect.Bottom <= workArea.Bottom - WorkAreaMarginPixels;

    private static int OverflowScore(Drawing.Rectangle rect, Drawing.Rectangle workArea)
    {
        var leftOverflow = Math.Max(0, workArea.Left + WorkAreaMarginPixels - rect.Left);
        var topOverflow = Math.Max(0, workArea.Top + WorkAreaMarginPixels - rect.Top);
        var rightOverflow = Math.Max(0, rect.Right - (workArea.Right - WorkAreaMarginPixels));
        var bottomOverflow = Math.Max(0, rect.Bottom - (workArea.Bottom - WorkAreaMarginPixels));
        return leftOverflow + topOverflow + rightOverflow + bottomOverflow;
    }

    private static Drawing.Rectangle ClampToWorkArea(Drawing.Rectangle rect, Drawing.Rectangle workArea)
    {
        var width = Math.Min(rect.Width, Math.Max(1, workArea.Width - WorkAreaMarginPixels * 2));
        var height = Math.Min(rect.Height, Math.Max(1, workArea.Height - WorkAreaMarginPixels * 2));
        var minLeft = workArea.Left + WorkAreaMarginPixels;
        var minTop = workArea.Top + WorkAreaMarginPixels;
        var maxLeft = workArea.Right - width - WorkAreaMarginPixels;
        var maxTop = workArea.Bottom - height - WorkAreaMarginPixels;

        return new Drawing.Rectangle(
            Clamp(rect.Left, minLeft, maxLeft),
            Clamp(rect.Top, minTop, maxTop),
            width,
            height);
    }

    private static TaskbarEdge GetTaskbarEdge(Forms.Screen screen)
    {
        var bounds = screen.Bounds;
        var workArea = screen.WorkingArea;

        if (workArea.Bottom < bounds.Bottom)
        {
            return TaskbarEdge.Bottom;
        }

        if (workArea.Top > bounds.Top)
        {
            return TaskbarEdge.Top;
        }

        if (workArea.Left > bounds.Left)
        {
            return TaskbarEdge.Left;
        }

        if (workArea.Right < bounds.Right)
        {
            return TaskbarEdge.Right;
        }

        return TaskbarEdge.Bottom;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }

    private delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookStruct
    {
        public MouseHookPoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}

internal enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right
}
