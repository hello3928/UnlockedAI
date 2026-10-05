using System.Text.Json;
using UnlockedAI.Core.Chat;
using UnlockedAI.Core.Data;

namespace UnlockedAI.Core.Tools.Builtin;

/// <summary>
/// Tells the model the date, the time and what PC it is on. The same facts are already in every
/// request (see <see cref="Situation"/>), but a model that is offered tools goes looking for one
/// when asked the date, and without this it reaches for run_command and makes the user approve it.
/// <para>
/// It takes one argument although it needs none: offering this model a tool with an empty
/// parameter list made its calls to the other tools much less reliable.
/// </para>
/// </summary>
public sealed class DateTimeInfoTool(SettingsService settings, TimeProvider? clock = null) : ToolBase
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public override string Name => "date_time_info";

    public override string Title => "Date and system info";

    public override string Summary => "Look up the current date and time and basic facts about this PC.";

    public override string Description =>
        "Get the current date, day of the week, time and time zone, and this PC's operating system, user name and working folder.";

    public override string ParametersJson =>
        """
        {
          "type": "object",
          "required": ["about"],
          "properties": {
            "about": { "type": "string", "description": "What you want to know: date, time or system." }
          }
        }
        """;

    public override string Describe(JsonElement arguments) => "Date, time and system details";

    protected override Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        Task.FromResult(Situation.Describe(_clock, settings.Current, withTools: true));
}
