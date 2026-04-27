namespace WindowPilot.Models;

public sealed class WindowLayout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = $"Layout {DateTime.Now:yyyy-MM-dd HH-mm}";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<WindowLayoutItem> Items { get; set; } = [];

    public string Summary => $"{Items.Count} windows, {UpdatedAt:yyyy-MM-dd HH:mm}";
}
