using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using UnlockedAI.Controls;

namespace UnlockedAI.Markdown;

/// <summary>
/// The design tokens the markdown renderer uses, read once. Rendering a long reply creates
/// thousands of elements, and a resource lookup for each one would be wasted work.
/// </summary>
internal sealed class MarkdownTheme
{
    private static MarkdownTheme? _current;

    private MarkdownTheme()
    {
    }

    public static MarkdownTheme Current => _current ??= new MarkdownTheme();

    public double BodySize { get; } = Tokens.Number("FontSizeBody");

    public double BodyLineHeight { get; } = Tokens.Number("LineHeightBody");

    public double Heading1Size { get; } = Tokens.Number("FontSizeSubtitle");

    public double Heading2Size { get; } = Tokens.Number("FontSizeHeading2");

    public double Heading3Size { get; } = Tokens.Number("FontSizeHeading3");

    public double MonoSize { get; } = Tokens.Number("FontSizeMono");

    public FontFamily MonoFont { get; } = Tokens.Font("FontFamilyMono");

    public double TightSpacing { get; } = Tokens.Number("Space1");

    public double BlockSpacing { get; } = Tokens.Number("Space2");

    public double ListMarkerWidth { get; } = Tokens.Number("ListMarkerWidth");

    public double RuleHeight { get; } = Tokens.Number("RuleHeight");

    public Thickness ParagraphGap { get; } = Tokens.Inset("InsetParagraphGap");

    public Thickness HeadingGap { get; } = Tokens.Inset("InsetHeadingGap");

    public Thickness QuoteStroke { get; } = Tokens.Inset("StrokeQuote");

    public Thickness QuoteInset { get; } = Tokens.Inset("InsetQuote");

    public Thickness CellInset { get; } = Tokens.Inset("InsetCell");

    public Thickness CellStroke { get; } = Tokens.Inset("StrokeThin");

    public CornerRadius TableRadius { get; } = Tokens.Radius("RadiusControl");

    public Brush Text { get; } = Tokens.Brush("TextPrimaryBrush");

    public Brush MutedText { get; } = Tokens.Brush("TextSecondaryBrush");

    public Brush Divider { get; } = Tokens.Brush("DividerBrush");

    public Brush HeaderFill { get; } = Tokens.Brush("CardBrush");
}
