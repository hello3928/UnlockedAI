using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

/// <summary>A yes/no question. Use <see cref="AskAsync"/>.</summary>
public sealed partial class ConfirmDialog : AppDialog
{
    private ConfirmDialog()
    {
    }

    /// <param name="confirmText">Names the action, such as "Delete". Never "OK" or "Yes".</param>
    /// <param name="isDestructive">
    /// When true, Cancel is the default button, so pressing Enter by reflex doesn't destroy anything.
    /// </param>
    /// <returns>True if the user confirmed.</returns>
    public static async Task<bool> AskAsync(
        XamlRoot root,
        string title,
        string message,
        string confirmText,
        bool isDestructive = false)
    {
        var dialog = new ConfirmDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, Style = Tokens.Style("BodyText") },
            PrimaryButtonText = confirmText,
            CloseButtonText = "Cancel",
            DefaultButton = isDestructive ? ContentDialogButton.Close : ContentDialogButton.Primary,
        };

        return await dialog.ShowAsync(root) == ContentDialogResult.Primary;
    }
}
