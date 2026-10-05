using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using UnlockedAI.Core.Chat;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Models;
using UnlockedAI.Platform;

namespace UnlockedAI.ViewModels;

/// <summary>The open chat: its messages, the draft being typed, and sending and stopping.</summary>
public sealed partial class ChatViewModel : ViewModelBase
{
    private const int TitleLength = 48;

    private readonly ConversationRepository _conversations;
    private readonly MessageRepository _messages;
    private readonly ChatSession _session;
    private readonly SettingsService _settings;
    private readonly UiBatcher<ChatEvent> _events;

    // The reply being written. Text gathers here and reaches the screen once per batch.
    private readonly StringBuilder _liveText = new();
    private MessageViewModel? _live;
    private bool _liveTextChanged;

    private Conversation? _conversation;
    private CancellationTokenSource? _stop;

    public ChatViewModel(
        ConversationRepository conversations,
        MessageRepository messages,
        ChatSession session,
        SettingsService settings)
    {
        _conversations = conversations;
        _messages = messages;
        _session = session;
        _settings = settings;
        _events = new UiBatcher<ChatEvent>(DispatcherQueue.GetForCurrentThread(), Apply, FlushLiveText);

        Draft = "";
        IsEmpty = true;
        Messages.CollectionChanged += (_, _) => IsEmpty = Messages.Count == 0;
    }

    /// <summary>Raised when the open chat is created or gets new activity, so lists of chats can refresh.</summary>
    public event EventHandler? ConversationChanged;

    public ObservableCollection<MessageViewModel> Messages { get; } = [];

    /// <summary>Id of the open chat, or null while it is new and nothing has been sent.</summary>
    public long? ConversationId => _conversation?.Id;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Draft { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    private bool CanSend => !IsGenerating && !string.IsNullOrWhiteSpace(Draft);

    private bool CanRetry => !IsGenerating && _conversation is not null;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendAsync()
    {
        var text = Draft.Trim();
        Draft = "";

        return GenerateAsync(async cancellationToken =>
        {
            try
            {
                _conversation ??= await _conversations.CreateAsync(
                    MakeTitle(text),
                    _settings.Current.DefaultModel,
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

    /// <summary>Asks the model again for a reply to what is already in the chat.</summary>
    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync() => GenerateAsync(static _ => Task.CompletedTask);

    [RelayCommand(CanExecute = nameof(IsGenerating))]
    private void Stop() => _stop?.Cancel();

    private async Task GenerateAsync(Func<CancellationToken, Task> prepare)
    {
        if (IsGenerating)
        {
            return;
        }

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
}
