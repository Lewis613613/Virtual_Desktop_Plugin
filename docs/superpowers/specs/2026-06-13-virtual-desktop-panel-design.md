# Virtual Desktop Panel — Design Spec

**Date:** 2026-06-13
**Status:** Draft

## 1. Overview

Virtual Desktop Panel is a lightweight Windows utility that replaces the traditional desktop with a clean, pop-up panel. The real Windows desktop stays empty (desktop icons hidden), while all shortcuts and files are presented inside a semi-transparent panel that appears above the taskbar when the user clicks the tray icon.

- **Taskbar-resident tray icon** — always visible, left-click toggles the panel
- **Reads the real Desktop folder** — no duplication, no separate file management
- **Custom icon layout** — grid-aligned drag-and-drop positioning, persisted to JSON
- **Configurable appearance** — background color, opacity, blur effects, multiple presets

### Core Experience

| Step | Action |
|------|--------|
| 1 | User hides desktop icons via Windows: Right-click desktop → View → uncheck "Show desktop icons" |
| 2 | Virtual Desktop Panel runs in the system tray |
| 3 | Left-click tray icon → panel pops up above the taskbar (~60% screen width) |
| 4 | Panel displays all files from `%USERPROFILE%\Desktop` as draggable icons |
| 5 | Double-click an icon → opens the file/app. Panel behavior (close/stay) is configurable |
| 6 | Click outside the panel or press Esc → panel hides |

## 2. Tech Stack

- **Language:** C# (.NET 8+)
- **UI Framework:** WPF (Windows Presentation Foundation)
- **Architecture:** Minimal code-behind (no MVVM framework)
- **Packaging:** Single-file self-contained executable

### Why WPF

- Native Windows taskbar/tray integration (`NotifyIcon`, `HwndSource`, P/Invoke)
- `Canvas` + custom `Panel` for drag-and-drop grid layout
- `AllowsTransparency` + `AcrylicBrush` / `MicaBrush` for modern blur effects
- Self-contained publish produces a single ~15-25 MB `.exe`

### Why Minimal Code-Behind

The app has a narrow, well-defined scope (8 components, ~8-10 files). MVVM's abstraction layers (ViewModels, commands, bindings) add ceremony without meaningful benefit at this scale. Code-behind keeps drag-and-drop logic direct and performant. If the app later outgrows this structure, extracting ViewModels is straightforward.

## 3. Components

### 3.1 App.xaml.cs — Application Entry

- Creates the system tray icon on startup
- Hides the main window (no initial popup)
- Handles application exit / cleanup

### 3.2 TrayIcon — System Tray Manager

**Technology:** `System.Windows.Forms.NotifyIcon` (WinForms interop via `WindowsFormsIntegration`)

**Responsibilities:**
- Left-click: Toggle panel show/hide
- Right-click context menu:
  - Settings → opens Settings window
  - Exit → closes the application
- Holds a reference to MainWindow, calls `Show()` / `Hide()` on toggle

### 3.3 MainWindow — Popup Panel Window

**Window properties:**

| Property | Value | Reason |
|----------|-------|--------|
| `WindowStyle` | `None` | No title bar or borders |
| `AllowsTransparency` | `True` | Enables rounded corners and blur |
| `ShowInTaskbar` | `False` | Only the tray icon is shown |
| `Topmost` | `True` | Always-on-top when visible |
| `ResizeMode` | `CanResize` | User can adjust panel size by dragging edges |

**Behavior:**
- On `IsVisibleChanged` → if becoming visible, recalculate position relative to taskbar
- On `Deactivated` → hide the window (lost focus = dismiss)
- On `KeyDown` (Esc) → hide the window
- Hosts the `IconGridPanel` as its main content

**Positioning logic:**
1. Find taskbar window: `FindWindow("Shell_TrayWnd")`
2. Read taskbar bounds and edge position (top/bottom/left/right)
3. Calculate panel position centered above/beside the taskbar
4. Default: taskbar at bottom → panel centered horizontally, bottom edge at taskbar top

### 3.4 DesktopScanner — Desktop File Reader

**Responsibilities:**
- Read `Environment.GetFolderPath(Environment.SpecialFolder.Desktop)` to enumerate files
- Generate `DesktopIcon` objects for each file/folder/`.lnk`/`.url`
- Extract icon images from `.lnk` files via `System.Drawing.Icon.ExtractAssociatedIcon`
- Use `FileSystemWatcher` to monitor for changes:
  - `Created` → add new icon to grid at first available empty slot
  - `Deleted` → remove icon from grid and layout
  - `Renamed` → update icon label, preserve position

### 3.5 IconGridPanel — Custom Grid Panel

**Technology:** Custom WPF `Panel` subclass, overriding `MeasureOverride` and `ArrangeOverride`

**Responsibilities:**
- Lays out icons on a configurable grid (default cell size: 80×100 px)
- Handles `MouseLeftButtonDown`/`MouseMove`/`MouseLeftButtonUp` for drag-and-drop
- Snap-to-grid on drop: icon position rounds to nearest cell
- Double-click: launch file/application via `Process.Start`
- Right-click: context menu (Open, Open file location, Delete, Properties)
- Drag visual feedback: slight scale-up and opacity change during drag
- New files are placed at first empty slot (row-major order), with a brief highlight animation

### 3.6 LayoutManager — Layout Persistence

**File:** `%APPDATA%\VirtualDesktopPanel\layout.json`

