using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WindowPilot.Models;
using WindowPilot.Native;
using MediaColor = System.Windows.Media.Color;

namespace WindowPilot.Views;

public enum OverlayKind
{
    TopMost,
    Transparent,
    ClickThrough,
    Mini,
    Selected
}

public partial class OverlayWindow : Window
{
    private static readonly SolidColorBrush TopMostBrush = new(MediaColor.FromRgb(0x25, 0x63, 0xEB));
    private static readonly SolidColorBrush TransparentBrush = new(MediaColor.FromRgb(0x7C, 0x3A, 0xED));
    private static readonly SolidColorBrush ClickThroughBrush = new(MediaColor.FromRgb(0xF9, 0x73, 0x16));
    private static readonly SolidColorBrush MiniBrush = new(MediaColor.FromRgb(0x16, 0xA3, 0x4A));
    private static readonly SolidColorBrush SelectedBrush = new(MediaColor.FromRgb(0x3B, 0x82, 0xF6));

    private nint _handle;
    private int _lastX = int.MinValue;
    private int _lastY = int.MinValue;
    private int _lastWidth = int.MinValue;
    private int _lastHeight = int.MinValue;
    private string _lastMode = string.Empty;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(_handle, NativeMethods.GWL_EXSTYLE).ToInt32();
        style |= NativeMethods.WS_EX_TOOLWINDOW |
                 NativeMethods.WS_EX_TRANSPARENT |
                 NativeMethods.WS_EX_LAYERED |
                 NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(_handle, NativeMethods.GWL_EXSTYLE, new nint(style));
    }

    public void UpdateFor(WindowInfo info, OverlayKind kind)
    {
        if (_handle == nint.Zero)
        {
            _handle = new WindowInteropHelper(this).Handle;
        }

        var mode = kind.ToString();
        var width = Math.Max(1, info.Width);
        var height = Math.Max(1, info.Height);
        if (_lastX == info.X &&
            _lastY == info.Y &&
            _lastWidth == width &&
            _lastHeight == height &&
            _lastMode == mode)
        {
            return;
        }

        _lastX = info.X;
        _lastY = info.Y;
        _lastWidth = width;
        _lastHeight = height;
        _lastMode = mode;

        switch (kind)
        {
            case OverlayKind.Mini:
                FrameBorder.BorderBrush = MiniBrush;
                Badge.Visibility = Visibility.Visible;
                BadgeText.Text = "MINI";
                Badge.Background = MiniBrush;
                break;
            case OverlayKind.ClickThrough:
                FrameBorder.BorderBrush = ClickThroughBrush;
                Badge.Visibility = Visibility.Visible;
                BadgeText.Text = "CLICK-THROUGH";
                Badge.Background = ClickThroughBrush;
                break;
            case OverlayKind.Transparent:
                FrameBorder.BorderBrush = TransparentBrush;
                Badge.Visibility = Visibility.Collapsed;
                BadgeText.Text = string.Empty;
                break;
            case OverlayKind.Selected:
                FrameBorder.BorderBrush = SelectedBrush;
                Badge.Visibility = Visibility.Collapsed;
                BadgeText.Text = string.Empty;
                break;
            default:
                FrameBorder.BorderBrush = TopMostBrush;
                Badge.Visibility = Visibility.Collapsed;
                BadgeText.Text = string.Empty;
                break;
        }

        NativeMethods.SetWindowPos(
            _handle,
            NativeMethods.HWND_TOPMOST,
            info.X,
            info.Y,
            width,
            height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }
}
