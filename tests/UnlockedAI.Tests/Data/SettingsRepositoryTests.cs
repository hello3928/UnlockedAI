using UnlockedAI.Core.Data;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Tests.Data;

public sealed class SettingsRepositoryTests : IDisposable
{
    private readonly Database _database = Database.OpenInMemory();
    private readonly SettingsRepository _settings;

    public SettingsRepositoryTests() => _settings = new SettingsRepository(_database);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Empty_database_gives_defaults()
    {
        var loaded = await _settings.LoadAsync(Ct);

        Assert.Equal(AppSettings.DefaultOllamaUrl, loaded.OllamaUrl);
        Assert.Equal(AppSettings.DefaultModelName, loaded.DefaultModel);
        Assert.Equal(ApprovalMode.AskForChanges, loaded.ApprovalMode);
        Assert.Empty(loaded.DisabledTools);
    }

    [Fact]
    public async Task Saved_settings_round_trip()
    {
        var saved = new AppSettings
        {
            OllamaUrl = "http://192.168.1.20:11434",
            DefaultModel = "dolphin-llama3:latest",
            SystemPrompt = "Answer briefly.\nUse metric units.",
            Temperature = 0.35,
            ContextLength = 16384,
            KeepAliveMinutes = 0,
            ApprovalMode = ApprovalMode.AllowEverything,
            CommandTimeoutSeconds = 120,
            WorkingDirectory = @"C:\Work",
            DisabledTools = new HashSet<string> { "run_command", "write_file" },
        };

        await _settings.SaveAsync(saved, Ct);
        var loaded = await _settings.LoadAsync(Ct);

        Assert.Equal(saved with { DisabledTools = loaded.DisabledTools }, loaded);
        Assert.Equal(["run_command", "write_file"], loaded.DisabledTools.Order());
    }

    [Fact]
    public async Task Out_of_range_values_are_pulled_into_range_on_save()
    {
        await _settings.SaveAsync(
            new AppSettings { Temperature = 9, ContextLength = 1, KeepAliveMinutes = -3, OllamaUrl = "  " },
            Ct);

        var loaded = await _settings.LoadAsync(Ct);

        Assert.Equal(2, loaded.Temperature);
        Assert.Equal(512, loaded.ContextLength);
        Assert.Equal(0, loaded.KeepAliveMinutes);
        Assert.Equal(AppSettings.DefaultOllamaUrl, loaded.OllamaUrl);
    }

    [Fact]
    public async Task Unreadable_stored_value_falls_back_to_the_default()
    {
        await _database.RunAsync(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO settings (key, value) VALUES ('temperature', 'warm')";
                command.ExecuteNonQuery();
            },
            Ct);

        Assert.Equal(new AppSettings().Temperature, (await _settings.LoadAsync(Ct)).Temperature);
    }
}
