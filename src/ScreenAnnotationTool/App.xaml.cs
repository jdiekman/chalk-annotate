using System.Threading;
using System.Windows;

namespace ScreenAnnotationTool;

public partial class App : Application
{
    private AppController? _controller;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "Chalk_SingleInstance_9f1c", out _ownsSingleInstanceMutex);
        if (!_ownsSingleInstanceMutex)
        {
            // Another instance already owns the global hotkeys; exit quietly.
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            _controller = new AppController();
            _controller.Start();
        }
        catch (Exception ex)
        {
            _controller?.Shutdown();
            Dialogs.Show("Chalk couldn't start safely:\n" + ex.Message);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Shutdown();

        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
            _ownsSingleInstanceMutex = false;
        }
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;

        base.OnExit(e);
    }
}
