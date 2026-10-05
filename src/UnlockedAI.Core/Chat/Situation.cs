using System.Globalization;
using System.Runtime.InteropServices;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Tools;

namespace UnlockedAI.Core.Chat;

/// <summary>
/// The facts a model can't know on its own: today's date, the time, and (when it has tools) what
/// machine it is working on. Given to the model with every message so it never has to guess, and
/// also what the date-and-time tool returns when the model asks.
/// </summary>
public static class Situation
{
    /// <param name="withTools">Adds the details that only matter for running commands and touching files.</param>
    public static string Describe(TimeProvider clock, AppSettings settings, bool withTools)
    {
        var now = clock.GetLocalNow();
        var offset = now.Offset;
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"It is {now:dddd, d MMMM yyyy}, {now:HH:mm} (UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset:hh\\:mm}).");

        if (!withTools)
        {
            return text;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{text} This PC runs {RuntimeInformation.OSDescription}. The user's Windows name is {Environment.UserName}. "
            + $"Commands start in {ToolPaths.WorkingDirectory(settings)}, and relative file paths are looked up there.");
    }
}
