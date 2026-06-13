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
