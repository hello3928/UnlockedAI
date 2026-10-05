using System.Text;
using System.Text.Json;
using UnlockedAI.Core.Data;

namespace UnlockedAI.Core.Tools.Builtin;

public sealed class WriteFileTool(SettingsService settings) : ToolBase
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public override string Name => "write_file";

    public override string Title => "Write file";

    public override string Summary => "Create a file, replace its contents, add to it, or change part of it.";

    public override string Description =>
        "Write a text file on the user's PC. Give content to create the file or replace all of it. "
        + "Set append to true to add content to the end instead. "
        + "Or give find and replace_with to change one exact piece of text in an existing file.";

    public override string ParametersJson =>
        """
        {
          "type": "object",
          "required": ["path"],
          "properties": {
            "path": { "type": "string", "description": "Path of the file." },
            "content": { "type": "string", "description": "Text to write." },
            "append": { "type": "boolean", "description": "Add to the end instead of replacing." },
            "find": { "type": "string", "description": "Exact existing text to change. Must appear once." },
            "replace_with": { "type": "string", "description": "Text to put in its place." }
          }
        }
        """;

    public override ToolRisk RiskFor(JsonElement arguments) => ToolRisk.ChangesPc;

    public override string Describe(JsonElement arguments)
    {
        var path = Text(arguments, "path");
        if (Text(arguments, "find").Length > 0)
        {
            return $"Change text in {path}";
        }

        return IsAppend(arguments) ? $"Add to {path}" : $"Write {path}";
    }

    protected override async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var path = ToolPaths.Resolve(settings.Current, RequiredText(arguments, "path"));
        if (Directory.Exists(path))
        {
            throw new ToolException($"\"{path}\" is a folder, not a file.");
        }

        var find = Text(arguments, "find");
        if (find.Length > 0)
        {
            return await ReplaceAsync(path, find, Text(arguments, "replace_with"), cancellationToken).ConfigureAwait(false);
        }

        var content = Text(arguments, "content");
        if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
        }

        if (IsAppend(arguments))
        {
            await File.AppendAllTextAsync(path, content, Utf8NoBom, cancellationToken).ConfigureAwait(false);
            return $"Added {content.Length:N0} characters to {path}.";
        }

        var existed = File.Exists(path);
        await File.WriteAllTextAsync(path, content, Utf8NoBom, cancellationToken).ConfigureAwait(false);
        return $"{(existed ? "Replaced" : "Created")} {path} with {content.Length:N0} characters.";
    }

    private static bool IsAppend(JsonElement arguments) =>
        Text(arguments, "append").Equals("true", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> ReplaceAsync(string path, string find, string replacement, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new ToolException($"The file \"{path}\" doesn't exist, so there is nothing to change in it.");
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        var first = text.IndexOf(find, StringComparison.Ordinal);
        if (first < 0)
        {
            throw new ToolException("The text in \"find\" wasn't found in the file. It must match exactly, including spaces and line breaks.");
        }

        if (text.IndexOf(find, first + find.Length, StringComparison.Ordinal) >= 0)
        {
            throw new ToolException("The text in \"find\" appears more than once. Include more of the surrounding text so it matches one place only.");
        }

        var changed = string.Concat(text.AsSpan(0, first), replacement, text.AsSpan(first + find.Length));
        await File.WriteAllTextAsync(path, changed, Utf8NoBom, cancellationToken).ConfigureAwait(false);
        return $"Changed 1 place in {path}.";
    }
}
