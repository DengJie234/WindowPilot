using System.Windows.Threading;
using WindowPilot.Views;

namespace WindowPilot.Services;

public sealed class OverlayService : IDisposable
{
    private readonly WindowService _windowService;
    private readonly Func<nint, bool> _isMiniWindow;
    private readonly Func<bool> _temporaryOnly;
    private readonly Func<bool> _highlightSelectedWindow;
    private readonly Func<nint?> _selectedWindowHandle;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<nint, OverlayWindow> _overlays = [];
    private readonly Dictionary<nint, DateTime> _firstShownAt = [];
    private readonly Dictionary<nint, string> _lastSignatures = [];
    private bool _enabled = true;

    public OverlayService(
        WindowService windowService,
        Func<nint, bool> isMiniWindow,
        Func<bool> temporaryOnly,
        Func<bool> highlightSelectedWindow,
        Func<nint?> selectedWindowHandle)
    {
        _windowService = windowService;
        _isMiniWindow = isMiniWindow;
        _temporaryOnly = temporaryOnly;
        _highlightSelectedWindow = highlightSelectedWindow;
        _selectedWindowHandle = selectedWindowHandle;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Sync();
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
            {
                HideAll();
            }
        }
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Sync()
    {
        if (!_enabled)
        {
            return;
        }

        var active = new HashSet<nint>();
        var selectedHandle = _highlightSelectedWindow() ? _selectedWindowHandle() : null;
        foreach (var window in _windowService.EnumerateVisibleWindows(sort: false))
        {
            var isMini = _isMiniWindow(window.Handle);
            if (!ShouldShowOverlay(window, isMini, selectedHandle, out var kind))
            {
                continue;
            }

            var signature = $"{kind}:{window.X}:{window.Y}:{window.Width}:{window.Height}:{window.OpacityPercent}:{window.IsTopMost}:{window.IsClickThrough}";
            if (!_lastSignatures.TryGetValue(window.Handle, out var lastSignature) || lastSignature != signature)
            {
                _lastSignatures[window.Handle] = signature;
                _firstShownAt[window.Handle] = DateTime.Now;
            }

            if (_temporaryOnly() &&
                kind is OverlayKind.TopMost or OverlayKind.Transparent or OverlayKind.Selected &&
                _firstShownAt.TryGetValue(window.Handle, out var firstShown) &&
                DateTime.Now - firstShown > TimeSpan.FromMilliseconds(1500))
            {
                CloseOverlay(window.Handle);
                continue;
            }

            active.Add(window.Handle);
            if (!_overlays.TryGetValue(window.Handle, out var overlay))
            {
                overlay = new OverlayWindow();
                _overlays[window.Handle] = overlay;
                overlay.Show();
            }

            overlay.UpdateFor(window, kind);
        }

        foreach (var stale in _overlays.Keys.Where(handle => !active.Contains(handle)).ToList())
        {
            CloseOverlay(stale);
        }
    }

    public void ClearAllOverlays() => HideAll();

    public void Dispose()
    {
        _timer.Stop();
        HideAll();
    }

    private void HideAll()
    {
        foreach (var overlay in _overlays.Values)
        {
            overlay.Close();
        }

        _overlays.Clear();
        _firstShownAt.Clear();
        _lastSignatures.Clear();
    }

    private bool ShouldShowOverlay(WindowPilot.Models.WindowInfo window, bool isMini, nint? selectedHandle, out OverlayKind kind)
    {
        kind = OverlayKind.TopMost;
        if (window.WindowState == WindowPilot.Models.SavedWindowState.Minimized)
        {
            return false;
        }

        if (isMini)
        {
            kind = OverlayKind.Mini;
            return true;
        }

        if (selectedHandle == window.Handle)
        {
            kind = OverlayKind.Selected;
            return true;
        }

        if (!_windowService.HasManagedVisualState(window))
        {
            return false;
        }

        if (window.IsClickThrough)
        {
            kind = OverlayKind.ClickThrough;
        }
        else if (window.OpacityPercent < 100)
        {
            kind = OverlayKind.Transparent;
        }
        else
        {
            kind = OverlayKind.TopMost;
        }

        return true;
    }

    private void CloseOverlay(nint handle)
    {
        if (_overlays.Remove(handle, out var overlay))
        {
            overlay.Close();
        }
    }
}
