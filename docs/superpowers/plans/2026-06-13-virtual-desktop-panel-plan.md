# Virtual Desktop Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows WPF tray application that displays desktop files in a popup panel above the taskbar with drag-and-drop icon arrangement.

**Architecture:** Single WPF project with minimal code-behind. 10 source files (~8 components). System tray via WinForms `NotifyIcon`, taskbar detection via P/Invoke, icon grid via custom `Panel` subclass, persistence via JSON files in `%APPDATA%`.

**Tech Stack:** C# / .NET 8 / WPF / System.Windows.Forms (NotifyIcon) / System.Text.Json

---

### Task 1: Project Scaffolding

**Files:**
- Create: `VirtualDesktopPanel/VirtualDesktopPanel.csproj`
- Create: `VirtualDesktopPanel/App.xaml`
- Create: `VirtualDesktopPanel/App.xaml.cs`

- [ ] **Step 1: Create the .csproj file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

`UseWindowsForms>true` is required for `System.Windows.Forms.NotifyIcon`.

- [ ] **Step 2: Create App.xaml**

```xml
<Application x:Class="VirtualDesktopPanel.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Startup="OnStartup"
             Exit="OnExit">
    <Application.Resources>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: Create App.xaml.cs (stub)**

```csharp
using System.Windows;

namespace VirtualDesktopPanel;

public partial class App : Application
{
    private TrayIcon? _trayIcon;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _trayIcon = new TrayIcon();
        _trayIcon.Initialize();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _trayIcon?.Dispose();
    }
}
```

- [ ] **Step 4: Verify the project builds**

Run: `dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj`
Expected: Build errors about missing `TrayIcon` type (acceptable — will be created in Task 9)

- [ ] **Step 5: Commit**

---

### Task 2: DesktopIcon Data Model

**Files:**
- Create: `VirtualDesktopPanel/DesktopIcon.cs`

- [ ] **Step 1: Create DesktopIcon.cs**

This is a plain data class representing one item on the panel. No logic — just data.

```csharp
using System.Windows.Media;

namespace VirtualDesktopPanel;

