using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

/// <summary>Asks for one line of text. Use <see cref="AskAsync"/>.</summary>
public sealed partial class PromptDialog : AppDialog
{
    private const int MaxLength = 120;

    private PromptDialog()
    {
    }

    /// <returns>The trimmed text, or null if the user cancelled.</returns>
    public static async Task<string?> AskAsync(
        XamlRoot root,
        string title,
        string label,
        string initialText,
        string confirmText)
    {
        var input = new TextBox { Text = initialText, MaxLength = MaxLength };
        input.SelectAll();

        var dialog = new PromptDialog
        {
            Title = title,
            Content = new FormRow { Label = label, Content = input },
            PrimaryButtonText = confirmText,
            CloseButtonText = "Cancel",
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(initialText),
        };
        input.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(input.Text);

        return await dialog.ShowAsync(root) == ContentDialogResult.Primary ? input.Text.Trim() : null;
    }
}
