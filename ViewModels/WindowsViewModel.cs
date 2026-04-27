using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using WindowPilot.Models;
using WindowPilot.Services;

namespace WindowPilot.ViewModels;

public sealed class WindowsViewModel : ViewModelBase
{
    private readonly WindowService _windowService;
    private readonly MiniWindowService _miniWindowService;
    private readonly BlacklistService _blacklistService;
    private readonly Action _saveConfig;
    private readonly Action<string, bool> _notify;
    private string _searchText = string.Empty;
    private string _selectedStatusFilter = "全部";
    private bool _onlyControllable;
    private bool _isRefreshing;
    private WindowRowViewModel? _selectedWindow;

    public WindowsViewModel(
        WindowService windowService,
        MiniWindowService miniWindowService,
        BlacklistService blacklistService,
        Action saveConfig,
        Action<string, bool> notify)
    {
        _windowService = windowService;
        _miniWindowService = miniWindowService;
        _blacklistService = blacklistService;
        _saveConfig = saveConfig;
        _notify = notify;

        WindowsView = CollectionViewSource.GetDefaultView(Windows);
        WindowsView.Filter = FilterWindow;

        RefreshCommand = new RelayCommand(Refresh);
        BatchRestoreCommand = new RelayCommand(() => Complete(_windowService.RestoreAll(), "批量恢复"));
        ToggleTopMostCommand = new RelayCommand(row => RunRow(row, "置顶/取消置顶", _windowService.ToggleTopMost));
        SetOpacity80Command = new RelayCommand(row => RunRow(row, "80% 透明", hwnd => _windowService.SetOpacityPercent(hwnd, 80)));
        ToggleClickThroughCommand = new RelayCommand(row => RunRow(row, "点击穿透/取消穿透", _windowService.ToggleClickThrough));
        MoveLeftCommand = new RelayCommand(row => RunRow(row, "左半屏", _windowService.MoveLeftHalf));
        MoveRightCommand = new RelayCommand(row => RunRow(row, "右半屏", _windowService.MoveRightHalf));
        CenterCommand = new RelayCommand(row => RunRow(row, "居中", _windowService.CenterWindow));
    }

    public ObservableCollection<WindowRowViewModel> Windows { get; } = [];
    public ICollectionView WindowsView { get; }
    public IReadOnlyList<string> StatusFilters { get; } = ["全部", "普通", "已置顶", "透明", "穿透", "小窗", "受保护"];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                WindowsView.Refresh();
                OnPropertyChanged(nameof(HasNoResults));
            }
        }
    }

    public bool OnlyControllable
    {
        get => _onlyControllable;
        set
        {
            if (SetProperty(ref _onlyControllable, value))
            {
                WindowsView.Refresh();
                OnPropertyChanged(nameof(HasNoResults));
            }
        }
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                WindowsView.Refresh();
                OnPropertyChanged(nameof(HasNoResults));
            }
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public WindowRowViewModel? SelectedWindow
    {
        get => _selectedWindow;
        set => SetProperty(ref _selectedWindow, value);
    }

    public bool HasNoWindows => Windows.Count == 0;
    public bool HasNoResults => Windows.Count > 0 && !WindowsView.Cast<object>().Any();
    public string CountText => $"{Windows.Count} 个窗口";

    public ICommand RefreshCommand { get; }
    public ICommand BatchRestoreCommand { get; }
    public ICommand ToggleTopMostCommand { get; }
    public ICommand SetOpacity80Command { get; }
    public ICommand ToggleClickThroughCommand { get; }
    public ICommand MoveLeftCommand { get; }
    public ICommand MoveRightCommand { get; }
    public ICommand CenterCommand { get; }

    public WindowInfo? SelectedWindowInfo => SelectedWindow?.Info;

    public void Refresh()
    {
        IsRefreshing = true;
        var selectedHandle = SelectedWindow?.Handle;
        var existingByHandle = Windows.ToDictionary(window => window.Handle);
        var activeHandles = new HashSet<nint>();

        foreach (var info in _windowService.EnumerateVisibleWindows())
        {
            activeHandles.Add(info.Handle);
            var isMini = _miniWindowService.IsMiniWindow(info.Handle);
            if (existingByHandle.TryGetValue(info.Handle, out var row))
            {
                row.Update(info, isMini);
            }
            else
            {
                Windows.Add(new WindowRowViewModel(info, _blacklistService, isMini));
            }
        }

        for (var index = Windows.Count - 1; index >= 0; index--)
        {
            if (!activeHandles.Contains(Windows[index].Handle))
            {
                Windows.RemoveAt(index);
            }
        }

        SelectedWindow = Windows.FirstOrDefault(window => window.Handle == selectedHandle);
        WindowsView.Refresh();
        IsRefreshing = false;
        OnPropertyChanged(nameof(HasNoWindows));
        OnPropertyChanged(nameof(HasNoResults));
        OnPropertyChanged(nameof(CountText));
    }

    private bool FilterWindow(object item)
    {
        if (item is not WindowRowViewModel window)
        {
            return false;
        }

        if (OnlyControllable && window.IsProtected)
        {
            return false;
        }

        if (!MatchesStatusFilter(window))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return window.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               window.ProcessName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               window.ProcessId.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RunRow(object? parameter, string action, Func<nint, OperationResult> operation)
    {
        if (parameter is not WindowRowViewModel row)
        {
            return;
        }

        if (row.IsProtected)
        {
            Complete(OperationResult.Fail("该窗口受保护，不能执行窗口控制操作。"), action);
            return;
        }

        Complete(operation(row.Handle), action);
    }

    private bool MatchesStatusFilter(WindowRowViewModel window) =>
        SelectedStatusFilter switch
        {
            "普通" => !window.IsProtected && !window.IsTopMost && !window.IsClickThrough && window.OpacityPercent >= 100 && !window.IsMini,
            "已置顶" => window.IsTopMost,
            "透明" => window.OpacityPercent < 100,
            "穿透" => window.IsClickThrough,
            "小窗" => window.IsMini,
            "受保护" => window.IsProtected,
            _ => true
        };

    private void Complete(OperationResult result, string action)
    {
        if (result.Success)
        {
            _notify($"{action}：{result.Message}", false);
        }
        else
        {
            _notify(result.Message, true);
        }

        Refresh();
        _saveConfig();
    }
}
