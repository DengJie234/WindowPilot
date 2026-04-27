# WindowPilot

WindowPilot 是一款轻量、现代、专业的 Windows 窗口管理工具。它使用 **C# + WPF + .NET 8** 开发，通过 Win32 API 控制桌面窗口，支持窗口置顶、透明度调整、点击穿透、布局保存与恢复、自动规则、小窗模式、窗口分组、托盘运行、自定义全局快捷键和 Overlay 高亮。

它的目标不是做一个臃肿的桌面套件，而是做一个长期放在托盘里、需要时能快速接管窗口状态的效率工具。

## 功能特性

- 当前活动窗口捕获
- 当前窗口信息展示：标题、进程、PID、句柄、位置、大小、显示器
- 窗口置顶 / 取消置顶
- 窗口透明度调整，最低限制 20%
- 点击穿透 / 取消点击穿透
- 紧急恢复全部窗口
- 窗口左半屏、右半屏、居中
- 当前可见窗口列表
- 对窗口列表中的窗口执行快捷操作
- 布局保存与恢复
- 自动窗口规则
- 小窗模式
- 窗口分组
- 黑名单 / 白名单保护
- 自定义全局快捷键
- 系统托盘运行
- 自定义 WPF 退出确认对话框
- Toast 轻提示
- Overlay 边框高亮
- JSON 配置保存与备份恢复
- 单实例运行
- 多显示器工作区适配
- DPI 缩放适配
- 系统关键窗口过滤

## 技术栈

- C#
- WPF
- .NET 8
- Win32 API
- System.Windows.Forms.NotifyIcon
- JSON 配置
- MVVM 分层结构

## 界面结构

WindowPilot 使用左侧导航栏 + 顶部状态栏 + 右侧页面内容区的桌面工具布局。

主要页面包括：

- 总览
- 窗口列表
- 布局管理
- 自动规则
- 小窗模式
- 分组管理
- 快捷键设置
- 设置
- 关于

## 快捷键

默认快捷键使用 `Ctrl + Alt + Shift + 主键`，降低和其他软件冲突的概率。

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

如果快捷键被其他软件占用，WindowPilot 不会崩溃。你可以在“快捷键设置”页面查看失败原因、修改组合键、禁用某个快捷键或恢复默认。

## 安全设计

WindowPilot 对一些高风险操作做了保护：

- 点击穿透有紧急恢复能力。
- 透明度最低不能低于 20%。
- 默认过滤系统关键窗口。
- 黑名单窗口不会被置顶、透明、穿透、移动或规则控制。
- 普通权限程序不会强制控制管理员权限窗口。
- 全屏应用场景下自动规则会尽量暂停。
- Overlay 不抢焦点，不拦截鼠标点击。
- 空自动规则不会匹配所有窗口。
- 删除规则会立即保存配置并刷新 UI。

## 项目结构

```text
WindowPilot/
  Assets/                  应用图标等资源文件
  Converters/              WPF Binding 转换器
  Models/                  配置、窗口、规则、布局、小窗、分组等模型
  Native/                  Win32 API P/Invoke
  Services/                窗口控制、托盘、快捷键、布局、规则、Overlay 等服务
  Styles/                  颜色、按钮、卡片、表格、标签、导航等样式
  ViewModels/              页面 ViewModel 和命令
  Views/                   WPF 页面、对话框和 Overlay 窗口
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

如果主界面处于前台，直接捕获当前活动窗口可能会捕获到 WindowPilot 自身。建议使用“3 秒后捕获”，然后切换到目标窗口。

## 构建

```powershell
dotnet build -c Release
```

如果你正在运行 `WindowPilot.exe`，构建可能因为 exe 被占用而失败。先从托盘退出旧实例，再构建。

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

如果要给别人下载，建议把 `publish` 目录压缩成 zip，然后上传到 GitHub Releases，不建议把 `bin` 目录提交到 Git 仓库。

## 配置文件

WindowPilot 的配置文件位于：

```text
%AppData%\WindowPilot\config.json
```

配置写入前会自动备份：

```text
%AppData%\WindowPilot\config.json.bak
```

配置文件保存内容包括：

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

使用紧急恢复快捷键，或托盘菜单中的“紧急恢复 / 安全恢复模式”。

### 普通窗口出现 Overlay 边框

正常情况下不会。Overlay 只会显示在 WindowPilot 管理过的窗口、小窗模式窗口，或用户主动开启选中窗口高亮时。

### 全屏游戏不生效

部分全屏游戏、反作弊游戏会拒绝或覆盖 Win32 窗口样式，WindowPilot 不会强制控制这类窗口。

## 上传 GitHub 时应该提交哪些文件

应该提交源码、资源和项目文件。

建议提交：

```text
Assets/
Converters/
Models/
Native/
Services/
Styles/
ViewModels/
Views/
app.manifest
App.xaml
App.xaml.cs
AssemblyInfo.cs
MainWindow.xaml
MainWindow.xaml.cs
WindowPilot.csproj
README.md
.gitignore
GITHUB_PROJECT_DESCRIPTION.md
```

不要提交：

```text
bin/
obj/
.vs/
*.user
*.suo
*.log
publish/
```

`bin` 和 `obj` 是编译生成物，不属于源码。它们会让仓库变大，也可能包含本机路径和临时文件。

## GitHub 上传建议

1. 在 GitHub 新建仓库，例如 `WindowPilot`。
2. 把当前 `WindowPilot` 项目目录作为仓库根目录。
3. 确认 `.gitignore` 已存在。
4. 提交源码文件，不提交 `bin` 和 `obj`。
5. 如果要提供 exe 下载，用 GitHub Releases 上传发布包 zip。

命令示例：

```powershell
git init
git add .
git commit -m "Initial WindowPilot release"
git branch -M main
git remote add origin https://github.com/你的用户名/WindowPilot.git
git push -u origin main
```

## License

如果你准备开源，建议添加一个 `LICENSE` 文件。常见选择：

- MIT：最宽松，适合工具类项目。
- Apache-2.0：带专利授权条款。
- GPL：要求衍生项目也开源。

如果不确定，推荐 MIT。

