using System.Globalization;

namespace UnlockedAI.Core.Files;

public static class FileSize
{
    /// <summary>A size as people read it: "512 bytes", "24 KB", "1.2 MB".</summary>
    public static string Format(long bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.CurrentCulture, $"{bytes} bytes"),
        < 1024 * 1024 => string.Create(CultureInfo.CurrentCulture, $"{bytes / 1024.0:0} KB"),
        _ => string.Create(CultureInfo.CurrentCulture, $"{bytes / (1024.0 * 1024.0):0.#} MB"),
    };
}
