namespace WindowPilot.Models;

public sealed class WindowSnapshot
{
    public nint Handle { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public int ExtendedStyle { get; init; }
    public bool WasTopMost { get; init; }
    public bool WasLayered { get; init; }
    public byte OriginalAlpha { get; init; } = 255;
    public bool HadAlphaAttribute { get; init; }
}
