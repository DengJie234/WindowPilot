using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WindowPilot.Services;

namespace WindowPilot.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _clockTimer;
    private readonly Action _saveConfig;
    private object _currentPage;
    private string _currentPageKey = "Dashboard";
    private string _statusMessage = "就绪";

    public MainViewModel(
        DashboardViewModel dashboard,
        WindowsViewModel windows,
        LayoutsViewModel layouts,
        RulesViewModel rules,
        ViewModelBase miniWindow,
        ViewModelBase groups,
        HotkeysViewModel hotkeys,
        SettingsViewModel settings,
        ViewModelBase about,
        Action saveConfig)
    {
        Dashboard = dashboard;
        Windows = windows;
        Layouts = layouts;
        Rules = rules;
        MiniWindow = miniWindow;
        Groups = groups;
        Hotkeys = hotkeys;
        Settings = settings;
        About = about;
        _saveConfig = saveConfig;
        _currentPage = dashboard;

        NavigateCommand = new RelayCommand(key => Navigate(key?.ToString() ?? "Dashboard"));
        RefreshCommand = new RelayCommand(RefreshCurrentPage);
        OpenSettingsCommand = new RelayCommand(() => Navigate("Settings"));

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentTimeText));
        };
        _clockTimer.Start();
    }

    public DashboardViewModel Dashboard { get; }
    public WindowsViewModel Windows { get; }
    public LayoutsViewModel Layouts { get; }
    public RulesViewModel Rules { get; }
    public ViewModelBase MiniWindow { get; }
    public ViewModelBase Groups { get; }
    public HotkeysViewModel Hotkeys { get; }
    public SettingsViewModel Settings { get; }
    public ViewModelBase About { get; }

    public object CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string CurrentPageKey
    {
        get => _currentPageKey;
        private set
        {
            if (SetProperty(ref _currentPageKey, value))
            {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(PageDescription));
            }
        }
    }

    public string PageTitle => CurrentPageKey switch
    {
        "Windows" => "窗口列表",
        "Layouts" => "布局管理",
        "Rules" => "自动规则",
        "Mini" => "小窗模式",
        "Groups" => "分组管理",
        "Hotkeys" => "快捷键",
        "Settings" => "设置",
        "About" => "关于",
        _ => "总览"
    };

    public string PageDescription => CurrentPageKey switch
    {
        "Windows" => "搜索、筛选并直接管理当前桌面上的可见窗口。",
        "Layouts" => "保存和恢复一组窗口的位置、大小和状态。",
        "Rules" => "让特定窗口打开后自动置顶、移动、透明或进入指定布局。",
        "Mini" => "把窗口固定成小窗并快速恢复原始位置。",
        "Groups" => "按工作场景批量管理多个窗口。",
        "Hotkeys" => "自定义全局快捷键，查看注册状态和失败原因。",
        "Settings" => "常规、外观、安全和黑名单设置。",
        "About" => "WindowPilot 版本和运行信息。",
        _ => "当前活动窗口、快捷操作和最近操作记录。"
    };

    public string CurrentTimeText => DateTime.Now.ToString("HH:mm:ss");

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand NavigateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ObservableCollection<ToastNotification> Toasts { get; } = [];

    public void RefreshAll()
    {
        Dashboard.RefreshCurrentWindow();
        Windows.Refresh();
        Layouts.Refresh();
        Rules.Refresh();
        _saveConfig();
    }

    public OperationResult ExecuteHotKey(HotKeyAction action)
    {
        var result = Dashboard.ExecuteHotKey(action);
        Windows.Refresh();
        StatusMessage = result.Message;
        return result;
    }

    public void Notify(string message, bool isError)
    {
        StatusMessage = message;
        ShowToast(message, isError ? ToastKind.Error : ToastKind.Success);
    }

    public void ShowToast(string message, ToastKind kind = ToastKind.Info, string? title = null)
    {
        var toast = new ToastNotification
        {
            Title = title ?? kind switch
            {
                ToastKind.Success => "完成",
                ToastKind.Warning => "请注意",
                ToastKind.Error => "操作失败",
                _ => "WindowPilot"
            },
            Message = message,
            Kind = kind
        };

        Toasts.Insert(0, toast);
        while (Toasts.Count > 4)
        {
            Toasts.RemoveAt(Toasts.Count - 1);
        }

        _ = Task.Delay(TimeSpan.FromSeconds(kind == ToastKind.Error ? 5 : 3)).ContinueWith(_ =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (Toasts.Contains(toast))
                {
                    Toasts.Remove(toast);
                }
            });
        });
    }

    public void Dispose()
    {
        _clockTimer.Stop();
    }

    private void Navigate(string key)
    {
        CurrentPageKey = key;
        CurrentPage = key switch
        {
            "Windows" => Windows,
            "Layouts" => Layouts,
            "Rules" => Rules,
            "Mini" => MiniWindow,
            "Groups" => Groups,
            "Hotkeys" => Hotkeys,
            "Settings" => Settings,
            "About" => About,
            _ => Dashboard
        };
    }

    private void RefreshCurrentPage()
    {
        if (CurrentPage == Windows)
        {
            Windows.Refresh();
        }
        else if (CurrentPage == Layouts)
        {
            Layouts.Refresh();
        }
        else if (CurrentPage == Rules)
        {
            Rules.Refresh();
        }
        else if (CurrentPage == Hotkeys)
        {
            Hotkeys.Refresh();
        }
        else
        {
            Dashboard.RefreshCurrentWindow();
        }
    }
}

public class PlaceholderPageViewModel : ViewModelBase
{
    public PlaceholderPageViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }
    public string Description { get; }
}

public sealed class MiniWindowPageViewModel : PlaceholderPageViewModel
{
    public MiniWindowPageViewModel() : base("小窗模式", "把窗口固定到屏幕角落，并保留一键恢复入口。")
    {
    }
}

public sealed class GroupsPageViewModel : PlaceholderPageViewModel
{
    public GroupsPageViewModel() : base("分组管理", "把多个窗口组织成工作组，后续可批量最小化、恢复或置顶。")
    {
    }
}

public sealed class AboutPageViewModel : PlaceholderPageViewModel
{
    public AboutPageViewModel() : base("关于 WindowPilot", "一个轻量、稳定的 Windows 窗口管理工具。")
    {
        OpenConfigDirectoryCommand = new RelayCommand(() => OpenDirectory(ConfigDirectory));
        OpenLogDirectoryCommand = new RelayCommand(() => OpenDirectory(LogDirectory));
    }

    public string VersionText => "v0.2 UI Refresh";
    public string ConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowPilot");
    public string LogDirectory => Path.Combine(ConfigDirectory, "logs");
    public ICommand OpenConfigDirectoryCommand { get; }
    public ICommand OpenLogDirectoryCommand { get; }

    private static void OpenDirectory(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
