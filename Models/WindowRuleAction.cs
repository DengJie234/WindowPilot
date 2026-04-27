namespace WindowPilot.Models;

public enum RuleActionType
{
    SetTopMost,
    ClearTopMost,
    SetOpacity,
    EnableClickThrough,
    DisableClickThrough,
    MoveLeftHalf,
    MoveRightHalf,
    Center,
    MoveTopLeft,
    MoveTopRight,
    MoveBottomLeft,
    MoveBottomRight,
    SetSize
}

public sealed class WindowRuleAction
{
    public RuleActionType Type { get; set; } = RuleActionType.SetTopMost;
    public int Opacity { get; set; } = 90;
    public int Width { get; set; } = 900;
    public int Height { get; set; } = 600;
}
