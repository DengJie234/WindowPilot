using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using WindowPilot.Models;
using WindowPilot.Native;

namespace WindowPilot.Services;

public enum HotKeyAction
{
    ToggleTopMost = 1,
    IncreaseOpacity = 2,
    DecreaseOpacity = 3,
    ResetOpacity = 4,
    ToggleClickThrough = 5,
    EmergencyRestore = 6,
    MoveLeftHalf = 7,
    MoveRightHalf = 8,
    CenterWindow = 9,
    ToggleMiniWindow = 10,
    ShowMainWindow = 11,
    RefreshActiveWindow = 12
}

public sealed class HotKeyService : IDisposable
{
    private readonly nint _windowHandle;
    private readonly HwndSource _source;
    private readonly AppConfig _config;
    private readonly Dictionary<int, HotKeyAction> _actionByHotkeyId = [];
    private readonly HashSet<int> _registeredIds = [];

    public event EventHandler<HotKeyAction>? HotKeyPressed;

    public HotKeyService(nint windowHandle, AppConfig config)
    {
        _windowHandle = windowHandle;
        _config = config;
        _source = HwndSource.FromHwnd(windowHandle) ?? throw new InvalidOperationException("无法获取窗口消息源。");
        _source.AddHook(WndProc);
        LoadHotkeys();
    }

    public IReadOnlyList<HotkeyItem> Hotkeys => _config.HotKeys;

    public IReadOnlyList<HotkeyItem> LoadHotkeys()
    {
        if (_config.HotKeys.Count == 0 || _config.HotKeys.Any(item => string.IsNullOrWhiteSpace(item.Id)))
        {
            _config.HotKeys = HotkeyItem.CreateDefaults();
        }

        foreach (var defaultHotkey in HotkeyItem.CreateDefaults())
        {
            var existing = _config.HotKeys.FirstOrDefault(item => SameId(item.Id, defaultHotkey.Id));
            if (existing is null)
            {
                _config.HotKeys.Add(defaultHotkey);
            }
            else
            {
                existing.CopyMetadataFrom(defaultHotkey);
            }
        }

        foreach (var item in _config.HotKeys)
        {
            item.HotkeyId = ResolveHotkeyId(item.Id);
            MarkPending(item);
        }

        return _config.HotKeys;
    }

    public void SaveHotkeys()
    {
        // The owning MainWindow persists the AppConfig. This method is kept so
        // callers have one stable API surface for hotkey management.
    }

    public IReadOnlyList<HotkeyItem> RegisterAllHotkeys()
    {
        UnregisterAllHotkeys();
        _actionByHotkeyId.Clear();

        foreach (var item in _config.HotKeys)
        {
            item.HotkeyId = ResolveHotkeyId(item.Id);
            MarkPending(item);
        }

        foreach (var item in _config.HotKeys.Where(hotkey => hotkey.Enabled))
        {
            var validation = ValidateHotkey(item);
            if (!validation.Success)
            {
                MarkFailed(item, validation.Message);
                continue;
            }

            var conflict = CheckInternalConflict(item);
            if (!conflict.Success)
            {
                MarkConflict(item, conflict.Message);
                continue;
            }

            RegisterHotkeyCore(item);
        }

        return _config.HotKeys.Where(item => item.Enabled && !item.IsRegistered).ToList();
    }

    public OperationResult RegisterHotkey(HotkeyItem item)
    {
        UnregisterHotkey(item);

        if (!item.Enabled)
        {
            MarkDisabled(item);
            return OperationResult.Ok("已禁用快捷键。");
        }

        var validation = ValidateHotkey(item);
        if (!validation.Success)
        {
            MarkFailed(item, validation.Message);
            return validation;
        }

        var conflict = CheckInternalConflict(item);
        if (!conflict.Success)
        {
            MarkConflict(item, conflict.Message);
            return conflict;
        }

        return RegisterHotkeyCore(item);
    }

    public void UnregisterHotkey(HotkeyItem item)
    {
        if (item.HotkeyId == 0)
        {
            item.HotkeyId = ResolveHotkeyId(item.Id);
        }

        if (_registeredIds.Remove(item.HotkeyId))
        {
            NativeMethods.UnregisterHotKey(_windowHandle, item.HotkeyId);
        }

        _actionByHotkeyId.Remove(item.HotkeyId);
        item.IsRegistered = false;
    }

