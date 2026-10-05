namespace UnlockedAI.Core.Models;

public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool,
}

public static class ChatRoleExtensions
{
    /// <summary>The lowercase name used both by Ollama and in the database.</summary>
    public static string ToWire(this ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        ChatRole.Tool => "tool",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public static ChatRole ParseWire(string value) => value switch
    {
        "system" => ChatRole.System,
        "user" => ChatRole.User,
        "assistant" => ChatRole.Assistant,
        "tool" => ChatRole.Tool,
        _ => throw new FormatException($"Unknown chat role \"{value}\"."),
    };
}
