# WindowPilot GitHub 发布文案

## Repository Description

WindowPilot is a lightweight Windows window manager built with C# WPF and .NET 8. It provides topmost control, opacity adjustment, click-through, layout restore, rules, mini-window mode, tray operation, custom global hotkeys, and overlay highlights.

## Topics

```text
wpf
dotnet
windows
window-manager
win32
csharp
desktop-app
productivity
hotkeys
tray-app
```

## README 简介

# WindowPilot

WindowPilot 是一款轻量、现代、专业的 Windows 窗口管理工具。它使用 C# + WPF + .NET 8 开发，通过 Win32 API 控制窗口，适合长期托盘运行，也适合打包为独立 exe。

## 主要功能

- 当前活动窗口捕获
- 可见窗口列表
- 窗口置顶 / 取消置顶
- 透明度调整，最低限制 20%
- 点击穿透 / 取消点击穿透
- 紧急恢复全部窗口
- 左半屏、右半屏、居中
- 布局保存与恢复
- 自动窗口规则
- 小窗模式
- 窗口分组
- 黑名单 / 白名单保护
- Overlay 边框高亮
- 自定义全局快捷键
- 托盘运行和托盘菜单
- 现代 WPF UI、Toast 轻提示、自定义确认对话框
- JSON 配置文件与备份恢复

## 技术栈

- C#
- WPF
- .NET 8
- Win32 API
- System.Windows.Forms.NotifyIcon
- JSON 配置

## 运行

```powershell
dotnet run
```

## 发布

框架依赖发布：

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

单文件自包含发布：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true
```

## 配置路径

```text
%AppData%\WindowPilot\config.json
```

配置写入前会自动备份：

```text
%AppData%\WindowPilot\config.json.bak
```

## 注意事项

- 普通权限程序通常无法控制管理员权限窗口。
- 全屏游戏、反作弊游戏可能拒绝或覆盖窗口样式。
- 点击穿透后可通过紧急恢复或安全恢复模式取消。
- 若全局快捷键注册失败，通常是被其他软件占用，可在快捷键设置页修改。

## Release Notes v0.2

- 重构主界面为左侧导航 + 顶部状态栏 + 页面内容区。
- 新增现代卡片 UI、统一按钮、状态标签和美化 DataGrid。
- 新增自定义全局快捷键系统。
- 新增托盘 AppIcon.ico 支持。
- 修复关闭到托盘后再次打开窗口的 WPF Close/Show 异常。
- 新增安全恢复模式。
- 新增自定义退出确认对话框，替代默认系统 MessageBox。
- 新增 Toast 轻提示系统。
- 优化窗口列表刷新，改为 hwnd Diff 更新。
- 开启 DataGrid 虚拟化。
- 优化自动规则冷却和日志数量。
- 优化 Overlay 同步，窗口位置未变化时不重复刷新。
- 配置文件写入增加 debounce 和备份恢复。

## Short Pitch

WindowPilot makes window management on Windows faster and safer: pin windows, adjust opacity, enable click-through, save layouts, automate rules, and recover everything from the tray when something goes wrong.