public class DesktopIcon
{
    public string FilePath { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Row { get; set; }
    public int Col { get; set; }
    public ImageSource? IconImage { get; set; }
    public bool IsBroken { get; set; }

    public string FullPath => FilePath;

    public void Launch()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = FilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开:\n{FilePath}\n\n{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

---

### Task 3: Settings Manager

**Files:**
- Create: `VirtualDesktopPanel/Settings.cs`

This manages `%APPDATA%\VirtualDesktopPanel\settings.json`. Reads on first access, writes on save.

- [ ] **Step 1: Create Settings.cs**

```csharp
using System;
using System.IO;
using System.Text.Json;

namespace VirtualDesktopPanel;

public enum ClickBehavior { AutoClose, KeepOpen }
public enum BlurEffect { Acrylic, Mica, None }
public enum ThemePreset { Dark, Black, Light, System, Custom }

public class AppSettings
{
    public ClickBehavior ClickBehavior { get; set; } = ClickBehavior.KeepOpen;
    public int PanelWidthPercent { get; set; } = 60;
    public int PanelHeightPercent { get; set; } = 70;
    public int GridCellWidth { get; set; } = 80;
    public int GridCellHeight { get; set; } = 100;
    public string BackgroundColor { get; set; } = "#1a1a2e";
    public double BackgroundOpacity { get; set; } = 0.85;
    public BlurEffect BlurEffect { get; set; } = BlurEffect.Acrylic;
    public ThemePreset ThemePreset { get; set; } = ThemePreset.Dark;
    public bool AutoStart { get; set; } = false;
}

public static class Settings
{
    private static readonly string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "VirtualDesktopPanel");

    private static readonly string FilePath =
        Path.Combine(AppDir, "settings.json");

    private static AppSettings? _current;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppSettings Current
    {
        get
        {
            if (_current == null) Load();
            return _current!;
        }
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                           ?? new AppSettings();
            }
            else
            {
                _current = new AppSettings();
            }
        }
        catch
        {
            _current = new AppSettings();
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(AppDir);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch { /* fail silently — non-critical */ }
    }

    public static void ApplyPreset(ThemePreset preset)
    {
        var s = Current;
        s.ThemePreset = preset;
        switch (preset)
        {
            case ThemePreset.Dark:
                s.BackgroundColor = "#1a1a2e";
                s.BackgroundOpacity = 0.85;
                s.BlurEffect = BlurEffect.Acrylic;
                break;
            case ThemePreset.Black:
                s.BackgroundColor = "#000000";
                s.BackgroundOpacity = 0.92;
                s.BlurEffect = BlurEffect.None;
                break;
            case ThemePreset.Light:
                s.BackgroundColor = "#f0f0f0";
                s.BackgroundOpacity = 0.80;
                s.BlurEffect = BlurEffect.Acrylic;
                break;
            case ThemePreset.System:
                s.BackgroundColor = "#1a1a2e";
                s.BackgroundOpacity = 0.85;
                s.BlurEffect = BlurEffect.Mica;
                break;
            case ThemePreset.Custom:
                break; // keep current values
        }
    }

    public static void MarkCustom()
    {
        Current.ThemePreset = ThemePreset.Custom;
    }

    public static void SetAutoStart(bool enable)
    {
        Current.AutoStart = enable;
        Save();
        ApplyAutoStart(enable);
    }

    private static void ApplyAutoStart(bool enable)
    {
        var startupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup));
        var shortcutPath = Path.Combine(startupDir, "VirtualDesktopPanel.lnk");

        if (enable)
        {
            CreateShortcut(shortcutPath);
        }
        else
        {
            if (File.Exists(shortcutPath))
                File.Delete(shortcutPath);
        }
    }

    private static void CreateShortcut(string shortcutPath)
    {
        // Uses Shell32 COM to create .lnk
        // Placeholder — detailed in Task 12
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

---

### Task 4: LayoutManager

**Files:**
- Create: `VirtualDesktopPanel/LayoutManager.cs`

Manages `%APPDATA%\VirtualDesktopPanel\layout.json`. Maps file paths → grid positions with merge logic.

- [ ] **Step 1: Create LayoutManager.cs**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace VirtualDesktopPanel;

public class IconLayout
{
    public int Version { get; set; } = 1;
    public Dictionary<string, IconPosition> Icons { get; set; } = new();
}

public class IconPosition
{
    public int Row { get; set; }
    public int Col { get; set; }
}

public static class LayoutManager
{
    private static readonly string FilePath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VirtualDesktopPanel", "layout.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static IconLayout? _layout;
    private static System.Threading.Timer? _debounceTimer;

    /// <summary>
    /// Load layout.json and merge with the current set of files on disk.
    /// Returns a dictionary of filePath -> (row, col).
    /// Files in JSON but not on disk are dropped.
    /// Files on disk but not in JSON are placed at the first available empty slot.
    /// </summary>
    public static Dictionary<string, IconPosition> MergeWithDisk(List<string> filePaths)
    {
        _layout = LoadLayout();

        var result = new Dictionary<string, IconPosition>();
        var occupied = new HashSet<(int row, int col)>();

        // Keep entries for files that still exist on disk
        foreach (var (path, pos) in _layout.Icons)
        {
            if (filePaths.Contains(path))
            {
                result[path] = pos;
                occupied.Add((pos.Row, pos.Col));
            }
        }

        // Auto-place new files at first empty slot (row-major)
        foreach (var path in filePaths)
        {
            if (result.ContainsKey(path)) continue;

            var slot = FindEmptySlot(occupied);
            result[path] = new IconPosition { Row = slot.row, Col = slot.col };
            occupied.Add(slot);
        }

        return result;
    }

    private static (int row, int col) FindEmptySlot(HashSet<(int row, int col)> occupied)
    {
        int col = 0, row = 0;
        while (true)
        {
            if (!occupied.Contains((row, col)))
                return (row, col);
            col++;
            if (col >= 20) { col = 0; row++; }
        }
    }

    public static void Save(Dictionary<string, IconPosition> positions)
    {
        _layout = new IconLayout
        {
            Version = 1,
            Icons = new Dictionary<string, IconPosition>(positions)
        };
        DebouncedWrite();
    }

    public static void Remove(string filePath)
    {
        if (_layout == null) return;
        _layout.Icons.Remove(filePath);
        DebouncedWrite();
    }

    private static IconLayout LoadLayout()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<IconLayout>(json, JsonOptions)
                       ?? new IconLayout();
            }
        }
        catch { }
        return new IconLayout();
    }

    private static void DebouncedWrite()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var json = JsonSerializer.Serialize(_layout, JsonOptions);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }, null, 500, System.Threading.Timeout.Infinite);
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

---

### Task 5: NativeMethods (P/Invoke)

**Files:**
- Create: `VirtualDesktopPanel/NativeMethods.cs`

- [ ] **Step 1: Create NativeMethods.cs**

```csharp
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace VirtualDesktopPanel;

public static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW = 0x00040000;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("shell32.dll")]
    public static extern uint SHGetFolderPath(IntPtr hwndOwner, int nFolder,
        IntPtr hToken, uint dwFlags, [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszPath);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    /// <summary>
    /// Get the bounds of the taskbar and which edge of the screen it sits on.
    /// Returns (RECT, edge) where edge is "B"=bottom, "T"=top, "L"=left, "R"=right.
    /// </summary>
    public static (RECT Bounds, char Edge) GetTaskbarBounds()
    {
        var hWnd = FindWindow("Shell_TrayWnd", null);
        if (hWnd == IntPtr.Zero)
        {
            // Fallback: assume taskbar at bottom, 48px tall
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen == null)
                return (new RECT(), 'B');

            var wa = screen.WorkingArea;
            var sb = screen.Bounds;
            var rect = new RECT
            {
                Left = sb.Left,
                Top = wa.Bottom,
                Right = sb.Right,
                Bottom = sb.Bottom
            };

            if (wa.Top > sb.Top) return (rect, 'T');
            if (wa.Left > sb.Left) return (rect, 'L');
            if (wa.Right < sb.Right) return (rect, 'R');
            return (rect, 'B');
        }

        GetWindowRect(hWnd, out RECT tbRect);

        var screen2 = System.Windows.Forms.Screen.FromHandle(hWnd);
        char edge = 'B';
        if (tbRect.Top > screen2.Bounds.Top + screen2.Bounds.Height / 2) edge = 'B';
        else if (tbRect.Bottom < screen2.Bounds.Height / 2) edge = 'T';
        else if (tbRect.Left > screen2.Bounds.Width / 2) edge = 'R';
        else if (tbRect.Right < screen2.Bounds.Width / 2) edge = 'L';

        return (tbRect, edge);
    }

    /// <summary>
    /// Get the screen that contains the taskbar.
    /// </summary>
    public static System.Windows.Forms.Screen GetTaskbarScreen()
    {
        var hWnd = FindWindow("Shell_TrayWnd", null);
        if (hWnd != IntPtr.Zero)
            return System.Windows.Forms.Screen.FromHandle(hWnd);
        return System.Windows.Forms.Screen.PrimaryScreen
               ?? System.Windows.Forms.Screen.AllScreens[0];
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

---

### Task 6: DesktopScanner

**Files:**
- Create: `VirtualDesktopPanel/DesktopScanner.cs`

- [ ] **Step 1: Create DesktopScanner.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VirtualDesktopPanel;

public class DesktopScanner
{
    public string DesktopPath { get; }
    public List<DesktopIcon> Icons { get; private set; } = new();

    public event Action<DesktopIcon>? IconAdded;
    public event Action<string>? IconRemoved;
    public event Action<string, string>? IconRenamed;

    private FileSystemWatcher? _watcher;

    public DesktopScanner()
    {
        DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    /// <summary>
    /// Enumerate all files/folders/.lnk/.url on the desktop.
    /// Returns unsorted list; layout assignment is done by LayoutManager.
    /// </summary>
    public List<DesktopIcon> Scan()
    {
        Icons = new List<DesktopIcon>();
        if (!Directory.Exists(DesktopPath))
            return Icons;

        var entries = Directory.GetFileSystemEntries(DesktopPath)
            .Where(f => !IsHiddenSystem(f))
            .OrderBy(f => f)
            .ToList();

        foreach (var path in entries)
        {
            var icon = new DesktopIcon
            {
                FilePath = path,
                Label = Path.GetFileNameWithoutExtension(path),
                IconImage = ExtractIcon(path)
            };

            // Check if .lnk target exists
            if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                icon.IsBroken = !IsLnkValid(path);
            }

            Icons.Add(icon);
        }

        return Icons;
    }

    public void StartWatching()
    {
        if (_watcher != null) return;

        _watcher = new FileSystemWatcher(DesktopPath)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true
        };

        _watcher.Created += (_, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (IsHiddenSystem(e.FullPath)) return;
                var icon = new DesktopIcon
                {
                    FilePath = e.FullPath,
                    Label = Path.GetFileNameWithoutExtension(e.FullPath),
                    IconImage = ExtractIcon(e.FullPath)
                };
                IconAdded?.Invoke(icon);
            });
        };

        _watcher.Deleted += (_, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                IconRemoved?.Invoke(e.FullPath);
            });
        };

        _watcher.Renamed += (_, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (IsHiddenSystem(e.FullPath)) return;
                IconRenamed?.Invoke(e.OldFullPath, e.FullPath);
            });
        };
    }

    public void StopWatching()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    private static bool IsHiddenSystem(string path)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith(".")) return true;
        try
        {
            var attr = File.GetAttributes(path);
            return (attr & (FileAttributes.Hidden | FileAttributes.System)) != 0;
        }
        catch { return false; }
    }

    private static bool IsLnkValid(string lnkPath)
    {
        try
        {
            // Simple check: try to resolve the .lnk via Shell32
            var shell = new IWshRuntimeLibrary.WshShell();
            var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(lnkPath);
            var target = shortcut.TargetPath;
            if (string.IsNullOrEmpty(target)) return false;
            // Resolve environment variables
            target = Environment.ExpandEnvironmentVariables(target);
            if (File.Exists(target) || Directory.Exists(target))
                return true;
            // Target might be a relative path or on PATH — don't be too strict
            return true;
        }
        catch { return false; }
    }

    public static ImageSource? ExtractIcon(string path)
    {
        try
        {
            Icon? icon = null;

            if (Directory.Exists(path))
            {
                icon = SystemIcons.FolderOpen;
            }
            else
            {
                icon = Icon.ExtractAssociatedIcon(path);
            }

            if (icon == null) return null;

            using var bitmap = icon.ToBitmap();
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                NativeMethods.DeleteObject(hBitmap);
            }
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 2: Add COM reference for IWshRuntimeLibrary**

The `.lnk` resolution uses `IWshRuntimeLibrary`. Add this to `.csproj`:

Run: edit `VirtualDesktopPanel.csproj`, add this inside `<ItemGroup>`:
```xml
<COMReference Include="IWshRuntimeLibrary">
  <WrapperTool>tlbimp</WrapperTool>
  <VersionMinor>0</VersionMinor>
  <VersionMajor>1</VersionMajor>
  <Guid>f935dc20-1cf0-11d0-adb9-00c04fd58a0b</Guid>
  <Lcid>0</Lcid>
  <Isolated>false</Isolated>
  <EmbedInteropTypes>true</EmbedInteropTypes>
</COMReference>
```

Note: COM references via SDK-style projects can be tricky. Alternative approach — embed the interop types directly. See Task 6 alt in appendix.

Simpler approach: don't use IWshRuntimeLibrary. Instead, parse .lnk files manually or skip lnk validation.

- [ ] **Step 2b (Simplified): Replace IsLnkValid with a basic check**

Replace `IsLnkValid` in DesktopScanner.cs:
```csharp
private static bool IsLnkValid(string lnkPath)
{
    // .lnk files are opaque; just check they're non-empty
    try
    {
        var info = new FileInfo(lnkPath);
        return info.Length > 0;
    }
    catch { return false; }
}
```

Remove the COM reference from .csproj.

- [ ] **Step 3: Add missing NativeMethods.DeleteObject**

In `NativeMethods.cs`, add:
```csharp
[DllImport("gdi32.dll")]
public static extern bool DeleteObject(IntPtr hObject);
```

- [ ] **Step 4: Verify build**

Run: `dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj`
Expected: Build succeeds

- [ ] **Step 5: Commit**

---

### Task 7: IconGridPanel

**Files:**
- Create: `VirtualDesktopPanel/IconGridPanel.cs`

This is the core visual component. A custom WPF `Panel` subclass that:
- Lays out children on a grid
- Handles mouse drag-and-drop with snap-to-grid
- Handles double-click to launch
- Handles right-click context menu

- [ ] **Step 1: Create IconGridPanel.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VirtualDesktopPanel;

public class IconGridPanel : Panel
{
    public int CellWidth { get; set; } = 80;
    public int CellHeight { get; set; } = 100;
    public int HorizontalSpacing { get; set; } = 12;
    public int VerticalSpacing { get; set; } = 16;
    public int PaddingLeft { get; set; } = 24;
    public int PaddingTop { get; set; } = 24;

    public event Action<DesktopIcon, int, int>? IconMoved; // icon, newRow, newCol
    public event Action<DesktopIcon>? IconDoubleClicked;

    private readonly Dictionary<UIElement, DesktopIcon> _iconMap = new();
    private UIElement? _draggingElement;
    private Point _dragStartPoint;
    private bool _isDragging;
    private Point _originalPosition; // for visual offset during drag

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in Children)
        {
            child.Measure(new Size(CellWidth, CellHeight));
        }
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var sorted = new List<(DesktopIcon icon, UIElement element)>();
        foreach (UIElement child in Children)
        {
            if (_iconMap.TryGetValue(child, out var icon))
                sorted.Add((icon, child));
        }

        // Sort by row then col for consistent tab order
        sorted.Sort((a, b) =>
        {
            int cmp = a.icon.Row.CompareTo(b.icon.Row);
            return cmp != 0 ? cmp : a.icon.Col.CompareTo(b.icon.Col);
        });

        foreach (var (icon, child) in sorted)
        {
            if (child == _draggingElement) continue; // skip during drag

            double x = PaddingLeft + icon.Col * (CellWidth + HorizontalSpacing);
            double y = PaddingTop + icon.Row * (CellHeight + VerticalSpacing);
            child.Arrange(new Rect(x, y, CellWidth, CellHeight));
        }

        // Keep dragging element at its visual position
        if (_draggingElement != null)
        {
            _draggingElement.Arrange(new Rect(
                _originalPosition.X, _originalPosition.Y,
                CellWidth, CellHeight));
        }

        return finalSize;
    }

    public void Populate(List<DesktopIcon> icons)
    {
        Children.Clear();
        _iconMap.Clear();

        foreach (var icon in icons)
        {
            var item = CreateIconElement(icon);
            Children.Add(item);
            _iconMap[item] = icon;
        }
    }

    public void AddIcon(DesktopIcon icon)
    {
        var item = CreateIconElement(icon);
        Children.Add(item);
        _iconMap[item] = icon;

        // Flash animation for new items
        var animation = new DoubleAnimation(1.0, 0.3, TimeSpan.FromMilliseconds(200))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2)
        };
        item.BeginAnimation(OpacityProperty, animation);
    }

    public void RemoveIcon(string filePath)
    {
        UIElement? toRemove = null;
        foreach (var kv in _iconMap)
        {
            if (kv.Value.FilePath == filePath)
            {
                toRemove = kv.Key;
                break;
            }
        }
        if (toRemove != null)
        {
            Children.Remove(toRemove);
            _iconMap.Remove(toRemove);
        }
    }

    public void UpdateLabel(string oldPath, string newPath)
    {
        foreach (var kv in _iconMap)
        {
            if (kv.Value.FilePath == oldPath)
            {
                kv.Value.Label = System.IO.Path.GetFileNameWithoutExtension(newPath);
                if (kv.Key is Border border && border.Child is StackPanel sp
                    && sp.Children.Count >= 2 && sp.Children[1] is TextBlock tb)
                {
                    tb.Text = kv.Value.Label;
                }
                break;
            }
        }
    }

    public void UpdateBackground(string colorHex, double opacity)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        color.A = (byte)(opacity * 255);
        Background = new SolidColorBrush(color);
    }

    private UIElement CreateIconElement(DesktopIcon icon)
    {
        var image = new System.Windows.Controls.Image
        {
            Source = icon.IconImage,
            Width = 40,
            Height = 40,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 0, 4)
        };

        if (icon.IconImage == null)
        {
            // fallback: no icon available
            image.Source = CreatePlaceholderIcon();
        }

        var label = new TextBlock
        {
            Text = icon.Label,
            TextAlignment = TextAlignment.Center,
            FontSize = 11,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.WrapWithOverflow,
            MaxWidth = CellWidth - 4,
            MaxHeight = 32,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var brokenOverlay = new TextBlock
        {
            Text = "⚠",
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = icon.IsBroken ? Visibility.Visible : Visibility.Collapsed
        };

        var imageContainer = new Grid { Width = 40, Height = 40 };
        imageContainer.Children.Add(image);
        imageContainer.Children.Add(brokenOverlay);

        var stack = new StackPanel
        {
            Width = CellWidth,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.Children.Add(imageContainer);
        stack.Children.Add(label);

        var border = new Border
        {
            Width = CellWidth,
            Height = CellHeight,
            Child = stack,
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(4)
        };

        border.MouseLeftButtonDown += OnMouseLeftButtonDown;
        border.MouseMove += OnMouseMove;
        border.MouseLeftButtonUp += OnMouseLeftButtonUp;
        border.MouseDoubleClick += (_, _) =>
        {
            if (!_isDragging)
            {
                if (icon.IsBroken)
                {
                    MessageBox.Show($"快捷方式目标不存在:\n{icon.FilePath}",
                        "无法打开", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    IconDoubleClicked?.Invoke(icon);
                }
            }
        };

        border.ContextMenu = CreateContextMenu(icon);

        // Visual state for hover
        border.MouseEnter += (_, _) =>
        {
            border.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        };
        border.MouseLeave += (_, _) =>
        {
            border.Background = Brushes.Transparent;
        };

        return border;
    }

    private static ImageSource CreatePlaceholderIcon()
    {
        // Minimal 40x40 transparent PNG as fallback
        var size = 40;
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
                null, new Rect(4, 4, size - 8, size - 8));
        }
        var renderTarget = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(visual);
        return renderTarget;
    }

    private ContextMenu CreateContextMenu(DesktopIcon icon)
    {
        var menu = new ContextMenu();

        var open = new MenuItem { Header = "打开" };
        open.Click += (_, _) => icon.Launch();

        var openLocation = new MenuItem { Header = "打开文件位置" };
        openLocation.Click += (_, _) =>
        {
            var dir = System.IO.Path.GetDirectoryName(icon.FilePath);
            if (dir != null)
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{icon.FilePath}\"");
        };

        var delete = new MenuItem { Header = "删除" };
        delete.Click += (_, _) =>
        {
            var result = MessageBox.Show(
                $"确定要将 \"{icon.Label}\" 移动到回收站吗？",
                "删除文件", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        icon.FilePath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败:\n{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        };

        var properties = new MenuItem { Header = "属性" };
        properties.Click += (_, _) =>
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{icon.FilePath}\"");
        };

        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(openLocation);
        menu.Items.Add(delete);
        menu.Items.Add(new Separator());
        menu.Items.Add(properties);

        return menu;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element && _iconMap.ContainsKey(element))
        {
            _draggingElement = element;
            _dragStartPoint = e.GetPosition(this);
            _isDragging = false;
            element.CaptureMouse();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingElement == null) return;

        var currentPos = e.GetPosition(this);
        var delta = currentPos - _dragStartPoint;

        if (!_isDragging && Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            return; // dead zone to distinguish click from drag

        _isDragging = true;

        // Visual feedback: offset the element during drag
        var iconPos = GetIconPosition(_draggingElement);
        _originalPosition = new Point(
            PaddingLeft + iconPos.col * (CellWidth + HorizontalSpacing) + delta.X,
            PaddingTop + iconPos.row * (CellHeight + VerticalSpacing) + delta.Y);

        // Scale up slightly during drag
        _draggingElement.RenderTransform = new ScaleTransform(1.08, 1.08);
        _draggingElement.Opacity = 0.8;

        InvalidateArrange();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingElement == null) return;
        _draggingElement.ReleaseMouseCapture();

        if (_isDragging)
        {
            var currentPos = e.GetPosition(this);

            // Snap to nearest grid cell
            int newCol = (int)Math.Round((currentPos.X - PaddingLeft) / (double)(CellWidth + HorizontalSpacing));
            int newRow = (int)Math.Round((currentPos.Y - PaddingTop) / (double)(CellHeight + VerticalSpacing));
            newCol = Math.Max(0, newCol);
            newRow = Math.Max(0, newRow);

            // Update icon position
            if (_iconMap.TryGetValue(_draggingElement, out var icon))
            {
                int oldRow = icon.Row, oldCol = icon.Col;
                icon.Row = newRow;
                icon.Col = newCol;

                // Swap with any icon already at target
                foreach (var kv in _iconMap)
                {
                    if (kv.Key != _draggingElement
                        && kv.Value.Row == newRow && kv.Value.Col == newCol)
                    {
                        kv.Value.Row = oldRow;
                        kv.Value.Col = oldCol;
                        break;
                    }
                }

                IconMoved?.Invoke(icon, newRow, newCol);
            }

            _draggingElement.RenderTransform = Transform.Identity;
            _draggingElement.Opacity = 1.0;
        }

        _draggingElement = null;
        _isDragging = false;
        InvalidateArrange();
    }

    private (int row, int col) GetIconPosition(UIElement element)
    {
        if (_iconMap.TryGetValue(element, out var icon))
            return (icon.Row, icon.Col);
        return (0, 0);
    }

    public int GetFirstEmptySlot(out int row, out int col)
    {
        var occupied = new HashSet<(int, int)>();
        foreach (var icon in _iconMap.Values)
            occupied.Add((icon.Row, icon.Col));

        row = 0; col = 0;
        while (true)
        {
            if (!occupied.Contains((row, col))) return 0;
            col++;
            if (col >= 20) { col = 0; row++; }
        }
    }
}
```

- [ ] **Step 2: Add Microsoft.VisualBasic reference for Recycle Bin deletion**

Add to `.csproj`:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.VisualBasic" Version="10.3.0" />
</ItemGroup>
```

