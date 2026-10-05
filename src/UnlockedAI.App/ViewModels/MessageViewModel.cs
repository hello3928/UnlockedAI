using CommunityToolkit.Mvvm.ComponentModel;
using UnlockedAI.Core.Models;

namespace UnlockedAI.ViewModels;

/// <summary>One user or assistant message as shown in the chat.</summary>
public sealed partial class MessageViewModel : ChatItemViewModel
{
    public MessageViewModel(ChatRole role, string text = "")
    {
        Role = role;
        Text = text;
    }

    public MessageViewModel(ChatMessage message)
        : this(message.Role, message.Content)
    {
        Attachments = [.. message.Attachments.Select(attachment => new AttachmentViewModel(attachment))];
    }

    public ChatRole Role { get; }

    public bool IsUser => Role == ChatRole.User;

    public string RoleLabel => IsUser ? "You" : "Assistant";

    public IReadOnlyList<AttachmentViewModel> Attachments { get; } = [];

    public bool HasAttachments => Attachments.Count > 0;

    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>True while the model is still writing this message.</summary>
    [ObservableProperty]
    public partial bool IsStreaming { get; set; }

    /// <summary>List rows are announced by screen readers using this text.</summary>
    public override string ToString() => $"{RoleLabel}: {Text}";
}
