using System.Text.Json;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Chat;

/// <summary>
/// Small local models sometimes write a tool call out as text instead of making it properly, for
/// example <c>Answer: {"name": "run_command", "parameters": {"command": "dir"}}</c>, or the same
/// object inside a ```json code fence after a sentence or heading. This recognises such a call so it
/// can be run instead of being shown to the user as a block of JSON.
/// </summary>
public static class TextToolCall
{
    // Room for one introductory sentence before a bare JSON object. More than that, with no code
    // fence, is the model explaining something rather than calling a tool.
    private const int MaxPrefixLength = 200;

    public static bool TryParse(string reply, IEnumerable<string> offeredTools, out ToolCall call)
    {
        var offered = offeredTools as ICollection<string> ?? offeredTools.ToList();
        foreach (var candidate in Candidates(reply.Trim()))
        {
            if (TryParseCall(candidate, offered, out call))
            {
                return true;
            }
        }

        call = null!;
        return false;
    }

    /// <summary>JSON snippets that might be a tool call, best first: the model may emit several code blocks.</summary>
    private static IEnumerable<string> Candidates(string text)
    {
        // Inside ```...``` code fences, which is where models usually put a call they write as text.
        var search = 0;
        while (true)
        {
            var open = text.IndexOf("```", search, StringComparison.Ordinal);
            if (open < 0)
            {
                break;
            }

            var contentStart = text.IndexOf('\n', open);
            var close = contentStart < 0 ? -1 : text.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (contentStart < 0 || close < 0)
            {
                break;
            }

            yield return text[(contentStart + 1)..close].Trim();
            search = close + 3;
        }

        // A bare object that the whole reply is essentially made of.
        var first = text.IndexOf('{');
        if (first >= 0 && first <= MaxPrefixLength && text.EndsWith('}'))
        {
            yield return text[first..];
        }
    }

    private static bool TryParseCall(string candidate, ICollection<string> offeredTools, out ToolCall call)
    {
        call = null!;
        if (candidate.Length == 0 || candidate[0] != '{')
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(candidate);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("name", out var name)
                || name.ValueKind != JsonValueKind.String
                || name.GetString() is not { } toolName
                || !offeredTools.Contains(toolName))
            {
                return false;
            }

            // Different model families call the arguments object by different names.
            if (!root.TryGetProperty("parameters", out var arguments) && !root.TryGetProperty("arguments", out arguments))
            {
                return false;
            }

            if (arguments.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            call = new ToolCall(toolName, arguments.GetRawText());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
