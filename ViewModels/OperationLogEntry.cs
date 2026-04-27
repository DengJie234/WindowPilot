namespace WindowPilot.ViewModels;

public sealed class OperationLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Target { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string DisplayText => $"{Timestamp:HH:mm}  {Target}  {Action}";
}
