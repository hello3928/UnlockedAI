using UnlockedAI.Core.Data;

namespace UnlockedAI.Platform;

/// <summary>Appends unhandled exceptions to crash.log in the app's data folder.</summary>
internal static class CrashLog
{
    private const long MaxBytes = 256 * 1024;

    private static readonly string LogFile = Path.Combine(AppPaths.DataFolder, "crash.log");

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataFolder);

            var entry = $"{DateTimeOffset.Now:u}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > MaxBytes)
            {
                File.WriteAllText(LogFile, entry);
            }
            else
            {
                File.AppendAllText(LogFile, entry);
            }
        }
        catch (Exception logFailure) when (logFailure is IOException or UnauthorizedAccessException)
        {
            // Nowhere left to report to; losing the log entry is better than a second crash.
        }
    }
}
