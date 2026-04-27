# WindowPilot UI Refactor Phase 1

本阶段只重构 UI 架构和交互层，不重写 Win32 窗口控制逻辑，不改托盘、全局快捷键、配置、单实例和窗口恢复的核心服务。

## 新 UI 架构

```text
MainWindow
  Sidebar
    Logo
    Navigation
    Version / Tray status
  MainArea
    TopBar
      Page title
      Page description
      Time
      Status
      Refresh
      Settings
    ContentControl
      DashboardPage
      WindowsPage
      LayoutsPage
      RulesPage
      MiniWindowPage
      GroupsPage
      HotkeysPage
      SettingsPage
      AboutPage
```

## 文件结构

```text
Styles/
  Colors.xaml       全局颜色和 Brush。
  Typography.xaml   标题、正文、说明文字样式。
  Buttons.xaml      Primary / Secondary / Danger / Ghost / Icon / Small buttons。
  Cards.xaml        Card 和 SubtleCard。
  DataGrid.xaml     DataGrid、Header、Row、Cell 样式。
  Tags.xaml         状态 Tag 基础样式。
  TextBoxes.xaml    TextBox、CheckBox 样式。
  Navigation.xaml   左侧导航 RadioButton 样式。

Converters/
  BoolToStatusTextConverter.cs
  BoolToVisibilityConverter.cs
  OpacityToPercentConverter.cs
  WindowStateToTagConverter.cs
  StringEqualsConverter.cs

ViewModels/
  ViewModelBase.cs
  RelayCommand.cs
  MainViewModel.cs
  DashboardViewModel.cs
  WindowsViewModel.cs
  LayoutsViewModel.cs
  RulesViewModel.cs
  SettingsViewModel.cs
  WindowRowViewModel.cs
  WindowStatusTag.cs
  OperationLogEntry.cs

Views/
  DashboardPage.xaml
  WindowsPage.xaml
  LayoutsPage.xaml
  RulesPage.xaml
  MiniWindowPage.xaml
  GroupsPage.xaml
  HotkeysPage.xaml
  SettingsPage.xaml
  AboutPage.xaml
```

## App.xaml 资源合并

`App.xaml` 现在合并所有样式资源，并注册 ViewModel 到 View 的 DataTemplate。主窗口只要把 `CurrentPage` 设为对应 ViewModel，WPF 会自动加载页面。

## 原功能迁移方式

- 原 `MainWindow.xaml.cs` 中的窗口操作按钮事件，迁移到：
  - `DashboardViewModel`：当前窗口捕获、置顶、透明、穿透、分屏、紧急恢复、小窗。
  - `WindowsViewModel`：窗口列表刷新、搜索筛选、行内操作。
- 原托盘、快捷键、退出恢复询问仍在 `MainWindow.xaml.cs`。
- 原 `WindowService`、`HotKeyService`、`TrayIconService`、`ConfigService` 不需要重写。

## 不要动的代码

- `Native/NativeMethods.cs`
- `Services/WindowService.cs`
- `Services/HotKeyService.cs`
- `Services/TrayIconService.cs`
- `Services/ConfigService.cs`
- `Services/SingleInstanceService.cs`

这些仍然是稳定性核心。后续 UI 继续迭代时，只通过 ViewModel 调用它们。

## 已保留功能

- 刷新当前活动窗口
- 3 秒后捕获窗口
- 置顶 / 取消置顶
- 透明度 Slider 和 80% 快捷透明
- 恢复不透明
- 点击穿透 / 取消点击穿透，带确认
- 紧急恢复全部，带确认
- 左半屏 / 右半屏 / 居中
- 窗口列表刷新、搜索、筛选
- 窗口列表行内操作
- 托盘菜单
- 全局快捷键
- JSON 配置
- 单实例运行
- 退出恢复询问

## 常见报错

- `UserControl is ambiguous`：项目启用了 WinForms 托盘，代码里要用 `System.Windows.Controls.UserControl` 或避免 WinForms 命名污染。
- `Binding is ambiguous`：Converter 中返回 `System.Windows.Data.Binding.DoNothing`。
- `StaticResource not found`：确认 `App.xaml` 中合并字典顺序，颜色资源要早于按钮、卡片和表格样式。
- `Cannot set Style twice`：同一个 XAML 元素不能同时写 `Style="..."` 和 `<Element.Style>`。
- 快捷键注册失败：通常是旧实例仍在托盘或其他软件占用快捷键，不是 UI 重构导致。
