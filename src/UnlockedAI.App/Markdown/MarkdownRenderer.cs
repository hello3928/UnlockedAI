using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;
using CodeBlockControl = UnlockedAI.Controls.CodeBlock;
using Doc = Microsoft.UI.Xaml.Documents;

namespace UnlockedAI.Markdown;

/// <summary>What <see cref="MarkdownRenderer.Append"/> added to a panel for one markdown block.</summary>
internal enum RenderedKind
{
    /// <summary>Nothing visible, such as a link reference definition.</summary>
    None,

    /// <summary>One paragraph, placed in the text block at the end of the panel.</summary>
    Paragraph,

    /// <summary>One element of its own at the end of the panel.</summary>
    Element,
}

/// <summary>
/// Turns markdown into native WinUI elements. Text that flows (paragraphs, headings) shares a
/// <see cref="RichTextBlock"/> so it can be selected together; code, lists, quotes and tables get their own element.
/// </summary>
internal static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseAutoLinks()
        // Models often put one item per line without list syntax; keep those lines apart.
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    public static MarkdownDocument Parse(string text) => Markdig.Markdown.Parse(text, Pipeline);

    /// <summary>Renders one block at the end of <paramref name="panel"/>.</summary>
    public static RenderedKind Append(Panel panel, Block block)
    {
        var theme = MarkdownTheme.Current;

        switch (block)
        {
            case HeadingBlock heading:
                AppendParagraph(panel, CreateHeading(heading, theme), theme.HeadingGap);
                return RenderedKind.Paragraph;

            case ParagraphBlock paragraph:
                var text = new Doc.Paragraph();
                AddInlines(text.Inlines, paragraph.Inline, theme);
                AppendParagraph(panel, text, theme.ParagraphGap);
                return RenderedKind.Paragraph;

            case HtmlBlock html:
                AppendParagraph(panel, CreateLiteral(html.Lines.ToString()), theme.ParagraphGap);
                return RenderedKind.Paragraph;

            case CodeBlock code:
                panel.Children.Add(FillCode(new CodeBlockControl(), code));
                return RenderedKind.Element;

            case ListBlock list:
                panel.Children.Add(CreateList(list, theme));
                return RenderedKind.Element;

            case QuoteBlock quote:
                panel.Children.Add(CreateQuote(quote, theme));
                return RenderedKind.Element;

            case Table table:
                panel.Children.Add(CreateTable(table, theme));
                return RenderedKind.Element;

            case ThematicBreakBlock:
                panel.Children.Add(new Border { Height = theme.RuleHeight, Background = theme.Divider });
                return RenderedKind.Element;

            default:
                return RenderedKind.None;
        }
    }

    /// <summary>Puts a markdown code block's text and language into a code block control.</summary>
    public static CodeBlockControl FillCode(CodeBlockControl control, CodeBlock code)
    {
        control.Code = code.Lines.ToString();
        control.LanguageName = (code as FencedCodeBlock)?.Info ?? "";
        return control;
    }

    /// <summary>A single paragraph of text shown exactly as written, with no markdown applied.</summary>
    public static void AppendPlainText(Panel panel, string text) =>
        AppendParagraph(panel, CreateLiteral(text), MarkdownTheme.Current.ParagraphGap);

    private static void AppendParagraph(Panel panel, Doc.Paragraph paragraph, Thickness gapAbove)
    {
        if (panel.Children.Count > 0 && panel.Children[^1] is RichTextBlock host)
        {
            paragraph.Margin = gapAbove;
        }
        else
        {
            var theme = MarkdownTheme.Current;
            host = new RichTextBlock
            {
                FontSize = theme.BodySize,
                LineHeight = theme.BodyLineHeight,
                Foreground = theme.Text,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            };
            panel.Children.Add(host);
        }

        host.Blocks.Add(paragraph);
    }

    private static StackPanel CreatePanel(ContainerBlock container, MarkdownTheme theme)
    {
        var panel = new StackPanel { Spacing = theme.BlockSpacing };
        foreach (var child in container)
        {
            Append(panel, child);
        }

        return panel;
    }

    private static Doc.Paragraph CreateLiteral(string text)
    {
        var paragraph = new Doc.Paragraph();
        paragraph.Inlines.Add(new Doc.Run { Text = text });
        return paragraph;
    }

    private static Doc.Paragraph CreateHeading(HeadingBlock heading, MarkdownTheme theme)
    {
        var paragraph = new Doc.Paragraph
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = heading.Level switch
            {
                1 => theme.Heading1Size,
                2 => theme.Heading2Size,
                3 => theme.Heading3Size,
                _ => theme.BodySize,
            },
        };
        AddInlines(paragraph.Inlines, heading.Inline, theme);
        return paragraph;
    }

    private static Grid CreateList(ListBlock list, MarkdownTheme theme)
    {
        var grid = new Grid { RowSpacing = theme.TightSpacing, ColumnSpacing = theme.TightSpacing };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = theme.ListMarkerWidth });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var number = list.IsOrdered && int.TryParse(list.OrderedStart, out var start) ? start : 1;
        var row = 0;

        foreach (var child in list)
        {
            if (child is not ListItemBlock item)
            {
                continue;
            }

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var marker = new TextBlock
            {
                Text = list.IsOrdered ? $"{number++}." : "•",
                FontSize = theme.BodySize,
                LineHeight = theme.BodyLineHeight,
                Foreground = theme.MutedText,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Grid.SetRow(marker, row);
            grid.Children.Add(marker);

            var content = CreatePanel(item, theme);
            Grid.SetRow(content, row);
            Grid.SetColumn(content, 1);
            grid.Children.Add(content);

            row++;
        }

        return grid;
    }

    private static Border CreateQuote(QuoteBlock quote, MarkdownTheme theme) => new()
    {
        BorderBrush = theme.Divider,
        BorderThickness = theme.QuoteStroke,
        Padding = theme.QuoteInset,
        Child = CreatePanel(quote, theme),
    };

    private static Border CreateTable(Table table, MarkdownTheme theme)
    {
        var grid = new Grid();
        var columns = table.ColumnDefinitions.Count > 0 ? table.ColumnDefinitions.Count : 1;
        for (var column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var rowIndex = 0;
        foreach (var rowBlock in table)
        {
            if (rowBlock is not TableRow row)
            {
                continue;
            }

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (var column = 0; column < row.Count && column < columns; column++)
            {
                var text = new TextBlock
                {
                    FontSize = theme.BodySize,
                    LineHeight = theme.BodyLineHeight,
                    Foreground = theme.Text,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    FontWeight = row.IsHeader ? FontWeights.SemiBold : FontWeights.Normal,
                };

                if (row[column] is TableCell cell)
                {
                    foreach (var cellBlock in cell)
                    {
                        AddInlines(text.Inlines, (cellBlock as LeafBlock)?.Inline, theme);
                    }
                }

                // Lines between cells only: the outer border is drawn by the wrapper.
                var frame = new Border
                {
                    Padding = theme.CellInset,
                    Background = row.IsHeader ? theme.HeaderFill : null,
                    BorderBrush = theme.Divider,
                    BorderThickness = new Thickness(
                        column == 0 ? 0 : theme.CellStroke.Left,
                        rowIndex == 0 ? 0 : theme.CellStroke.Top,
                        0,
                        0),
                    Child = text,
                };
                Grid.SetRow(frame, rowIndex);
                Grid.SetColumn(frame, column);
                grid.Children.Add(frame);
            }

            rowIndex++;
        }

        return new Border
        {
            BorderBrush = theme.Divider,
            BorderThickness = theme.CellStroke,
            CornerRadius = theme.TableRadius,
            Child = grid,
        };
    }

    private static void AddInlines(Doc.InlineCollection target, ContainerInline? container, MarkdownTheme theme)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            AddInline(target, inline, theme);
        }
    }

    private static void AddInline(Doc.InlineCollection target, Inline inline, MarkdownTheme theme)
    {
        switch (inline)
        {
            case LiteralInline literal:
                target.Add(new Doc.Run { Text = literal.Content.ToString() });
                break;

            case CodeInline code:
                target.Add(new Doc.Run
                {
                    Text = code.Content,
                    FontFamily = theme.MonoFont,
                    FontSize = theme.MonoSize,
                    FontWeight = FontWeights.SemiLight,
                });
                break;

            case LineBreakInline:
                target.Add(new Doc.LineBreak());
                break;

            case EmphasisInline emphasis:
                Doc.Span span = emphasis switch
                {
                    { DelimiterChar: '~' } => new Doc.Span { TextDecorations = TextDecorations.Strikethrough },
                    { DelimiterCount: >= 2 } => new Doc.Bold(),
                    _ => new Doc.Italic(),
                };
                AddInlines(span.Inlines, emphasis, theme);
                target.Add(span);
                break;

            case LinkInline link:
                AddLink(target, link, theme);
                break;

            case AutolinkInline autolink:
                AddHyperlink(target, autolink.IsEmail ? $"mailto:{autolink.Url}" : autolink.Url, autolink.Url);
                break;

            case HtmlEntityInline entity:
                target.Add(new Doc.Run { Text = entity.Transcoded.ToString() });
                break;

            case HtmlInline html:
                target.Add(new Doc.Run { Text = html.Tag });
                break;

            case ContainerInline other:
                AddInlines(target, other, theme);
                break;
        }
    }

    private static void AddLink(Doc.InlineCollection target, LinkInline link, MarkdownTheme theme)
    {
        var url = link.GetDynamicUrl?.Invoke() ?? link.Url;

        // Remote images are not downloaded; the reader gets a link and decides.
        if (link.IsImage)
        {
            var alt = link.FirstChild is LiteralInline literal ? literal.Content.ToString() : "";
            AddHyperlink(target, url, alt.Length > 0 ? $"Image: {alt}" : "Image");
            return;
        }

        if (!TryCreateWebUri(url, out var uri))
        {
            // Not something safe to open, so show the text without making it clickable.
            AddInlines(target, link, theme);
            return;
        }

        var hyperlink = new Doc.Hyperlink { NavigateUri = uri };
        AddInlines(hyperlink.Inlines, link, theme);
        if (hyperlink.Inlines.Count == 0)
        {
            hyperlink.Inlines.Add(new Doc.Run { Text = uri.AbsoluteUri });
        }

        // The label is the model's wording; the tooltip shows where the link really goes.
        ToolTipService.SetToolTip(hyperlink, uri.AbsoluteUri);
        target.Add(hyperlink);
    }

    private static void AddHyperlink(Doc.InlineCollection target, string? url, string label)
    {
        if (!TryCreateWebUri(url, out var uri))
        {
            target.Add(new Doc.Run { Text = label });
            return;
        }

        var hyperlink = new Doc.Hyperlink { NavigateUri = uri };
        hyperlink.Inlines.Add(new Doc.Run { Text = label });
        ToolTipService.SetToolTip(hyperlink, uri.AbsoluteUri);
        target.Add(hyperlink);
    }

    /// <summary>Only web and mail links become clickable. File paths and other schemes stay as text.</summary>
    private static bool TryCreateWebUri(string? url, out Uri uri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeMailto))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }
}
