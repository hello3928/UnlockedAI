using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

/// <summary>Base for every dialog in the app, so they all share the same style and are shown the same way.</summary>
public partial class AppDialog : ContentDialog
{
    public AppDialog()
    {
        Style = Tokens.Style("DefaultContentDialogStyle");
        RequestedTheme = ElementTheme.Dark;
        DefaultButton = ContentDialogButton.Primary;
    }

    // WinUI allows one dialog at a time and throws if a second is shown, which used to close the
    // whole app when a button that opens a dialog was double-clicked. Only touched on the UI thread.
    private static bool _isDialogOpen;

    /// <summary>
    /// Shows the dialog over the window that <paramref name="root"/> belongs to.
    /// If another dialog is already open, does nothing and reports that this one was dismissed.
    /// </summary>
    public async Task<ContentDialogResult> ShowAsync(XamlRoot root)
    {
        if (_isDialogOpen)
        {
            return ContentDialogResult.None;
        }

        _isDialogOpen = true;
        try
        {
            XamlRoot = root;
            return await ShowAsync();
        }
        finally
        {
            _isDialogOpen = false;
        }
    }
}
