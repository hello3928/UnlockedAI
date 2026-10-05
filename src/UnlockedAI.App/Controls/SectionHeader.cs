using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

/// <summary>Heading for a group of content. Announced as a heading by screen readers.</summary>
public sealed partial class SectionHeader : Control
{
    public static readonly DependencyProperty TextProperty =
        Dp.Register<SectionHeader, string>(nameof(Text), "");

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
