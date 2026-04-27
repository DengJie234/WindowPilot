using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using WindowPilot.Models;
using WindowPilot.Services;

namespace WindowPilot.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly WindowService _windowService;
    private readonly MiniWindowService _miniWindowService;
    private readonly BlacklistService _blacklistService;
    private readonly Action _saveConfig;
    private readonly Action _refreshWindows;
    private readonly Func<string, string, bool> _confirm;
    private readonly Action<string, bool> _notify;
    private WindowInfo? _currentWindow;
    private string _statusMessage = "就绪";
    private bool _isUpdatingOpacity;
    private int _opacityPercent = 100;

    public DashboardViewModel(
        WindowService windowService,
        MiniWindowService miniWindowService,
        BlacklistService blacklistService,
        Action saveConfig,
        Action refreshWindows,
        Func<string, string, bool> confirm,
        Action<string, bool> notify)
    {
        _windowService = windowService;
        _miniWindowService = miniWindowService;
        _blacklistService = blacklistService;
        _saveConfig = saveConfig;
        _refreshWindows = refreshWindows;
        _confirm = confirm;
        _notify = notify;

        RefreshCurrentCommand = new RelayCommand(RefreshCurrentWindow);
        DelayedCaptureCommand = new RelayCommand(async () => await DelayedCaptureAsync());
        ToggleTopMostCommand = new RelayCommand(() => RunCurrent("置顶/取消置顶", _windowService.ToggleTopMost));
        SetOpacity80Command = new RelayCommand(() => RunCurrent("设置 80% 透明", hwnd => _windowService.SetOpacityPercent(hwnd, 80)));
        RestoreOpacityCommand = new RelayCommand(() => RunCurrent("恢复不透明", _windowService.RestoreOpacity));
        ToggleClickThroughCommand = new RelayCommand(ToggleClickThrough);
        MoveLeftCommand = new RelayCommand(() => RunCurrent("左半屏", _windowService.MoveLeftHalf));
        MoveRightCommand = new RelayCommand(() => RunCurrent("右半屏", _windowService.MoveRightHalf));
        CenterCommand = new RelayCommand(() => RunCurrent("居中", _windowService.CenterWindow));
        EmergencyRestoreCommand = new RelayCommand(EmergencyRestore);
        MiniBottomRightCommand = new RelayCommand(() => RunMini(MiniWindowCorner.BottomRight));
        RestoreMiniWindowsCommand = new RelayCommand(RestoreMiniWindows);
    }

    public WindowInfo? CurrentWindow
    {
        get => _currentWindow;
        private set
        {
            if (SetProperty(ref _currentWindow, value))
            {
                OnPropertyChanged(nameof(HasCurrentWindow));
                OnPropertyChanged(nameof(CurrentWindowTitle));
                OnPropertyChanged(nameof(ProcessDisplay));
                OnPropertyChanged(nameof(PidDisplay));
                OnPropertyChanged(nameof(HandleDisplay));
                OnPropertyChanged(nameof(BoundsDisplay));
                OnPropertyChanged(nameof(MonitorDisplay));
                OnPropertyChanged(nameof(TopMostText));
                OnPropertyChanged(nameof(ClickThroughText));
                OnPropertyChanged(nameof(ProtectionText));
                OnPropertyChanged(nameof(IsTopMost));
                OnPropertyChanged(nameof(IsClickThrough));
                OnPropertyChanged(nameof(IsProtected));
                OnPropertyChanged(nameof(CurrentStatusTags));
                SetOpacitySilently(value?.OpacityPercent ?? 100);
            }
        }
    }

    public bool HasCurrentWindow => CurrentWindow is not null;
    public string CurrentWindowTitle => CurrentWindow?.Title is { Length: > 0 } title ? title : "当前没有可控制的活动窗口";
    public string ProcessDisplay => CurrentWindow?.ProcessName ?? "-";
    public string PidDisplay => CurrentWindow?.ProcessId.ToString() ?? "-";
    public string HandleDisplay => CurrentWindow?.HandleHex ?? "-";
    public string BoundsDisplay => CurrentWindow?.Bounds ?? "-";
    public string MonitorDisplay => CurrentWindow?.MonitorDeviceName ?? "-";
    public bool IsTopMost => CurrentWindow?.IsTopMost == true;
    public bool IsClickThrough => CurrentWindow?.IsClickThrough == true;
    public bool IsProtected => CurrentWindow is not null && _blacklistService.IsBlocked(CurrentWindow, out _);
    public string TopMostText => IsTopMost ? "已置顶" : "未置顶";
    public string ClickThroughText => IsClickThrough ? "已穿透" : "未穿透";
    public string ProtectionText => IsProtected ? "已保护" : "可控制";

    public int OpacityPercent
    {
        get => _opacityPercent;
        set
        {
            value = Math.Clamp(value, 20, 100);
            if (!SetProperty(ref _opacityPercent, value))
            {
                return;
            }

            OnPropertyChanged(nameof(OpacityText));
            if (_isUpdatingOpacity || CurrentWindow is null)
            {
                return;
            }

            RunCurrent($"设置 {value}% 不透明", hwnd => _windowService.SetOpacityPercent(hwnd, value), showFailureDialog: false);
        }
    }

    public string OpacityText => $"{OpacityPercent}% 不透明";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<OperationLogEntry> RecentOperations { get; } = [];
    public bool HasRecentOperations => RecentOperations.Count > 0;

    public IReadOnlyList<WindowStatusTag> CurrentStatusTags
    {
        get
        {
            if (CurrentWindow is null)
            {
                return [new WindowStatusTag { Text = "无活动窗口", Kind = "Normal" }];
            }

            var tags = new List<WindowStatusTag>
            {
                new() { Text = TopMostText, Kind = IsTopMost ? "TopMost" : "Normal" },
                new() { Text = ClickThroughText, Kind = IsClickThrough ? "ClickThrough" : "Normal" },
                new() { Text = OpacityPercent < 100 ? "透明" : "100% 不透明", Kind = OpacityPercent < 100 ? "Transparent" : "Normal" },
                new() { Text = ProtectionText, Kind = IsProtected ? "Protected" : "Mini" }
            };
            return tags;
        }
    }

    public ICommand RefreshCurrentCommand { get; }
    public ICommand DelayedCaptureCommand { get; }
    public ICommand ToggleTopMostCommand { get; }
    public ICommand SetOpacity80Command { get; }
    public ICommand RestoreOpacityCommand { get; }
    public ICommand ToggleClickThroughCommand { get; }
    public ICommand MoveLeftCommand { get; }
    public ICommand MoveRightCommand { get; }
    public ICommand CenterCommand { get; }
    public ICommand EmergencyRestoreCommand { get; }
    public ICommand MiniBottomRightCommand { get; }
    public ICommand RestoreMiniWindowsCommand { get; }

    public void RefreshCurrentWindow()
    {
        CurrentWindow = _windowService.GetForegroundWindowInfo();
        StatusMessage = CurrentWindow is null
            ? "没有可控制的活动窗口。"
            : $"已捕获 {CurrentWindow.ProcessName}";
    }

    public void RefreshCurrentFromHandle()
    {
        CurrentWindow = CurrentWindow is null ? _windowService.GetForegroundWindowInfo() : _windowService.GetWindowInfo(CurrentWindow.Handle);
    }

    public OperationResult ExecuteHotKey(HotKeyAction action)
    {
        var target = _windowService.GetForegroundWindowInfo();
        if (action == HotKeyAction.RefreshActiveWindow)
        {
            RefreshCurrentWindow();
            return OperationResult.Ok(StatusMessage);
        }

        if (target is null && action != HotKeyAction.EmergencyRestore)
        {
            StatusMessage = "没有可控制的活动窗口。";
            return OperationResult.Fail(StatusMessage);
        }

        var result = action switch
        {
            HotKeyAction.ToggleTopMost => _windowService.ToggleTopMost(target!.Handle),
            HotKeyAction.IncreaseOpacity => _windowService.IncreaseOpacity(target!.Handle),
            HotKeyAction.DecreaseOpacity => _windowService.DecreaseOpacity(target!.Handle),
            HotKeyAction.ResetOpacity => _windowService.RestoreOpacity(target!.Handle),
            HotKeyAction.ToggleClickThrough => _windowService.ToggleClickThrough(target!.Handle),
            HotKeyAction.EmergencyRestore => _windowService.RestoreAll(),
            HotKeyAction.MoveLeftHalf => _windowService.MoveLeftHalf(target!.Handle),
            HotKeyAction.MoveRightHalf => _windowService.MoveRightHalf(target!.Handle),
            HotKeyAction.CenterWindow => _windowService.CenterWindow(target!.Handle),
            HotKeyAction.ToggleMiniWindow => ToggleMiniFromHotkey(target),
            _ => OperationResult.Fail("未知快捷键")
        };

        CompleteOperation(result, target?.DisplayName ?? "全部窗口", action.ToString(), showFailureDialog: false);
        return result;
    }

    private async Task DelayedCaptureAsync()
    {
        StatusMessage = "请在 3 秒内切换到目标窗口...";
        await Task.Delay(TimeSpan.FromSeconds(3));
        RefreshCurrentWindow();
    }

    private void ToggleClickThrough()
    {
        if (!_confirm("点击穿透会让目标窗口无法直接用鼠标点中。\n确认后仍可使用 Ctrl+Alt+R 紧急恢复。", "确认点击穿透"))
        {
            return;
        }

        RunCurrent("点击穿透/取消穿透", _windowService.ToggleClickThrough);
    }

    private void EmergencyRestore()
    {
        if (!_confirm("将恢复所有被 WindowPilot 修改过的窗口状态，并取消小窗模式。是否继续？", "紧急恢复全部"))
        {
            return;
        }

        var mini = _miniWindowService.RestoreAll();
        var windows = _windowService.RestoreAll();
        var result = mini.Success && windows.Success
            ? OperationResult.Ok($"{windows.Message}; {mini.Message}")
            : OperationResult.Fail($"{windows.Message}; {mini.Message}");
        CompleteOperation(result, "全部窗口", "紧急恢复全部");
    }

    private void RestoreMiniWindows()
    {
        CompleteOperation(_miniWindowService.RestoreAll(), "小窗", "恢复所有小窗");
    }

    private OperationResult ToggleMiniFromHotkey(WindowInfo? target)
    {
        if (target is null)
        {
            return OperationResult.Fail("没有可控制的活动窗口。");
        }

        var state = _miniWindowService.MiniWindows.FirstOrDefault(item => item.Identity.Hwnd == target.Handle.ToInt64());
        return state is null
            ? _miniWindowService.EnterMiniMode(target, MiniWindowCorner.BottomRight)
            : _miniWindowService.RestoreMiniWindow(state);
    }

    private void RunMini(MiniWindowCorner corner)
    {
        CompleteOperation(_miniWindowService.EnterMiniMode(CurrentWindow, corner), CurrentWindow?.DisplayName ?? "当前窗口", "小窗模式");
    }

    private void RunCurrent(string action, Func<nint, OperationResult> operation, bool showFailureDialog = true)
    {
        if (CurrentWindow is null)
        {
            StatusMessage = "请先捕获当前活动窗口。";
            return;
        }

        CompleteOperation(operation(CurrentWindow.Handle), CurrentWindow.DisplayName, action, showFailureDialog);
    }

    private void CompleteOperation(OperationResult result, string target, string action, bool showFailureDialog = true)
    {
        StatusMessage = result.Message;
        RecentOperations.Insert(0, new OperationLogEntry
        {
            Target = target,
            Action = action,
            Success = result.Success,
            Message = result.Message
        });

        while (RecentOperations.Count > 5)
        {
            RecentOperations.RemoveAt(RecentOperations.Count - 1);
        }

        OnPropertyChanged(nameof(HasRecentOperations));

        if (!result.Success && showFailureDialog)
        {
            _notify(result.Message, true);
        }
        else if (result.Success)
        {
            _notify(result.Message, false);
        }

        RefreshCurrentFromHandle();
        _refreshWindows();
        _saveConfig();
    }

    private void SetOpacitySilently(int percent)
    {
        _isUpdatingOpacity = true;
        OpacityPercent = Math.Clamp(percent, 20, 100);
        _isUpdatingOpacity = false;
        OnPropertyChanged(nameof(OpacityText));
        OnPropertyChanged(nameof(CurrentStatusTags));
    }
}
