namespace UnlockedAI.Core.Data;

public static class AppPaths
{
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UnlockedAI");

    public static string DatabaseFile { get; } = Path.Combine(DataFolder, "unlockedai.db");
}
