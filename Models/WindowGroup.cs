namespace WindowPilot.Models;

public sealed class WindowGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = $"Group {DateTime.Now:HH-mm}";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<WindowIdentity> Windows { get; set; } = [];

    public string Summary => $"{Windows.Count} windows";
}
