using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Ollama;
using UnlockedAI.Core.Secrets;
using UnlockedAI.Core.Tools;

namespace UnlockedAI.ViewModels;

/// <summary>An editable copy of the settings. Nothing is stored until <see cref="SaveAsync"/>.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private static readonly int[] StandardContextLengths = [2048, 4096, 8192, 16384, 32768, 65536, 131072];

    private static readonly NumberOption[] StandardKeepAlive =
    [
        new(0, "Right away"),
        new(5, "5 minutes"),
        new(15, "15 minutes"),
        new(30, "30 minutes"),
        new(60, "1 hour"),
    ];

    private static readonly NumberOption[] StandardTimeouts =
    [
        new(30, "30 seconds"),
        new(60, "1 minute"),
        new(120, "2 minutes"),
        new(300, "5 minutes"),
        new(600, "10 minutes"),
    ];

    private static readonly NumberOption AskFirst = new((int)ApprovalMode.AskForChanges, "Ask me first");
    private static readonly NumberOption RunWithoutAsking = new((int)ApprovalMode.AllowEverything, "Run without asking");

    private readonly SettingsService _settings;
    private readonly ISecretStore _secrets;

    public SettingsViewModel(
        SettingsService settings,
        IReadOnlyList<ModelInfo> installedModels,
        ToolRegistry tools,
        ISecretStore secrets)
    {
        _settings = settings;
        _secrets = secrets;
        var current = settings.Current;

        ModelNames = WithCurrent(installedModels.Select(model => model.Name), current.DefaultModel);
        ContextLengths = WithCurrent(StandardContextLengths, current.ContextLength)
            .Select(length => new NumberOption(length, $"{length.ToString("N0", CultureInfo.CurrentCulture)} tokens"))
            .ToList();
        KeepAliveOptions = WithCurrent(StandardKeepAlive, current.KeepAliveMinutes, minutes => $"{minutes} minutes");
        TimeoutOptions = WithCurrent(StandardTimeouts, current.CommandTimeoutSeconds, seconds => $"{seconds} seconds");
        ApprovalOptions = [AskFirst, RunWithoutAsking];
        Tools = [.. tools.All.Select(tool => new ToolToggleViewModel(tool, !current.DisabledTools.Contains(tool.Name)))];

        OllamaUrl = current.OllamaUrl;
        DefaultModel = current.DefaultModel;
        SystemPrompt = current.SystemPrompt;
        Temperature = current.Temperature;
        ContextLength = ContextLengths.First(option => option.Value == current.ContextLength);
        KeepAlive = KeepAliveOptions.First(option => option.Value == current.KeepAliveMinutes);
        Approval = current.ApprovalMode == ApprovalMode.AllowEverything ? RunWithoutAsking : AskFirst;
        CommandTimeout = TimeoutOptions.First(option => option.Value == current.CommandTimeoutSeconds);
        WorkingDirectory = current.WorkingDirectory;
        OllamaApiKey = secrets.Read(SecretNames.OllamaApiKey) ?? "";
    }

    public IReadOnlyList<string> ModelNames { get; }

    public IReadOnlyList<NumberOption> ContextLengths { get; }

    public IReadOnlyList<NumberOption> KeepAliveOptions { get; }

    public IReadOnlyList<NumberOption> ApprovalOptions { get; }

    public IReadOnlyList<NumberOption> TimeoutOptions { get; }

    public IReadOnlyList<ToolToggleViewModel> Tools { get; }

    public string OllamaUrlError =>
        AppSettings.IsValidOllamaUrl(OllamaUrl) ? "" : "Enter an address that starts with http:// or https://.";

    public string WorkingDirectoryError =>
        WorkingDirectory.Trim().Length == 0 || Directory.Exists(Environment.ExpandEnvironmentVariables(WorkingDirectory.Trim()))
            ? ""
            : "That folder doesn't exist.";

    public string TemperatureLabel =>
        string.Create(CultureInfo.CurrentCulture, $"Temperature: {Temperature:0.0}");

    /// <summary>True when the user has picked "Run without asking", to show what that means before they save.</summary>
    public bool IsAutoApprove => Approval == RunWithoutAsking;

    public bool CanSave => OllamaUrlError.Length == 0 && WorkingDirectoryError.Length == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OllamaUrlError))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string OllamaUrl { get; set; }

    [ObservableProperty]
    public partial string? DefaultModel { get; set; }

    [ObservableProperty]
    public partial string SystemPrompt { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemperatureLabel))]
    public partial double Temperature { get; set; }

    [ObservableProperty]
    public partial NumberOption? ContextLength { get; set; }

    [ObservableProperty]
    public partial NumberOption? KeepAlive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAutoApprove))]
    public partial NumberOption? Approval { get; set; }

    [ObservableProperty]
    public partial NumberOption? CommandTimeout { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkingDirectoryError))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string WorkingDirectory { get; set; }

    [ObservableProperty]
    public partial string OllamaApiKey { get; set; }

    /// <summary>Stores the edited values. Returns false, with <see cref="ViewModelBase.Error"/> set, if that failed.</summary>
    public Task<bool> SaveAsync()
    {
        var current = _settings.Current;
        var edited = current with
        {
            OllamaUrl = OllamaUrl,
            DefaultModel = DefaultModel ?? current.DefaultModel,
            SystemPrompt = SystemPrompt.Trim(),
            Temperature = Math.Round(Temperature, 1),
            ContextLength = ContextLength?.Value ?? current.ContextLength,
            KeepAliveMinutes = KeepAlive?.Value ?? current.KeepAliveMinutes,
            ApprovalMode = IsAutoApprove ? ApprovalMode.AllowEverything : ApprovalMode.AskForChanges,
            CommandTimeoutSeconds = CommandTimeout?.Value ?? current.CommandTimeoutSeconds,
            WorkingDirectory = WorkingDirectory,
            DisabledTools = Tools.Where(tool => !tool.IsEnabled).Select(tool => tool.Name).ToHashSet(),
        };

        return TryAsync(async () =>
        {
            await _settings.SaveAsync(edited);
            _secrets.Write(SecretNames.OllamaApiKey, OllamaApiKey);
        });
    }

    /// <summary>The standard choices, plus the saved value if it isn't one of them, in order.</summary>
    private static List<T> WithCurrent<T>(IEnumerable<T> standard, T current)
        where T : notnull
    {
        var values = standard.ToList();
        if (!values.Contains(current))
        {
            values.Add(current);
            values.Sort();
        }

        return values;
    }

    private static List<NumberOption> WithCurrent(NumberOption[] standard, int current, Func<int, string> label)
    {
        var options = standard.ToList();
        if (options.TrueForAll(option => option.Value != current))
        {
            options.Add(new NumberOption(current, label(current)));
            options.Sort(static (a, b) => a.Value.CompareTo(b.Value));
        }

        return options;
    }
}
