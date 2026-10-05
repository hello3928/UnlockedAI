using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

/// <summary>What a view shows when it has nothing to list: icon, title, a line of help, one optional action.</summary>
public sealed partial class EmptyState : UserControl
{
    public static readonly DependencyProperty IconProperty =
        Dp.Register<EmptyState, AppIcon>(nameof(Icon), AppIcon.None);

    public static readonly DependencyProperty TitleProperty =
        Dp.Register<EmptyState, string>(nameof(Title), "");

    public static readonly DependencyProperty MessageProperty =
        Dp.Register<EmptyState, string>(nameof(Message), "");

    public static readonly DependencyProperty ActionTextProperty =
        Dp.Register<EmptyState, string>(nameof(ActionText), "");

    public static readonly DependencyProperty ActionCommandProperty =
        Dp.Register<EmptyState, ICommand?>(nameof(ActionCommand), null);

    public EmptyState()
    {
        InitializeComponent();
    }

    public AppIcon Icon
    {
        get => (AppIcon)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Label of the action button. Empty hides the button.</summary>
    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    // Instance method because x:Bind calls it through the control instance.
    private Visibility IconVisibility(AppIcon icon) => Visible.If(icon != AppIcon.None);
}
