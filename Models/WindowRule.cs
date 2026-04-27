using System.Text.Json.Serialization;

namespace WindowPilot.Models;

public sealed class WindowRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New rule";
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public int CooldownSeconds { get; set; } = 10;
    public DateTime LastExecutedAt { get; set; } = DateTime.MinValue;
    public string ProcessName { get; set; } = string.Empty;
    public string TitleContains { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string ProcessPath { get; set; } = string.Empty;
    public WindowRuleAction Action { get; set; } = new();

    [JsonIgnore]
    public bool IsValid => HasMatchCondition(this);

    [JsonIgnore]
    public string StatusText => IsValid
        ? IsEnabled ? "已启用" : "已禁用"
        : "无效规则";

    [JsonIgnore]
    public string ValidationMessage => IsValid
        ? string.Empty
        : "请至少填写一个匹配条件，例如进程名或窗口标题关键词。";

    [JsonIgnore]
    public string MatchSummary
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(ProcessName))
            {
                parts.Add($"进程：{ProcessName}");
            }

            if (!string.IsNullOrWhiteSpace(TitleContains))
            {
                parts.Add($"标题：{TitleContains}");
            }

            if (!string.IsNullOrWhiteSpace(ClassName))
            {
                parts.Add($"类名：{ClassName}");
            }

            if (!string.IsNullOrWhiteSpace(ProcessPath))
            {
                parts.Add("路径已设置");
            }

            return parts.Count == 0 ? "未设置匹配条件" : string.Join(" / ", parts);
        }
    }

    [JsonIgnore]
    public string ActionDisplay => Action.Type switch
    {
        RuleActionType.SetTopMost => "设置置顶",
        RuleActionType.ClearTopMost => "取消置顶",
        RuleActionType.SetOpacity => $"设置透明度 {Action.Opacity}%",
        RuleActionType.EnableClickThrough => "开启点击穿透",
        RuleActionType.DisableClickThrough => "关闭点击穿透",
        RuleActionType.MoveLeftHalf => "左半屏",
        RuleActionType.MoveRightHalf => "右半屏",
        RuleActionType.Center => "居中",
        RuleActionType.MoveTopLeft => "左上角",
        RuleActionType.MoveTopRight => "右上角",
        RuleActionType.MoveBottomLeft => "左下角",
        RuleActionType.MoveBottomRight => "右下角",
        RuleActionType.SetSize => $"设置大小 {Action.Width} x {Action.Height}",
        _ => Action.Type.ToString()
    };

    public static bool HasMatchCondition(WindowRule rule) =>
        !string.IsNullOrWhiteSpace(rule.ProcessName) ||
        !string.IsNullOrWhiteSpace(rule.TitleContains) ||
        !string.IsNullOrWhiteSpace(rule.ClassName) ||
        !string.IsNullOrWhiteSpace(rule.ProcessPath);
}

public sealed class RuleExecutionLog
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string RuleName { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public string Display => $"{Timestamp:HH:mm:ss} [{(Success ? "OK" : "FAIL")}] {RuleName} -> {Target}: {Message}";
}