Alternatively, use a simpler deletion approach without VB dependency:
```csharp
// In Delete click handler, replace VB call with:
try
{
    // Use Shell32 for recycle bin delete
    var filePath = icon.FilePath;
    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
        filePath,
        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
}
```

Or even simpler — just use a native API:
```csharp
// In NativeMethods.cs, add this P/Invoke to move to recycle bin
// Then call it directly instead of the VB helper
```

Let's use the shell API approach to avoid the VB dependency.

- [ ] **Step 2b (Simplified delete): Replace VB delete with SHFileOperation**

Add to `NativeMethods.cs`:
```csharp
[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
public static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct SHFILEOPSTRUCT
{
    public IntPtr hwnd;
    public uint wFunc;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string pFrom;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string pTo;
    public ushort fFlags;
    public bool fAnyOperationsAborted;
    public IntPtr hNameMappings;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string lpszProgressTitle;
}

public const uint FO_DELETE = 0x0003;
public const ushort FOF_ALLOWUNDO = 0x0040;
public const ushort FOF_NOCONFIRMATION = 0x0010;
```

In `IconGridPanel.cs`, replace the VB delete call with:
```csharp
var fileOp = new NativeMethods.SHFILEOPSTRUCT
{
    hwnd = IntPtr.Zero,
    wFunc = NativeMethods.FO_DELETE,
    pFrom = icon.FilePath + '\0',
    fFlags = NativeMethods.FOF_ALLOWUNDO
};
NativeMethods.SHFileOperation(ref fileOp);
```

