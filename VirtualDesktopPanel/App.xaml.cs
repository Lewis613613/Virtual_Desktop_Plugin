using System.Windows;
using Application = System.Windows.Application;

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
