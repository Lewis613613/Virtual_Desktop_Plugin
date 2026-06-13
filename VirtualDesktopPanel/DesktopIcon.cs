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
