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
    public string UserDesktopPath { get; }
    public string PublicDesktopPath { get; }

    public List<DesktopIcon> Icons { get; private set; } = new();

    public event Action<DesktopIcon>? IconAdded;
    public event Action<string>? IconRemoved;
    public event Action<string, string>? IconRenamed;

    private FileSystemWatcher? _userWatcher;
    private FileSystemWatcher? _publicWatcher;

    public DesktopScanner()
    {
        UserDesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        PublicDesktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
    }

    /// <summary>
    /// Enumerate all files from both user and public desktop.
    /// If the same filename exists in both, the user's version wins.
    /// </summary>
    public List<DesktopIcon> Scan()
    {
        Icons = new List<DesktopIcon>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Scan user desktop first (takes priority)
        ScanPath(UserDesktopPath, seen);
        // Scan public desktop (skip already-seen filenames)
        ScanPath(PublicDesktopPath, seen);

        return Icons;
    }

    private void ScanPath(string path, HashSet<string> seen)
    {
        if (!Directory.Exists(path)) return;

        var entries = Directory.GetFileSystemEntries(path)
            .Where(f => !IsHiddenSystem(f))
            .OrderBy(f => f);

        foreach (var filePath in entries)
        {
            var name = Path.GetFileName(filePath);
            if (!seen.Add(name)) continue; // duplicate, skip

            var icon = new DesktopIcon
            {
                FilePath = filePath,
                Label = Path.GetFileNameWithoutExtension(filePath),
                IconImage = ExtractIcon(filePath)
            };

            if (Path.GetExtension(filePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                icon.IsBroken = !IsLnkValid(filePath);

            Icons.Add(icon);
        }
    }

    public void StartWatching()
    {
        _userWatcher = CreateWatcher(UserDesktopPath);
        _publicWatcher = CreateWatcher(PublicDesktopPath);
    }

    private FileSystemWatcher CreateWatcher(string path)
    {
        if (!Directory.Exists(path)) return null!;

        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true
        };

        watcher.Created += (_, e) =>
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
                if (Path.GetExtension(e.FullPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                    icon.IsBroken = !IsLnkValid(e.FullPath);
                Icons.Add(icon);
                IconAdded?.Invoke(icon);
            });
        };

        watcher.Deleted += (_, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Icons.RemoveAll(i => i.FilePath == e.FullPath);
                IconRemoved?.Invoke(e.FullPath);
            });
        };

        watcher.Renamed += (_, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (IsHiddenSystem(e.FullPath)) return;
                var icon = Icons.Find(i => i.FilePath == e.OldFullPath);
                if (icon != null)
                {
                    icon.FilePath = e.FullPath;
                    icon.Label = Path.GetFileNameWithoutExtension(e.FullPath);
                }
                IconRenamed?.Invoke(e.OldFullPath, e.FullPath);
            });
        };

        return watcher;
    }

    public void StopWatching()
    {
        _userWatcher?.Dispose();
        _userWatcher = null;
        _publicWatcher?.Dispose();
        _publicWatcher = null;
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
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return true;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            string target = shortcut.TargetPath ?? "";
            if (string.IsNullOrEmpty(target)) return false;

            target = Environment.ExpandEnvironmentVariables(target);
            return File.Exists(target) || Directory.Exists(target);
        }
        catch { return true; }
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
