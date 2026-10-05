using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

/// <summary>A saved chat's row in the sidebar: one line of title, cut with an ellipsis, full title on hover.</summary>
public sealed partial class ConversationListItem : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        Dp.Register<ConversationListItem, string>(nameof(Title), "");

    public ConversationListItem()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
