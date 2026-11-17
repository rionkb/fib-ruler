using System.Windows;

namespace FibRuler;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        MainWindow overlay = new();
        ControlWindow control = new(overlay);
        MainWindow = control;
        overlay.Show();
        control.Show();
    }
}

