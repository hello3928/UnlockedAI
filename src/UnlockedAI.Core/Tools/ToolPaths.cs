using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Tools;

/// <summary>Works out which folder and file a tool argument refers to.</summary>
public static class ToolPaths
{
    /// <summary>Where commands start and relative paths are resolved from: the folder in Settings, else the user's profile.</summary>
    public static string WorkingDirectory(AppSettings settings)
    {
        var configured = Environment.ExpandEnvironmentVariables(settings.WorkingDirectory);
        return configured.Length > 0 && Directory.Exists(configured)
            ? configured
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>Makes a path absolute. Understands %VARIABLES% and a leading ~ for the user's profile.</summary>
    public static string Resolve(AppSettings settings, string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (expanded == "~" || expanded.StartsWith("~/", StringComparison.Ordinal) || expanded.StartsWith("~\\", StringComparison.Ordinal))
        {
            expanded = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), expanded[1..]);
        }

        try
        {
            return Path.GetFullPath(expanded, WorkingDirectory(settings));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ToolException($"\"{path}\" is not a valid path.");
        }
    }
}
