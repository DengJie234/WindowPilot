using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WindowPilot.Models;

namespace WindowPilot.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ConfigDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowPilot");

    public string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var created = new AppConfig();
                Save(created);
                return Normalize(created);
            }

            var json = File.ReadAllText(ConfigPath);
            return Normalize(JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig());
        }
        catch
        {
            var backupPath = ConfigPath + ".bak";
            if (File.Exists(backupPath))
            {
                try
                {
                    var backupJson = File.ReadAllText(backupPath);
                    return Normalize(JsonSerializer.Deserialize<AppConfig>(backupJson, JsonOptions) ?? new AppConfig());
                }
                catch
                {
                    // Fall through to default config when both primary and backup are unreadable.
                }
            }

            return Normalize(new AppConfig());
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        if (File.Exists(ConfigPath))
        {
            File.Copy(ConfigPath, ConfigPath + ".bak", overwrite: true);
        }

        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    private static AppConfig Normalize(AppConfig config)
    {
        config.Settings ??= new WindowPilotSettings();
        if (config.HotKeys is null || config.HotKeys.Count == 0 || config.HotKeys.Any(item => string.IsNullOrWhiteSpace(item.Id)))
        {
            config.HotKeys = HotkeyItem.CreateDefaults();
        }
        else
        {
            foreach (var defaultHotkey in HotkeyItem.CreateDefaults())
            {
                var existing = config.HotKeys.FirstOrDefault(item => string.Equals(item.Id, defaultHotkey.Id, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    config.HotKeys.Add(defaultHotkey);
                }
                else
                {
                    existing.CopyMetadataFrom(defaultHotkey);
                    if (string.IsNullOrWhiteSpace(existing.Key))
                    {
                        existing.CopyShortcutFrom(defaultHotkey);
                    }
                }
            }
        }
        config.WindowStates ??= [];
        config.Layouts ??= [];
        config.Rules ??= [];
        foreach (var rule in config.Rules.Where(rule => !WindowRule.HasMatchCondition(rule)))
        {
            rule.IsEnabled = false;
        }

        config.RuleLogs ??= [];
        config.WindowGroups ??= [];
        config.MiniWindows ??= [];
        config.Blacklist ??= BlacklistConfig.CreateDefault();
        config.Blacklist.BlacklistItems ??= [];
        config.Blacklist.WhitelistItems ??= [];

        foreach (var item in BlacklistConfig.CreateDefault().BlacklistItems)
        {
            if (!config.Blacklist.BlacklistItems.Any(existing =>
                    existing.Type == item.Type &&
                    string.Equals(existing.Value, item.Value, StringComparison.OrdinalIgnoreCase)))
            {
                config.Blacklist.BlacklistItems.Add(item);
            }
        }

        return config;
    }
}
