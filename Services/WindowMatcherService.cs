using WindowPilot.Models;

namespace WindowPilot.Services;

public sealed class WindowMatcherService
{
    private readonly WindowService _windowService;

    public WindowMatcherService(WindowService windowService)
    {
        _windowService = windowService;
    }

    public nint? Match(WindowLayoutItem item, out string message) => Match(item.Identity, out message);

    public nint? Match(WindowIdentity identity, out string message)
    {
        if (identity.Hwnd != 0)
        {
            var hwnd = new nint(identity.Hwnd);
            var info = _windowService.GetWindowInfo(hwnd);
            if (info is not null)
            {
                message = "通过 hwnd 匹配。";
                return hwnd;
            }
        }

        var windows = _windowService.EnumerateVisibleWindows(sort: false);

        var byPid = windows.Where(w => identity.ProcessId != 0 && w.ProcessId == identity.ProcessId).ToList();
        if (TryPick(byPid, "通过 processId 匹配。", out var pidHandle, out message))
        {
            return pidHandle;
        }

        var byNameTitle = windows
            .Where(w => SameProcessName(w.ProcessName, identity.ProcessName) && TitleLooksRelated(w.Title, identity.WindowTitle))
            .ToList();
        if (TryPick(byNameTitle, "通过 processName + windowTitle 关键词匹配。", out var nameTitleHandle, out message))
        {
            return nameTitleHandle;
        }

        var byPath = windows
            .Where(w => !string.IsNullOrWhiteSpace(identity.ProcessPath) &&
                        string.Equals(w.ProcessPath, identity.ProcessPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (TryPick(byPath, "通过 processPath 匹配。", out var pathHandle, out message))
        {
            return pathHandle;
        }

        var byClass = windows
            .Where(w => !string.IsNullOrWhiteSpace(identity.ClassName) &&
                        string.Equals(w.ClassName, identity.ClassName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (TryPick(byClass, "通过 className 匹配。", out var classHandle, out message))
        {
            return classHandle;
        }

        message = "未找到匹配窗口，窗口可能已经关闭或标题/进程信息已变化。";
        return null;
    }

    private static bool TryPick(List<WindowInfo> candidates, string reason, out nint? hwnd, out string message)
    {
        if (candidates.Count == 0)
        {
            hwnd = null;
            message = string.Empty;
            return false;
        }

        hwnd = candidates[0].Handle;
        message = candidates.Count == 1
            ? reason
            : $"{reason} 匹配到 {candidates.Count} 个窗口，已选择 Z 序最靠前的窗口。";
        return true;
    }

    private static bool SameProcessName(string current, string saved)
    {
        current = NormalizeProcessName(current);
        saved = NormalizeProcessName(saved);
        return !string.IsNullOrWhiteSpace(current) &&
               string.Equals(current, saved, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProcessName(string value)
    {
        value = value.Trim();
        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
    }

    private static bool TitleLooksRelated(string current, string saved)
    {
        if (string.IsNullOrWhiteSpace(saved))
        {
            return true;
        }

        if (string.Equals(current, saved, StringComparison.OrdinalIgnoreCase) ||
            current.Contains(saved, StringComparison.OrdinalIgnoreCase) ||
            saved.Contains(current, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var keywords = saved
            .Split([' ', '-', '_', '|', '/', '\\', ':', '，', '。', '·', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5);

        return keywords.Any(keyword => current.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
