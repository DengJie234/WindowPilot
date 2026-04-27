using WindowPilot.Models;

namespace WindowPilot.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly Action _saveConfig;
    private readonly Action _overlaySettingsChanged;

    public SettingsViewModel(AppConfig config, Action saveConfig, Action? overlaySettingsChanged = null)
    {
        _config = config;
        _saveConfig = saveConfig;
        _overlaySettingsChanged = overlaySettingsChanged ?? (() => { });
    }

    public bool MinimizeToTrayOnClose
    {
        get => _config.Settings.MinimizeToTrayOnClose;
        set
        {
            if (_config.Settings.MinimizeToTrayOnClose == value)
            {
                return;
            }

            _config.Settings.MinimizeToTrayOnClose = value;
            OnPropertyChanged();
            _saveConfig();
        }
    }

    public bool RulesPaused
    {
        get => _config.Settings.RulesPaused;
        set
        {
            if (_config.Settings.RulesPaused == value)
            {
                return;
            }

            _config.Settings.RulesPaused = value;
            OnPropertyChanged();
            _saveConfig();
        }
    }

    public bool OverlayEnabled
    {
        get => _config.Settings.OverlayEnabled;
        set
        {
            if (_config.Settings.OverlayEnabled == value)
            {
                return;
            }

            _config.Settings.OverlayEnabled = value;
            OnPropertyChanged();
            _overlaySettingsChanged();
            _saveConfig();
        }
    }

    public bool OverlayTemporaryOnly
    {
        get => _config.Settings.OverlayTemporaryOnly;
        set
        {
            if (_config.Settings.OverlayTemporaryOnly == value)
            {
                return;
            }

            _config.Settings.OverlayTemporaryOnly = value;
            OnPropertyChanged();
            _overlaySettingsChanged();
            _saveConfig();
        }
    }

    public bool HighlightSelectedWindow
    {
        get => _config.Settings.HighlightSelectedWindow;
        set
        {
            if (_config.Settings.HighlightSelectedWindow == value)
            {
                return;
            }

            _config.Settings.HighlightSelectedWindow = value;
            OnPropertyChanged();
            _overlaySettingsChanged();
            _saveConfig();
        }
    }

    public IReadOnlyList<BlacklistItem> BlacklistItems => _config.Blacklist.BlacklistItems;
    public IReadOnlyList<HotkeyItem> HotKeys => _config.HotKeys;
}
