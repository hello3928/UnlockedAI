using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using UnlockedAI.Core.Chat;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Errors;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Ollama;
using UnlockedAI.Platform;

namespace UnlockedAI.ViewModels;

/// <summary>The open chat: its messages, its model, the draft being typed, and sending and stopping.</summary>
public sealed partial class ChatViewModel : ViewModelBase
{
    private const int TitleLength = 48;

    private readonly ConversationRepository _conversations;
    private readonly MessageRepository _messages;
    private readonly ChatSession _session;
    private readonly IOllamaClient _ollama;
    private readonly SettingsService _settings;
    private readonly UiBatcher<ChatEvent> _events;

    // The reply being written. Text gathers here and reaches the screen once per batch.
    private readonly StringBuilder _liveText = new();
    private MessageViewModel? _live;
    private bool _liveTextChanged;

    private Conversation? _conversation;
    private CancellationTokenSource? _stop;
    private Task _generation = Task.CompletedTask;

    // Counts requests to show a different chat, so a slow load can tell it has been overtaken.
    private int _openRequests;

    // True while this class is changing the selected model itself, so that isn't saved as the user's choice.
    private bool _syncingModel;

    public ChatViewModel(
        ConversationRepository conversations,
        MessageRepository messages,
        ChatSession session,
        IOllamaClient ollama,
        SettingsService settings)
    {
        _conversations = conversations;
        _messages = messages;
        _session = session;
        _ollama = ollama;
        _settings = settings;
        _events = new UiBatcher<ChatEvent>(DispatcherQueue.GetForCurrentThread(), Apply, FlushLiveText);

        Draft = "";
        Models = [];
        Messages = [];

        // Through ShowMessages, so that this first list also keeps IsEmpty up to date.
        ShowMessages([]);
    }

    /// <summary>Raised when the open chat is created or gets new activity, so lists of chats can refresh.</summary>
    public event EventHandler? ConversationChanged;

    /// <summary>Raised after a saved chat has been put on screen.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised after the screen has been cleared for a new chat.</summary>
    public event EventHandler? Started;

    /// <summary>Id of the open chat, or null while it is new and nothing has been sent.</summary>
    public long? ConversationId => _conversation?.Id;

    public string ComposerPlaceholder =>
        SelectedModel is { } model ? $"Message {ShortName(model.Name)}" : "Message";

