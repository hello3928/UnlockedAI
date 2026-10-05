using CommunityToolkit.Mvvm.ComponentModel;
using UnlockedAI.Core.Models;

namespace UnlockedAI.ViewModels;

/// <summary>One user or assistant message as shown in the chat.</summary>
public sealed partial class MessageViewModel : ObservableObject
{
    public MessageViewModel(ChatRole role, string text = "")
    {
        Role = role;
        Text = text;
    }

    public MessageViewModel(ChatMessage message)
        : this(message.Role, message.Content)
    {
        Attachments = message.Attachments;
    }

    public ChatRole Role { get; }

    public bool IsUser => Role == ChatRole.User;

    public string RoleLabel => IsUser ? "You" : "Assistant";

    public IReadOnlyList<Attachment> Attachments { get; } = [];

    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>True while the model is still writing this message.</summary>
    [ObservableProperty]
    public partial bool IsStreaming { get; set; }
}
