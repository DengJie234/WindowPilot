using System.Windows;
using System.Windows.Input;

namespace WindowPilot.Views.Tray;

public partial class TrayFlyout : Window
{
    public TrayFlyout()
    {
        InitializeComponent();
    }

    public bool CloseOnDeactivate { get; set; } = true;

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (CloseOnDeactivate)
        {
            Close();
        }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
}
