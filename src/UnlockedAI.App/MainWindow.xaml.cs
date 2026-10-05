using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using UnlockedAI.Platform;

namespace UnlockedAI;

public sealed partial class MainWindow : Window
{
    private const int MinimumWidth = 560;
    private const int MinimumHeight = 420;

    internal MainWindow(AppHost host)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // The app is dark only, so the caption buttons must be light even when Windows is in light mode.
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.Dark;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinimumWidth;
            presenter.PreferredMinimumHeight = MinimumHeight;
        }

        Chat.ViewModel = host.Chat;
    }
}