    public void UnregisterAllHotkeys()
    {
        foreach (var id in _registeredIds.ToList())
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
        }

        _registeredIds.Clear();
        _actionByHotkeyId.Clear();

        foreach (var item in _config.HotKeys)
        {
            item.IsRegistered = false;
        }
    }

    public OperationResult UpdateHotkey(string id, HotkeyItem newHotkey)
    {
        var existing = _config.HotKeys.FirstOrDefault(item => SameId(item.Id, id));
        if (existing is null)
        {
            return OperationResult.Fail("未找到要修改的快捷键。");
        }

        var old = existing.Clone();
        existing.CopyShortcutFrom(newHotkey);
        RegisterAllHotkeys();

        if (!existing.Enabled || existing.IsRegistered)
        {
            return OperationResult.Ok("快捷键已更新。");
        }

        var failure = string.IsNullOrWhiteSpace(existing.ErrorMessage)
            ? "新快捷键注册失败，已恢复旧快捷键。"
            : $"新快捷键注册失败：{existing.ErrorMessage}。已恢复旧快捷键。";

        existing.CopyShortcutFrom(old);
        RegisterAllHotkeys();
        return OperationResult.Fail(failure);
    }

    public OperationResult ResetToDefault(string id)
    {
        var defaultHotkey = HotkeyItem.CreateDefaults().FirstOrDefault(item => SameId(item.Id, id));
        if (defaultHotkey is null)
        {
            return OperationResult.Fail("未找到默认快捷键。");
        }

        return UpdateHotkey(id, defaultHotkey);
    }

    public OperationResult ResetAllToDefault()
    {
        _config.HotKeys.Clear();
        foreach (var item in HotkeyItem.CreateDefaults())
        {
            _config.HotKeys.Add(item);
        }

        RegisterAllHotkeys();
        var failures = _config.HotKeys.Count(item => item.Enabled && !item.IsRegistered);
        return failures == 0
            ? OperationResult.Ok("已恢复全部默认快捷键。")
            : OperationResult.Fail($"已恢复默认快捷键，但有 {failures} 项注册失败。");
    }

    public OperationResult ValidateHotkey(HotkeyItem item)
    {
        if (!item.Enabled)
        {
            return OperationResult.Ok();
        }

        if (string.IsNullOrWhiteSpace(item.Key))
        {
            return OperationResult.Fail("没有主键。");
        }

        if (ConvertKeyToVirtualKey(item.Key) is null)
        {
            return OperationResult.Fail("无法识别主键。");
        }

        var modifierCount = CountModifiers(item);
        if (modifierCount == 0 && IsPlainCharacterKey(item.Key))
        {
            return OperationResult.Fail("普通字母或数字必须搭配 Ctrl、Alt、Shift 或 Win。");
        }

        if (modifierCount == 0)
        {
            return OperationResult.Fail("快捷键至少需要一个修饰键。");
        }

        if (item.Alt && !item.Ctrl && !item.Shift && !item.Win && string.Equals(item.Key, "F4", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Fail("Alt + F4 是系统关闭窗口快捷键，不能使用。");
        }

        if (item.Ctrl && !item.Alt && !item.Shift && !item.Win && IsCommonEditingKey(item.Key))
        {
            return OperationResult.Fail("Ctrl + C/V/X/Z/S 等常用编辑快捷键不能作为全局快捷键。");
        }

        if (string.Equals(item.Key, "Escape", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Fail("Escape 容易影响系统和应用操作，不能作为全局快捷键。");
        }

        return OperationResult.Ok();
    }

    public OperationResult CheckInternalConflict(HotkeyItem item)
    {
        if (!item.Enabled)
        {
            return OperationResult.Ok();
        }

        var chord = BuildChordKey(item);
        var other = _config.HotKeys.FirstOrDefault(candidate =>
            !ReferenceEquals(candidate, item) &&
            candidate.Enabled &&
            string.Equals(BuildChordKey(candidate), chord, StringComparison.OrdinalIgnoreCase));

        return other is null
            ? OperationResult.Ok()
            : OperationResult.Fail($"与“{other.Name}”快捷键冲突。");
    }

    public uint ConvertToModifiers(HotkeyItem item)
    {
        uint modifiers = NativeMethods.MOD_NOREPEAT;
        if (item.Ctrl)
        {
            modifiers |= NativeMethods.MOD_CONTROL;
        }

        if (item.Alt)
        {
            modifiers |= NativeMethods.MOD_ALT;
        }

        if (item.Shift)
        {
            modifiers |= NativeMethods.MOD_SHIFT;
        }

        if (item.Win)
        {
            modifiers |= NativeMethods.MOD_WIN;
        }

        return modifiers;
    }

    public int? ConvertKeyToVirtualKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        key = key.Trim();
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                return c;
            }
        }

        if (key.StartsWith('F') && int.TryParse(key[1..], out var functionNumber) && functionNumber is >= 1 and <= 24)
        {
            return 0x70 + functionNumber - 1;
        }

        return key switch
        {
            "Left" => NativeMethods.VK_LEFT,
            "Right" => NativeMethods.VK_RIGHT,
            "Up" => NativeMethods.VK_UP,
            "Down" => NativeMethods.VK_DOWN,
            "Space" => NativeMethods.VK_SPACE,
            "Home" => 0x24,
            "End" => 0x23,
            "PageUp" or "Prior" => 0x21,
            "PageDown" or "Next" => 0x22,
            "Insert" => 0x2D,
            "Delete" => 0x2E,
            "Tab" => 0x09,
            _ => null
        };
    }

    public void Dispose()
    {
        UnregisterAllHotkeys();
        _source.RemoveHook(WndProc);
    }

    private OperationResult RegisterHotkeyCore(HotkeyItem item)
    {
        if (!Enum.TryParse<HotKeyAction>(item.Id, out var action))
        {
            MarkFailed(item, "无法识别快捷键功能。");
            return OperationResult.Fail(item.ErrorMessage);
        }

        var virtualKey = ConvertKeyToVirtualKey(item.Key);
        if (virtualKey is null)
        {
            MarkFailed(item, "无法识别主键。");
            return OperationResult.Fail(item.ErrorMessage);
        }

        item.HotkeyId = ResolveHotkeyId(item.Id);
        var ok = NativeMethods.RegisterHotKey(_windowHandle, item.HotkeyId, ConvertToModifiers(item), (uint)virtualKey.Value);
        if (ok)
        {
            _registeredIds.Add(item.HotkeyId);
            _actionByHotkeyId[item.HotkeyId] = action;
            item.IsRegistered = true;
            item.Status = "已注册";
            item.ErrorMessage = string.Empty;
            return OperationResult.Ok("快捷键已注册。");
        }

        var error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
        MarkFailed(item, $"注册失败，可能被其他软件占用。{error}");
        return OperationResult.Fail(item.ErrorMessage);
    }

    private static void MarkPending(HotkeyItem item)
    {
        item.IsRegistered = false;
        item.ErrorMessage = string.Empty;
        item.Status = item.Enabled ? "未注册" : "已禁用";
    }

    private static void MarkDisabled(HotkeyItem item)
    {
        item.IsRegistered = false;
        item.ErrorMessage = string.Empty;
        item.Status = "已禁用";
    }

    private static void MarkFailed(HotkeyItem item, string message)
    {
        item.IsRegistered = false;
        item.ErrorMessage = message;
        item.Status = "注册失败";
    }

    private static void MarkConflict(HotkeyItem item, string message)
    {
        item.IsRegistered = false;
        item.ErrorMessage = message;
        item.Status = "冲突";
    }

    private static int ResolveHotkeyId(string id)
    {
        if (Enum.TryParse<HotKeyAction>(id, out var action))
        {
            return 9000 + (int)action;
        }

        return 9500 + Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(id) % 400);
    }

    private static bool SameId(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static int CountModifiers(HotkeyItem item) =>
        (item.Ctrl ? 1 : 0) + (item.Alt ? 1 : 0) + (item.Shift ? 1 : 0) + (item.Win ? 1 : 0);

    private static bool IsPlainCharacterKey(string key) =>
        key.Length == 1 && char.IsLetterOrDigit(key[0]);

    private static bool IsCommonEditingKey(string key) =>
        key is "C" or "V" or "X" or "Z" or "S" or "A";

    private static string BuildChordKey(HotkeyItem item) =>
        $"{item.Ctrl}|{item.Alt}|{item.Shift}|{item.Win}|{item.Key}".ToUpperInvariant();

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _actionByHotkeyId.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            HotKeyPressed?.Invoke(this, action);
        }

        return nint.Zero;
    }
}
