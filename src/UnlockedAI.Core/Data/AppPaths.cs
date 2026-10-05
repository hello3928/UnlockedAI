namespace UnlockedAI.Core.Data;

public static class AppPaths
{
    /// <summary>Set this environment variable to keep the app's data somewhere other than the default folder.</summary>
    public const string DataFolderVariable = "UNLOCKEDAI_DATA_DIR";

    public static string DataFolder { get; } = ResolveDataFolder();

    public static string DatabaseFile { get; } = Path.Combine(DataFolder, "unlockedai.db");

    private static string ResolveDataFolder()
    {
        var custom = Environment.GetEnvironmentVariable(DataFolderVariable);
        return string.IsNullOrWhiteSpace(custom)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnlockedAI")
            : Path.GetFullPath(custom);
    }
}
