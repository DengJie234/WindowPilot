using System.Windows;
using WindowPilot.Models;

namespace WindowPilot.Views.Dialogs;

public partial class ExitConfirmDialog : Window
{
    public ExitConfirmDialog()
    {
        InitializeComponent();
    }

    public ExitConfirmResult Result { get; private set; } = ExitConfirmResult.Cancel;

    private void RestoreAndExit_Click(object sender, RoutedEventArgs e)
    {
        Result = ExitConfirmResult.RestoreAndExit;
        DialogResult = true;
    }

    private void ExitDirectly_Click(object sender, RoutedEventArgs e)
    {
        Result = ExitConfirmResult.ExitDirectly;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = ExitConfirmResult.Cancel;
        DialogResult = false;
    }
}
