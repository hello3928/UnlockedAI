using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnlockedAI.ViewModels;

namespace UnlockedAI.Views;

/// <summary>Picks the row template for an item in the chat: a message or a tool call.</summary>
public sealed partial class ChatItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Message { get; set; }

    public DataTemplate? ToolCall { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) =>
        item is ToolCallViewModel ? ToolCall : Message;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) =>
        SelectTemplateCore(item);
}
