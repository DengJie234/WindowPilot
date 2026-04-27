namespace WindowPilot.Models;

public enum BlacklistItemType
{
    ProcessName,
    TitleKeyword,
    ProcessPath,
    ClassName
}

public sealed class BlacklistItem
{
    public BlacklistItemType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public sealed class BlacklistConfig
{
    public bool WhitelistMode { get; set; }
    public List<BlacklistItem> BlacklistItems { get; set; } = [];
    public List<BlacklistItem> WhitelistItems { get; set; } = [];

    public static BlacklistConfig CreateDefault()
    {
        string[] processNames =
        [
            "explorer.exe",
            "SystemSettings.exe",
            "Taskmgr.exe",
            "LockApp.exe",
            "SearchHost.exe",
            "StartMenuExperienceHost.exe",
            "ShellExperienceHost.exe",
            "TextInputHost.exe",
            "ApplicationFrameHost.exe",
            "SecurityHealthSystray.exe"
        ];

        return new BlacklistConfig
        {
            BlacklistItems = processNames
                .Select(name => new BlacklistItem { Type = BlacklistItemType.ProcessName, Value = name })
                .ToList()
        };
    }
}

public sealed class MiniWindowState
{
    public WindowIdentity Identity { get; set; } = new();
    public WindowStateInfo OriginalState { get; set; } = new();
    public MiniWindowCorner Corner { get; set; } = MiniWindowCorner.BottomRight;
    public int Width { get; set; } = 420;
    public int Height { get; set; } = 240;
    public int Opacity { get; set; } = 90;
    public bool AutoTopMost { get; set; } = true;
}

public enum MiniWindowCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}
