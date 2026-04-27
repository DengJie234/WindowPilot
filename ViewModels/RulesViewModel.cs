using System.Collections.ObjectModel;
using System.Windows.Input;
using WindowPilot.Models;
using WindowPilot.Services;

namespace WindowPilot.ViewModels;

public sealed class RulesViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly RuleService _ruleService;
    private readonly Action _saveConfig;
    private readonly Func<WindowRule?> _createRuleDialog;
    private readonly Func<WindowRule, bool> _confirmDeleteRule;
    private readonly Action<string, bool> _notify;
    private readonly Action _refreshOverlays;
    private WindowRule? _selectedRule;

    public RulesViewModel(
        AppConfig config,
        RuleService ruleService,
        Action saveConfig,
        Func<WindowRule?> createRuleDialog,
        Func<WindowRule, bool> confirmDeleteRule,
        Action<string, bool> notify,
        Action refreshOverlays)
    {
        _config = config;
        _ruleService = ruleService;
        _saveConfig = saveConfig;
        _createRuleDialog = createRuleDialog;
        _confirmDeleteRule = confirmDeleteRule;
        _notify = notify;
        _refreshOverlays = refreshOverlays;
        _ruleService.DisableInvalidRules();
        TogglePauseCommand = new RelayCommand(() => RulesPaused = !RulesPaused);
        CreateRuleCommand = new RelayCommand(CreateRule);
        DeleteRuleCommand = new RelayCommand(DeleteRule);
        ToggleRuleEnabledCommand = new RelayCommand(ToggleRuleEnabled);
        Refresh();
    }

    public ObservableCollection<WindowRule> Rules { get; } = [];
    public ObservableCollection<RuleExecutionLog> Logs { get; } = [];

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

    public WindowRule? SelectedRule
    {
        get => _selectedRule;
        set => SetProperty(ref _selectedRule, value);
    }

    public bool HasRules => Rules.Count > 0;
    public ICommand TogglePauseCommand { get; }
    public ICommand CreateRuleCommand { get; }
    public ICommand DeleteRuleCommand { get; }
    public ICommand ToggleRuleEnabledCommand { get; }

    public void Refresh()
    {
        Rules.Clear();
        foreach (var rule in _ruleService.Rules)
        {
            Rules.Add(rule);
        }

        Logs.Clear();
        foreach (var log in _ruleService.Logs.Take(20))
        {
            Logs.Add(log);
        }

        OnPropertyChanged(nameof(HasRules));
    }

    private void CreateRule()
    {
        var rule = _createRuleDialog();
        if (rule is null)
        {
            return;
        }

        var result = _ruleService.AddRule(rule);
        if (!result.Success)
        {
            _notify(result.Message, true);
            return;
        }

        _saveConfig();
        Refresh();
        _notify(result.Message, false);
    }

    private void DeleteRule(object? parameter)
    {
        var rule = parameter as WindowRule ?? SelectedRule;
        if (rule is null)
        {
            _notify("请先选择要删除的规则。", true);
            return;
        }

        if (!_confirmDeleteRule(rule))
        {
            return;
        }

        var result = _ruleService.DeleteRule(rule.Id);
        if (!result.Success)
        {
            _notify(result.Message, true);
            return;
        }

        SelectedRule = null;
        _saveConfig();
        Refresh();
        _refreshOverlays();
        _notify(result.Message, false);
    }

    private void ToggleRuleEnabled(object? parameter)
    {
        if (parameter is not WindowRule rule)
        {
            return;
        }

        if (!rule.IsValid)
        {
            _notify(rule.ValidationMessage, true);
            return;
        }

        rule.IsEnabled = !rule.IsEnabled;
        _saveConfig();
        Refresh();
        _notify(rule.IsEnabled ? "规则已启用。" : "规则已禁用。", false);
    }
}
