using System.Windows;
using System.Windows.Controls;
using WindowPilot.Models;
using WindowPilot.Services;

namespace WindowPilot.Views;

public partial class LayoutPage : System.Windows.Controls.UserControl
{
    private LayoutService? _layoutService;
    private Func<WindowInfo?>? _getSelectedWindow;
    private Action? _saveConfig;

    public LayoutPage()
    {
        InitializeComponent();
    }

    public void Initialize(LayoutService layoutService, Func<WindowInfo?> getSelectedWindow, Action saveConfig)
    {
        _layoutService = layoutService;
        _getSelectedWindow = getSelectedWindow;
        _saveConfig = saveConfig;
        Refresh();
    }

    public void Refresh()
    {
        if (_layoutService is null)
        {
            return;
        }

        var selected = LayoutsList.SelectedItem;
        LayoutsList.ItemsSource = null;
        LayoutsList.ItemsSource = _layoutService.Layouts;
        LayoutsList.SelectedItem = selected;
    }

    private void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        if (_layoutService is null)
        {
            return;
        }

        var name = TextInputDialog.Prompt(Window.GetWindow(this)!, "保存布局", "布局名称", $"Layout {DateTime.Now:yyyy-MM-dd HH-mm}");
        if (name is null)
        {
            return;
        }

        var layout = _layoutService.SaveAllVisible(name);
        LayoutLogText.Text = $"已保存布局：{layout.Name}，窗口数 {layout.Items.Count}";
        _saveConfig?.Invoke();
        Refresh();
    }

    private void SaveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_layoutService is null || _getSelectedWindow is null)
        {
            return;
        }

        var name = TextInputDialog.Prompt(Window.GetWindow(this)!, "保存选中窗口", "布局名称", $"Selected {DateTime.Now:HH-mm}");
        if (name is null)
        {
            return;
        }

        var layout = _layoutService.SaveSelected(_getSelectedWindow(), name);
        LayoutLogText.Text = layout is null ? "没有选中窗口。" : $"已保存：{layout.Name}";
        _saveConfig?.Invoke();
        Refresh();
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_layoutService is null || LayoutsList.SelectedItem is not WindowLayout layout)
        {
            return;
        }

        var report = _layoutService.Restore(layout);
        LayoutLogText.Text = report.ToOperationResult().Message;
        _saveConfig?.Invoke();
        Refresh();
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (_layoutService is null || LayoutsList.SelectedItem is not WindowLayout layout)
        {
            return;
        }

        var name = TextInputDialog.Prompt(Window.GetWindow(this)!, "重命名布局", "新名称", layout.Name);
        if (name is null)
        {
            return;
        }

        _layoutService.Rename(layout, name);
        _saveConfig?.Invoke();
        Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_layoutService is null || LayoutsList.SelectedItem is not WindowLayout layout)
        {
            return;
        }

        _layoutService.Delete(layout);
        LayoutItemsGrid.ItemsSource = null;
        _saveConfig?.Invoke();
        Refresh();
    }

    private void LayoutsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LayoutItemsGrid.ItemsSource = (LayoutsList.SelectedItem as WindowLayout)?.Items;
    }
}
