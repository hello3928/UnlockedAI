using System.Globalization;
using Microsoft.Data.Sqlite;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Data;

/// <summary>Stores <see cref="AppSettings"/> as one row per setting. Missing or unreadable rows fall back to defaults.</summary>
public sealed class SettingsRepository(Database database) : RepositoryBase(database)
{
    private const string OllamaUrl = "ollama_url";
    private const string DefaultModel = "default_model";
    private const string SystemPrompt = "system_prompt";
    private const string Temperature = "temperature";
    private const string ContextLength = "context_length";
    private const string KeepAliveMinutes = "keep_alive_minutes";
    private const string ApprovalModeKey = "approval_mode";
    private const string CommandTimeoutSeconds = "command_timeout_seconds";
    private const string WorkingDirectory = "working_directory";
    private const string DisabledTools = "disabled_tools";
    private const string ToolsEnabled = "tools_enabled";

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
        Database.RunAsync(
            connection =>
            {
                using var command = Command(connection, "SELECT key, value FROM settings");
                using var reader = command.ExecuteReader();

                var stored = new Dictionary<string, string>(StringComparer.Ordinal);
                while (reader.Read())
                {
                    stored[reader.GetString(0)] = reader.GetString(1);
                }

                return FromRows(stored);
            },
            cancellationToken);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        Database.RunAsync(
            connection =>
            {
                using var transaction = connection.BeginTransaction();
                using var command = Command(
                    connection,
                    "INSERT INTO settings (key, value) VALUES ($key, $value) ON CONFLICT (key) DO UPDATE SET value = excluded.value");
                var key = command.Parameters.Add("$key", SqliteType.Text);
                var value = command.Parameters.Add("$value", SqliteType.Text);

                foreach (var (rowKey, rowValue) in ToRows(settings.Normalized()))
                {
                    key.Value = rowKey;
                    value.Value = rowValue;
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            },
            cancellationToken);

    private static AppSettings FromRows(Dictionary<string, string> rows)
    {
        var defaults = new AppSettings();

        return new AppSettings
        {
            OllamaUrl = rows.GetValueOrDefault(OllamaUrl, defaults.OllamaUrl),
            DefaultModel = rows.GetValueOrDefault(DefaultModel, defaults.DefaultModel),
            SystemPrompt = rows.GetValueOrDefault(SystemPrompt, defaults.SystemPrompt),
            Temperature = ReadDouble(rows, Temperature, defaults.Temperature),
            ContextLength = ReadInt(rows, ContextLength, defaults.ContextLength),
            KeepAliveMinutes = ReadInt(rows, KeepAliveMinutes, defaults.KeepAliveMinutes),
            ApprovalMode = rows.TryGetValue(ApprovalModeKey, out var mode) && Enum.TryParse<ApprovalMode>(mode, out var parsed)
                ? parsed
                : defaults.ApprovalMode,
            CommandTimeoutSeconds = ReadInt(rows, CommandTimeoutSeconds, defaults.CommandTimeoutSeconds),
            WorkingDirectory = rows.GetValueOrDefault(WorkingDirectory, defaults.WorkingDirectory),
            ToolsEnabled = rows.TryGetValue(ToolsEnabled, out var toolsOn) && bool.TryParse(toolsOn, out var on)
                ? on
                : defaults.ToolsEnabled,
            DisabledTools = rows.TryGetValue(DisabledTools, out var tools)
                ? tools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal)
                : defaults.DisabledTools,
        }.Normalized();
    }

    private static IEnumerable<(string Key, string Value)> ToRows(AppSettings settings)
    {
        yield return (OllamaUrl, settings.OllamaUrl);
        yield return (DefaultModel, settings.DefaultModel);
        yield return (SystemPrompt, settings.SystemPrompt);
        yield return (Temperature, settings.Temperature.ToString("R", CultureInfo.InvariantCulture));
        yield return (ContextLength, settings.ContextLength.ToString(CultureInfo.InvariantCulture));
        yield return (KeepAliveMinutes, settings.KeepAliveMinutes.ToString(CultureInfo.InvariantCulture));
        yield return (ApprovalModeKey, settings.ApprovalMode.ToString());
        yield return (CommandTimeoutSeconds, settings.CommandTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
        yield return (WorkingDirectory, settings.WorkingDirectory);
        yield return (ToolsEnabled, settings.ToolsEnabled.ToString());
        yield return (DisabledTools, string.Join(',', settings.DisabledTools.Order(StringComparer.Ordinal)));
    }

    private static int ReadInt(Dictionary<string, string> rows, string key, int fallback) =>
        rows.TryGetValue(key, out var text)
        && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static double ReadDouble(Dictionary<string, string> rows, string key, double fallback) =>
        rows.TryGetValue(key, out var text)
        && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
}
