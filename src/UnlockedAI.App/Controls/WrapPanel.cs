using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace UnlockedAI.Controls;

/// <summary>Lays children out left to right and starts a new row when the next one doesn't fit.</summary>
public sealed partial class WrapPanel : Panel
{
    public static readonly DependencyProperty SpacingProperty =
        Dp.Register<WrapPanel, double>(nameof(Spacing), 0d, (panel, _) => panel.InvalidateMeasure());

    /// <summary>Gap between children, both along a row and between rows.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }

        return Layout(availableSize.Width, arrange: false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width, arrange: true);
        return finalSize;
    }

    private Size Layout(double rowWidth, bool arrange)
    {
        double x = 0, y = 0, rowHeight = 0, widest = 0;

        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (size.Width == 0 && size.Height == 0)
            {
                continue;
            }

            if (x > 0 && x + size.Width > rowWidth)
            {
                x = 0;
                y += rowHeight + Spacing;
                rowHeight = 0;
            }

            if (arrange)
            {
                child.Arrange(new Rect(x, y, size.Width, size.Height));
            }

            widest = Math.Max(widest, x + size.Width);
            rowHeight = Math.Max(rowHeight, size.Height);
            x += size.Width + Spacing;
        }

        return new Size(widest, y + rowHeight);
    }
}
