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

        var settingsItem = new ToolStripMenuItem("Settings");
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

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _mainWindow?.Close();
            System.Windows.Application.Current.Shutdown();
        };

        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        // Pre-load icons so they're ready (don't show panel yet)
        _mainWindow.LoadIcons();
    }

    private static Icon CreateTrayIcon()
    {
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);

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
