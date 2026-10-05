namespace UnlockedAI.Core.Secrets;

/// <summary>Keeps API keys out of the database and the settings file. The app stores them in Windows' credential locker.</summary>
public interface ISecretStore
{
    /// <returns>The stored value, or null when there is none.</returns>
    string? Read(string name);

    /// <summary>Stores a value. Null or empty removes it.</summary>
    void Write(string name, string? value);
}

public static class SecretNames
{
    public const string OllamaApiKey = "ollama-api-key";
}
