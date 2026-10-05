using System.Text.Json;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Tools;

public enum ToolRisk
{
    /// <summary>Only looks things up. Runs without asking.</summary>
    ReadOnly,

    /// <summary>Can change this PC or reach devices on the local network. Waits for the user unless they allowed everything.</summary>
    ChangesPc,
}

/// <param name="Output">Sent back to the model, and shown to the user in the tool's card.</param>
public sealed record ToolResult(ToolOutcome Outcome, string Output)
{
    public const string DeniedMessage = "The user did not allow this tool call. Do not try it again; carry on without it or ask the user.";

    public static ToolResult Success(string output) => new(ToolOutcome.Succeeded, output);

    public static ToolResult Failure(string message) => new(ToolOutcome.Failed, $"Error: {message}");

    public static ToolResult Denied() => new(ToolOutcome.Denied, DeniedMessage);
}

/// <summary>Something the model can ask the app to do. Adding a tool means adding one class and registering it.</summary>
public interface ITool
{
    /// <summary>The name the model calls it by. Lower-case with underscores; never changes once released.</summary>
    string Name { get; }

    /// <summary>The name shown to the user, such as "Web search".</summary>
    string Title { get; }

    /// <summary>One sentence for Settings, saying what the tool lets the model do.</summary>
    string Summary { get; }

    /// <summary>Tells the model when and how to use the tool.</summary>
    string Description { get; }

    /// <summary>JSON Schema for the arguments object.</summary>
    string ParametersJson { get; }

    /// <summary>How risky this particular call is. May depend on the arguments.</summary>
    ToolRisk RiskFor(JsonElement arguments);

    /// <summary>One line saying what this call will do, in the user's terms: the command, the address, the path.</summary>
    string Describe(JsonElement arguments);

    /// <summary>Runs the tool. Failures come back as a failed result, not an exception; only cancellation throws.</summary>
    Task<ToolResult> RunAsync(JsonElement arguments, CancellationToken cancellationToken);
}