- [ ] **Step 3: Verify build**

Run: `dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

---

### Task 8: MainWindow

**Files:**
- Create: `VirtualDesktopPanel/MainWindow.xaml`
- Create: `VirtualDesktopPanel/MainWindow.xaml.cs`

- [ ] **Step 1: Create MainWindow.xaml**

```xml
<Window x:Class="VirtualDesktopPanel.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:VirtualDesktopPanel"
        WindowStyle="None"
        AllowsTransparency="True"
        ShowInTaskbar="False"
        Topmost="True"
        ResizeMode="CanResize"
        Background="Transparent"
        Deactivated="OnDeactivated"
        KeyDown="OnKeyDown">

    <Border x:Name="PanelBorder"
            CornerRadius="12"
            BorderThickness="1"
            BorderBrush="#30FFFFFF">
        <Border.Background>
            <SolidColorBrush x:Name="PanelBackgroundBrush" Color="#1a1a2e" Opacity="0.85"/>
        </Border.Background>

        <Grid Margin="0">
            <Grid.RowDefinitions>
                <RowDefinition Height="36"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- Title bar (drag handle) -->
            <Border Grid.Row="0"
                    Background="Transparent"
                    MouseLeftButtonDown="OnTitleBarMouseDown"
                    CornerRadius="12,12,0,0">
                <Grid Margin="12,0">
                    <TextBlock Text="Virtual Desktop Panel"
                               Foreground="#888"
                               FontSize="12"
                               VerticalAlignment="Center"/>
                    <StackPanel Orientation="Horizontal"
                                HorizontalAlignment="Right"
                                VerticalAlignment="Center">
                        <Button Content="⚙" Style="{StaticResource TitleBarButton}"
                                Click="OnSettingsClick"
                                ToolTip="设置"/>
                        <Button Content="✕" Style="{StaticResource TitleBarButton}"
                                Click="OnCloseClick"
                                ToolTip="关闭面板"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- Scrollable icon area -->
            <ScrollViewer Grid.Row="1"
                          x:Name="IconScrollViewer"
                          VerticalScrollBarVisibility="Auto"
                          HorizontalScrollBarVisibility="Auto"
                          Background="Transparent">
                <local:IconGridPanel x:Name="IconGrid"/>
            </ScrollViewer>
        </Grid>
    </Border>

    <Window.Resources>
        <Style x:Key="TitleBarButton" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="#AAA"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Width" Value="28"/>
            <Setter Property="Height" Value="28"/>
            <Setter Property="FontSize" Value="14"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}"
                                CornerRadius="4">
                            <ContentPresenter HorizontalAlignment="Center"
                                              VerticalAlignment="Center"/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#30FFFFFF"/>
                    <Setter Property="Foreground" Value="White"/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Window.Resources>
