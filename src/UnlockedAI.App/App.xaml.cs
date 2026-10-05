using Microsoft.UI.Xaml;
using UnlockedAI.Platform;

namespace UnlockedAI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        UnhandledException += (_, e) => CrashLog.Write(e.Exception);

        try
        {
            InitializeComponent();
        }
        catch (Exception exception)
        {
            // A broken resource dictionary fails here, before any window exists to show it.
            CrashLog.Write(exception);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