    /// <summary>Replaced as a whole when a chat is opened, so the list view resets once instead of per message.</summary>
    [ObservableProperty]
    public partial ObservableCollection<MessageViewModel> Messages { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<ModelInfo> Models { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ComposerPlaceholder))]
    public partial ModelInfo? SelectedModel { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Draft { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    private bool CanSend => !IsGenerating && !string.IsNullOrWhiteSpace(Draft);

    /// <summary>True when the chat ends with something the model hasn't answered yet.</summary>
    private bool IsAwaitingReply => _conversation is not null && Messages.LastOrDefault() is { IsUser: true };

    /// <summary>A model name without the default ":latest" tag.</summary>
    public static string ShortName(string modelName) =>
        modelName.EndsWith(":latest", StringComparison.Ordinal) ? modelName[..^":latest".Length] : modelName;

    public Task InitializeAsync() => RefreshModelsAsync(quiet: false);

    /// <summary>Shows a saved chat. Stops any reply in progress first.</summary>
    public async Task OpenAsync(long conversationId)
    {
        if (_conversation?.Id == conversationId)
        {
            return;
        }

        var request = ++_openRequests;
        await StopAndWaitAsync();

        await TryAsync(async () =>
        {
            var conversation = await _conversations.GetAsync(conversationId)
                ?? throw new AppException(AppError.Storage("That chat no longer exists."));
            var stored = await _messages.ListAsync(conversationId);

            if (request != _openRequests)
            {
                return;
            }

            _conversation = conversation;
            Draft = "";
            Error = null;
            ShowMessages(stored.Where(IsShown).Select(message => new MessageViewModel(message)));
            SelectModel(conversation.Model);
            Opened?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>Clears the screen for a chat that doesn't exist until its first message is sent.</summary>
    public async Task StartNewAsync()
    {
        ++_openRequests;
        await StopAndWaitAsync();

        _conversation = null;
        Draft = "";
        Error = null;
        ShowMessages([]);
        SelectModel(_settings.Current.DefaultModel);
        Started?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Picks up saved settings: the model list may come from a new address, and new chats use the new default.</summary>
    public async Task ApplySettingsAsync()
    {
        Error = null;
        await RefreshModelsAsync(quiet: false);

        if (_conversation is null)
        {
            SelectModel(_settings.Current.DefaultModel);
        }
    }

    /// <summary>
    /// Reloads the installed models from Ollama. With <paramref name="quiet"/>, a failure is ignored;
    /// use that for background refreshes the user didn't ask for.
    /// </summary>
    public async Task RefreshModelsAsync(bool quiet)
    {
        try
        {
            var models = await _ollama.ListModelsAsync();
            if (!models.SequenceEqual(Models))
            {
                var wanted = SelectedModel?.Name ?? _conversation?.Model ?? _settings.Current.DefaultModel;
                Models = models;
                SelectModel(wanted);
            }
        }
        catch (Exception exception) when (!quiet)
        {
            Report(exception);
        }
        catch (Exception)
        {
            // Background refresh: the list on screen stays as it was.
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendAsync()
    {
        var text = Draft.Trim();
        Draft = "";

        return Generate(async cancellationToken =>
        {
            try
            {
                _conversation ??= await _conversations.CreateAsync(
                    MakeTitle(text),
                    SelectedModel?.Name ?? _settings.Current.DefaultModel,
                    cancellationToken);

                var saved = await _messages.AppendAsync(
                    _conversation.Id,
                    new NewMessage(ChatRole.User, text),
                    cancellationToken);
                Messages.Add(new MessageViewModel(saved));
            }
            catch
            {
                // Nothing was sent, so give the text back instead of losing it.
                Draft = text;
                throw;
            }
        });
    }

    /// <summary>Tries again whatever just failed: loading the models, or getting a reply.</summary>
    [RelayCommand]
    private async Task RetryAsync()
    {
        Error = null;

        if (Models.Count == 0)
        {
            await RefreshModelsAsync(quiet: false);
        }

        if (Error is null && IsAwaitingReply)
        {
            await Generate(static _ => Task.CompletedTask);
        }
    }

    [RelayCommand(CanExecute = nameof(IsGenerating))]
    private void Stop() => _stop?.Cancel();

    partial void OnSelectedModelChanged(ModelInfo? value)
    {
        if (_syncingModel || value is null || _conversation is null || _conversation.Model == value.Name)
        {
            return;
        }

        // The model is part of the chat, so the choice is saved with it.
        _conversation = _conversation with { Model = value.Name };
        var id = _conversation.Id;
        _ = TryAsync(() => _conversations.SetModelAsync(id, value.Name));
    }

    private static bool IsShown(ChatMessage message) =>
        message.Role is ChatRole.User or ChatRole.Assistant && message.Content.Length > 0;

    private static string MakeTitle(string firstMessage)
    {
        var firstLine = firstMessage.AsSpan().Trim();
        var lineEnd = firstLine.IndexOfAny('\r', '\n');
        if (lineEnd >= 0)
        {
            firstLine = firstLine[..lineEnd].TrimEnd();
        }

        return firstLine.Length <= TitleLength
            ? firstLine.ToString()
            : string.Concat(firstLine[..TitleLength].TrimEnd(), "…");
    }

    private void ShowMessages(IEnumerable<MessageViewModel> messages)
    {
        var collection = new ObservableCollection<MessageViewModel>(messages);
        collection.CollectionChanged += (_, _) => IsEmpty = collection.Count == 0;

        Messages = collection;
        IsEmpty = collection.Count == 0;
    }

    /// <summary>Selects a model by name. A model that is no longer installed is still shown, so the chat says what it used.</summary>
    private void SelectModel(string name)
    {
        _syncingModel = true;
        try
        {
            var match = Models.FirstOrDefault(model => model.Name == name);
            if (match is null && _conversation is not null)
            {
                match = new ModelInfo(name, SizeBytes: 0, SupportsTools: false, ParameterSize: null);
                Models = [match, .. Models];
            }

            SelectedModel = match ?? Models.FirstOrDefault();
        }
        finally
        {
            _syncingModel = false;
        }
    }

    private async Task StopAndWaitAsync()
    {
        _stop?.Cancel();
        await _generation;
    }

    private Task Generate(Func<CancellationToken, Task> prepare)
    {
        if (!IsGenerating)
        {
            _generation = GenerateAsync(prepare);
        }

        return _generation;
    }

    /// <summary>Never throws: failures are reported through <see cref="ViewModelBase.Error"/>.</summary>
    private async Task GenerateAsync(Func<CancellationToken, Task> prepare)
    {
        Error = null;
        IsGenerating = true;
        _stop = new CancellationTokenSource();
        var cancellationToken = _stop.Token;

        try
        {
            await prepare(cancellationToken);
            var conversation = _conversation!;
            ConversationChanged?.Invoke(this, EventArgs.Empty);

            _events.Start();

            // The session runs on a worker thread and posts events; the UI catches up in batches.
            await Task.Run(
                async () =>
                {
                    await foreach (var chatEvent in _session.RunAsync(conversation, cancellationToken).ConfigureAwait(false))
                    {
                        _events.Post(chatEvent);
                    }
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            Report(exception);
        }
        finally
        {
            _events.Stop();
            FinishLiveMessage();

            _stop.Dispose();
            _stop = null;
            IsGenerating = false;
            ConversationChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Apply(ChatEvent chatEvent)
    {
        switch (chatEvent)
        {
            case AssistantStarted:
                FinishLiveMessage();
                _live = new MessageViewModel(ChatRole.Assistant) { IsStreaming = true };
                Messages.Add(_live);
                break;

            case AssistantDelta delta:
                _liveText.Append(delta.Text);
                _liveTextChanged = true;
                break;

            case AssistantCompleted:
                FinishLiveMessage();
                break;
        }
    }

    private void FlushLiveText()
    {
        if (_liveTextChanged && _live is not null)
        {
            _live.Text = _liveText.ToString();
        }

        _liveTextChanged = false;
    }

    private void FinishLiveMessage()
    {
        if (_live is null)
        {
            return;
        }

        FlushLiveText();
        _live.IsStreaming = false;

        // Nothing arrived (the request failed before the first word), so don't leave an empty row behind.
        if (_live.Text.Length == 0)
        {
            Messages.Remove(_live);
        }

        _live = null;
        _liveText.Clear();
    }
}