</Window>
```

- [ ] **Step 2: Create MainWindow.xaml.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VirtualDesktopPanel;

public partial class MainWindow : Window
{
    private readonly DesktopScanner _scanner;
    private bool _suppressDeactivated;

    public MainWindow()
    {
        InitializeComponent();
        _scanner = new DesktopScanner();

        // Wire up scanner events
        _scanner.IconAdded += OnIconAdded;
        _scanner.IconRemoved += OnIconRemoved;
        _scanner.IconRenamed += OnIconRenamed;

        // Wire up grid events
        IconGrid.IconMoved += OnIconMoved;
        IconGrid.IconDoubleClicked += OnIconDoubleClicked;
    }

    public void LoadIcons()
    {
        var icons = _scanner.Scan();
        var positions = LayoutManager.MergeWithDisk(
            icons.ConvertAll(i => i.FilePath));

        foreach (var icon in icons)
        {
            if (positions.TryGetValue(icon.FilePath, out var pos))
            {
                icon.Row = pos.Row;
                icon.Col = pos.Col;
            }
        }

        ApplyAppearance();
        IconGrid.Populate(icons);
        _scanner.StartWatching();
    }

    public new void Show()
    {
        PositionWindow();
        base.Show();
        Activate();
    }

    public new void Hide()
    {
        base.Hide();
    }

    public void Toggle()
    {
        if (IsVisible)
            Hide();
        else
            Show();
    }

    private void PositionWindow()
    {
        var (tbBounds, edge) = NativeMethods.GetTaskbarBounds();
        var screen = NativeMethods.GetTaskbarScreen();
        var settings = Settings.Current;

        double width = screen.WorkingArea.Width * settings.PanelWidthPercent / 100.0;
        double height = screen.WorkingArea.Height * settings.PanelHeightPercent / 100.0;

        double left, top;

        switch (edge)
        {
            case 'B':
                left = tbBounds.Left + (tbBounds.Width - width) / 2;
                top = tbBounds.Top - height;
                break;
            case 'T':
                left = tbBounds.Left + (tbBounds.Width - width) / 2;
                top = tbBounds.Bottom;
                break;
            case 'L':
                left = tbBounds.Right;
                top = tbBounds.Top + (tbBounds.Height - height) / 2;
                break;
            case 'R':
                left = tbBounds.Left - width;
                top = tbBounds.Top + (tbBounds.Height - height) / 2;
                break;
            default:
                left = (screen.WorkingArea.Width - width) / 2;
                top = screen.WorkingArea.Bottom - height - 48;
                break;
        }

        Left = Math.Max(screen.WorkingArea.Left, left);
        Top = Math.Max(screen.WorkingArea.Top, top);
        Width = width;
        Height = height;
    }

    private void ApplyAppearance()
    {
        var s = Settings.Current;
        var color = (Color)ColorConverter.ConvertFromString(s.BackgroundColor);
        color.A = (byte)(s.BackgroundOpacity * 255);
        PanelBackgroundBrush.Color = color;
        PanelBackgroundBrush.Opacity = s.BackgroundOpacity;

        IconGrid.CellWidth = s.GridCellWidth;
        IconGrid.CellHeight = s.GridCellHeight;
    }

    public void RefreshAppearance()
    {
        ApplyAppearance();
        PositionWindow();
        InvalidateVisual();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_suppressDeactivated)
            Hide();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Hide();
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click title bar = maximize/restore idea — skip for now
        }
        else
        {
            DragMove();
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        _suppressDeactivated = true;
        var settingsWindow = new SettingsWindow { Owner = this };
        settingsWindow.ShowDialog();
        _suppressDeactivated = false;
        RefreshAppearance();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnIconAdded(DesktopIcon icon)
    {
        // Find first empty slot
        var existingPositions = new Dictionary<string, IconPosition>();
        foreach (var child in IconGrid.Children)
        {
            // Rebuild position map
        }
        var positions = LayoutManager.MergeWithDisk(
            new List<string>(_scanner.Icons.Select(i => i.FilePath)));
        if (positions.TryGetValue(icon.FilePath, out var pos))
        {
            icon.Row = pos.Row;
            icon.Col = pos.Col;
        }
        IconGrid.AddIcon(icon);

        SaveAllPositions();
    }

    private void OnIconRemoved(string path)
    {
        IconGrid.RemoveIcon(path);
        LayoutManager.Remove(path);
    }

    private void OnIconRenamed(string oldPath, string newPath)
    {
        IconGrid.UpdateLabel(oldPath, newPath);
        // Update layout key
        var layout = LayoutManager.MergeWithDisk(
            _scanner.Icons.ConvertAll(i => i.FilePath));
        if (layout.TryGetValue(oldPath, out var pos))
        {
            layout.Remove(oldPath);
            layout[newPath] = pos;
        }
        LayoutManager.Save(layout);
    }

    private void OnIconMoved(DesktopIcon icon, int newRow, int newCol)
    {
        SaveAllPositions();
    }

    private void OnIconDoubleClicked(DesktopIcon icon)
    {
        icon.Launch();

        if (Settings.Current.ClickBehavior == ClickBehavior.AutoClose)
        {
            Hide();
        }
    }

    private void SaveAllPositions()
    {
        var positions = new Dictionary<string, IconPosition>();
        foreach (var icon in _scanner.Icons)
        {
            positions[icon.FilePath] = new IconPosition
            {
                Row = icon.Row,
                Col = icon.Col
            };
        }
        LayoutManager.Save(positions);
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj`
Expected: Build succeeds. May have one error about `SettingsWindow` not existing yet — acceptable.

