using System.Threading;
using System.Windows;

namespace ScreenAnnotationTool;

public partial class App : Application
{
    private AppController? _controller;
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "ScreenAnnotationTool_SingleInstance_9f1c", out bool createdNew);
        if (!createdNew)
        {
            // Another instance already owns the global hotkeys; exit quietly.
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _controller = new AppController();
        _controller.Start();
    }
}
