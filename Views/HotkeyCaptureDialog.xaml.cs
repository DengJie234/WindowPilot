using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WindowPilot.Models;

namespace WindowPilot.Views;

public partial class HotkeyCaptureDialog : Window, INotifyPropertyChanged
{
    private readonly HotkeyItem _captured;
    private string _previewText;
    private string _message = string.Empty;

    public HotkeyCaptureDialog(HotkeyItem item)
    {
        InitializeComponent();
        _captured = item.Clone();
        _previewText = _captured.DisplayText;
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public HotkeyItem? Result { get; private set; }

    public string PreviewText
    {
        get => _previewText;
        private set
        {
            if (_previewText != value)
            {
                _previewText = value;
                OnPropertyChanged();
            }
        }
    }

    public string Message
    {
        get => _message;
        private set
        {
            if (_message != value)
            {
                _message = value;
                OnPropertyChanged();
            }
        }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        key = key == Key.ImeProcessed ? e.ImeProcessedKey : key;

        if (IsModifierKey(key))
        {
            Message = "请再按一个主键，例如 T、F5、方向键或 Space。";
            return;
        }

        var keyText = ConvertKeyToText(key);
        if (string.IsNullOrWhiteSpace(keyText))
        {
            Message = "这个按键暂不支持作为全局快捷键主键。";
            return;
        }

        var modifiers = Keyboard.Modifiers;
        _captured.Enabled = true;
        _captured.Ctrl = modifiers.HasFlag(ModifierKeys.Control);
        _captured.Alt = modifiers.HasFlag(ModifierKeys.Alt);
        _captured.Shift = modifiers.HasFlag(ModifierKeys.Shift);
        _captured.Win = modifiers.HasFlag(ModifierKeys.Windows);
        _captured.Key = keyText;
        PreviewText = _captured.DisplayText;
        Message = string.Empty;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _captured.Enabled = false;
        _captured.Ctrl = false;
        _captured.Alt = false;
        _captured.Shift = false;
        _captured.Win = false;
        _captured.Key = string.Empty;
        PreviewText = "已禁用";
        Message = string.Empty;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = _captured;
        DialogResult = true;
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private static string ConvertKeyToText(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return key.ToString();
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString();
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return ((int)(key - Key.NumPad0)).ToString();
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            return key.ToString();
        }

        return key switch
        {
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Space => "Space",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Tab => "Tab",
            Key.Escape => "Escape",
            _ => string.Empty
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