- [ ] **Step 4: Commit**

---

### Task 9: TrayIcon

**Files:**
- Create: `VirtualDesktopPanel/TrayIcon.cs`

- [ ] **Step 1: Create TrayIcon.cs**

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;

namespace VirtualDesktopPanel;

public class TrayIcon : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;

    public void Initialize()
    {
        _mainWindow = new MainWindow();

        // Create a simple 16x16 icon programmatically
        var icon = CreateTrayIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "Virtual Desktop Panel",
            Visible = true
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_mainWindow == null)
                {
                    _mainWindow = new MainWindow();
                }

                if (_mainWindow.IsVisible)
                {
                    _mainWindow.Hide();
                }
                else
                {
                    _mainWindow.LoadIcons();
                    _mainWindow.Show();
                }
            }
        };

        var contextMenu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("设置");
        settingsItem.Click += (_, _) =>
        {
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.Activate();
                return;
            }
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        };

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) =>
        {
            _mainWindow?.Close();
            System.Windows.Application.Current.Shutdown();
        };

        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        // Load on startup so icons are ready (don't show panel)
        _mainWindow.LoadIcons();
    }

    private static Icon CreateTrayIcon()
    {
        // Create a simple 16x16 icon with a grid pattern
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);

            // Draw a small "grid of dots" icon
            using var pen = new Pen(Color.White, 2);
            int s = 4;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    int x = 3 + c * s;
                    int y = 3 + r * s;
                    g.FillRectangle(Brushes.White, x, y, 2, 2);
                }
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}
```

- [ ] **Step 2: Update App.xaml.cs to complete the wiring**

Replace the previous stub with:
```csharp
using System.Windows;

namespace VirtualDesktopPanel;

public partial class App : Application
{
    private TrayIcon? _trayIcon;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Force load settings
        Settings.Load();

