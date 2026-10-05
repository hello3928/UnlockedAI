using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace UnlockedAI.Core.Tools.Builtin;

/// <summary>
/// Windows PowerShell writes its error stream as "CLIXML" when that stream is redirected:
/// a marker line followed by XML. This turns it back into the lines a person would have seen.
/// </summary>
internal static partial class PowerShellErrors
{
    private const string Marker = "#< CLIXML";

    public static string Decode(string errorOutput)
    {
        var start = errorOutput.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return errorOutput;
        }

        var text = new StringBuilder(errorOutput, 0, start, errorOutput.Length);
        foreach (Match message in MessagePattern().Matches(errorOutput, start))
        {
            var decoded = WebUtility.HtmlDecode(message.Groups[1].Value);
            text.Append(EscapePattern().Replace(
                decoded,
                escape => ((char)int.Parse(escape.Groups[1].ValueSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString()));
        }

        return text.ToString().ReplaceLineEndings("\n").TrimEnd();
    }

    // <S S="Error">text</S>, and the same for warnings and the other message streams.
    [GeneratedRegex("""<S S="(?:Error|Warning|Verbose|Debug|Information)">(.*?)</S>""", RegexOptions.Singleline)]
    private static partial Regex MessagePattern();

    // Control characters are written as _x000D_, _x000A_ and so on.
    [GeneratedRegex("_x([0-9A-Fa-f]{4})_")]
    private static partial Regex EscapePattern();
}
