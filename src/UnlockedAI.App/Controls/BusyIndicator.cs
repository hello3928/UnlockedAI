using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

public enum BusySize
{
    Small,
    Large,
}

/// <summary>A progress ring at one of the two standard sizes.</summary>
public sealed partial class BusyIndicator : ProgressRing
{
    public static readonly DependencyProperty SizeProperty =
        Dp.Register<BusyIndicator, BusySize>(nameof(Size), BusySize.Small, (indicator, _) => indicator.ApplySize());

    public BusyIndicator()
    {
        ApplySize();
    }

    public BusySize Size
    {
        get => (BusySize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private void ApplySize()
    {
        var size = Tokens.Number(Size == BusySize.Small ? "IconSize" : "IconSizeLarge");
        Width = size;
        Height = size;
        MinWidth = size;
        MinHeight = size;
    }
}
