using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WindowPilot.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Drawing.Icon? _icon;

    public event EventHandler? OpenRequested;
    public event EventHandler<TrayFlyoutRequestedEventArgs>? FlyoutRequested;

    public TrayIconService()
    {
        _icon = LoadAppIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon ?? Drawing.SystemIcons.Application,
            Text = "WindowPilot",
            Visible = true
        };

        _notifyIcon.MouseDoubleClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                OpenRequested?.Invoke(this, EventArgs.Empty);
            }
        };

        _notifyIcon.MouseUp += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                OpenRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (args.Button != Forms.MouseButtons.Right)
            {
                return;
            }

            FlyoutRequested?.Invoke(
                this,
                new TrayFlyoutRequestedEventArgs(TryGetIconBounds(), Forms.Cursor.Position));
        };
    }

    public void ShowBalloon(string title, string text, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
    }

    private Drawing.Rectangle? TryGetIconBounds()
    {
        if (!TryGetNotifyIconIdentifier(out var identifier))
        {
            return null;
        }

        var result = Shell_NotifyIconGetRect(ref identifier, out var rect);
        if (result != 0)
        {
            return null;
        }

        return Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private bool TryGetNotifyIconIdentifier(out NotifyIconIdentifier identifier)
    {
        identifier = default;

        var notifyIconType = typeof(Forms.NotifyIcon);
        var idField = notifyIconType.GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? notifyIconType.GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
        var windowField = notifyIconType.GetField("_window", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? notifyIconType.GetField("window", BindingFlags.NonPublic | BindingFlags.Instance);

        if (idField?.GetValue(_notifyIcon) is not { } idValue ||
            windowField?.GetValue(_notifyIcon) is not { } nativeWindow)
        {
            return false;
        }

        var hwnd = GetNativeWindowHandle(nativeWindow);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        identifier = new NotifyIconIdentifier
        {
            CbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            HWnd = hwnd,
            UID = Convert.ToUInt32(idValue)
        };
        return true;
    }

    private static IntPtr GetNativeWindowHandle(object nativeWindow)
    {
        var handleProperty = nativeWindow.GetType().GetProperty(
            "Handle",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (handleProperty?.GetValue(nativeWindow) is IntPtr handle)
        {
            return handle;
        }

        var handleField = nativeWindow.GetType().GetField(
            "_handle",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return handleField?.GetValue(nativeWindow) is IntPtr fieldHandle
            ? fieldHandle
            : IntPtr.Zero;
    }

    private static Drawing.Icon? LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            return new Drawing.Icon(iconPath);
        }

        var sourcePath = Path.Combine(AppContext.BaseDirectory, "AppIcon.ico");
        if (File.Exists(sourcePath))
        {
            return new Drawing.Icon(sourcePath);
        }

        return null;
    }

    [DllImport("shell32.dll", SetLastError = false)]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out NativeRect iconLocation);

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint CbSize;
        public IntPtr HWnd;
        public uint UID;
        public Guid GuidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

public sealed class TrayFlyoutRequestedEventArgs : EventArgs
{
    public TrayFlyoutRequestedEventArgs(Drawing.Rectangle? iconBounds, Drawing.Point cursorPosition)
    {
        IconBounds = iconBounds;
        CursorPosition = cursorPosition;
    }

    public Drawing.Rectangle? IconBounds { get; }
    public Drawing.Point CursorPosition { get; }
}
