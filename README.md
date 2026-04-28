# WindowPilot

WindowPilot 是一款轻量、现代、专业的 Windows 窗口管理工具。它使用 **C# + WPF + .NET 8** 开发，通过 Win32 API 控制桌面窗口，支持窗口置顶、透明度调整、点击穿透、布局管理、自动规则、小窗模式、窗口分组、自定义全局快捷键、托盘运行和 Overlay 高亮。

它的目标不是替代完整桌面环境，而是成为一个长期放在托盘里的效率工具：需要时快速接管窗口状态，异常时可以一键安全恢复。

## 功能特性

- 当前活动窗口捕获
- 可见窗口列表，支持搜索、筛选和批量恢复
- 窗口列表显示应用图标，例如 Chrome、QQ、Steam、资源管理器等
- 窗口置顶 / 取消置顶
- 窗口透明度调整，最低限制 20%
- 点击穿透 / 取消点击穿透
- 紧急恢复全部窗口状态
- 安全恢复模式
- 窗口左半屏、右半屏、居中
- 布局保存与恢复
- 自动窗口规则
- 小窗模式
- 窗口分组
- 黑名单 / 白名单保护
- 自定义全局快捷键
- 现代 WPF 托盘 Flyout，替代默认系统菜单
- 左键托盘图标打开主窗口，右键打开托盘面板
- 托盘 Flyout 支持外部点击关闭、Esc 关闭、菜单项执行后自动关闭
- 自定义退出确认对话框
- Toast 轻提示
- Overlay 边框高亮
- JSON 配置文件保存
- 单实例运行
- 多显示器工作区适配
- Per-Monitor DPI 适配
- 系统关键窗口过滤

## 截图

可以在仓库上传截图后替换下面路径：

```markdown
![WindowPilot Dashboard](docs/images/dashboard.png)
![WindowPilot Windows List](docs/images/windows-list.png)
```

## 技术栈

- C#
- WPF
- .NET 8
- Win32 API / PInvoke
- System.Windows.Forms.NotifyIcon
- JSON 配置
- MVVM 分层

## 主要页面

- 总览
- 窗口列表
- 布局管理
- 自动规则
- 小窗模式
- 分组管理
- 快捷键设置
- 设置
- 关于

## 默认快捷键

默认快捷键使用 `Ctrl + Alt + Shift + 主键`，降低和其他软件冲突的概率。所有快捷键都可以在软件内修改、禁用或恢复默认。

| 功能 | 默认快捷键 |
|---|---|
| 置顶 / 取消置顶 | `Ctrl + Alt + Shift + T` |
| 增加不透明度 | `Ctrl + Alt + Shift + Up` |
| 降低不透明度 | `Ctrl + Alt + Shift + Down` |
| 恢复不透明 | `Ctrl + Alt + Shift + 0` |
| 点击穿透 / 取消 | `Ctrl + Alt + Shift + X` |
| 紧急恢复全部 | `Ctrl + Alt + Shift + R` |
| 左半屏 | `Ctrl + Alt + Shift + Left` |
| 右半屏 | `Ctrl + Alt + Shift + Right` |
| 居中窗口 | `Ctrl + Alt + Shift + C` |
| 小窗模式 | `Ctrl + Alt + Shift + M` |
| 显示 / 隐藏主界面 | `Ctrl + Alt + Shift + Space` |
| 刷新当前活动窗口 | `Ctrl + Alt + Shift + F5` |

如果快捷键被其他软件占用，WindowPilot 不会崩溃。你可以在“快捷键设置”页面查看失败原因，修改组合键、禁用某个快捷键，或恢复默认快捷键。

## 安全设计

WindowPilot 对高风险窗口操作做了防护：

- 点击穿透始终可以通过紧急恢复取消
- 透明度最低不低于 20%
- 默认过滤系统关键窗口
- 黑名单窗口不会被置顶、透明、穿透、移动或规则控制
- 普通权限程序不会强制控制管理员权限窗口
- 自动规则不会执行空匹配规则，避免误控所有窗口
- 全屏应用场景下尽量暂停自动规则和 Overlay
- Overlay 不抢焦点，不拦截鼠标点击
- 删除规则会立即保存配置并刷新 UI
- 托盘退出前可选择是否恢复所有被修改过的窗口状态

## 项目结构

