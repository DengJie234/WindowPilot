namespace WindowPilot.ViewModels;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

public sealed class ToastNotification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public ToastKind Kind { get; init; } = ToastKind.Info;
}
