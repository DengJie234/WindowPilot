using System.Collections.ObjectModel;
using System.Windows.Input;
using WindowPilot.Models;
using WindowPilot.Services;

namespace WindowPilot.ViewModels;

public sealed class LayoutsViewModel : ViewModelBase
{
    private readonly LayoutService _layoutService;
    private readonly Action _saveConfig;
    private readonly Action<string, bool> _notify;
    private WindowLayout? _selectedLayout;

    public LayoutsViewModel(LayoutService layoutService, Action saveConfig, Action<string, bool> notify)
    {
        _layoutService = layoutService;
        _saveConfig = saveConfig;
        _notify = notify;
        SaveCurrentLayoutCommand = new RelayCommand(SaveCurrentLayout);
        RestoreLayoutCommand = new RelayCommand(RestoreSelected);
        DeleteLayoutCommand = new RelayCommand(DeleteSelected);
        Refresh();
    }

    public ObservableCollection<WindowLayout> Layouts { get; } = [];

    public WindowLayout? SelectedLayout
    {
        get => _selectedLayout;
        set => SetProperty(ref _selectedLayout, value);
    }

    public bool HasLayouts => Layouts.Count > 0;
    public ICommand SaveCurrentLayoutCommand { get; }
    public ICommand RestoreLayoutCommand { get; }
    public ICommand DeleteLayoutCommand { get; }

    public void Refresh()
    {
        Layouts.Clear();
        foreach (var layout in _layoutService.Layouts)
        {
            Layouts.Add(layout);
        }

        OnPropertyChanged(nameof(HasLayouts));
    }

    private void SaveCurrentLayout()
    {
        var layout = _layoutService.SaveAllVisible($"Layout {DateTime.Now:yyyy-MM-dd HH-mm}");
        _notify($"已保存布局：{layout.Name}", false);
        _saveConfig();
        Refresh();
    }

    private void RestoreSelected()
    {
        if (SelectedLayout is null)
        {
            return;
        }

        var result = _layoutService.Restore(SelectedLayout).ToOperationResult();
        _notify(result.Message, !result.Success);
        _saveConfig();
    }

    private void DeleteSelected()
    {
        if (SelectedLayout is null)
        {
            return;
        }

        _layoutService.Delete(SelectedLayout);
        SelectedLayout = null;
        _saveConfig();
        Refresh();
    }
}
