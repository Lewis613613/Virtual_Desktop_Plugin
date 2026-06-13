using System.Windows;
using System.Windows.Input;

// Disambiguate types ambiguous between WPF and WinForms (both available via implicit usings)
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace VirtualDesktopPanel;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
