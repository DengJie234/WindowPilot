using System.IO;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WindowPilot.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly Drawing.Icon? _icon;

    public event EventHandler? OpenRequested;
    public event EventHandler? SafeRecoveryRequested;
    public event EventHandler? ClearTopMostRequested;
    public event EventHandler? RestoreOpacityRequested;
    public event EventHandler? ClearClickThroughRequested;
    public event EventHandler? RestoreMiniWindowsRequested;
    public event EventHandler? EmergencyRestoreRequested;
    public event EventHandler? PauseRulesRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        _icon = LoadAppIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon ?? Drawing.SystemIcons.Application,
            Text = "WindowPilot",
            Visible = true,
            ContextMenuStrip = _menu
        };

        AddItem("打开 WindowPilot", (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        AddDisabledItem("当前状态：运行中");
        AddSeparator();
        AddItem("安全恢复模式", (_, _) => SafeRecoveryRequested?.Invoke(this, EventArgs.Empty));
        AddItem("取消全部置顶", (_, _) => ClearTopMostRequested?.Invoke(this, EventArgs.Empty));
        AddItem("恢复全部透明度", (_, _) => RestoreOpacityRequested?.Invoke(this, EventArgs.Empty));
        AddItem("取消全部点击穿透", (_, _) => ClearClickThroughRequested?.Invoke(this, EventArgs.Empty));
        AddItem("恢复所有小窗", (_, _) => RestoreMiniWindowsRequested?.Invoke(this, EventArgs.Empty));
        AddItem("紧急恢复", (_, _) => EmergencyRestoreRequested?.Invoke(this, EventArgs.Empty));
        AddSeparator();
        AddItem("暂停自动规则 30 秒", (_, _) => PauseRulesRequested?.Invoke(this, EventArgs.Empty));
        AddItem("设置", (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        AddSeparator();
        AddItem("退出", (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.MouseUp += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                _menu.Show(Forms.Cursor.Position);
            }
        };
    }

    public void ShowBalloon(string title, string text, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
        _menu.Dispose();
    }

    private static Drawing.Icon? LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            return new Drawing.Icon(iconPath);
        }

        var sourcePath = Path.Combine(AppContext.BaseDirectory, "AppIcon.ico");
        if (File.Exists(sourcePath))
        {
            return new Drawing.Icon(sourcePath);
        }

        return null;
    }

    private void AddItem(string text, EventHandler handler)
    {
        _menu.Items.Add(new Forms.ToolStripMenuItem(text, null, handler));
    }

    private void AddDisabledItem(string text)
    {
        _menu.Items.Add(new Forms.ToolStripMenuItem(text) { Enabled = false });
    }

    private void AddSeparator()
    {
        _menu.Items.Add(new Forms.ToolStripSeparator());
    }
}
