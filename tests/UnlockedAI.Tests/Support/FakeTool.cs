using System.Text.Json;
using UnlockedAI.Core.Tools;

namespace UnlockedAI.Tests.Support;

/// <summary>A tool whose behaviour is supplied by the test.</summary>
internal sealed class FakeTool(string name, ToolRisk risk, Func<JsonElement, CancellationToken, Task<string>> run) : ToolBase
{
    public FakeTool(string name, ToolRisk risk = ToolRisk.ReadOnly, string output = "done")
        : this(name, risk, (_, _) => Task.FromResult(output))
    {
    }

    /// <summary>How many times the tool actually ran.</summary>
    public int Runs { get; private set; }

    public override string Name => name;

    public override string Title => $"Fake {name}";

    public override string Summary => "For tests.";

    public override string Description => "For tests.";

    public override string ParametersJson => """{ "type": "object", "properties": {} }""";

    public override ToolRisk RiskFor(JsonElement arguments) => risk;

    public override string Describe(JsonElement arguments) => $"{name} {Text(arguments, "text")}".Trim();

    protected override Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        Runs++;
        return run(arguments, cancellationToken);
    }
}
