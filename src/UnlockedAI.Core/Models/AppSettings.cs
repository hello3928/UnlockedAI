namespace UnlockedAI.Core.Models;

public enum ApprovalMode
{
    /// <summary>Tools that change the PC wait for the user. Read-only tools run on their own.</summary>
    AskForChanges,

    /// <summary>Every tool runs without asking.</summary>
    AllowEverything,
}

public sealed record AppSettings
{
    public const string DefaultOllamaUrl = "http://localhost:11434";
    public const string DefaultModelName = "huihui_ai/dolphin3-abliterated:latest";

    public string OllamaUrl { get; init; } = DefaultOllamaUrl;
    public string DefaultModel { get; init; } = DefaultModelName;
    public string SystemPrompt { get; init; } = "";
    public double Temperature { get; init; } = 0.7;

    /// <summary>Tokens of context Ollama reserves (num_ctx). Larger values use more RAM or VRAM.</summary>
    public int ContextLength { get; init; } = 8192;

    /// <summary>Minutes Ollama keeps the model loaded after the last request. 0 unloads at once.</summary>
    public int KeepAliveMinutes { get; init; } = 5;

    /// <summary>
    /// Whether the model is offered tools at all. Off by default: small local models that are given
    /// tools use one for nearly every message, which makes plain chat slow and sends ordinary
    /// questions to a search engine. The user switches it on from the chat header when they want it.
    /// </summary>
    public bool ToolsEnabled { get; init; }

    public ApprovalMode ApprovalMode { get; init; } = ApprovalMode.AskForChanges;
    public int CommandTimeoutSeconds { get; init; } = 60;

    /// <summary>Where run_command starts. Empty means the user's profile folder.</summary>
    public string WorkingDirectory { get; init; } = "";

    public IReadOnlySet<string> DisabledTools { get; init; } = new HashSet<string>();

    /// <summary>True for an absolute http or https address, which is what Ollama listens on.</summary>
    public static bool IsValidOllamaUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Returns a copy with every number pulled into its allowed range and blanks filled in.</summary>
    public AppSettings Normalized() => this with
    {
        OllamaUrl = string.IsNullOrWhiteSpace(OllamaUrl) ? DefaultOllamaUrl : OllamaUrl.Trim().TrimEnd('/'),
        DefaultModel = string.IsNullOrWhiteSpace(DefaultModel) ? DefaultModelName : DefaultModel.Trim(),
        Temperature = Math.Clamp(Temperature, 0, 2),
        ContextLength = Math.Clamp(ContextLength, 512, 131072),
        KeepAliveMinutes = Math.Clamp(KeepAliveMinutes, 0, 1440),
        CommandTimeoutSeconds = Math.Clamp(CommandTimeoutSeconds, 5, 3600),
        WorkingDirectory = WorkingDirectory.Trim(),
    };
}
