using System.Windows.Input;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace UnlockedAI.Controls;

/// <summary>
/// The message box at the bottom of a chat. Enter sends, Shift+Enter adds a line.
/// Shows Send normally and Stop while <see cref="IsBusy"/> is true.
/// </summary>
public sealed partial class Composer : UserControl
{
    public static readonly DependencyProperty TextProperty =
        Dp.Register<Composer, string>(nameof(Text), "");

    public static readonly DependencyProperty PlaceholderTextProperty =
        Dp.Register<Composer, string>(nameof(PlaceholderText), "");

    public static readonly DependencyProperty SendCommandProperty =
        Dp.Register<Composer, ICommand?>(nameof(SendCommand), null);

    public static readonly DependencyProperty StopCommandProperty =
        Dp.Register<Composer, ICommand?>(nameof(StopCommand), null);

    public static readonly DependencyProperty IsBusyProperty =
        Dp.Register<Composer, bool>(nameof(IsBusy), false);

    public Composer()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public ICommand? SendCommand
    {
        get => (ICommand?)GetValue(SendCommandProperty);
        set => SetValue(SendCommandProperty, value);
    }

    public ICommand? StopCommand
    {
        get => (ICommand?)GetValue(StopCommandProperty);
        set => SetValue(StopCommandProperty, value);
    }

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    /// <summary>Puts the caret in the message box.</summary>
    public void FocusInput() => Input.Focus(FocusState.Programmatic);

    private static bool IsShiftDown() =>
        InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

    private void OnInputPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || IsShiftDown())
        {
            return;
        }

        // Plain Enter never adds a line, even when there is nothing to send yet.
        e.Handled = true;
        if (SendCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void OnInputGotFocus(object sender, RoutedEventArgs e) =>
        VisualStateManager.GoToState(this, "Focused", useTransitions: false);

    private void OnInputLostFocus(object sender, RoutedEventArgs e) =>
        VisualStateManager.GoToState(this, "Unfocused", useTransitions: false);
}
