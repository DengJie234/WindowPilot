using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace WindowPilot.Models;

public sealed class HotkeyItem : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private bool _enabled = true;
    private bool _ctrl;
    private bool _alt;
    private bool _shift;
    private bool _win;
    private string _key = string.Empty;
    private bool _isRegistered;
    private string _status = "未注册";
    private string _errorMessage = string.Empty;
    private int _hotkeyId;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetField(ref _enabled, value))
            {
                NotifyDisplayChanged();
            }
        }
    }

    public bool Ctrl
    {
        get => _ctrl;
        set
        {
            if (SetField(ref _ctrl, value))
            {
                NotifyDisplayChanged();
            }
        }
    }

    public bool Alt
    {
        get => _alt;
        set
        {
            if (SetField(ref _alt, value))
            {
                NotifyDisplayChanged();
            }
        }
    }

    public bool Shift
    {
        get => _shift;
        set
        {
            if (SetField(ref _shift, value))
            {
                NotifyDisplayChanged();
            }
        }
    }

    public bool Win
    {
        get => _win;
        set
        {
            if (SetField(ref _win, value))
            {
                NotifyDisplayChanged();
            }
        }
    }

    public string Key
    {
        get => _key;
        set
        {
            if (SetField(ref _key, value))
            {
                NotifyDisplayChanged();
            }
        }
    }

    [JsonIgnore]
    public string DisplayText => Enabled ? FormatDisplay(Ctrl, Alt, Shift, Win, Key) : "已禁用";

    [JsonIgnore]
    public bool IsRegistered
    {
        get => _isRegistered;
        set
        {
            if (SetField(ref _isRegistered, value))
            {
                OnPropertyChanged(nameof(StatusKind));
            }
        }
    }

    [JsonIgnore]
    public string Status
    {
        get => _status;
        set
        {
            if (SetField(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusKind));
            }
        }
    }

    [JsonIgnore]
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    [JsonIgnore]
    public int HotkeyId
    {
        get => _hotkeyId;
        set => SetField(ref _hotkeyId, value);
    }

    [JsonIgnore]
    public string EnabledText => Enabled ? "启用" : "禁用";

    [JsonIgnore]
    public string ToggleEnabledText => Enabled ? "禁用" : "启用";

    [JsonIgnore]
    public string StatusKind
    {
        get
        {
            if (!Enabled)
            {
                return "Disabled";
            }

            if (Status.Contains("冲突", StringComparison.OrdinalIgnoreCase))
            {
                return "Conflict";
            }

            return IsRegistered ? "Registered" : "Failed";
        }
    }

    public static List<HotkeyItem> CreateDefaults() =>
    [
        Create("ToggleTopMost", "置顶 / 取消置顶", "切换当前活动窗口置顶状态", true, true, true, false, "T"),
        Create("IncreaseOpacity", "增加不透明度", "提高当前活动窗口不透明度", true, true, true, false, "Up"),
        Create("DecreaseOpacity", "降低不透明度", "降低当前活动窗口不透明度", true, true, true, false, "Down"),
        Create("ResetOpacity", "恢复不透明", "把当前活动窗口恢复为 100% 不透明", true, true, true, false, "0"),
        Create("ToggleClickThrough", "点击穿透 / 取消", "切换当前活动窗口点击穿透", true, true, true, false, "X"),
        Create("EmergencyRestore", "紧急恢复全部", "恢复所有被 WindowPilot 修改过的窗口", true, true, true, false, "R"),
        Create("MoveLeftHalf", "左半屏", "把当前活动窗口移动到左半屏", true, true, true, false, "Left"),
        Create("MoveRightHalf", "右半屏", "把当前活动窗口移动到右半屏", true, true, true, false, "Right"),
        Create("CenterWindow", "居中窗口", "把当前活动窗口移动到当前显示器中央", true, true, true, false, "C"),
        Create("ToggleMiniWindow", "小窗模式", "把当前活动窗口切换到小窗模式", true, true, true, false, "M"),
        Create("ShowMainWindow", "显示 / 隐藏主界面", "显示或隐藏 WindowPilot 主界面", true, true, true, false, "Space"),
        Create("RefreshActiveWindow", "刷新当前活动窗口", "重新捕获当前活动窗口", true, true, true, false, "F5")
    ];

    public HotkeyItem Clone() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Enabled = Enabled,
        Ctrl = Ctrl,
        Alt = Alt,
        Shift = Shift,
        Win = Win,
        Key = Key,
        IsRegistered = IsRegistered,
        Status = Status,
        ErrorMessage = ErrorMessage,
        HotkeyId = HotkeyId
    };

    public void CopyShortcutFrom(HotkeyItem source)
    {
        Enabled = source.Enabled;
        Ctrl = source.Ctrl;
        Alt = source.Alt;
        Shift = source.Shift;
        Win = source.Win;
        Key = source.Key;
    }

    public void CopyMetadataFrom(HotkeyItem source)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = source.Name;
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            Description = source.Description;
        }
    }

    public static string FormatDisplay(bool ctrl, bool alt, bool shift, bool win, string key)
    {
        var parts = new List<string>();
        if (ctrl)
        {
            parts.Add("Ctrl");
        }

        if (alt)
        {
            parts.Add("Alt");
        }

        if (shift)
        {
            parts.Add("Shift");
        }

        if (win)
        {
            parts.Add("Win");
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            parts.Add(NormalizeDisplayKey(key));
        }

        return parts.Count == 0 ? "未设置" : string.Join(" + ", parts);
    }

    private static HotkeyItem Create(string id, string name, string description, bool ctrl, bool alt, bool shift, bool win, string key) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        Enabled = true,
        Ctrl = ctrl,
        Alt = alt,
        Shift = shift,
        Win = win,
        Key = key
    };

    private static string NormalizeDisplayKey(string key) =>
        key switch
        {
            "Prior" => "PageUp",
            "Next" => "PageDown",
            "OemPlus" => "+",
            "OemMinus" => "-",
            _ => key
        };

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void NotifyDisplayChanged()
    {
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(EnabledText));
        OnPropertyChanged(nameof(ToggleEnabledText));
        OnPropertyChanged(nameof(StatusKind));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
