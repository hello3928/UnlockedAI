using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Ollama;

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

    private readonly SettingsService _settings;

    public SettingsViewModel(SettingsService settings, IReadOnlyList<ModelInfo> installedModels)
    {
        _settings = settings;
        var current = settings.Current;

        ModelNames = WithCurrent(installedModels.Select(model => model.Name), current.DefaultModel);
        ContextLengths = WithCurrent(StandardContextLengths, current.ContextLength)
            .Select(length => new NumberOption(length, $"{length.ToString("N0", CultureInfo.CurrentCulture)} tokens"))
            .ToList();
        KeepAliveOptions = StandardKeepAlive.Any(option => option.Value == current.KeepAliveMinutes)
            ? StandardKeepAlive
            : [.. StandardKeepAlive, new NumberOption(current.KeepAliveMinutes, $"{current.KeepAliveMinutes} minutes")];

        OllamaUrl = current.OllamaUrl;
        DefaultModel = current.DefaultModel;
        SystemPrompt = current.SystemPrompt;
        Temperature = current.Temperature;
        ContextLength = ContextLengths.First(option => option.Value == current.ContextLength);
        KeepAlive = KeepAliveOptions.First(option => option.Value == current.KeepAliveMinutes);
    }

    public IReadOnlyList<string> ModelNames { get; }

    public IReadOnlyList<NumberOption> ContextLengths { get; }

    public IReadOnlyList<NumberOption> KeepAliveOptions { get; }

    public string OllamaUrlError =>
        AppSettings.IsValidOllamaUrl(OllamaUrl) ? "" : "Enter an address that starts with http:// or https://.";

    public string TemperatureLabel =>
        string.Create(CultureInfo.CurrentCulture, $"Temperature: {Temperature:0.0}");

    public bool CanSave => OllamaUrlError.Length == 0;

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
        };

        return TryAsync(() => _settings.SaveAsync(edited));
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
}