**Schema:**
```json
{
  "version": 1,
  "icons": {
    "C:\\Users\\...\\Desktop\\project.lnk": { "row": 0, "col": 2 },
    "C:\\Users\\...\\Desktop\\readme.txt":   { "row": 1, "col": 0 }
  }
}
```

**Behavior:**
- On load: read JSON, merge with current file system state. Files in JSON but not on disk → removed. Files on disk but not in JSON → auto-place at first empty slot
- On any layout change (drag-drop, add, remove): save immediately (debounced, 500ms)
- If file is missing or corrupt: rebuild from scratch, fill grid row-by-row sorted by name

### 3.7 Settings — Configuration Manager

**File:** `%APPDATA%\VirtualDesktopPanel\settings.json`

**Configuration items:**

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `clickBehavior` | `enum` | `keepOpen` | `autoClose` or `keepOpen` |
| `panelWidthPercent` | `int` | `60` | Panel width as % of screen width (40-90) |
| `panelHeightPercent` | `int` | `70` | Panel height as % of work area (40-90) |
| `gridCellWidth` | `int` | `80` | Grid cell width in pixels |
| `gridCellHeight` | `int` | `100` | Grid cell height in pixels |
| `backgroundColor` | `string` | `#1a1a2e` | Hex background color |
| `backgroundOpacity` | `double` | `0.85` | 0.0–1.0 |
| `blurEffect` | `enum` | `acrylic` | `acrylic`, `mica`, `none` |
| `themePreset` | `enum` | `dark` | `dark`, `black`, `light`, `system`, `custom`. Presets auto-fill color/opacity/blur; any manual adjustment switches preset to `custom`. |
| `autoStart` | `bool` | `false` | Register in Windows startup |

### 3.8 SettingsWindow — Settings UI

A simple WPF `Window` with:
- Radio buttons: click behavior (auto-close / keep open)
- Numeric inputs: panel width %, panel height %
- Numeric inputs: grid cell width, height (px)
- Color picker + opacity slider
- Dropdown: blur effect
- Dropdown: theme preset
- Checkbox: auto-start with Windows
- Save / Cancel buttons

## 4. Data Flow

```
System Tray (left-click)
        │
        ▼
  TrayIcon.Toggle()
        │
        ├── panel is hidden → MainWindow.Show()
        │                          │
        │                          ├── Query taskbar position
        │                          ├── Set window bounds
        │                          ├── DesktopScanner.Refresh()
        │                          │       │
        │                          │       ├── Enumerate Desktop folder
        │                          │       └── Push DesktopIcon list
        │                          │
        │                          └── IconGridPanel.LayoutIcons()
        │                                  │
        │                                  └── LayoutManager.Merge()
        │                                          ├── Load layout.json
        │                                          ├── Reconcile with file system
        │                                          └── Return positions
        │
        └── panel is visible → MainWindow.Hide()
```

```
File Change (external)
        │
        ▼
  FileSystemWatcher event
        │
        ▼
  DesktopScanner.OnFileChanged()
        │
        ├── Created → IconGridPanel.AddIcon(firstEmptySlot)
        ├── Deleted → IconGridPanel.RemoveIcon(path)
        └── Renamed → IconGridPanel.UpdateLabel(path, newName)
        │
        ▼
  LayoutManager.Save()  [debounced 500ms]
```

## 5. Edge Cases

| Scenario | Handling |
|----------|----------|
| Desktop file deleted externally | `FileSystemWatcher.Deleted` → remove icon, update layout.json |
| New file added to Desktop | `FileSystemWatcher.Created` → place at first empty grid slot, flash animation |
| File renamed | `FileSystemWatcher.Renamed` → update label, keep position |
| layout.json missing or corrupt | Rebuild: fill icons row-major, sorted by filename |
| Taskbar position changed | Query taskbar bounds on every `Show()`, never cache |
| Multiple monitors | Pop up on the monitor containing the taskbar (`Shell_TrayWnd`) |
| Desktop folder is empty | Show message: "Desktop is empty. Add files to your Desktop folder to see them here." |
| Non-existent .lnk target | Show icon with warning overlay; double-click shows error message |
| App launched but Desktop folder inaccessible | Show error in panel, log to AppData |

## 6. Auto-Start with Windows

When the user enables auto-start in settings:
- Create a shortcut in `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\`
- Or use registry key `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Preference: use the Startup folder (user-visible, easy to manage)

## 7. File Structure

```
VirtualDesktopPanel/
├── App.xaml / App.xaml.cs          — Entry point, tray init
├── MainWindow.xaml / .cs           — Popup panel window
├── SettingsWindow.xaml / .cs       — Settings dialog
├── TrayIcon.cs                     — System tray manager
├── DesktopScanner.cs               — Desktop folder reader + watcher
├── IconGridPanel.cs                — Custom grid panel with drag-and-drop
├── LayoutManager.cs                — JSON layout persistence
├── Settings.cs                     — JSON settings persistence
├── DesktopIcon.cs                  — Data model (path, label, icon, position)
├── NativeMethods.cs                — P/Invoke (FindWindow, GetWindowRect, etc.)
```

## 8. Acceptance Criteria

1. Tray icon appears on app launch; left-click toggles the panel
2. Panel pops up centered above the taskbar at configured size
3. All files from the Desktop folder appear as icons in the panel
4. Icons can be dragged to any grid cell; position persists across restarts
5. Double-clicking an icon opens the file or launches the application
6. Panel hides when clicking outside it or pressing Esc
7. Click behavior (auto-close / keep open) is configurable in settings
8. Background color, opacity, and blur effect are configurable
9. New files added to Desktop folder appear automatically in the panel
10. Files deleted from Desktop disappear from the panel