```text
WindowPilot/
  Assets/                  应用图标和默认图标资源
  Converters/              WPF Binding 转换器
  Models/                  配置、窗口、规则、布局、小窗、分组等模型
  Native/                  Win32 API P/Invoke
  Services/                窗口控制、托盘、快捷键、布局、规则、Overlay 等服务
  Styles/                  颜色、按钮、卡片、表格、标签、导航等样式
  ViewModels/              页面 ViewModel 和命令
  Views/                   WPF 页面、对话框、Overlay、Tray Flyout
  app.manifest             应用清单，启用 PerMonitorV2 DPI
  App.xaml                 应用资源入口
  App.xaml.cs              单实例保护和主窗口启动
  MainWindow.xaml          主窗口布局
  MainWindow.xaml.cs       主窗口生命周期、托盘事件、服务连接
  WindowPilot.csproj       .NET 8 WPF 项目文件
```

## 运行项目

需要安装：

- Windows 10 / Windows 11
- .NET 8 SDK

运行：

```powershell
cd WindowPilot
dotnet run
```

如果主界面处于前台，捕获当前活动窗口可能会捕获到 WindowPilot 自己。建议使用“3 秒后捕获”，然后切换到目标窗口。

## 构建

```powershell
dotnet build -c Release
```

如果你正在运行 `WindowPilot.exe`，构建可能会因为 exe 被占用而失败。先从托盘退出旧实例，再重新构建。

## 发布 exe

框架依赖发布，体积较小，目标机器需要安装 .NET 8 Desktop Runtime：

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

自包含单文件发布，体积较大，目标机器通常不需要额外安装运行时：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true
```

发布输出目录通常在：

```text
bin\Release\net8.0-windows\win-x64\publish\
```

如果要给别人下载，建议把 `publish` 目录压缩成 zip，然后上传到 GitHub Releases。不要把 `bin`、`obj`、`publish` 目录提交到 Git 仓库。

## 配置文件

WindowPilot 配置文件位于：

```text
%AppData%\WindowPilot\config.json
```

配置写入前会自动备份：

```text
%AppData%\WindowPilot\config.json.bak
```

配置内容包括：

- 快捷键配置
- 关闭到托盘设置
- Overlay 设置
- 黑名单 / 白名单
- 布局数据
- 自动规则
- 小窗状态
- 窗口分组

## 常见问题

### 快捷键注册失败

通常是快捷键被其他软件占用。进入“快捷键设置”页面，修改失败项或禁用该快捷键即可。

### 无法控制管理员权限窗口

普通权限程序通常不能稳定控制管理员权限窗口。可以用管理员身份运行 WindowPilot，但不建议长期这样做。

### 点击穿透后点不到窗口

使用紧急恢复快捷键，或托盘 Flyout 中的“紧急恢复 / 安全恢复模式”取消点击穿透。

### 普通窗口出现 Overlay 边框

正常情况下不会。Overlay 只显示在 WindowPilot 管理过的窗口、小窗模式窗口，或用户主动启用选中窗口高亮时。

### 全屏游戏不生效

部分全屏游戏、反作弊游戏会拒绝或覆盖 Win32 窗口样式。WindowPilot 不会强制控制这类窗口。

### 托盘 Flyout 不关闭

新版托盘 Flyout 已加入全局外部点击监听。点击桌面、其他应用或任务栏其他区域都会关闭面板。

## Release Notes

### v0.2

- 重构主界面为左侧导航 + 顶部状态栏 + 页面内容区
- 新增现代卡片 UI、统一按钮、状态标签和美化 DataGrid
- 新增窗口列表应用图标显示
- 新增自定义全局快捷键系统
- 新增现代 WPF 托盘 Flyout
- 修复托盘 Flyout 定位、外部点击关闭和左右键交互
- 修复关闭到托盘后再次打开窗口的 WPF Close/Show 异常
- 新增托盘 AppIcon.ico 支持
- 新增安全恢复模式
- 新增自定义退出确认对话框，替代系统 MessageBox
- 新增 Toast 轻提示系统
- 优化窗口列表刷新，使用 hwnd Diff 更新
- 开启 DataGrid 虚拟化
- 优化自动规则冷却和日志数量
- 修复空规则误匹配所有窗口的问题
- 修复普通窗口误显示 Overlay 边框的问题
- 优化 Overlay 显示条件和样式
- 配置文件写入增加 debounce 和备份恢复

