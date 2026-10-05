using UnlockedAI.Core.Files;
using UnlockedAI.Core.Models;

namespace UnlockedAI.ViewModels;

/// <summary>An attached file as a chip shows it: its name and size. Used both before and after sending.</summary>
public sealed class AttachmentViewModel
{
    /// <summary>A file that has been read and is waiting to be sent.</summary>
    public AttachmentViewModel(NewAttachment pending)
        : this(pending.FileName, pending.SizeBytes, pending.Truncated)
    {
        Pending = pending;
    }

    /// <summary>A file that was sent with a saved message.</summary>
    public AttachmentViewModel(Attachment saved)
        : this(saved.FileName, saved.SizeBytes, saved.Truncated)
    {
    }

    private AttachmentViewModel(string name, long sizeBytes, bool truncated)
    {
        Name = name;
        Detail = truncated ? $"{FileSize.Format(sizeBytes)}, first part only" : FileSize.Format(sizeBytes);
    }

    public string Name { get; }

    public string Detail { get; }

    /// <summary>The extracted text, held only until the message is sent.</summary>
    public NewAttachment? Pending { get; }

    public override string ToString() => Name;
}
