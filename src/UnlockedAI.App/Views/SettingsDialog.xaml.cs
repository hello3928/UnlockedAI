using Microsoft.UI.Xaml.Controls;
using UnlockedAI.Controls;
using UnlockedAI.ViewModels;

namespace UnlockedAI.Views;

/// <summary>Edits the app's settings. Closes on Save only once the settings are actually stored.</summary>
public sealed partial class SettingsDialog : AppDialog
{
    public SettingsDialog(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    private async void OnSaveClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Hold the dialog open while saving; if that fails it stays open and shows why.
        var deferral = args.GetDeferral();
        try
        {
            args.Cancel = !await ViewModel.SaveAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }
}
