using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

// Disambiguate types that also exist in System.Drawing (globally imported via WinForms implicit usings)
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

// Disambiguate types ambiguous between WPF and WinForms (both available via implicit usings)
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace VirtualDesktopPanel;

public partial class MainWindow : Window
{
    private readonly DesktopScanner _scanner;
    private bool _suppressDeactivated;

    public MainWindow()
    {
        InitializeComponent();
        _scanner = new DesktopScanner();

        _scanner.IconAdded += OnIconAdded;
        _scanner.IconRemoved += OnIconRemoved;
        _scanner.IconRenamed += OnIconRenamed;

        IconGrid.IconMoved += OnIconMoved;
        IconGrid.IconDoubleClicked += OnIconDoubleClicked;
    }

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
        var color = (WpfColor)WpfColorConverter.ConvertFromString(s.BackgroundColor);
        color.A = (byte)(s.BackgroundOpacity * 255);
        PanelBackgroundBrush.Color = color;

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
        DragMove();
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
        var existingPaths = new List<string>();
        foreach (var i in _scanner.Icons)
            existingPaths.Add(i.FilePath);

        var positions = LayoutManager.MergeWithDisk(existingPaths);
        if (positions.TryGetValue(icon.FilePath, out var pos))
        {
            icon.Row = pos.Row;
            icon.Col = pos.Col;
        }
        IconGrid.AddIcon(icon);
        IconGrid.HideEmptyMessage();
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
