namespace WindowPilot.Models;

public enum ExitConfirmResult
{
    Cancel,
    RestoreAndExit,
    ExitDirectly
}

public enum ConfirmDialogKind
{
    Information,
    Warning,
    Danger
}

public enum ConfirmDialogResult
{
    Cancel,
    Primary,
    Secondary
}

public sealed class ConfirmDialogOptions
{
    public string Title { get; init; } = "确认操作";
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> DetailLines { get; init; } = [];
    public string PrimaryButtonText { get; init; } = "确认";
    public string SecondaryButtonText { get; init; } = string.Empty;
    public string CancelButtonText { get; init; } = "取消";
    public ConfirmDialogKind Kind { get; init; } = ConfirmDialogKind.Information;
    public bool IsPrimaryDanger { get; init; }
}
