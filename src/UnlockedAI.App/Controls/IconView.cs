using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

/// <summary>Shows one <see cref="AppIcon"/> at the standard icon size.</summary>
public sealed partial class IconView : FontIcon
{
    public static readonly DependencyProperty IconProperty =
        Dp.Register<IconView, AppIcon>(nameof(Icon), AppIcon.None, (view, icon) => view.Glyph = icon.ToGlyph());

    public IconView()
    {
        FontSize = Tokens.Number("IconSize");
    }

    public AppIcon Icon
    {
        get => (AppIcon)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
