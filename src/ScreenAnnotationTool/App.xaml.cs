using System.Windows;

namespace ScreenAnnotationTool;

public partial class App : Application
{
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _controller = new AppController();
        _controller.Start();
    }
}
