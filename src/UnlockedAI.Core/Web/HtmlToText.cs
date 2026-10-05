using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace UnlockedAI.Core.Web;

/// <summary>
/// Reduces a web page to its readable text: page furniture (menus, scripts, forms) is dropped,
/// and headings, paragraphs and list items each start on their own line.
/// </summary>
public static class HtmlToText
{
    private const string Clutter = "script, style, noscript, template, svg, canvas, iframe, nav, header, footer, aside, form, button, [hidden], [aria-hidden=true]";

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "section", "article", "main", "br", "tr", "table", "ul", "ol", "li",
        "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "pre", "dt", "dd", "figcaption", "hr",
    };

    public static (string Title, string Text) Convert(string html)
    {
        using var document = new HtmlParser().ParseDocument(html);
        var title = Collapse(document.Title ?? "");

        foreach (var element in document.QuerySelectorAll(Clutter))
        {
            element.Remove();
        }

        // Prefer the part the page itself marks as its content.
        INode? root = document.QuerySelector("main, article, [role=main]") ?? (INode?)document.Body;
        if (root is null)
        {
            return (title, "");
        }

        var builder = new StringBuilder();
        Append(root, builder);
        return (title, TidyLines(builder));
    }

    private static void Append(INode node, StringBuilder builder)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case IText text:
                    builder.Append(text.Data);
                    break;

                case IElement element:
                    var isBlock = BlockTags.Contains(element.LocalName);
                    if (isBlock)
                    {
                        builder.Append('\n');
                    }

                    if (element.LocalName == "li")
                    {
                        builder.Append("- ");
                    }
                    else if (element.LocalName is ['h', >= '1' and <= '6'])
                    {
                        builder.Append('#', element.LocalName[1] - '0').Append(' ');
                    }

                    Append(element, builder);

                    if (isBlock)
                    {
                        builder.Append('\n');
                    }
                    else if (element.LocalName is "td" or "th")
                    {
                        builder.Append(" | ");
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Collapses runs of spaces within a line and drops blank lines. One block per line keeps the
    /// text compact, which matters because every character counts against the model's context.
    /// </summary>
    private static string TidyLines(StringBuilder raw)
    {
        var result = new StringBuilder(raw.Length);

        foreach (var line in raw.ToString().Split('\n'))
        {
            var tidy = Collapse(line);
            if (tidy.Length == 0)
            {
                continue;
            }

            if (result.Length > 0)
            {
                result.Append('\n');
            }

            result.Append(tidy);
        }

        return result.ToString();
    }

    private static string Collapse(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
