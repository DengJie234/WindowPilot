using System.Drawing;
using WindowPilot.Models;
using Forms = System.Windows.Forms;

namespace WindowPilot.Services;

public sealed class LayoutRestoreReport
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string> Messages { get; } = [];

    public OperationResult ToOperationResult()
    {
        var message = $"布局恢复完成：成功 {SuccessCount}，失败 {FailureCount}";
        if (Messages.Count > 0)
        {
            message += "\n" + string.Join("\n", Messages.Take(8));
        }

        return FailureCount == 0 ? OperationResult.Ok(message) : OperationResult.Fail(message);
    }
}

public sealed class LayoutService
{
    private readonly AppConfig _config;
    private readonly WindowService _windowService;
    private readonly WindowMatcherService _matcher;

    public LayoutService(AppConfig config, WindowService windowService, WindowMatcherService matcher)
    {
        _config = config;
        _windowService = windowService;
        _matcher = matcher;
    }

    public IReadOnlyList<WindowLayout> Layouts => _config.Layouts;

    public WindowLayout SaveAllVisible(string name)
    {
        var layout = new WindowLayout
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Layout {DateTime.Now:yyyy-MM-dd HH-mm}" : name.Trim(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Items = _windowService.EnumerateVisibleWindows().Select(_windowService.CreateLayoutItem).ToList()
        };

        _config.Layouts.Add(layout);
        return layout;
    }

    public WindowLayout? SaveSelected(WindowInfo? selected, string name)
    {
        if (selected is null)
        {
            return null;
        }

        var layout = new WindowLayout
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Selected {DateTime.Now:yyyy-MM-dd HH-mm}" : name.Trim(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Items = [_windowService.CreateLayoutItem(selected)]
        };

        _config.Layouts.Add(layout);
        return layout;
    }

    public void Rename(WindowLayout layout, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        layout.Name = name.Trim();
        layout.UpdatedAt = DateTime.Now;
    }

    public void Delete(WindowLayout layout)
    {
        _config.Layouts.Remove(layout);
    }

    public LayoutRestoreReport Restore(WindowLayout layout)
    {
        var report = new LayoutRestoreReport();

        foreach (var item in layout.Items)
        {
            var hwnd = _matcher.Match(item, out var matchMessage);
            if (hwnd is null)
            {
                report.FailureCount++;
                report.Messages.Add($"{item.ProcessName} / {item.WindowTitle}: {matchMessage}");
                continue;
            }

            var rect = ResolveTargetRect(item, hwnd.Value);
            var restoreResult = _windowService.RestoreWindow(hwnd.Value);
            var moveResult = _windowService.MoveResizeWindow(hwnd.Value, rect.X, rect.Y, rect.Width, rect.Height);
            var topResult = _windowService.SetTopMost(hwnd.Value, item.IsTopMost);
            var opacityResult = _windowService.SetOpacityPercent(hwnd.Value, item.Opacity);
            var clickResult = _windowService.SetClickThrough(hwnd.Value, item.IsClickThrough);
            var stateResult = item.WindowState == SavedWindowState.Normal
                ? OperationResult.Ok("窗口保持普通状态")
                : _windowService.SetSavedWindowState(hwnd.Value, item.WindowState);

            var all = new[] { restoreResult, moveResult, topResult, opacityResult, clickResult, stateResult };
            if (all.All(r => r.Success))
            {
                report.SuccessCount++;
            }
            else
            {
                report.FailureCount++;
                report.Messages.Add($"{item.ProcessName} / {item.WindowTitle}: {string.Join("; ", all.Where(r => !r.Success).Select(r => r.Message))}");
            }
        }

        layout.UpdatedAt = DateTime.Now;
        return report;
    }

    private Rectangle ResolveTargetRect(WindowLayoutItem item, nint hwnd)
    {
        var screens = Forms.Screen.AllScreens;
        var targetScreen = screens.FirstOrDefault(screen =>
            string.Equals(screen.DeviceName, item.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));
        var fallback = Forms.Screen.PrimaryScreen ?? screens.FirstOrDefault();

        if (targetScreen is null)
        {
            return CenterInScreen(item, fallback?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080));
        }

        var workArea = targetScreen.WorkingArea;
        var currentDpi = _windowService.GetWindowInfo(hwnd)?.Dpi ?? 96;
        var savedDpi = item.State.Dpi == 0 ? 96 : item.State.Dpi;
        var scale = currentDpi / (double)savedDpi;

        var relativeX = item.X - item.State.MonitorX;
        var relativeY = item.Y - item.State.MonitorY;
        var x = workArea.Left + (int)Math.Round(relativeX * scale);
        var y = workArea.Top + (int)Math.Round(relativeY * scale);
        var width = (int)Math.Round(item.Width * scale);
        var height = (int)Math.Round(item.Height * scale);

        return ClampToScreen(new Rectangle(x, y, width, height), workArea);
    }

    private static Rectangle CenterInScreen(WindowLayoutItem item, Rectangle workArea)
    {
        var width = Math.Min(Math.Max(item.Width, 120), workArea.Width);
        var height = Math.Min(Math.Max(item.Height, 90), workArea.Height);
        return new Rectangle(
            workArea.Left + (workArea.Width - width) / 2,
            workArea.Top + (workArea.Height - height) / 2,
            width,
            height);
    }

    private static Rectangle ClampToScreen(Rectangle rect, Rectangle workArea)
    {
        var width = Math.Min(Math.Max(rect.Width, 120), workArea.Width);
        var height = Math.Min(Math.Max(rect.Height, 90), workArea.Height);
        var x = Math.Clamp(rect.X, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        var y = Math.Clamp(rect.Y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        return new Rectangle(x, y, width, height);
    }
}
