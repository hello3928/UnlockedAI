using System.Runtime.CompilerServices;
using UnlockedAI.Core.Chat;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Errors;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Ollama;
using UnlockedAI.Core.Tools;
using UnlockedAI.Tests.Support;

namespace UnlockedAI.Tests.Chat;

public sealed class ChatSessionTests : IDisposable
{
    private readonly Database _database = Database.OpenInMemory();
    private readonly ConversationRepository _conversations;
    private readonly MessageRepository _messages;
    private readonly SettingsService _settings;
    private readonly FakeOllama _ollama = new();
    private readonly ChatSession _session;

    public ChatSessionTests()
    {
        _conversations = new ConversationRepository(_database);
        _messages = new MessageRepository(_database);
        _settings = new SettingsService(new SettingsRepository(_database));
        _session = new ChatSession(
            _ollama,
            _messages,
            new AttachmentRepository(_database),
            _settings,
            new ToolRegistry([]),
            new FakeGate(),
            FixedClock.Default);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Reply_is_streamed_then_saved_whole()
    {
        var conversation = await StartChatAsync("hi");
        _ollama.Reply(new ModelText("Hel"), new ModelText("lo"), new ModelDone(3, 2));

        var events = await _session.RunAsync(conversation, useTools: false, Ct).ToListAsync(Ct);

        Assert.IsType<AssistantStarted>(events[0]);
        Assert.Equal(["Hel", "lo"], events.OfType<AssistantDelta>().Select(delta => delta.Text));
        var completed = Assert.IsType<AssistantCompleted>(events[^1]);
        Assert.Equal("Hello", completed.Message.Content);

        var stored = await _messages.ListAsync(conversation.Id, Ct);
        Assert.Equal([ChatRole.User, ChatRole.Assistant], stored.Select(message => message.Role));
        Assert.Equal("Hello", stored[1].Content);
    }

    [Fact]
    public async Task Request_uses_the_chats_model_the_settings_and_the_system_prompt()
    {
        await _settings.SaveAsync(
            new AppSettings { SystemPrompt = "Be brief.", Temperature = 0.2, ContextLength = 4096, KeepAliveMinutes = 1 },
            Ct);
        var conversation = await StartChatAsync("hi", model: "chosen-model");
        _ollama.Reply(new ModelText("ok"));

        await _session.RunAsync(conversation, useTools: false, Ct).ToListAsync(Ct);

        var (options, turns, tools) = Assert.Single(_ollama.Requests);
        Assert.Equal(new ModelOptions("chosen-model", 0.2, 4096, 1), options);
        Assert.Equal([ChatRole.System, ChatRole.System, ChatRole.User], turns.Select(turn => turn.Role));
        Assert.Equal("Be brief.", turns[0].Content);
        Assert.Equal("It is Monday, 9 March 2026, 14:30 (UTC+10:00).", turns[1].Content);
        Assert.Empty(tools);
    }

    [Fact]
    public async Task Stopping_mid_reply_keeps_the_part_already_shown()
    {
        var conversation = await StartChatAsync("tell me a story");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        _ollama.Reply(SlowReply);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chatEvent in _session.RunAsync(conversation, useTools: false, stop.Token))
            {
                if (chatEvent is AssistantDelta)
                {
                    await stop.CancelAsync();
                }
            }
        });

        var stored = await _messages.ListAsync(conversation.Id, Ct);
        Assert.Equal("Once upon", stored[^1].Content);
        Assert.Equal(ChatRole.Assistant, stored[^1].Role);

        static async IAsyncEnumerable<ModelEvent> SlowReply([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new ModelText("Once upon");
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    [Fact]
    public async Task Failure_before_any_text_saves_nothing()
    {
        var conversation = await StartChatAsync("hi");
        _ollama.Reply(_ => Fail());

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await _session.RunAsync(conversation, useTools: false, Ct).ToListAsync(Ct));

        Assert.Equal(AppErrorKind.OllamaUnreachable, exception.Error.Kind);
        Assert.Equal(ChatRole.User, Assert.Single(await _messages.ListAsync(conversation.Id, Ct)).Role);

        static async IAsyncEnumerable<ModelEvent> Fail()
        {
            await Task.Yield();
            throw new AppException(AppError.OllamaUnreachable("http://localhost:11434"));
#pragma warning disable CS0162 // Unreachable code: the yield makes this method an iterator.
            yield break;
#pragma warning restore CS0162
        }
    }

    [Fact]
    public async Task Empty_reply_is_a_retryable_error_and_is_not_saved()
    {
        var conversation = await StartChatAsync("hi");
        _ollama.Reply(new ModelDone(1, 0));

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await _session.RunAsync(conversation, useTools: false, Ct).ToListAsync(Ct));

        Assert.True(exception.Error.IsRetryable);
        Assert.Single(await _messages.ListAsync(conversation.Id, Ct));
    }

    private async Task<Conversation> StartChatAsync(string firstMessage, string model = "test-model")
    {
        var conversation = await _conversations.CreateAsync("Test chat", model, Ct);
        await _messages.AppendAsync(conversation.Id, new NewMessage(ChatRole.User, firstMessage), Ct);
        return conversation;
    }
}
