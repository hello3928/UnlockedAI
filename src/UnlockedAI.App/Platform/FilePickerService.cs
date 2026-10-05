using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;

namespace UnlockedAI.Platform;

/// <summary>Shows the Windows "open file" dialog over the app's window.</summary>
public sealed class FilePickerService
{
    private WindowId? _owner;

    /// <summary>Tells the picker which window to appear over. Called once the window exists.</summary>
    public void SetOwner(WindowId owner) => _owner = owner;

    /// <returns>The chosen file paths; empty if the user cancelled.</returns>
    public async Task<IReadOnlyList<string>> PickFilesAsync()
    {
        if (_owner is not { } owner)
        {
            return [];
        }

        var picker = new FileOpenPicker(owner) { CommitButtonText = "Attach" };
        picker.FileTypeFilter.Add("*");

        var picked = await picker.PickMultipleFilesAsync();
        return picked.Select(file => file.Path).ToList();
    }
}
