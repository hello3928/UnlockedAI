using System.Text.Json;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Errors;
using UnlockedAI.Core.Files;

namespace UnlockedAI.Core.Tools.Builtin;

public sealed class ReadFileTool(SettingsService settings, AttachmentService files) : ToolBase
{
    public override string Name => "read_file";

    public override string Title => "Read file";

    public override string Summary => "Read a text, PDF or Word file from this PC.";

    public override string Description =>
        "Read a text, source, PDF or Word file on the user's PC and return its text. Long files are cut off.";

    public override string ParametersJson =>
        """
        {
          "type": "object",
          "required": ["path"],
          "properties": {
            "path": { "type": "string", "description": "Path of the file." }
          }
        }
        """;

    public override string Describe(JsonElement arguments) => Text(arguments, "path");

    protected override async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var path = ToolPaths.Resolve(settings.Current, RequiredText(arguments, "path"));
        if (Directory.Exists(path))
        {
            throw new ToolException($"\"{path}\" is a folder. Use list_directory to see what is in it.");
        }

        try
        {
            // The same reader as attachments, so the same types and size limits apply.
            var file = await files.ReadAsync(path, cancellationToken).ConfigureAwait(false);
            return file.Truncated ? $"{file.Text}\n[The file is longer; only the first part was read.]" : file.Text;
        }
        catch (AppException exception)
        {
            throw new ToolException(exception.Error.Message);
        }
    }
}
