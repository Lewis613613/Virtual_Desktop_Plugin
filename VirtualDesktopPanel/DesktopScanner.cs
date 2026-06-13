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
        // .lnk files are opaque; just check they're non-empty
        try
        {
            var info = new FileInfo(lnkPath);
            return info.Length > 0;
        }
        catch { return false; }
    }

    public static ImageSource? ExtractIcon(string path)
    {
        try
        {
            using Icon? icon = Icon.ExtractAssociatedIcon(path);

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
