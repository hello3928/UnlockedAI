using System.Text;
using System.Text.Json;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Files;

namespace UnlockedAI.Core.Tools.Builtin;

public sealed class ListDirectoryTool(SettingsService settings) : ToolBase
{
    private const int MaxEntries = 200;

    public override string Name => "list_directory";

    public override string Title => "List folder";

    public override string Summary => "See the files and folders inside a folder on this PC.";

    public override string Description =>
        "List the files and subfolders in a folder on the user's PC, with file sizes.";

    public override string ParametersJson =>
        """
        {
          "type": "object",
          "required": ["path"],
          "properties": {
            "path": { "type": "string", "description": "Path of the folder. Use . for the working folder." }
          }
        }
        """;

    public override string Describe(JsonElement arguments) =>
        Text(arguments, "path") is { Length: > 0 } path ? path : ToolPaths.WorkingDirectory(settings.Current);

    protected override Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var requested = Text(arguments, "path");
        var path = requested.Length > 0
            ? ToolPaths.Resolve(settings.Current, requested)
            : ToolPaths.WorkingDirectory(settings.Current);

        var folder = new DirectoryInfo(path);
        if (!folder.Exists)
        {
            throw new ToolException(File.Exists(path)
                ? $"\"{path}\" is a file, not a folder. Use read_file to read it."
                : $"The folder \"{path}\" doesn't exist.");
        }

        var text = new StringBuilder().Append("Contents of ").Append(folder.FullName).AppendLine(":");
        var shown = 0;
        var total = 0;

        // Folders first, then files, each by name. Enumerated lazily so a huge folder isn't loaded whole.
        var entries = folder.EnumerateDirectories().Cast<FileSystemInfo>().OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Concat(folder.EnumerateFiles().OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase));

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total++;
            if (shown == MaxEntries)
            {
                continue;
            }

            shown++;
            text.AppendLine(entry is FileInfo file
                ? $"{file.Name}  ({FileSize.Format(file.Length)})"
                : $"[folder] {entry.Name}");
        }

        if (total == 0)
        {
            text.AppendLine("(empty)");
        }
        else if (total > shown)
        {
            text.Append('[').Append(total - shown).AppendLine(" more entries not shown.]");
        }

        return Task.FromResult(text.ToString());
    }
}
