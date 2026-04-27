using System.Windows;
using WindowPilot.Services;

namespace WindowPilot;

public partial class App : System.Windows.Application
{
    private SingleInstanceService? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceService("WindowPilot.SingleInstance");
        if (!_singleInstance.TryAcquire())
        {
            System.Windows.MessageBox.Show("WindowPilot 已在运行。", "WindowPilot", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
