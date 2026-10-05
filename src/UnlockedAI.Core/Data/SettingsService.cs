using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Data;

/// <summary>
/// Holds the settings in memory so services can read <see cref="Current"/> without touching the database.
/// </summary>
public sealed class SettingsService(SettingsRepository repository)
{
    public AppSettings Current { get; private set; } = new();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Current = await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = settings.Normalized();
        await repository.SaveAsync(normalized, cancellationToken).ConfigureAwait(false);

        Current = normalized;
    }
}
