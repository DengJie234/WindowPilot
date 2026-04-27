using System.Windows.Threading;
using WindowPilot.Models;

namespace WindowPilot.Services;

public sealed class RuleService : IDisposable
{
    private readonly AppConfig _config;
    private readonly WindowService _windowService;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, DateTime> _lastRuns = [];
    private readonly Dictionary<Guid, HashSet<nint>> _affectedWindowsByRuleId = [];

    public event EventHandler<RuleExecutionLog>? LogAdded;

    public RuleService(AppConfig config, WindowService windowService)
    {
        _config = config;
        _windowService = windowService;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(1000, config.Settings.RuleScanIntervalMs))
        };
        _timer.Tick += (_, _) => EvaluateRules();
    }

    public IReadOnlyList<WindowRule> Rules => _config.Rules;
    public IReadOnlyList<RuleExecutionLog> Logs => _config.RuleLogs;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Dispose() => _timer.Stop();

    public WindowRule CreateRule(string name, WindowRuleAction action)
    {
        var priority = _config.Rules.Count == 0 ? 100 : _config.Rules.Max(r => r.Priority) + 10;
        var rule = new WindowRule
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Rule {DateTime.Now:HH-mm-ss}" : name.Trim(),
            Priority = priority,
            Action = action
        };
        _config.Rules.Add(rule);
        return rule;
    }

    public OperationResult AddRule(WindowRule rule)
    {
        var validation = ValidateRule(rule);
        if (!validation.Success)
        {
            return validation;
        }

        if (rule.Id == Guid.Empty)
        {
            rule.Id = Guid.NewGuid();
        }

        if (rule.Priority <= 0)
        {
            rule.Priority = _config.Rules.Count == 0 ? 100 : _config.Rules.Max(r => r.Priority) + 10;
        }

        if (rule.CooldownSeconds <= 0)
        {
            rule.CooldownSeconds = 10;
        }

        rule.Name = string.IsNullOrWhiteSpace(rule.Name) ? $"规则 {DateTime.Now:HH-mm-ss}" : rule.Name.Trim();
        _config.Rules.Add(rule);
        return OperationResult.Ok("规则已创建。");
    }

    public OperationResult DeleteRule(Guid ruleId, bool restoreAffectedWindows = true)
    {
        var rule = _config.Rules.FirstOrDefault(item => item.Id == ruleId);
        if (rule is null)
        {
            return OperationResult.Fail("规则不存在，可能已经被删除。");
        }

        _config.Rules.Remove(rule);
        foreach (var key in _lastRuns.Keys.Where(key => key.StartsWith($"{ruleId:N}:", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _lastRuns.Remove(key);
        }

        _config.RuleLogs.RemoveAll(log => string.Equals(log.RuleName, rule.Name, StringComparison.OrdinalIgnoreCase));

        var restored = 0;
        if (_affectedWindowsByRuleId.Remove(ruleId, out var handles) && restoreAffectedWindows)
        {
            foreach (var hwnd in handles)
            {
                if (_windowService.RestoreManagedWindow(hwnd).Success)
                {
                    restored++;
                }
            }
        }

        return OperationResult.Ok(restored > 0
            ? $"规则已删除，并恢复 {restored} 个受影响窗口。"
            : "规则已删除。");
    }

    public OperationResult ValidateRule(WindowRule rule) =>
        WindowRule.HasMatchCondition(rule)
            ? OperationResult.Ok()
            : OperationResult.Fail("请至少填写一个匹配条件，例如进程名或窗口标题关键词。");

    public void DisableInvalidRules()
    {
        foreach (var rule in _config.Rules.Where(rule => !WindowRule.HasMatchCondition(rule)))
        {
            rule.IsEnabled = false;
        }
    }

    public void EvaluateRules()
    {
        if (_config.Settings.RulesPaused)
        {
            return;
        }

        if (_windowService.IsFullScreenApplicationActive())
        {
            return;
        }

        var windows = _windowService.EnumerateVisibleWindows(sort: false);
        foreach (var rule in _config.Rules.Where(r => r.IsEnabled && WindowRule.HasMatchCondition(r)).OrderBy(r => r.Priority))
        {
            foreach (var window in windows.Where(window => Matches(rule, window)))
            {
                if (IsAlreadyApplied(rule.Action, window))
                {
                    continue;
                }

                var key = $"{rule.Id:N}:{window.Handle}";
                var cooldown = TimeSpan.FromSeconds(Math.Max(1, rule.CooldownSeconds));
                if (_lastRuns.TryGetValue(key, out var lastRun) && DateTime.Now - lastRun < cooldown)
                {
                    continue;
                }

                var result = Execute(rule.Action, window.Handle);
                _lastRuns[key] = DateTime.Now;
                rule.LastExecutedAt = DateTime.Now;
                if (result.Success)
                {
                    if (!_affectedWindowsByRuleId.TryGetValue(rule.Id, out var handles))
                    {
                        handles = [];
                        _affectedWindowsByRuleId[rule.Id] = handles;
                    }

                    handles.Add(window.Handle);
                }

                AddLog(rule, window, result);
            }
        }
    }

    private OperationResult Execute(WindowRuleAction action, nint hwnd)
    {
        return action.Type switch
        {
            RuleActionType.SetTopMost => _windowService.SetTopMost(hwnd, true),
            RuleActionType.ClearTopMost => _windowService.SetTopMost(hwnd, false),
            RuleActionType.SetOpacity => _windowService.SetOpacityPercent(hwnd, action.Opacity),
            RuleActionType.EnableClickThrough => _windowService.SetClickThrough(hwnd, true),
            RuleActionType.DisableClickThrough => _windowService.SetClickThrough(hwnd, false),
            RuleActionType.MoveLeftHalf => _windowService.MoveLeftHalf(hwnd),
            RuleActionType.MoveRightHalf => _windowService.MoveRightHalf(hwnd),
            RuleActionType.Center => _windowService.CenterWindow(hwnd),
            RuleActionType.MoveTopLeft => _windowService.MoveTopLeft(hwnd),
            RuleActionType.MoveTopRight => _windowService.MoveTopRight(hwnd),
            RuleActionType.MoveBottomLeft => _windowService.MoveBottomLeft(hwnd),
            RuleActionType.MoveBottomRight => _windowService.MoveBottomRight(hwnd),
            RuleActionType.SetSize => _windowService.SetWindowSize(hwnd, action.Width, action.Height),
            _ => OperationResult.Fail("未知规则动作")
        };
    }

    private void AddLog(WindowRule rule, WindowInfo window, OperationResult result)
    {
        var log = new RuleExecutionLog
        {
            Timestamp = DateTime.Now,
            RuleName = rule.Name,
            Target = window.DisplayName,
            Success = result.Success,
            Message = result.Message
        };

        _config.RuleLogs.Insert(0, log);
        while (_config.RuleLogs.Count > 200)
        {
            _config.RuleLogs.RemoveAt(_config.RuleLogs.Count - 1);
        }

        LogAdded?.Invoke(this, log);
    }

    private static bool Matches(WindowRule rule, WindowInfo window)
    {
        if (!WindowRule.HasMatchCondition(rule))
        {
            return false;
        }

        var matched = true;
        if (!string.IsNullOrWhiteSpace(rule.ProcessName))
        {
            matched &= MatchesEquals(window.ProcessName, rule.ProcessName, normalizeProcess: true);
        }

        if (!string.IsNullOrWhiteSpace(rule.TitleContains))
        {
            matched &= MatchesContains(window.Title, rule.TitleContains);
        }

        if (!string.IsNullOrWhiteSpace(rule.ClassName))
        {
            matched &= MatchesEquals(window.ClassName, rule.ClassName, normalizeProcess: false);
        }

        if (!string.IsNullOrWhiteSpace(rule.ProcessPath))
        {
            matched &= MatchesEquals(window.ProcessPath, rule.ProcessPath, normalizeProcess: false);
        }

        return matched;
    }

    private static bool IsAlreadyApplied(WindowRuleAction action, WindowInfo window)
    {
        return action.Type switch
        {
            RuleActionType.SetTopMost => window.IsTopMost,
            RuleActionType.ClearTopMost => !window.IsTopMost,
            RuleActionType.SetOpacity => Math.Abs(window.OpacityPercent - action.Opacity) <= 1,
            RuleActionType.EnableClickThrough => window.IsClickThrough,
            RuleActionType.DisableClickThrough => !window.IsClickThrough,
            RuleActionType.SetSize => Math.Abs(window.Width - action.Width) <= 2 && Math.Abs(window.Height - action.Height) <= 2,
            _ => false
        };
    }

    private static bool MatchesContains(string source, string pattern) =>
        !string.IsNullOrWhiteSpace(pattern) &&
        source.Contains(pattern.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool MatchesEquals(string source, string pattern, bool normalizeProcess)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        source = normalizeProcess ? NormalizeProcessName(source) : source.Trim();
        pattern = normalizeProcess ? NormalizeProcessName(pattern) : pattern.Trim();
        return string.Equals(source, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProcessName(string value)
    {
        value = value.Trim();
        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
    }
}
