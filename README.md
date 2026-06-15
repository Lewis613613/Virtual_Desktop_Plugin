# Virtual Desktop Panel（虚拟桌面面板）

一个 Windows 桌面工具，让真实桌面保持整洁，所有桌面图标通过系统托盘图标一键呼出，显示在任务栏上方的浮动面板中。

## 功能特性

- **桌面图标管理** — 扫描用户桌面和公共桌面，在面板中以网格形式展示所有文件和快捷方式
- **一键呼出** — 点击系统托盘图标即可显示/隐藏面板，支持 Esc 关闭
- **拖拽排序** — 图标可在面板内自由拖拽重排，位置自动保存
- **双击启动** — 双击图标打开对应文件或程序
- **右键菜单** — 打开、打开文件位置、删除、属性等常用操作
- **实时同步** — 监听桌面文件夹变化（新增/删除/重命名），面板即时更新
- **多主题支持** — 暗色、黑色、亮色、系统 Mica 四种预设，支持自定义背景颜色和透明度
- **模糊效果** — 亚克力 / 云母背景模糊，可关闭
- **开机自启** — 可配置随系统启动
- **任务栏适配** — 自动检测任务栏位置和尺寸，面板始终紧贴任务栏显示
- **图标校验** — 检测失效的 .lnk 快捷方式并标记警告图标

## 系统要求

- Windows 10 / 11 (x64)
- .NET 8.0 Desktop Runtime（自包含发布则无需安装）

## 安装与运行

### 下载运行

从 [Releases](../../releases) 下载最新版 `VirtualDesktopPanel.exe`，双击运行即可。程序会在系统托盘显示一个 3×3 点阵图标。

### 从源码构建

```bash
git clone git@github.com:Lewis613613/Virtual_Desktop_Plugin.git
cd Virtual_Desktop_Plugin/VirtualDesktopPanel
dotnet build -c Release
```

### 发布单文件

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

输出文件位于 `publish/VirtualDesktopPanel.exe`。

## 使用说明

| 操作 | 方式 |
|------|------|
| 显示/隐藏面板 | 左键点击托盘图标 |
| 关闭面板 | Esc 键 或 点击面板右上角 X |
| 移动图标 | 按住拖拽到目标位置 |
| 打开文件 | 双击图标 |
| 右键操作 | 右键点击图标弹出菜单 |
| 设置 | 右键托盘图标 → Settings，或点击面板标题栏 Setting 按钮 |

## 项目结构

```
VirtualDesktopPanel/
├── App.xaml(.cs)          # WPF 应用入口，初始化托盘图标
├── TrayIcon.cs            # 系统托盘图标（WinForms NotifyIcon），含自动提升到任务栏逻辑
├── MainWindow.xaml(.cs)   # 主面板窗口，定位、外观、事件处理
├── SettingsWindow.xaml(.cs) # 设置窗口 UI
├── IconGridPanel.cs       # 自定义 WPF Panel，网格布局 + 拖拽排序
├── DesktopScanner.cs      # 扫描用户桌面和公共桌面，FileSystemWatcher 实时监听
├── DesktopIcon.cs         # 桌面图标数据模型（路径、图标、位置、有效性）
├── Settings.cs            # 设置读写（JSON）、主题预设、开机自启快捷方式
├── LayoutManager.cs       # 图标网格位置持久化（JSON），500ms 防抖写入
└── NativeMethods.cs       # P/Invoke（任务栏检测、Shell 操作、GDI 清理）
```

## 配置存储

所有配置文件保存在 `%APPDATA%\VirtualDesktopPanel\`：

- `settings.json` — 面板大小、主题、背景、模糊效果、点击行为等
- `layout.json` — 每个图标的网格坐标（行/列）

## 技术栈

- C# / .NET 8
- WPF（UI 框架）
- WinForms（NotifyIcon 系统托盘）
- P/Invoke（Win32 API 调用）
- COM（WScript.Shell 快捷方式处理）

## 许可证

MIT
