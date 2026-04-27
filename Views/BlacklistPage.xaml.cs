using System.Windows;
using System.Windows.Controls;
using WindowPilot.Models;
using WindowPilot.Services;

namespace WindowPilot.Views;

public partial class BlacklistPage : System.Windows.Controls.UserControl
{
    private BlacklistService? _blacklistService;
    private Action? _saveConfig;

    public BlacklistPage()
    {
        InitializeComponent();
        ItemTypeCombo.ItemsSource = Enum.GetValues<BlacklistItemType>();
        ItemTypeCombo.SelectedItem = BlacklistItemType.ProcessName;
    }

    public void Initialize(BlacklistService blacklistService, Action saveConfig)
    {
        _blacklistService = blacklistService;
        _saveConfig = saveConfig;
        WhitelistModeCheck.IsChecked = blacklistService.Config.WhitelistMode;
        Refresh();
    }

    public void Refresh()
    {
        if (_blacklistService is null)
        {
            return;
        }

        BlacklistGrid.ItemsSource = null;
        BlacklistGrid.ItemsSource = _blacklistService.Config.BlacklistItems;
        WhitelistGrid.ItemsSource = null;
        WhitelistGrid.ItemsSource = _blacklistService.Config.WhitelistItems;
    }

    private void AddBlacklist_Click(object sender, RoutedEventArgs e) => AddItem(whitelist: false);

    private void AddWhitelist_Click(object sender, RoutedEventArgs e) => AddItem(whitelist: true);

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_blacklistService is null)
        {
            return;
        }

        if (BlacklistGrid.SelectedItem is BlacklistItem blackItem)
        {
            _blacklistService.Remove(blackItem, whitelist: false);
        }
        else if (WhitelistGrid.SelectedItem is BlacklistItem whiteItem)
        {
            _blacklistService.Remove(whiteItem, whitelist: true);
        }

        _saveConfig?.Invoke();
        Refresh();
    }

    private void WhitelistMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_blacklistService is null)
        {
            return;
        }

        _blacklistService.Config.WhitelistMode = WhitelistModeCheck.IsChecked == true;
        _saveConfig?.Invoke();
    }

    private void AddItem(bool whitelist)
    {
        if (_blacklistService is null || ItemTypeCombo.SelectedItem is not BlacklistItemType type)
        {
            return;
        }

        var value = ItemValueText.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var list = whitelist ? _blacklistService.Config.WhitelistItems : _blacklistService.Config.BlacklistItems;
        if (!list.Any(item => item.Type == type && string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(new BlacklistItem { Type = type, Value = value });
        }

        ItemValueText.Clear();
        _saveConfig?.Invoke();
        Refresh();
    }
}
