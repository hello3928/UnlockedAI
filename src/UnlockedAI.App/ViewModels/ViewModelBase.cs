using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnlockedAI.Core.Errors;
using UnlockedAI.Platform;

namespace UnlockedAI.ViewModels;

/// <summary>
/// Base for view models. Gives every screen the same error handling: failures land in <see cref="Error"/>,
/// which a view shows with an <c>ErrorMessage</c> control.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    public partial AppError? Error { get; set; }

    [RelayCommand]
    private void DismissError() => Error = null;

    /// <summary>Runs an action and reports any failure. Returns false if it failed.</summary>
    protected async Task<bool> TryAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception exception)
        {
            Report(exception);
            return false;
        }
    }

    /// <summary>Shows the failure to the user. A cancellation is the user's own doing and is not shown.</summary>
    protected void Report(Exception exception)
    {
        var error = ErrorMapper.Map(exception);
        if (error.Kind == AppErrorKind.Cancelled)
        {
            return;
        }

        if (error.Kind == AppErrorKind.Unknown)
        {
            // Not a failure we anticipated, so keep the full detail for troubleshooting.
            CrashLog.Write(exception);
        }

        Error = error;
    }
}
