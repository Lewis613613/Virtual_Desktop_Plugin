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
    public static Dictionary<string, IconPosition> MergeWithDisk(List<string> filePaths, int maxColumns = 20)
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

            var slot = FindEmptySlot(occupied, maxColumns);
            result[path] = new IconPosition { Row = slot.row, Col = slot.col };
            occupied.Add(slot);
        }

        return result;
    }

    private static (int row, int col) FindEmptySlot(HashSet<(int row, int col)> occupied, int maxColumns = 20)
    {
        int col = 0, row = 0;
        while (true)
        {
            if (!occupied.Contains((row, col)))
                return (row, col);
            col++;
            if (col >= maxColumns) { col = 0; row++; }
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
