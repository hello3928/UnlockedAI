using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

public enum NoticeSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// An inline status bar: icon, title, message, and optionally one action and a dismiss button.
/// Use <see cref="ErrorMessage"/> for errors; use this directly for everything else.
/// </summary>
public partial class Notice : UserControl
{
    public static readonly DependencyProperty SeverityProperty =
        Dp.Register<Notice, NoticeSeverity>(nameof(Severity), NoticeSeverity.Info, (notice, _) => notice.ApplySeverity());

    public static readonly DependencyProperty TitleProperty =
        Dp.Register<Notice, string>(nameof(Title), "");

    public static readonly DependencyProperty MessageProperty =
        Dp.Register<Notice, string>(nameof(Message), "");

    public static readonly DependencyProperty ActionTextProperty =
        Dp.Register<Notice, string>(nameof(ActionText), "");

    public static readonly DependencyProperty ActionCommandProperty =
        Dp.Register<Notice, ICommand?>(nameof(ActionCommand), null);

    public static readonly DependencyProperty IsDismissibleProperty =
        Dp.Register<Notice, bool>(nameof(IsDismissible), false);

    public static readonly DependencyProperty DismissCommandProperty =
        Dp.Register<Notice, ICommand?>(nameof(DismissCommand), null);

    public Notice()
    {
        InitializeComponent();
        ApplySeverity();

        // Visual states only take once the control is in the tree, so apply again on load.
        Loaded += (_, _) => ApplySeverity();
    }

    public event EventHandler? ActionInvoked;

    public event EventHandler? Dismissed;

    public NoticeSeverity Severity
    {
        get => (NoticeSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
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

    public bool IsDismissible
    {
        get => (bool)GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    /// <summary>Runs when the dismiss button is clicked. Without one, the notice just hides itself.</summary>
    public ICommand? DismissCommand
    {
        get => (ICommand?)GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }

    private void ApplySeverity()
    {
        SeverityIcon.Icon = Severity switch
        {
            NoticeSeverity.Success => AppIcon.Success,
            NoticeSeverity.Warning => AppIcon.Warning,
            NoticeSeverity.Error => AppIcon.Error,
            _ => AppIcon.Info,
        };
        VisualStateManager.GoToState(this, Severity.ToString(), useTransitions: false);
    }

    private void OnActionClick(object sender, RoutedEventArgs e) => ActionInvoked?.Invoke(this, EventArgs.Empty);

    private void OnDismissClick(object sender, RoutedEventArgs e)
    {
        Dismissed?.Invoke(this, EventArgs.Empty);

        if (DismissCommand is { } command)
        {
            command.Execute(null);
        }
        else
        {
            Visibility = Visibility.Collapsed;
        }
    }
}
