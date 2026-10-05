namespace UnlockedAI.Core.Errors;

/// <summary>Thrown by Core code that already knows which <see cref="AppError"/> applies.</summary>
public sealed class AppException(AppError error, Exception? innerException = null)
    : Exception(error.Message, innerException)
{
    public AppError Error { get; } = error;
}
