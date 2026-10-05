using System.Text.Json;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Chat;

/// <summary>
/// Small local models sometimes write a tool call out as text instead of making it properly, for
/// example <c>Answer: {"name": "run_command", "parameters": {"command": "dir"}}</c>, or the same
/// JSON after the sentence "Here's a JSON for a function call with its proper arguments that best
/// answers the given prompt:". This recognises a reply that ends with such a call, so it can be run
/// instead of being shown to the user as a line of JSON.
/// </summary>
public static class TextToolCall
{
    // Room for one introductory sentence before the JSON. A reply with more than that in front
    // is the model explaining something, not calling a tool.
    private const int MaxPrefixLength = 200;

    public static bool TryParse(string reply, IEnumerable<string> offeredTools, out ToolCall call)
    {
        call = null!;

        var text = reply.AsSpan().Trim();
        var start = text.IndexOf('{');
        if (start < 0 || start > MaxPrefixLength || text[^1] != '}')
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text[start..].ToString());
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("name", out var name)
                || name.ValueKind != JsonValueKind.String
                || !offeredTools.Contains(name.GetString()))
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

            call = new ToolCall(name.GetString()!, arguments.GetRawText());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
