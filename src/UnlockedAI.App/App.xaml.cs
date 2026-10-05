using Microsoft.UI.Xaml;
using UnlockedAI.Controls;
using UnlockedAI.Core.Errors;
using UnlockedAI.Platform;

namespace UnlockedAI;

public partial class App : Application
{
    private AppHost? _host;
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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _host = new AppHost();
            await _host.InitializeAsync();
            _window = new MainWindow(_host);
        }
        catch (Exception exception)
        {
            // Usually the database file can't be opened. Say so instead of closing silently.
            CrashLog.Write(exception);
            _window = new Window
            {
                Title = "UnlockedAI",
                Content = new ErrorMessage
                {
                    Margin = (Thickness)Resources["Inset6"],
                    VerticalAlignment = VerticalAlignment.Top,
                    IsDismissible = false,
                    Error = ErrorMapper.Map(exception),
                },
            };
        }

        _window.Closed += (_, _) => _host?.Dispose();
        _window.Activate();
    }
}
