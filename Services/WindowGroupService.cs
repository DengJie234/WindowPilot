using WindowPilot.Models;

namespace WindowPilot.Services;

public sealed class WindowGroupService
{
    private readonly AppConfig _config;
    private readonly WindowService _windowService;
    private readonly WindowMatcherService _matcher;

    public WindowGroupService(AppConfig config, WindowService windowService, WindowMatcherService matcher)
    {
        _config = config;
        _windowService = windowService;
        _matcher = matcher;
    }

    public IReadOnlyList<WindowGroup> Groups => _config.WindowGroups;

    public WindowGroup CreateGroup(string name)
    {
        var group = new WindowGroup
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Group {DateTime.Now:HH-mm}" : name.Trim()
        };
        _config.WindowGroups.Add(group);
        return group;
    }

    public void DeleteGroup(WindowGroup group)
    {
        _config.WindowGroups.Remove(group);
    }

    public OperationResult AddWindow(WindowGroup group, WindowInfo? window)
    {
        if (window is null)
        {
            return OperationResult.Fail("请先选择或捕获一个窗口。");
        }

        if (group.Windows.Any(item => item.Hwnd == window.Handle.ToInt64()))
        {
            return OperationResult.Ok("窗口已在分组中。");
        }

        group.Windows.Add(window.Identity);
        return OperationResult.Ok("已加入分组");
    }

    public OperationResult RemoveWindow(WindowGroup group, WindowIdentity? identity)
    {
        if (identity is null)
        {
            return OperationResult.Fail("请先选择分组内窗口。");
        }

        group.Windows.Remove(identity);
        return OperationResult.Ok("已从分组移除");
    }

    public OperationResult MinimizeGroup(WindowGroup group) => RunGroupAction(group, _windowService.MinimizeWindow, "最小化");

    public OperationResult RestoreGroup(WindowGroup group) => RunGroupAction(group, _windowService.RestoreWindow, "恢复");

    public OperationResult TopMostGroup(WindowGroup group) => RunGroupAction(group, hwnd => _windowService.SetTopMost(hwnd, true), "置顶");

    public void CleanupClosedWindows()
    {
        foreach (var group in _config.WindowGroups)
        {
            foreach (var identity in group.Windows.ToList())
            {
                if (_matcher.Match(identity, out _) is null)
                {
                    group.Windows.Remove(identity);
                }
            }
        }
    }

    private OperationResult RunGroupAction(WindowGroup group, Func<nint, OperationResult> action, string name)
    {
        CleanupClosedWindows();
        var success = 0;
        var failed = 0;

        foreach (var identity in group.Windows)
        {
            var hwnd = _matcher.Match(identity, out _);
            if (hwnd is null)
            {
                failed++;
                continue;
            }

            var result = action(hwnd.Value);
            if (result.Success)
            {
                success++;
            }
            else
            {
                failed++;
            }
        }

        return failed == 0
            ? OperationResult.Ok($"分组{name}完成：{success} 个窗口")
            : OperationResult.Fail($"分组{name}完成：成功 {success}，失败 {failed}");
    }
}
