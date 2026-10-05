using System.Globalization;
using System.Text.Json;
using UnlockedAI.Core.Errors;

namespace UnlockedAI.Core.Tools;

/// <summary>Thrown inside a tool for a failure the model should be told about in plain words.</summary>
public sealed class ToolException(string message) : Exception(message);

/// <summary>
/// Base for tools. Handles what every tool needs: turning exceptions into a failed result,
/// capping the output, and reading arguments leniently (small models are sloppy with types).
/// </summary>
public abstract class ToolBase : ITool
{
    /// <summary>Longest output handed back to the model. More than this crowds out the conversation.</summary>
    public const int MaxOutputChars = 12_000;

    public abstract string Name { get; }

    public abstract string Title { get; }

    public abstract string Summary { get; }

    public abstract string Description { get; }

    public abstract string ParametersJson { get; }

    public virtual ToolRisk RiskFor(JsonElement arguments) => ToolRisk.ReadOnly;

    public abstract string Describe(JsonElement arguments);

    public async Task<ToolResult> RunAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        try
        {
            var output = await ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
            return ToolResult.Success(Cap(string.IsNullOrWhiteSpace(output) ? "(no output)" : output));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ToolException exception)
        {
            return ToolResult.Failure(Cap(exception.Message));
        }
        catch (Exception exception)
        {
            var error = ErrorMapper.Map(exception);
            return ToolResult.Failure(error.Detail is { Length: > 0 } detail ? $"{error.Message} ({detail})" : error.Message);
        }
    }

    protected static string Cap(string text) =>
        text.Length <= MaxOutputChars
            ? text
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{text.AsSpan(0, MaxOutputChars)}\n[Output cut here. {text.Length - MaxOutputChars:N0} more characters were not shown.]");

    /// <summary>Reads a string argument, or an empty string when it is missing. Never throws.</summary>
    protected static string Text(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var value))
        {
            return "";
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            _ => value.GetRawText(),
        };
    }

    /// <summary>Reads a string argument that must be present.</summary>
    protected static string RequiredText(JsonElement arguments, string name)
    {
        var text = Text(arguments, name);
        return string.IsNullOrWhiteSpace(text)
            ? throw new ToolException($"The \"{name}\" argument is required.")
            : text;
    }

    protected abstract Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}
