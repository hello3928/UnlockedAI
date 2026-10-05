using UnlockedAI.Core.Chat;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Files;
using UnlockedAI.Core.Ollama;
using UnlockedAI.Core.Search;
using UnlockedAI.Core.Secrets;
using UnlockedAI.Core.Tools;
using UnlockedAI.Core.Tools.Builtin;
using UnlockedAI.Core.Web;
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
    private readonly WebReader _web;

    public AppHost()
    {
        _database = Database.Open(AppPaths.DatabaseFile);

        var conversations = new ConversationRepository(_database);
        var messages = new MessageRepository(_database);
        var attachments = new AttachmentRepository(_database);

        Settings = new SettingsService(new SettingsRepository(_database));
        _ollama = new OllamaClient(Settings);

        // One web client for every tool that goes online.
        _web = new WebReader();
        var files = new AttachmentService();
        var search = new SearchService(new DuckDuckGoSearch(_web), new OllamaWebSearch(_web, Secrets));
        Tools = BuiltinTools.CreateRegistry(Settings, _web, search, files);

        var approvals = new ApprovalGate();
        var session = new ChatSession(_ollama, messages, attachments, Settings, Tools, approvals);

        Chat = new ChatViewModel(conversations, messages, session, _ollama, Settings, files, FilePicker, Tools, approvals);
        Shell = new ShellViewModel(conversations, Chat);
    }

    public SettingsService Settings { get; }

    public ISecretStore Secrets { get; } = new CredentialLockerSecretStore();

    public ToolRegistry Tools { get; }

    public FilePickerService FilePicker { get; } = new();

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
        _web.Dispose();
        _ollama.Dispose();
        _database.Dispose();
    }
}
