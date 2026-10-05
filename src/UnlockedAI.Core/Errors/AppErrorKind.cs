namespace UnlockedAI.Core.Errors;

public enum AppErrorKind
{
    Unknown,
    Cancelled,
    Timeout,
    Network,
    OllamaUnreachable,
    ModelNotFound,
    Storage,
    AccessDenied,
    FileNotFound,
    FileUnreadable,
    FileTooLarge,
    FileUnsupported,
    InvalidInput,
    ToolFailed,
}
