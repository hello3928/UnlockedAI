using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using UnlockedAI.Core.Chat;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Errors;
using UnlockedAI.Core.Files;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Ollama;
using UnlockedAI.Core.Tools;
using UnlockedAI.Platform;

namespace UnlockedAI.ViewModels;

/// <summary>The open chat: its messages, its model, the draft being typed, and sending and stopping.</summary>
public sealed partial class ChatViewModel : ViewModelBase
{
    private const int TitleLength = 48;
    private const int MaxAttachments = 5;

    // Rough rule of thumb for English text; good enough to warn before the model silently drops part of a file.
    private const int CharsPerToken = 4;

    private readonly ConversationRepository _conversations;
    private readonly MessageRepository _messages;
    private readonly ChatSession _session;
    private readonly IOllamaClient _ollama;
    private readonly SettingsService _settings;
    private readonly AttachmentService _attachmentService;
    private readonly FilePickerService _filePicker;
    private readonly ToolRegistry _tools;
    private readonly ApprovalGate _approvals;
    private readonly UiBatcher<ChatEvent> _events;

    // The reply being written. Text gathers here and reaches the screen once per batch.
    private readonly StringBuilder _liveText = new();
    private MessageViewModel? _live;
    private bool _liveTextChanged;

    // Tool calls of the reply in progress, by the id the session gave them.
    private readonly Dictionary<int, ToolCallViewModel> _liveToolCalls = [];

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
        SettingsService settings,
        AttachmentService attachmentService,
        FilePickerService filePicker,
        ToolRegistry tools,
        ApprovalGate approvals)
    {
        _conversations = conversations;
        _messages = messages;
        _session = session;
        _ollama = ollama;
        _settings = settings;
        _attachmentService = attachmentService;
        _filePicker = filePicker;
        _tools = tools;
        _approvals = approvals;
        _events = new UiBatcher<ChatEvent>(DispatcherQueue.GetForCurrentThread(), Apply, FlushLiveText);

        PendingAttachments.CollectionChanged += (_, _) => OnPendingAttachmentsChanged();
        AttachmentWarning = "";
        ToolsNote = "";
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

    /// <summary>Files read and waiting to go out with the next message.</summary>
    public ObservableCollection<AttachmentViewModel> PendingAttachments { get; } = [];

    /// <summary>Replaced as a whole when a chat is opened, so the list view resets once instead of per message.</summary>
    [ObservableProperty]
    public partial ObservableCollection<ChatItemViewModel> Messages { get; private set; }

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

    [ObservableProperty]
    public partial bool HasPendingAttachments { get; set; }

    /// <summary>True while a picked or dropped file is being read.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsAttaching { get; set; }

    /// <summary>Set when the waiting files hold more text than the model can take in. Empty otherwise.</summary>
    [ObservableProperty]
    public partial string AttachmentWarning { get; set; }

    /// <summary>The Tools switch in the header: whether the model is offered tools. Remembered between sessions.</summary>
    [ObservableProperty]
    public partial bool UseTools { get; set; }

    /// <summary>False when the selected model can't call tools, which disables the switch.</summary>
    [ObservableProperty]
    public partial bool CanUseTools { get; set; }

    /// <summary>True when tools that change the PC run without asking. Shown in the header so it is never on unnoticed.</summary>
    [ObservableProperty]
    public partial bool IsAutoApproveOn { get; set; }

    /// <summary>Says why tools aren't available for the selected model. Empty when they are.</summary>
    [ObservableProperty]
    public partial string ToolsNote { get; set; }

    private bool CanSend =>
        !IsGenerating && !IsAttaching && (!string.IsNullOrWhiteSpace(Draft) || PendingAttachments.Count > 0);

    /// <summary>True when the chat ends with something the model hasn't answered yet.</summary>
    private bool IsAwaitingReply =>
        _conversation is not null && Messages.LastOrDefault() is ToolCallViewModel or MessageViewModel { IsUser: true };

    /// <summary>A model name without the default ":latest" tag.</summary>
    public static string ShortName(string modelName) =>
        modelName.EndsWith(":latest", StringComparison.Ordinal) ? modelName[..^":latest".Length] : modelName;

    public Task InitializeAsync()
    {
        UseTools = _settings.Current.ToolsEnabled;
        ShowToolStatus();
        return RefreshModelsAsync(quiet: false);
    }

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
            PendingAttachments.Clear();
            ShowMessages(ToItems(stored));
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
        PendingAttachments.Clear();
        ShowMessages([]);
        SelectModel(_settings.Current.DefaultModel);
        Started?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Picks up saved settings: the model list may come from a new address, and new chats use the new default.</summary>
    public async Task ApplySettingsAsync()
    {
        Error = null;
        ShowToolStatus();
        OnPendingAttachmentsChanged();
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

    /// <summary>
    /// Reads files and adds them to the next message. A file that can't be used is reported and
    /// skipped; the rest are still added.
    /// </summary>
    public async Task AddFilesAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        Error = null;
        IsAttaching = true;
        try
        {
            foreach (var path in paths)
            {
                if (PendingAttachments.Count >= MaxAttachments)
                {
                    Error = AppError.InvalidInput($"A message can carry up to {MaxAttachments} files. The rest were left out.");
                    break;
                }

                try
                {
                    PendingAttachments.Add(new AttachmentViewModel(await _attachmentService.ReadAsync(path)));
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
            }
        }
        finally
        {
            IsAttaching = false;
        }
    }

    [RelayCommand]
    private async Task AttachAsync()
    {
        try
        {
            await AddFilesAsync(await _filePicker.PickFilesAsync());
        }
        catch (Exception exception)
        {
            Report(exception);
        }
    }

    [RelayCommand]
    private void RemoveAttachment(AttachmentViewModel? attachment)
    {
        if (attachment is not null)
        {
            PendingAttachments.Remove(attachment);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendAsync()
    {
        var text = Draft.Trim();
        var files = PendingAttachments.ToList();
        Draft = "";
        PendingAttachments.Clear();

        return Generate(async cancellationToken =>
        {
            try
            {
                _conversation ??= await _conversations.CreateAsync(
                    MakeTitle(text.Length > 0 ? text : files[0].Name),
                    SelectedModel?.Name ?? _settings.Current.DefaultModel,
                    cancellationToken);

                var saved = await _messages.AppendAsync(
                    _conversation.Id,
                    new NewMessage(ChatRole.User, text) { Attachments = [.. files.Select(file => file.Pending!)] },
                    cancellationToken);
                Messages.Add(new MessageViewModel(saved));
            }
            catch
            {
                // Nothing was sent, so give the text and files back instead of losing them.
                Draft = text;
                foreach (var file in files)
                {
                    PendingAttachments.Add(file);
                }

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

    partial void OnUseToolsChanged(bool value)
    {
        ShowToolStatus();

        if (_settings.Current.ToolsEnabled != value)
        {
            _ = TryAsync(() => _settings.SaveAsync(_settings.Current with { ToolsEnabled = value }));
        }
    }

    partial void OnSelectedModelChanged(ModelInfo? value)
    {
        ShowToolStatus();

        if (_syncingModel || value is null || _conversation is null || _conversation.Model == value.Name)
        {
            return;
        }

        // The model is part of the chat, so the choice is saved with it.
        _conversation = _conversation with { Model = value.Name };
        var id = _conversation.Id;
        _ = TryAsync(() => _conversations.SetModelAsync(id, value.Name));
    }

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

    /// <summary>
    /// Turns saved messages into chat rows. A tool call is saved in two parts, the model's request and
    /// the tool's answer; here they are joined back into one card each.
    /// </summary>
    private List<ChatItemViewModel> ToItems(List<ChatMessage> stored)
    {
        var items = new List<ChatItemViewModel>(stored.Count);
        var unanswered = new Queue<ToolCallViewModel>();

        foreach (var message in stored)
        {
            switch (message.Role)
            {
                case ChatRole.User or ChatRole.Assistant:
                    if (message.Content.Length > 0 || message.Attachments.Count > 0)
                    {
                        items.Add(new MessageViewModel(message));
                    }

                    foreach (var call in message.ToolCalls)
                    {
                        var card = CardFor(call);
                        unanswered.Enqueue(card);
                        items.Add(card);
                    }

                    break;

                case ChatRole.Tool when unanswered.TryDequeue(out var answered):
                    answered.Output = message.Content;
                    answered.State = ToolCallViewModel.StateFor(message.ToolOutcome ?? ToolOutcome.Succeeded);
                    break;
            }
        }

        return items;
    }

    private ToolCallViewModel CardFor(ToolCall call)
    {
        if (_tools.Find(call.Name) is not { } tool)
        {
            return new ToolCallViewModel(0, call.Name, call.Name, call.ArgumentsJson, ToolCallState.Stopped);
        }

        try
        {
            using var arguments = JsonDocument.Parse(call.ArgumentsJson);
            return new ToolCallViewModel(0, tool.Name, tool.Title, tool.Describe(arguments.RootElement), ToolCallState.Stopped);
        }
        catch (JsonException)
        {
            return new ToolCallViewModel(0, tool.Name, tool.Title, call.ArgumentsJson, ToolCallState.Stopped);
        }
    }

    private void ShowMessages(IEnumerable<ChatItemViewModel> items)
    {
        var collection = new ObservableCollection<ChatItemViewModel>(items);
        collection.CollectionChanged += (_, _) => IsEmpty = collection.Count == 0;

        Messages = collection;
        IsEmpty = collection.Count == 0;
    }

    private void ShowToolStatus()
    {
        CanUseTools = SelectedModel?.SupportsTools ?? false;
        ToolsNote = SelectedModel is { SupportsTools: false } ? "This model can't use tools" : "";

        // Only a warning when it is actually in effect: tools on, and a model that can call them.
        IsAutoApproveOn = UseTools && CanUseTools && _settings.Current.ApprovalMode == ApprovalMode.AllowEverything;
    }

    private void OnPendingAttachmentsChanged()
    {
        HasPendingAttachments = PendingAttachments.Count > 0;
        SendCommand.NotifyCanExecuteChanged();

        var contextLength = _settings.Current.ContextLength;
        var fileTokens = PendingAttachments.Sum(file => file.Pending?.Text.Length ?? 0) / CharsPerToken;
        AttachmentWarning = fileTokens > contextLength
            ? $"These files hold about {fileTokens:N0} tokens, more than the context length of {contextLength:N0}. "
              + "The model will only see part of them. You can raise the context length in Settings."
            : "";
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
            var useTools = UseTools && CanUseTools;
            ConversationChanged?.Invoke(this, EventArgs.Empty);

            _events.Start();

            // The session runs on a worker thread and posts events; the UI catches up in batches.
            await Task.Run(
                async () =>
                {
                    await foreach (var chatEvent in _session.RunAsync(conversation, useTools, cancellationToken).ConfigureAwait(false))
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
            FinishLiveToolCalls();

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

            case AssistantCompleted completed:
                // What was saved wins over what streamed: a tool call the model wrote out as text is
                // saved as a call with no message text, so that raw text must leave the screen.
                _liveText.Clear().Append(completed.Message.Content);
                _liveTextChanged = true;
                FinishLiveMessage();
                break;

            case ToolStarted started:
                var card = new ToolCallViewModel(
                    started.CallId,
                    started.ToolName,
                    started.Title,
                    started.Summary,
                    started.NeedsApproval ? ToolCallState.AwaitingApproval : ToolCallState.Running,
                    _approvals);
                _liveToolCalls[started.CallId] = card;
                Messages.Add(card);
                break;

            case ToolRunning running when _liveToolCalls.TryGetValue(running.CallId, out var allowed):
                allowed.State = ToolCallState.Running;
                break;

            case ToolFinished finished when _liveToolCalls.Remove(finished.CallId, out var done):
                done.Output = finished.Output;
                done.State = ToolCallViewModel.StateFor(finished.Outcome);
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

        // Nothing arrived: either the request failed before the first word, or the model went
        // straight to a tool. Either way, don't leave an empty row behind.
        if (_live.Text.Length == 0)
        {
            Messages.Remove(_live);
        }

        _live = null;
        _liveText.Clear();
    }

    /// <summary>Any tool call still open when the reply ends was cut short, so its card must stop saying "Running".</summary>
    private void FinishLiveToolCalls()
    {
        foreach (var card in _liveToolCalls.Values)
        {
            card.State = ToolCallState.Stopped;
        }

        _liveToolCalls.Clear();
    }
}
