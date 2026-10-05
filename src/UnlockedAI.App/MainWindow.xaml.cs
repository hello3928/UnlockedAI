using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UnlockedAI.Platform;
using UnlockedAI.ViewModels;
using UnlockedAI.Views;

namespace UnlockedAI;

public sealed partial class MainWindow : Window
{
    private const int MinimumWidth = 760;
    private const int MinimumHeight = 480;

    private readonly AppHost _host;

    internal MainWindow(AppHost host)
    {
        _host = host;
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

        host.FilePicker.SetOwner(AppWindow.Id);
        Sidebar.ViewModel = host.Shell;
        Chat.ViewModel = host.Chat;

        Activated += OnActivated;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // Coming back to the app is when a model pulled in a terminal should show up.
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            await _host.Chat.RefreshModelsAsync(quiet: true);
        }
    }

    private void OnNewChatInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _host.Shell.NewChatCommand.Execute(null);
        args.Handled = true;
    }

    private async void OnSettingsRequested(object? sender, EventArgs e)
    {
        // Built when asked for and dropped when closed; nothing about settings stays in memory.
        var dialog = new SettingsDialog(
            new SettingsViewModel(_host.Settings, _host.Chat.Models, _host.Tools, _host.Secrets));
        if (await dialog.ShowAsync(Content.XamlRoot) == ContentDialogResult.Primary)
        {
            await _host.Chat.ApplySettingsAsync();
        }
    }
}
