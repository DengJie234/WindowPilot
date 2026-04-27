using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using WindowPilot.Models;
using WindowPilot.Services;
using WindowPilot.ViewModels;
using WindowPilot.Views;
using WindowPilot.Views.Dialogs;

namespace WindowPilot;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService = new();
    private readonly AppConfig _config;
    private readonly WindowService _windowService;
    private readonly RuleService _ruleService;
    private readonly MiniWindowService _miniWindowService;
    private readonly MainViewModel _viewModel;
    private TrayIconService? _trayIconService;
    private HotKeyService? _hotKeyService;
    private OverlayService? _overlayService;
    private bool _isExitRequested;
    private bool _servicesDisposed;
    private readonly DispatcherTimer _configSaveTimer;
    private CancellationTokenSource? _rulePauseCts;

    public MainWindow()
    {
        InitializeComponent();

        _configSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        _configSaveTimer.Tick += (_, _) =>
        {
            _configSaveTimer.Stop();
            SaveConfigNow();
        };

        _config = _configService.Load();
        var blacklistService = new BlacklistService(_config.Blacklist);
        _windowService = new WindowService(
            _config.Settings.MinimumOpacity,
            _config.Settings.MaximumOpacity,
            _config.Settings.OpacityStep,
            blacklistService);
        var matcherService = new WindowMatcherService(_windowService);
        var layoutService = new LayoutService(_config, _windowService, matcherService);
        _ruleService = new RuleService(_config, _windowService);
        _miniWindowService = new MiniWindowService(_config, _windowService, matcherService);

        var dashboard = new DashboardViewModel(
            _windowService,
            _miniWindowService,
            blacklistService,
            SaveConfig,
            RefreshWindowsAndOverlay,
            Confirm,
            Notify);
        var windows = new WindowsViewModel(_windowService, _miniWindowService, blacklistService, SaveConfig, Notify);
        var layouts = new LayoutsViewModel(layoutService, SaveConfig, Notify);
        var rules = new RulesViewModel(
            _config,
            _ruleService,
            SaveConfigNow,
            ShowRuleEditorDialog,
            ConfirmDeleteRule,
            Notify,
            RefreshWindowsAndOverlay);
        var settings = new SettingsViewModel(_config, SaveConfig, SyncOverlaySettings);
        var hotkeys = new HotkeysViewModel(_config, SaveConfig, CaptureHotkey, Notify);

        _viewModel = new MainViewModel(
            dashboard,
            windows,
            layouts,
            rules,
            new MiniWindowPageViewModel(),
            new GroupsPageViewModel(),
            hotkeys,
            settings,
            new AboutPageViewModel(),
            SaveConfig);
        DataContext = _viewModel;

        Loaded += (_, _) => _viewModel.RefreshAll();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        _hotKeyService = new HotKeyService(hwnd, _config);
        _hotKeyService.HotKeyPressed += OnHotKeyPressed;
        _viewModel.Hotkeys.Initialize(_hotKeyService);
        var failures = _hotKeyService.RegisterAllHotkeys();
        _viewModel.Hotkeys.Refresh();
        if (failures.Count > 0)
        {
            _viewModel.StatusMessage = $"{failures.Count} 个快捷键注册失败，请到快捷键页修改。";
        }

        _trayIconService = new TrayIconService();
        _trayIconService.OpenRequested += (_, _) => ShowMainWindow();
        _trayIconService.SafeRecoveryRequested += (_, _) => SafeRecoveryMode();
        _trayIconService.ClearTopMostRequested += (_, _) => RunGlobalOperation(_windowService.ClearAllTopMost);
        _trayIconService.RestoreOpacityRequested += (_, _) => RunGlobalOperation(_windowService.RestoreAllOpacity);
        _trayIconService.ClearClickThroughRequested += (_, _) => RunGlobalOperation(_windowService.ClearAllClickThrough);
        _trayIconService.RestoreMiniWindowsRequested += (_, _) => RunGlobalOperation(_miniWindowService.RestoreAll);
        _trayIconService.EmergencyRestoreRequested += (_, _) => EmergencyRestore();
        _trayIconService.PauseRulesRequested += (_, _) => PauseRulesFor(TimeSpan.FromSeconds(30));
        _trayIconService.SettingsRequested += (_, _) =>
        {
            ShowMainWindow();
            _viewModel.NavigateCommand.Execute("Settings");
        };
        _trayIconService.ExitRequested += (_, _) => ExitApplication();

        _overlayService = new OverlayService(
            _windowService,
            _miniWindowService.IsMiniWindow,
            () => _config.Settings.OverlayTemporaryOnly,
            () => _config.Settings.HighlightSelectedWindow,
            () => _viewModel.Windows.SelectedWindow?.Handle)
        {
            Enabled = _config.Settings.OverlayEnabled
        };
        _overlayService.Start();
        _ruleService.Start();
    }

    private void OnHotKeyPressed(object? sender, HotKeyAction action)
    {
        if (action == HotKeyAction.ShowMainWindow)
        {
            ToggleMainWindowVisibility();
            return;
        }

        var result = _viewModel.ExecuteHotKey(action);
        if (result.Success)
        {
            _overlayService?.Sync();
            SaveConfig();
        }
    }

    private void RunGlobalOperation(Func<OperationResult> operation)
    {
        var result = operation();
        Notify(result.Message, !result.Success);
        RefreshWindowsAndOverlay();
        SaveConfig();
    }

    private void EmergencyRestore()
    {
        if (!Confirm("将恢复所有被 WindowPilot 修改过的窗口状态，并取消所有小窗。是否继续？", "紧急恢复全部"))
        {
            return;
        }

        var mini = _miniWindowService.RestoreAll();
        var windows = _windowService.RestoreAll();
        var result = mini.Success && windows.Success
            ? OperationResult.Ok($"{windows.Message}; {mini.Message}")
            : OperationResult.Fail($"{windows.Message}; {mini.Message}");
        Notify(result.Message, !result.Success);
        _trayIconService?.ShowBalloon("WindowPilot", result.Message);
        RefreshWindowsAndOverlay();
        SaveConfig();
    }

    private void SafeRecoveryMode()
    {
        if (!Confirm("安全恢复模式会取消全部置顶、恢复透明度、取消点击穿透、恢复全部小窗，并暂停自动规则 30 秒。是否继续？", "安全恢复模式"))
        {
            return;
        }

        PauseRulesFor(TimeSpan.FromSeconds(30));
        var overlayWasEnabled = _overlayService?.Enabled == true;
        if (_overlayService is not null)
        {
            _overlayService.Enabled = false;
        }

        var top = _windowService.ClearAllTopMost();
        var opacity = _windowService.RestoreAllOpacity();
        var clickThrough = _windowService.ClearAllClickThrough();
        var mini = _miniWindowService.RestoreAll();

        if (overlayWasEnabled && _overlayService is not null)
        {
            _overlayService.Enabled = _config.Settings.OverlayEnabled;
            _overlayService.Sync();
        }

        var results = new[] { top, opacity, clickThrough, mini };
        var failed = results.Where(result => !result.Success).ToList();
        var message = failed.Count == 0
            ? "安全恢复完成，自动规则已暂停 30 秒。"
            : "安全恢复完成，但部分操作失败：" + string.Join("; ", failed.Select(result => result.Message));
        Notify(message, failed.Count > 0);
        RefreshWindowsAndOverlay();
        SaveConfig();
    }

    private void PauseRulesFor(TimeSpan duration)
    {
        _rulePauseCts?.Cancel();
        _rulePauseCts = new CancellationTokenSource();
        var token = _rulePauseCts.Token;
        var wasPaused = _config.Settings.RulesPaused;

        _config.Settings.RulesPaused = true;
        _ruleService.Stop();
        Notify($"自动规则已暂停 {duration.TotalSeconds:0} 秒。", false);
        SaveConfig();

        if (wasPaused)
        {
            return;
        }

        _ = Task.Delay(duration, token).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                return;
            }

            Dispatcher.Invoke(() =>
            {
                if (_isExitRequested)
                {
                    return;
                }

                _config.Settings.RulesPaused = false;
                _ruleService.Start();
                Notify("自动规则已恢复。", false);
                SaveConfig();
            });
        }, TaskScheduler.Default);
    }

    private void RefreshWindowsAndOverlay()
    {
        _viewModel.Windows.Refresh();
        _overlayService?.Sync();
    }

    private void SyncOverlaySettings()
    {
        if (_overlayService is null)
        {
            return;
        }

        _overlayService.Enabled = _config.Settings.OverlayEnabled;
        _overlayService.Sync();
    }

    private bool Confirm(string message, string title) =>
        ShowConfirmDialog(new ConfirmDialogOptions
        {
            Title = title,
            Message = message,
            PrimaryButtonText = "确认",
            CancelButtonText = "取消",
            Kind = ConfirmDialogKind.Warning
        }) == ConfirmDialogResult.Primary;

    private void Notify(string message, bool isError)
    {
        _viewModel.Notify(message, isError);
    }

    private void ShowMainWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_isExitRequested)
            {
                return;
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            ShowInTaskbar = true;
            Show();
            Activate();

            Topmost = true;
            Topmost = false;
            Focus();
        });
    }

    private void ToggleMainWindowVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
            return;
        }

        ShowMainWindow();
    }

    private HotkeyItem? CaptureHotkey(HotkeyItem item)
    {
        var dialog = new HotkeyCaptureDialog(item)
        {
            Owner = this
        };

        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        if (!_config.Settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            ExitApplication();
            return;
        }

        e.Cancel = true;
        Hide();
        ShowInTaskbar = false;
        _trayIconService?.ShowBalloon("WindowPilot", "WindowPilot 正在托盘运行。");
    }

    private void ExitApplication()
    {
        var shouldRestore = ShowExitConfirmDialog();

        if (shouldRestore == ExitConfirmResult.Cancel)
        {
            return;
        }

        if (shouldRestore == ExitConfirmResult.RestoreAndExit)
        {
            _overlayService?.Dispose();
            _miniWindowService.RestoreAll();
            _windowService.RestoreAll();
        }

        _isExitRequested = true;
        SaveConfigNow();
        DisposeServices();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private ExitConfirmResult ShowExitConfirmDialog()
    {
        var dialog = new ExitConfirmDialog();
        if (IsVisible)
        {
            dialog.Owner = this;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true ? dialog.Result : ExitConfirmResult.Cancel;
    }

    private ConfirmDialogResult ShowConfirmDialog(ConfirmDialogOptions options)
    {
        var dialog = new ConfirmDialog(options);
        if (IsVisible)
        {
            dialog.Owner = this;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true ? dialog.Result : ConfirmDialogResult.Cancel;
    }

    private WindowRule? ShowRuleEditorDialog()
    {
        var dialog = new RuleEditorDialog();
        if (IsVisible)
        {
            dialog.Owner = this;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true ? dialog.Rule : null;
    }

    private bool ConfirmDeleteRule(WindowRule rule) =>
        ShowConfirmDialog(new ConfirmDialogOptions
        {
            Title = "删除自动规则",
            Message = $"确定要删除规则“{rule.Name}”吗？",
            DetailLines = ["删除后会立即停止该规则继续扫描，并尝试恢复由该规则修改过的窗口状态。"],
            PrimaryButtonText = "删除",
            CancelButtonText = "取消",
            Kind = ConfirmDialogKind.Danger,
            IsPrimaryDanger = true
        }) == ConfirmDialogResult.Primary;

    private void SaveConfig()
    {
        _config.WindowStates = _windowService.CreatePersistedStates();
        _configSaveTimer.Stop();
        _configSaveTimer.Start();
    }

    private void SaveConfigNow()
    {
        _config.WindowStates = _windowService.CreatePersistedStates();
        _configService.Save(_config);
    }

    private void DisposeServices()
    {
        if (_servicesDisposed)
        {
            return;
        }

        _servicesDisposed = true;
        _rulePauseCts?.Cancel();
        _rulePauseCts?.Dispose();
        _configSaveTimer.Stop();
        _viewModel.Dispose();
        _ruleService.Dispose();
        _overlayService?.Dispose();
        _hotKeyService?.Dispose();
        _trayIconService?.Dispose();
    }
}
