using System.Windows.Input;

namespace WindowPilot.ViewModels;

public sealed class TrayFlyoutViewModel : ViewModelBase
{
    private readonly Action _closeFlyout;

    public TrayFlyoutViewModel(
        Action closeFlyout,
        Action openMainWindow,
        Action safeRecovery,
        Action clearTopMost,
        Action restoreOpacity,
        Action clearClickThrough,
        Action restoreMiniWindows,
        Action emergencyRestore,
        Action pauseRules,
        Action openSettings,
        Action exitApplication)
    {
        _closeFlyout = closeFlyout;
        OpenMainWindowCommand = CreateCommand(openMainWindow);
        SafeRecoveryCommand = CreateCommand(safeRecovery);
        ClearTopMostCommand = CreateCommand(clearTopMost);
        RestoreOpacityCommand = CreateCommand(restoreOpacity);
        ClearClickThroughCommand = CreateCommand(clearClickThrough);
        RestoreMiniWindowsCommand = CreateCommand(restoreMiniWindows);
        EmergencyRestoreCommand = CreateCommand(emergencyRestore);
        PauseRulesCommand = CreateCommand(pauseRules);
        OpenSettingsCommand = CreateCommand(openSettings);
        ExitApplicationCommand = CreateCommand(exitApplication);
    }

    public string StatusText => "正在运行";
    public string HintText => "后台托盘运行，快捷键与自动规则保持可用";

    public ICommand OpenMainWindowCommand { get; }
    public ICommand SafeRecoveryCommand { get; }
    public ICommand ClearTopMostCommand { get; }
    public ICommand RestoreOpacityCommand { get; }
    public ICommand ClearClickThroughCommand { get; }
    public ICommand RestoreMiniWindowsCommand { get; }
    public ICommand EmergencyRestoreCommand { get; }
    public ICommand PauseRulesCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitApplicationCommand { get; }

    private ICommand CreateCommand(Action action) => new RelayCommand(() =>
    {
        _closeFlyout();
        action();
    });
}
