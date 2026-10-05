using UnlockedAI.Core.Chat;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Ollama;
using UnlockedAI.ViewModels;

namespace UnlockedAI.Platform;

/// <summary>
/// Builds the app's long-lived objects once and owns them until the window closes.
/// This is the only place that knows how the pieces are wired together.
/// </summary>
internal sealed class AppHost : IDisposable
{
    private readonly Database _database;
    private readonly OllamaClient _ollama;

    public AppHost()
    {
        _database = Database.Open(AppPaths.DatabaseFile);

        var conversations = new ConversationRepository(_database);
        var messages = new MessageRepository(_database);
        var attachments = new AttachmentRepository(_database);

        Settings = new SettingsService(new SettingsRepository(_database));
        _ollama = new OllamaClient(Settings);

        var session = new ChatSession(_ollama, messages, attachments, Settings);
        Chat = new ChatViewModel(conversations, messages, session, _ollama, Settings);
        Shell = new ShellViewModel(conversations, Chat);
    }

    public SettingsService Settings { get; }

    public ChatViewModel Chat { get; }

    public ShellViewModel Shell { get; }

    /// <summary>Loads what the first screen needs. Settings come first because everything else reads them.</summary>
    public async Task InitializeAsync()
    {
        await Settings.LoadAsync();
        await Shell.InitializeAsync();

        // Not awaited: the window should open even while Ollama is slow or not running.
        _ = Chat.InitializeAsync();
    }

    public void Dispose()
    {
        _ollama.Dispose();
        _database.Dispose();
    }
}
