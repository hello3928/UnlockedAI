using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

/// <summary>A block of code in monospace with its language name and a copy button.</summary>
public sealed partial class CodeBlock : UserControl
{
    public static readonly DependencyProperty CodeProperty =
        Dp.Register<CodeBlock, string>(nameof(Code), "");

    public static readonly DependencyProperty LanguageNameProperty =
        Dp.Register<CodeBlock, string>(nameof(LanguageName), "");

    public CodeBlock()
    {
        InitializeComponent();
    }

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <summary>Shown as a small label above the code, for example "python". May be empty.</summary>
    public string LanguageName
    {
        get => (string)GetValue(LanguageNameProperty);
        set => SetValue(LanguageNameProperty, value);
    }
}
