using WindowPilot.Models;
using WindowPilot.Services;

namespace WindowPilot.ViewModels;

public sealed class WindowRowViewModel : ViewModelBase
{
    private readonly BlacklistService _blacklistService;
    private WindowInfo _info;
    private bool _isMini;

    public WindowRowViewModel(WindowInfo info, BlacklistService blacklistService, bool isMini)
    {
        _info = info;
        _blacklistService = blacklistService;
        _isMini = isMini;
    }

    public WindowInfo Info => _info;
    public bool IsMini => _isMini;
    public nint Handle => Info.Handle;
    public string Title => string.IsNullOrWhiteSpace(Info.Title) ? "(无标题窗口)" : Info.Title;
    public string ProcessName => Info.ProcessName;
    public int ProcessId => Info.ProcessId;
    public string Bounds => Info.WindowState == SavedWindowState.Minimized || (Info.X <= -30000 && Info.Y <= -30000)
        ? "已最小化"
        : Info.Bounds;
    public string HandleHex => Info.HandleHex;
    public bool IsTopMost => Info.IsTopMost;
    public bool IsClickThrough => Info.IsClickThrough;
    public int OpacityPercent => Info.OpacityPercent;

    public bool IsProtected => _blacklistService.IsBlocked(Info, out _);

    public IReadOnlyList<WindowStatusTag> StatusTags
    {
        get
        {
            var tags = new List<WindowStatusTag>();
            if (IsProtected)
            {
                tags.Add(new WindowStatusTag { Text = "受保护", Kind = "Protected" });
            }

            if (IsTopMost)
            {
                tags.Add(new WindowStatusTag { Text = "置顶", Kind = "TopMost" });
            }

            if (OpacityPercent < 100)
            {
                tags.Add(new WindowStatusTag { Text = "透明", Kind = "Transparent" });
            }

            if (IsClickThrough)
            {
                tags.Add(new WindowStatusTag { Text = "穿透", Kind = "ClickThrough" });
            }

            if (IsMini)
            {
                tags.Add(new WindowStatusTag { Text = "小窗", Kind = "Mini" });
            }

            if (tags.Count == 0)
            {
                tags.Add(new WindowStatusTag { Text = "普通", Kind = "Normal" });
            }

            return tags;
        }
    }

    public void Update(WindowInfo info, bool isMini)
    {
        if (!HasChanged(info, isMini))
        {
            return;
        }

        _info = info;
        _isMini = isMini;
        OnPropertyChanged(nameof(Info));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ProcessName));
        OnPropertyChanged(nameof(ProcessId));
        OnPropertyChanged(nameof(Bounds));
        OnPropertyChanged(nameof(HandleHex));
        OnPropertyChanged(nameof(IsTopMost));
        OnPropertyChanged(nameof(IsClickThrough));
        OnPropertyChanged(nameof(OpacityPercent));
        OnPropertyChanged(nameof(IsProtected));
        OnPropertyChanged(nameof(IsMini));
        OnPropertyChanged(nameof(StatusTags));
    }

    private bool HasChanged(WindowInfo info, bool isMini) =>
        _isMini != isMini ||
        _info.Title != info.Title ||
        _info.ProcessName != info.ProcessName ||
        _info.ProcessId != info.ProcessId ||
        _info.X != info.X ||
        _info.Y != info.Y ||
        _info.Width != info.Width ||
        _info.Height != info.Height ||
        _info.WindowState != info.WindowState ||
        _info.IsTopMost != info.IsTopMost ||
        _info.IsClickThrough != info.IsClickThrough ||
        _info.OpacityPercent != info.OpacityPercent;
}