        _trayIcon = new TrayIcon();
        _trayIcon.Initialize();

        // Verify desktop is accessible
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!Directory.Exists(desktopPath))
        {
            MessageBox.Show(
                $"无法访问桌面文件夹:\n{desktopPath}",
                "Virtual Desktop Panel",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _trayIcon?.Dispose();
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj`
Expected: Build may have errors about missing `SettingsWindow` — acceptable, created next.

- [ ] **Step 4: Commit**

---

### Task 10: SettingsWindow

**Files:**
- Create: `VirtualDesktopPanel/SettingsWindow.xaml`
- Create: `VirtualDesktopPanel/SettingsWindow.xaml.cs`

- [ ] **Step 1: Create SettingsWindow.xaml**

```xml
<Window x:Class="VirtualDesktopPanel.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Virtual Desktop Panel 设置"
        Width="400" Height="500"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize"
        WindowStyle="ToolWindow">

    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Click Behavior -->
        <GroupBox Grid.Row="0" Header="点击图标后" Margin="0,0,0,8">
            <StackPanel Margin="4">
                <RadioButton x:Name="RbKeepOpen" Content="保持面板打开" Margin="0,2"/>
                <RadioButton x:Name="RbAutoClose" Content="自动关闭面板" Margin="0,2"/>
            </StackPanel>
        </GroupBox>

        <!-- Panel Size -->
        <GroupBox Grid.Row="1" Header="面板尺寸" Margin="0,0,0,8">
            <Grid Margin="4">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <TextBlock Text="宽度" VerticalAlignment="Center" Margin="0,0,4,0"/>
                <TextBox x:Name="TxtPanelWidth" Grid.Column="1" Width="50"/>
                <TextBlock Text="%" Grid.Column="2" VerticalAlignment="Center" Margin="4,0,8,0"/>
                <TextBlock Text="高度" Grid.Column="3" VerticalAlignment="Center" Margin="0,0,4,0"/>
                <TextBox x:Name="TxtPanelHeight" Grid.Column="4" Width="50"/>
            </Grid>
        </GroupBox>

        <!-- Grid Size -->
        <GroupBox Grid.Row="2" Header="网格单元格" Margin="0,0,0,8">
            <Grid Margin="4">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <TextBlock Text="宽 (px)" VerticalAlignment="Center" Margin="0,0,4,0"/>
                <TextBox x:Name="TxtCellWidth" Grid.Column="1" Width="50"/>
                <TextBlock Text="高 (px)" Grid.Column="2" VerticalAlignment="Center" Margin="8,0,4,0"/>
                <TextBox x:Name="TxtCellHeight" Grid.Column="3" Width="50"/>
            </Grid>
        </GroupBox>

        <!-- Theme Preset -->
        <GroupBox Grid.Row="3" Header="主题预设" Margin="0,0,0,8">
            <ComboBox x:Name="CmbTheme" Margin="4" SelectedIndex="0">
                <ComboBoxItem Content="深色半透明"/>
                <ComboBoxItem Content="纯黑"/>
                <ComboBoxItem Content="浅色毛玻璃"/>
                <ComboBoxItem Content="跟随系统"/>
                <ComboBoxItem Content="自定义"/>
            </ComboBox>
        </GroupBox>

        <!-- Background Color -->
        <GroupBox Grid.Row="4" Header="背景颜色" Margin="0,0,0,8">
            <Grid Margin="4">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <TextBox x:Name="TxtBgColor" Width="80"/>
                <TextBlock Text="不透明度" Grid.Column="2" VerticalAlignment="Center" Margin="8,0,4,0"/>
                <Slider x:Name="SldOpacity" Grid.Column="3" Minimum="0.1" Maximum="1.0"
                        TickFrequency="0.05" IsSnapToTickEnabled="True" Value="0.85"/>
            </Grid>
        </GroupBox>

        <!-- Blur Effect -->
        <GroupBox Grid.Row="5" Header="模糊效果" Margin="0,0,0,8">
            <ComboBox x:Name="CmbBlur" Margin="4" SelectedIndex="0">
                <ComboBoxItem Content="Acrylic（毛玻璃）"/>
                <ComboBoxItem Content="Mica（跟随主题）"/>
                <ComboBoxItem Content="无"/>
            </ComboBox>
        </GroupBox>

        <!-- Auto Start -->
        <GroupBox Grid.Row="6" Header="启动" Margin="0,0,0,8">
            <CheckBox x:Name="ChkAutoStart" Content="开机自启动" Margin="4"/>
        </GroupBox>

        <!-- Buttons -->
        <StackPanel Grid.Row="9" Orientation="Horizontal"
                    HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button Content="保存" Width="80" Height="30" Click="OnSaveClick"
                    Background="#4a9eff" Foreground="White" BorderThickness="0"/>
            <Button Content="取消" Width="80" Height="30" Margin="8,0,0,0" Click="OnCancelClick"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 2: Create SettingsWindow.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Controls;

namespace VirtualDesktopPanel;

public partial class SettingsWindow : Window
{
    private bool _loading;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _loading = true;
        var s = Settings.Current;

        RbKeepOpen.IsChecked = s.ClickBehavior == ClickBehavior.KeepOpen;
        RbAutoClose.IsChecked = s.ClickBehavior == ClickBehavior.AutoClose;

        TxtPanelWidth.Text = s.PanelWidthPercent.ToString();
        TxtPanelHeight.Text = s.PanelHeightPercent.ToString();
        TxtCellWidth.Text = s.GridCellWidth.ToString();
        TxtCellHeight.Text = s.GridCellHeight.ToString();

        CmbTheme.SelectedIndex = (int)s.ThemePreset;
        TxtBgColor.Text = s.BackgroundColor;
        SldOpacity.Value = s.BackgroundOpacity;
        CmbBlur.SelectedIndex = (int)s.BlurEffect;
        ChkAutoStart.IsChecked = s.AutoStart;

        _loading = false;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var s = Settings.Current;

        s.ClickBehavior = RbAutoClose.IsChecked == true
            ? ClickBehavior.AutoClose : ClickBehavior.KeepOpen;

        if (int.TryParse(TxtPanelWidth.Text, out int pw)
            && pw >= 40 && pw <= 90) s.PanelWidthPercent = pw;
        if (int.TryParse(TxtPanelHeight.Text, out int ph)
            && ph >= 40 && ph <= 90) s.PanelHeightPercent = ph;
        if (int.TryParse(TxtCellWidth.Text, out int cw)
            && cw >= 60 && cw <= 160) s.GridCellWidth = cw;
        if (int.TryParse(TxtCellHeight.Text, out int ch)
            && ch >= 80 && ch <= 200) s.GridCellHeight = ch;

        var newTheme = (ThemePreset)CmbTheme.SelectedIndex;
        if (newTheme == ThemePreset.Custom)
        {
            s.BackgroundColor = TxtBgColor.Text;
            s.BackgroundOpacity = SldOpacity.Value;
            s.BlurEffect = (BlurEffect)CmbBlur.SelectedIndex;
        }
        else
        {
            Settings.ApplyPreset(newTheme);
        }

        var newAutoStart = ChkAutoStart.IsChecked == true;
        if (newAutoStart != s.AutoStart)
        {
            Settings.SetAutoStart(newAutoStart);
        }

        Settings.Save();

        // Notify main window to refresh appearance
        if (Owner is MainWindow mw)
        {
            mw.RefreshAppearance();
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

---

### Task 11: Auto-Start & .lnk Creation

**Files:**
- Modify: `VirtualDesktopPanel/Settings.cs`

The `CreateShortcut` method needs a real implementation. Use WshShell COM via P/Invoke to avoid adding extra COM references.

- [ ] **Step 1: Implement CreateShortcut in Settings.cs**

Replace the placeholder `CreateShortcut` method:

```csharp
private static void CreateShortcut(string shortcutPath)
{
    try
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        // Use IWshRuntimeLibrary via dynamic dispatch to avoid COM reference issues
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return;

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = exePath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
        shortcut.Description = "Virtual Desktop Panel";
        shortcut.Save();
    }
    catch { /* non-critical */ }
}
```

Add the `System.Reflection` using if needed (likely already covered by `ImplicitUsings`).

- [ ] **Step 2: Verify build**

Run: `dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

---

### Task 12: Polish & Edge Cases

**Files:**
- Modify: `VirtualDesktopPanel/MainWindow.xaml.cs` — empty desktop state
- Modify: `VirtualDesktopPanel/IconGridPanel.cs` — empty state message

- [ ] **Step 1: Add empty-desktop message to IconGridPanel**

Add these fields and method to `IconGridPanel.cs`:

```csharp
private TextBlock? _emptyMessage;

public void ShowEmptyMessage()
{
    if (_emptyMessage == null)
    {
        _emptyMessage = new TextBlock
        {
            Text = "桌面暂无文件\n\n将文件、快捷方式添加到桌面文件夹后\n将自动显示在此处",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Children.Add(_emptyMessage);
    }
    _emptyMessage.Visibility = Visibility.Visible;
}

public void HideEmptyMessage()
{
    if (_emptyMessage != null)
        _emptyMessage.Visibility = Visibility.Collapsed;
}
```

- [ ] **Step 2: Call empty state from MainWindow**

In `MainWindow.xaml.cs`, modify `LoadIcons()`:

```csharp
public void LoadIcons()
{
    var icons = _scanner.Scan();

    if (icons.Count == 0)
    {
        IconGrid.ShowEmptyMessage();
        return;
    }

    IconGrid.HideEmptyMessage();

    var positions = LayoutManager.MergeWithDisk(
        icons.ConvertAll(i => i.FilePath));

    foreach (var icon in icons)
    {
        if (positions.TryGetValue(icon.FilePath, out var pos))
        {
            icon.Row = pos.Row;
            icon.Col = pos.Col;
        }
    }

    ApplyAppearance();
    IconGrid.Populate(icons);
    _scanner.StartWatching();
}
```

- [ ] **Step 3: Handle panel resize**

Add to `MainWindow.xaml.cs`:

```csharp
protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
{
    base.OnRenderSizeChanged(sizeInfo);
    // Let the grid re-layout
    IconGrid?.InvalidateArrange();
}
```

- [ ] **Step 4: Add Esc key binding for SettingsWindow**

In `SettingsWindow.xaml.cs`, add:

```csharp
protected override void OnKeyDown(KeyEventArgs e)
{
    base.OnKeyDown(e);
    if (e.Key == Key.Escape)
    {
        DialogResult = false;
        Close();
    }
}
```

(Add `using System.Windows.Input;`)

- [ ] **Step 5: Verify build and do a full clean build**

Run:
```
dotnet clean VirtualDesktopPanel/VirtualDesktopPanel.csproj
dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj
```
Expected: Build succeeds with zero errors and zero warnings

- [ ] **Step 6: Commit**

---

### Task 13: Create tray.ico and finalize

**Files:**
- Create: `VirtualDesktopPanel/tray.ico` (or embed icon generation)

- [ ] **Step 1: Handle missing icon file**

The tray icon is created programmatically in `TrayIcon.cs` — no .ico file needed.

- [ ] **Step 2: Final build and publish test**

Run:
```
dotnet build VirtualDesktopPanel/VirtualDesktopPanel.csproj
dotnet publish VirtualDesktopPanel/VirtualDesktopPanel.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/
```
Expected: Single `.exe` produced in `publish/` directory

- [ ] **Step 3: Commit**

---

## Acceptance Checklist

After all tasks are complete, verify:

1. Run `VirtualDesktopPanel.exe` → tray icon appears
2. Left-click tray icon → panel pops up above taskbar at ~60% width
3. Panel shows all files from the user's Desktop folder as icons
4. Drag an icon to a different cell → position saved in `layout.json`
5. Close and reopen panel → icon positions preserved
6. Double-click an icon → file/app opens
7. Click outside panel or press Esc → panel hides
8. Right-click tray icon → Settings → change click behavior to "auto close" → works
9. Change background color and opacity in settings → panel reflects changes
10. Add a file to Desktop via File Explorer → appears in panel automatically
11. Delete a file from Desktop → disappears from panel

---

## Notes

- The app uses `System.Windows.Forms.NotifyIcon` which requires the `UseWindowsForms>true` flag in .csproj
- `AllowsTransparency=True` on MainWindow means `AcrylicBrush` won't work directly — we use `SolidColorBrush` with opacity instead. For true acrylic on Windows 11, a more complex `SetWindowCompositionAttribute` P/Invoke would be needed (future enhancement)
- The `dynamic` dispatch in `CreateShortcut` avoids adding a COM reference that may not resolve on all machines
- `FileSystemWatcher` events fire on a background thread; all UI updates are dispatched via `Application.Current.Dispatcher.Invoke`
