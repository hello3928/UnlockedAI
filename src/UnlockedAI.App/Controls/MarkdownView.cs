using Markdig.Syntax;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnlockedAI.Markdown;
using UnlockedAI.Platform;

namespace UnlockedAI.Controls;

/// <summary>
/// Shows markdown as native text, lists, tables and code blocks.
/// <para>
/// Built for text that grows: when <see cref="Text"/> changes, blocks whose source is unchanged keep
/// their elements and only the rest is rebuilt. While a reply streams in, that is just the last block.
/// </para>
/// </summary>
public sealed partial class MarkdownView : StackPanel
{
    public static readonly DependencyProperty TextProperty =
        Dp.Register<MarkdownView, string>(nameof(Text), "", (view, _) => view.Render());

    public static readonly DependencyProperty IsPlainTextProperty =
        Dp.Register<MarkdownView, bool>(nameof(IsPlainText), false, (view, _) => view.Reset());

    // One entry per top-level markdown block currently on screen, in order.
    private readonly List<Entry> _entries = [];

    // The text those entries were rendered from.
    private string _renderedText = "";

    // Set when rendering threw: the text it failed on.
    private string? _failedText;

    public MarkdownView()
    {
        Spacing = Tokens.Number("Space2");
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Shows the text exactly as written. For what the user typed, where a stray * or # means itself.</summary>
    public bool IsPlainText
    {
        get => (bool)GetValue(IsPlainTextProperty);
        set => SetValue(IsPlainTextProperty, value);
    }

    private void Reset()
    {
        Children.Clear();
        _entries.Clear();
        _renderedText = "";
        _failedText = null;
        Render();
    }

    private void Render()
    {
        var text = Text ?? "";

        // Still the same reply that failed to render, only longer: keep it plain instead of failing again.
        var alreadyFailed = _failedText is not null && text.StartsWith(_failedText, StringComparison.Ordinal);
        if (IsPlainText || alreadyFailed)
        {
            ShowPlain(text);
            return;
        }

        _failedText = null;
        try
        {
            RenderMarkdown(text);
        }
        catch (Exception exception)
        {
            // Half-written markdown from a model is unpredictable input. If rendering it ever
            // fails, the reader still gets the text; the app must not go down over formatting.
            CrashLog.Write(exception);
            _failedText = text;
            _entries.Clear();
            _renderedText = "";
            ShowPlain(text);
        }
    }

    private void ShowPlain(string text)
    {
        Children.Clear();
        if (text.Length > 0)
        {
            MarkdownRenderer.AppendPlainText(this, text);
        }
    }

    private void RenderMarkdown(string text)
    {
        // After a fallback to plain text the entries are empty but the plain paragraph is still on screen.
        if (_entries.Count == 0)
        {
            Children.Clear();
        }

        if (_entries.Count > 0 && string.Equals(text, _renderedText, StringComparison.Ordinal))
        {
            return;
        }

        var document = MarkdownRenderer.Parse(text);

        // The last block is never counted as unchanged: it is the one still being written, and the
        // parser doesn't report a reliable source range for a block that hasn't been closed yet.
        var unchanged = 0;
        while (unchanged < _entries.Count
            && unchanged < document.Count - 1
            && IsUnchanged(_entries[unchanged], document[unchanged], text))
        {
            unchanged++;
        }

        // A code block that is still being written is updated in place, so it doesn't flicker
        // or lose its scroll position on every new line.
        if (unchanged == _entries.Count - 1
            && unchanged < document.Count
            && _entries[unchanged].IsCode
            && document[unchanged] is Markdig.Syntax.CodeBlock code
            && Children[^1] is CodeBlock control)
        {
            MarkdownRenderer.FillCode(control, code);
            _entries[unchanged] = Entry.For(code, RenderedKind.Element);
            unchanged++;
        }

        RemoveFrom(unchanged);

        for (var index = unchanged; index < document.Count; index++)
        {
            var block = document[index];
            _entries.Add(Entry.For(block, MarkdownRenderer.Append(this, block)));
        }

        _renderedText = text;
    }

    private bool IsUnchanged(Entry entry, Block block, string text)
    {
        // A block without a source range can't be compared, so it always counts as changed.
        if (entry.Length <= 0 || entry.Start != block.Span.Start || entry.Length != block.Span.Length)
        {
            return false;
        }

        // The parser can report a range that runs past the end of unfinished text.
        var end = entry.Start + entry.Length;
        if (end > _renderedText.Length || end > text.Length)
        {
            return false;
        }

        return _renderedText.AsSpan(entry.Start, entry.Length).SequenceEqual(text.AsSpan(entry.Start, entry.Length));
    }

    /// <summary>Takes the elements of every entry from <paramref name="first"/> onwards off the screen, last first.</summary>
    private void RemoveFrom(int first)
    {
        for (var index = _entries.Count - 1; index >= first; index--)
        {
            switch (_entries[index].Kind)
            {
                case RenderedKind.Element:
                    Children.RemoveAt(Children.Count - 1);
                    break;

                case RenderedKind.Paragraph when Children[^1] is RichTextBlock host:
                    host.Blocks.RemoveAt(host.Blocks.Count - 1);
                    if (host.Blocks.Count == 0)
                    {
                        Children.RemoveAt(Children.Count - 1);
                    }

                    break;
            }
        }

        _entries.RemoveRange(first, _entries.Count - first);
    }

    /// <param name="Start">Where the block's source starts in the rendered text.</param>
    private readonly record struct Entry(int Start, int Length, RenderedKind Kind, bool IsCode)
    {
        public static Entry For(Block block, RenderedKind kind) =>
            new(block.Span.Start, block.Span.Length, kind, block is Markdig.Syntax.CodeBlock);
    }
}
