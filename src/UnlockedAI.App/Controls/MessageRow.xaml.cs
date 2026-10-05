using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnlockedAI.ViewModels;

namespace UnlockedAI.Controls;

/// <summary>One message in the chat: who wrote it, the text, and a copy button.</summary>
public sealed partial class MessageRow : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        Dp.Register<MessageRow, MessageViewModel?>(nameof(Message), null, (row, _) => row.ApplyRole());

    public MessageRow()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyRole();
    }

    public MessageViewModel? Message
    {
        get => (MessageViewModel?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    private void ApplyRole() =>
        VisualStateManager.GoToState(this, Message is { IsUser: true } ? "User" : "Assistant", useTransitions: false);
}
