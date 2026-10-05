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
        Dp.Register<Composer, ICommand?>(nameof(SendCommand), null, (composer, _) => composer.UpdateAction());

    public static readonly DependencyProperty StopCommandProperty =
        Dp.Register<Composer, ICommand?>(nameof(StopCommand), null, (composer, _) => composer.UpdateAction());

    public static readonly DependencyProperty IsBusyProperty =
        Dp.Register<Composer, bool>(nameof(IsBusy), false, (composer, _) => composer.UpdateAction());

    public static readonly DependencyProperty AttachmentsProperty =
        Dp.Register<Composer, object?>(nameof(Attachments), null);

    public static readonly DependencyProperty HasAttachmentsProperty =
        Dp.Register<Composer, bool>(nameof(HasAttachments), false);

    public static readonly DependencyProperty IsAttachingProperty =
        Dp.Register<Composer, bool>(nameof(IsAttaching), false);

    public static readonly DependencyProperty AttachCommandProperty =
        Dp.Register<Composer, ICommand?>(nameof(AttachCommand), null);

    public static readonly DependencyProperty RemoveAttachmentCommandProperty =
        Dp.Register<Composer, ICommand?>(nameof(RemoveAttachmentCommand), null);

    public Composer()
    {
        InitializeComponent();
        UpdateAction();
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

    /// <summary>The files waiting to be sent, shown as chips above the message box.</summary>
    public object? Attachments
    {
        get => GetValue(AttachmentsProperty);
        set => SetValue(AttachmentsProperty, value);
    }

    public bool HasAttachments
    {
        get => (bool)GetValue(HasAttachmentsProperty);
        set => SetValue(HasAttachmentsProperty, value);
    }

    /// <summary>True while a file is being read; the attach button shows progress.</summary>
    public bool IsAttaching
    {
        get => (bool)GetValue(IsAttachingProperty);
        set => SetValue(IsAttachingProperty, value);
    }

    public ICommand? AttachCommand
    {
        get => (ICommand?)GetValue(AttachCommandProperty);
        set => SetValue(AttachCommandProperty, value);
    }

    /// <summary>Called with the attachment whose chip was removed.</summary>
    public ICommand? RemoveAttachmentCommand
    {
        get => (ICommand?)GetValue(RemoveAttachmentCommandProperty);
        set => SetValue(RemoveAttachmentCommandProperty, value);
    }

    /// <summary>Puts the caret in the message box.</summary>
    public void FocusInput() => Input.Focus(FocusState.Programmatic);

    private void OnAttachmentRemoved(object? sender, EventArgs e)
    {
        if (sender is Chip chip && RemoveAttachmentCommand is { } command)
        {
            command.Execute(chip.RemoveParameter);
            FocusInput();
        }
    }

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

    private void UpdateAction()
    {
        var busy = IsBusy;
        ActionButton.Icon = busy ? AppIcon.Stop : AppIcon.Send;
        ActionButton.Label = busy ? "Stop (Esc)" : "Send (Enter)";
        ActionButton.Variant = busy ? ButtonVariant.Secondary : ButtonVariant.Primary;
        ActionButton.Command = busy ? StopCommand : SendCommand;
    }

    // After a click the next thing the user does is type, so the caret goes back to the box.
    private void OnActionClick(object sender, RoutedEventArgs e) => FocusInput();

    private void OnInputGotFocus(object sender, RoutedEventArgs e) =>
        VisualStateManager.GoToState(this, "Focused", useTransitions: false);

    private void OnInputLostFocus(object sender, RoutedEventArgs e) =>
        VisualStateManager.GoToState(this, "Unfocused", useTransitions: false);
}
