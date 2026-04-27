using WindowPilot.Models;

namespace WindowPilot.Services;

public sealed class BlacklistService
{
    private readonly BlacklistConfig _config;

    public BlacklistService(BlacklistConfig config)
    {
        _config = config;
    }

    public BlacklistConfig Config => _config;

    public bool IsBlocked(WindowInfo window, out string reason)
    {
        if (_config.WhitelistMode)
        {
            if (!MatchesAny(_config.WhitelistItems, window))
            {
                reason = "白名单模式已启用，该窗口不在白名单中。";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        var match = _config.BlacklistItems.FirstOrDefault(item => item.IsEnabled && Matches(item, window));
        if (match is not null)
        {
            reason = $"窗口命中黑名单：{match.Type} = {match.Value}";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    public void AddProcessName(string value, bool whitelist = false) => Add(BlacklistItemType.ProcessName, value, whitelist);

    public void AddTitleKeyword(string value, bool whitelist = false) => Add(BlacklistItemType.TitleKeyword, value, whitelist);

    public void Remove(BlacklistItem item, bool whitelist = false)
    {
        var list = whitelist ? _config.WhitelistItems : _config.BlacklistItems;
        list.Remove(item);
    }

    private void Add(BlacklistItemType type, string value, bool whitelist)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var list = whitelist ? _config.WhitelistItems : _config.BlacklistItems;
        if (list.Any(item => item.Type == type && string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        list.Add(new BlacklistItem { Type = type, Value = value });
    }

    private static bool MatchesAny(IEnumerable<BlacklistItem> items, WindowInfo window) =>
        items.Any(item => item.IsEnabled && Matches(item, window));

    private static bool Matches(BlacklistItem item, WindowInfo window)
    {
        return item.Type switch
        {
            BlacklistItemType.ProcessName => EqualsLoose(window.ProcessName, item.Value),
            BlacklistItemType.TitleKeyword => ContainsLoose(window.Title, item.Value),
            BlacklistItemType.ProcessPath => EqualsLoose(window.ProcessPath, item.Value),
            BlacklistItemType.ClassName => EqualsLoose(window.ClassName, item.Value),
            _ => false
        };
    }

    private static bool EqualsLoose(string source, string value)
    {
        source = NormalizeProcessName(source);
        value = NormalizeProcessName(value);
        return string.Equals(source, value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsLoose(string source, string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeProcessName(string value)
    {
        value = value.Trim();
        if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return value[..^4];
        }

        return value;
    }
}
