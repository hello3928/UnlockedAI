namespace UnlockedAI.Core.Errors;

/// <summary>
/// The one error shape the app shows to the user. Every failure ends up as one of these,
/// either through a factory method below or through <see cref="ErrorMapper"/>.
/// </summary>
/// <param name="Title">Short heading, sentence case, no trailing full stop.</param>
/// <param name="Message">What happened and what to do next, in plain words.</param>
/// <param name="Detail">Raw technical text for troubleshooting. Never shown as the main message.</param>
/// <param name="IsRetryable">True when trying the same action again can succeed.</param>
public sealed record AppError(
    AppErrorKind Kind,
    string Title,
    string Message,
    string? Detail = null,
    bool IsRetryable = false)
{
    public static AppError Unknown(string? detail = null) => new(
        AppErrorKind.Unknown,
        "Something went wrong",
        "The action didn't finish. Try again.",
        detail,
        IsRetryable: true);

    public static AppError Cancelled() => new(
        AppErrorKind.Cancelled,
        "Stopped",
        "The action was stopped before it finished.");

    public static AppError Timeout(string? detail = null) => new(
        AppErrorKind.Timeout,
        "Timed out",
        "This took too long and was stopped. Try again.",
        detail,
        IsRetryable: true);

    public static AppError Network(string? detail = null) => new(
        AppErrorKind.Network,
        "Network problem",
        "The request couldn't be completed. Check your connection and try again.",
        detail,
        IsRetryable: true);

    public static AppError OllamaUnreachable(string url, string? detail = null) => new(
        AppErrorKind.OllamaUnreachable,
        "Can't reach Ollama",
        $"Nothing answered at {url}. Start Ollama, then try again.",
        detail,
        IsRetryable: true);

    public static AppError ModelNotFound(string model) => new(
        AppErrorKind.ModelNotFound,
        "Model not found",
        $"Ollama doesn't have \"{model}\". Pull it with \"ollama pull {model}\" or pick another model.");

    public static AppError Storage(string? detail = null) => new(
        AppErrorKind.Storage,
        "Couldn't save",
        "The chat database couldn't be read or written. Your last change may not be saved.",
        detail,
        IsRetryable: true);

    public static AppError AccessDenied(string? detail = null) => new(
        AppErrorKind.AccessDenied,
        "Access denied",
        "Windows blocked access to that location.",
        detail);

    public static AppError FileNotFound(string? detail = null) => new(
        AppErrorKind.FileNotFound,
        "File not found",
        "That file or folder doesn't exist.",
        detail);

    public static AppError FileUnreadable(string fileName, string? detail = null) => new(
        AppErrorKind.FileUnreadable,
        "Couldn't read file",
        $"\"{fileName}\" couldn't be read. It may be open in another app or damaged.",
        detail,
        IsRetryable: true);

    public static AppError FileTooLarge(string fileName, long limitBytes) => new(
        AppErrorKind.FileTooLarge,
        "File too large",
        $"\"{fileName}\" is over the {limitBytes / (1024 * 1024)} MB limit.");

    public static AppError FileUnsupported(string fileName) => new(
        AppErrorKind.FileUnsupported,
        "File type not supported",
        $"\"{fileName}\" can't be attached. Use a text, code, PDF or Word file.");

    public static AppError InvalidInput(string message) => new(
        AppErrorKind.InvalidInput,
        "Check your input",
        message);

    public static AppError ToolFailed(string toolName, string? detail = null) => new(
        AppErrorKind.ToolFailed,
        "Tool failed",
        $"The \"{toolName}\" tool didn't finish.",
        detail);
}
