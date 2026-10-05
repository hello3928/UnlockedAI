using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

/// <summary>A small tag: icon, text, optional detail and an optional remove button. Used for attached files.</summary>
public sealed partial class Chip : UserControl
{
    public static readonly DependencyProperty IconProperty =
        Dp.Register<Chip, AppIcon>(nameof(Icon), AppIcon.None);

    public static readonly DependencyProperty TextProperty =
        Dp.Register<Chip, string>(nameof(Text), "");

    public static readonly DependencyProperty DetailProperty =
        Dp.Register<Chip, string>(nameof(Detail), "");

    public static readonly DependencyProperty IsRemovableProperty =
        Dp.Register<Chip, bool>(nameof(IsRemovable), false);

    public static readonly DependencyProperty RemoveCommandProperty =
        Dp.Register<Chip, ICommand?>(nameof(RemoveCommand), null);

    public static readonly DependencyProperty RemoveParameterProperty =
        Dp.Register<Chip, object?>(nameof(RemoveParameter), null);

    public Chip()
    {
        InitializeComponent();
    }

    public event EventHandler? Removed;

    public AppIcon Icon
    {
        get => (AppIcon)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Secondary text after the main text, such as a file size.</summary>
    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public bool IsRemovable
    {
        get => (bool)GetValue(IsRemovableProperty);
        set => SetValue(IsRemovableProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public object? RemoveParameter
    {
        get => GetValue(RemoveParameterProperty);
        set => SetValue(RemoveParameterProperty, value);
    }

    // Instance method because x:Bind calls it through the control instance.
    private string RemoveLabel(string text) => $"Remove {text}";

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        Removed?.Invoke(this, EventArgs.Empty);

        if (RemoveCommand is { } command && command.CanExecute(RemoveParameter))
        {
            command.Execute(RemoveParameter);
        }
    }
}
