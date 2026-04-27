namespace WindowPilot.Models;

public sealed class AppConfig
{
    public WindowPilotSettings Settings { get; set; } = new();
    public List<HotkeyItem> HotKeys { get; set; } = HotkeyItem.CreateDefaults();
    public List<PersistedWindowState> WindowStates { get; set; } = [];
    public List<WindowLayout> Layouts { get; set; } = [];
    public List<WindowRule> Rules { get; set; } = [];
    public List<RuleExecutionLog> RuleLogs { get; set; } = [];
    public List<WindowGroup> WindowGroups { get; set; } = [];
    public BlacklistConfig Blacklist { get; set; } = BlacklistConfig.CreateDefault();
    public List<MiniWindowState> MiniWindows { get; set; } = [];
}

public sealed class WindowPilotSettings
{
    public byte MinimumOpacity { get; set; } = 51;
    public byte MaximumOpacity { get; set; } = 255;
    public byte OpacityStep { get; set; } = 25;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool RulesPaused { get; set; }
    public bool OverlayEnabled { get; set; } = true;
    public bool OverlayTemporaryOnly { get; set; } = true;
    public bool HighlightSelectedWindow { get; set; }
    public int RuleScanIntervalMs { get; set; } = 2000;
    public int DefaultMiniWidth { get; set; } = 420;
    public int DefaultMiniHeight { get; set; } = 240;
    public int DefaultMiniOpacity { get; set; } = 90;
    public bool DefaultMiniTopMost { get; set; } = true;
}

public sealed class PersistedWindowState
{
    public string HandleHex { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public bool IsTopMost { get; set; }
    public bool IsClickThrough { get; set; }
    public int OpacityPercent { get; set; }
}
