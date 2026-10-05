using Microsoft.Data.Sqlite;

namespace UnlockedAI.Core.Errors;

/// <summary>The single place a raw exception becomes an <see cref="AppError"/>.</summary>
public static class ErrorMapper
{
    public static AppError Map(Exception exception) => exception switch
    {
        AppException app => app.Error,
        // HttpClient reports its own timeout as a cancellation wrapping a TimeoutException.
        OperationCanceledException { InnerException: TimeoutException } => AppError.Timeout(exception.Message),
        OperationCanceledException => AppError.Cancelled(),
        TimeoutException => AppError.Timeout(exception.Message),
        HttpRequestException => AppError.Network(exception.Message),
        SqliteException => AppError.Storage(exception.Message),
        UnauthorizedAccessException => AppError.AccessDenied(exception.Message),
        FileNotFoundException or DirectoryNotFoundException => AppError.FileNotFound(exception.Message),
        IOException => new AppError(
            AppErrorKind.FileUnreadable,
            "Couldn't read or write file",
            "The file couldn't be accessed. It may be open in another app.",
            exception.Message,
            IsRetryable: true),
        _ => AppError.Unknown(exception.Message),
    };
}
