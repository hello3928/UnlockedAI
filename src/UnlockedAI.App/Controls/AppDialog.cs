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

    /// <summary>Shows the dialog over the window that <paramref name="root"/> belongs to.</summary>
    public async Task<ContentDialogResult> ShowAsync(XamlRoot root)
    {
        XamlRoot = root;
        return await ShowAsync();
    }
}
