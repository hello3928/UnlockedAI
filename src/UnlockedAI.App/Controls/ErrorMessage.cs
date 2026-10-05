using System.Windows.Input;
using Microsoft.UI.Xaml;
using UnlockedAI.Core.Errors;

namespace UnlockedAI.Controls;

/// <summary>
/// The one way the app shows an error. Bind <see cref="Error"/> to a view model's error;
/// it appears when that is set and disappears when it is cleared. "Try again" is offered only
/// when the error says a retry can work and a <see cref="RetryCommand"/> is supplied.
/// </summary>
public sealed partial class ErrorMessage : Notice
{
    public static readonly DependencyProperty ErrorProperty =
        Dp.Register<ErrorMessage, AppError?>(nameof(Error), null, (message, _) => message.Apply());

    public static readonly DependencyProperty RetryCommandProperty =
        Dp.Register<ErrorMessage, ICommand?>(nameof(RetryCommand), null, (message, _) => message.Apply());

    public ErrorMessage()
    {
        Severity = NoticeSeverity.Error;
        IsDismissible = true;
        Apply();
    }

    public AppError? Error
    {
        get => (AppError?)GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    private void Apply()
    {
        var error = Error;
        var canRetry = error is { IsRetryable: true } && RetryCommand is not null;

        Visibility = Visible.IfSet(error);
        Title = error?.Title ?? "";
        Message = error?.Message ?? "";
        ActionText = canRetry ? "Try again" : "";
        ActionCommand = canRetry ? RetryCommand : null;
    }
}
