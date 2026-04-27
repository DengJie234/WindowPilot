using WindowPilot.Models;

namespace WindowPilot.Services;

public sealed class MiniWindowService
{
    private readonly AppConfig _config;
    private readonly WindowService _windowService;
    private readonly WindowMatcherService _matcher;

    public MiniWindowService(AppConfig config, WindowService windowService, WindowMatcherService matcher)
    {
        _config = config;
        _windowService = windowService;
        _matcher = matcher;
    }

    public IReadOnlyList<MiniWindowState> MiniWindows => _config.MiniWindows;

    public OperationResult EnterMiniMode(WindowInfo? window, MiniWindowCorner corner = MiniWindowCorner.BottomRight)
    {
        if (window is null)
        {
            return OperationResult.Fail("请先选择或捕获一个窗口。");
        }

        var existing = _config.MiniWindows.FirstOrDefault(state => state.Identity.Hwnd == window.Handle.ToInt64());
        if (existing is not null)
        {
            existing.Corner = corner;
            return ApplyMiniState(existing, window.Handle);
        }

        var state = new MiniWindowState
        {
            Identity = window.Identity,
            OriginalState = _windowService.CreateLayoutItem(window).State,
            Corner = corner,
            Width = _config.Settings.DefaultMiniWidth,
            Height = _config.Settings.DefaultMiniHeight,
            Opacity = _config.Settings.DefaultMiniOpacity,
            AutoTopMost = _config.Settings.DefaultMiniTopMost
        };

        _config.MiniWindows.Add(state);
        return ApplyMiniState(state, window.Handle);
    }

    public OperationResult RestoreMiniWindow(MiniWindowState state)
    {
        var hwnd = _matcher.Match(state.Identity, out var matchMessage);
        if (hwnd is null)
        {
            _config.MiniWindows.Remove(state);
            return OperationResult.Fail($"小窗目标已关闭，已清理记录：{matchMessage}");
        }

        var original = state.OriginalState;
        var restore = _windowService.RestoreWindow(hwnd.Value);
        var move = _windowService.MoveResizeWindow(hwnd.Value, original.X, original.Y, original.Width, original.Height);
        var top = _windowService.SetTopMost(hwnd.Value, original.IsTopMost);
        var opacity = _windowService.SetOpacityPercent(hwnd.Value, original.Opacity);
        var click = _windowService.SetClickThrough(hwnd.Value, original.IsClickThrough);
        var savedState = original.WindowState == SavedWindowState.Normal
            ? OperationResult.Ok()
            : _windowService.SetSavedWindowState(hwnd.Value, original.WindowState);

        _config.MiniWindows.Remove(state);
        var failed = new[] { restore, move, top, opacity, click, savedState }.Where(r => !r.Success).ToList();
        return failed.Count == 0
            ? OperationResult.Ok("已恢复小窗")
            : OperationResult.Fail(string.Join("; ", failed.Select(r => r.Message)));
    }

    public OperationResult RestoreAll()
    {
        var restored = 0;
        var failed = 0;
        foreach (var state in _config.MiniWindows.ToList())
        {
            var result = RestoreMiniWindow(state);
            if (result.Success)
            {
                restored++;
            }
            else
            {
                failed++;
            }
        }

        return failed == 0
            ? OperationResult.Ok($"已恢复 {restored} 个小窗")
            : OperationResult.Fail($"小窗恢复完成：成功 {restored}，失败 {failed}");
    }

    public bool IsMiniWindow(nint hwnd) =>
        _config.MiniWindows.Any(state => state.Identity.Hwnd == hwnd.ToInt64());

    public void CleanupClosedWindows()
    {
        foreach (var state in _config.MiniWindows.ToList())
        {
            if (_matcher.Match(state.Identity, out _) is null)
            {
                _config.MiniWindows.Remove(state);
            }
        }
    }

    private OperationResult ApplyMiniState(MiniWindowState state, nint hwnd)
    {
        var workArea = _windowService.GetWorkAreaForWindow(hwnd);
        var width = Math.Clamp(state.Width, 160, workArea.Width);
        var height = Math.Clamp(state.Height, 120, workArea.Height);
        var x = state.Corner is MiniWindowCorner.TopLeft or MiniWindowCorner.BottomLeft
            ? workArea.Left
            : workArea.Right - width;
        var y = state.Corner is MiniWindowCorner.TopLeft or MiniWindowCorner.TopRight
            ? workArea.Top
            : workArea.Bottom - height;

        x = Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        y = Math.Clamp(y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));

        var move = _windowService.MoveResizeWindow(hwnd, x, y, width, height);
        var top = state.AutoTopMost ? _windowService.SetTopMost(hwnd, true) : OperationResult.Ok();
        var opacity = _windowService.SetOpacityPercent(hwnd, state.Opacity);
        var failed = new[] { move, top, opacity }.Where(r => !r.Success).ToList();
        return failed.Count == 0
            ? OperationResult.Ok("已进入小窗模式")
            : OperationResult.Fail(string.Join("; ", failed.Select(r => r.Message)));
    }
}
