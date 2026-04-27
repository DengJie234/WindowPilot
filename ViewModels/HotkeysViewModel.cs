using System.Collections.ObjectModel;
using System.Windows.Input;
using WindowPilot.Models;
using WindowPilot.Services;

namespace WindowPilot.ViewModels;

public sealed class HotkeysViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly Action _saveConfig;
    private readonly Func<HotkeyItem, HotkeyItem?> _captureHotkey;
    private readonly Action<string, bool> _notify;
    private HotKeyService? _service;

    public HotkeysViewModel(
        AppConfig config,
        Action saveConfig,
        Func<HotkeyItem, HotkeyItem?> captureHotkey,
        Action<string, bool> notify)
    {
        _config = config;
        _saveConfig = saveConfig;
        _captureHotkey = captureHotkey;
        _notify = notify;
        Hotkeys = new ObservableCollection<HotkeyItem>(_config.HotKeys);

        EditCommand = new RelayCommand(EditHotkey);
        ToggleEnabledCommand = new RelayCommand(ToggleEnabled);
        ResetCommand = new RelayCommand(ResetOne);
        ReRegisterAllCommand = new RelayCommand(ReRegisterAll);
        ResetAllCommand = new RelayCommand(ResetAll);
        SaveCommand = new RelayCommand(Save);
    }

    public ObservableCollection<HotkeyItem> Hotkeys { get; }

    public string SummaryText
    {
        get
        {
            var failed = Hotkeys.Count(item => item.Enabled && !item.IsRegistered);
            return failed == 0
                ? "所有启用的快捷键都已注册。"
                : $"{failed} 个快捷键注册失败，请修改或禁用对应项。";
        }
    }

    public ICommand EditCommand { get; }
    public ICommand ToggleEnabledCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand ReRegisterAllCommand { get; }
    public ICommand ResetAllCommand { get; }
    public ICommand SaveCommand { get; }

    public void Initialize(HotKeyService service)
    {
        _service = service;
        SyncFromConfig();
        Refresh();
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(SummaryText));
        foreach (var item in Hotkeys)
        {
            item.Status = item.Status;
        }
    }

    private void EditHotkey(object? parameter)
    {
        if (parameter is not HotkeyItem item)
        {
            return;
        }

        var updated = _captureHotkey(item.Clone());
        if (updated is null)
        {
            return;
        }

        var result = _service?.UpdateHotkey(item.Id, updated) ?? ApplyWithoutRegistration(item, updated);
        CompleteChange(result);
    }

    private void ToggleEnabled(object? parameter)
    {
        if (parameter is not HotkeyItem item)
        {
            return;
        }

        var updated = item.Clone();
        updated.Enabled = !item.Enabled;
        var result = _service?.UpdateHotkey(item.Id, updated) ?? ApplyWithoutRegistration(item, updated);
        CompleteChange(result);
    }

    private void ResetOne(object? parameter)
    {
        if (parameter is not HotkeyItem item)
        {
            return;
        }

        var defaultItem = HotkeyItem.CreateDefaults().FirstOrDefault(defaultHotkey => string.Equals(defaultHotkey.Id, item.Id, StringComparison.OrdinalIgnoreCase));
        if (defaultItem is null)
        {
            CompleteChange(OperationResult.Fail("未找到默认快捷键。"));
            return;
        }

        var result = _service?.ResetToDefault(item.Id) ?? ApplyWithoutRegistration(item, defaultItem);
        CompleteChange(result);
    }

    private void ReRegisterAll()
    {
        if (_service is null)
        {
            CompleteChange(OperationResult.Fail("快捷键服务尚未初始化。"));
            return;
        }

        var failures = _service.RegisterAllHotkeys();
        CompleteChange(failures.Count == 0
            ? OperationResult.Ok("全部快捷键已重新注册。")
            : OperationResult.Fail($"{failures.Count} 个快捷键注册失败。"));
    }

    private void ResetAll()
    {
        if (_service is null)
        {
            _config.HotKeys.Clear();
            foreach (var item in HotkeyItem.CreateDefaults())
            {
                _config.HotKeys.Add(item);
            }

            SyncFromConfig();
            CompleteChange(OperationResult.Ok("已恢复全部默认快捷键。"));
            return;
        }

        CompleteChange(_service.ResetAllToDefault());
        SyncFromConfig();
    }

    private void Save()
    {
        _saveConfig();
        _notify("快捷键设置已保存。", false);
    }

    private OperationResult ApplyWithoutRegistration(HotkeyItem item, HotkeyItem updated)
    {
        item.CopyShortcutFrom(updated);
        return OperationResult.Ok("快捷键已更新，稍后会在快捷键服务初始化后注册。");
    }

    private void CompleteChange(OperationResult result)
    {
        SyncFromConfig();
        Refresh();
        _saveConfig();
        _notify(result.Message, !result.Success);
    }

    private void SyncFromConfig()
    {
        Hotkeys.Clear();
        foreach (var item in _config.HotKeys)
        {
            Hotkeys.Add(item);
        }

        OnPropertyChanged(nameof(SummaryText));
    }
}
