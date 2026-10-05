using UnlockedAI.Core.Chat;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Errors;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Ollama;
using UnlockedAI.Core.Tools;
using UnlockedAI.Tests.Support;

namespace UnlockedAI.Tests.Chat;

public sealed class ChatSessionToolTests : IDisposable
{
    private readonly Database _database = Database.OpenInMemory();
    private readonly ConversationRepository _conversations;
    private readonly MessageRepository _messages;
    private readonly SettingsService _settings;
    private readonly FakeOllama _ollama = new();
    private readonly FakeTool _lookup = new("lookup", ToolRisk.ReadOnly, "42");
    private readonly FakeTool _risky = new("risky", ToolRisk.ChangesPc, "changed");

    public ChatSessionToolTests()
    {
        _conversations = new ConversationRepository(_database);
        _messages = new MessageRepository(_database);
        _settings = new SettingsService(new SettingsRepository(_database));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Tool_result_goes_back_to_the_model_which_then_answers()
    {
        var conversation = await StartChatAsync();
        _ollama
            .Reply(new ModelToolCall(Call("lookup", """{"text":"answer"}""")))
            .Reply(new ModelText("It is 42."));

        var events = await Session().RunAsync(conversation, useTools: true, Ct).ToListAsync(Ct);

        var started = Assert.Single(events.OfType<ToolStarted>());
        Assert.Equal(("lookup", "Fake lookup", "lookup answer", false), (started.ToolName, started.Title, started.Summary, started.NeedsApproval));
        var finished = Assert.Single(events.OfType<ToolFinished>());
        Assert.Equal((started.CallId, ToolOutcome.Succeeded, "42"), (finished.CallId, finished.Outcome, finished.Output));
        Assert.Equal("It is 42.", ((AssistantCompleted)events[^1]).Message.Content);

        // The second request carries the model's own tool call and the tool's answer.
        var secondRequest = _ollama.Requests[1].Turns;
        Assert.Equal("lookup", Assert.Single(secondRequest[^2].ToolCalls!).Name);
        Assert.Equal((ChatRole.Tool, "42", "lookup"), (secondRequest[^1].Role, secondRequest[^1].Content, secondRequest[^1].ToolName));

        var stored = await _messages.ListAsync(conversation.Id, Ct);
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant], stored.Select(message => message.Role));
        Assert.Equal(ToolOutcome.Succeeded, stored[2].ToolOutcome);
    }

    [Fact]
    public async Task Offered_tools_come_with_guidance_in_the_system_prompt()
    {
        var conversation = await StartChatAsync();
        _ollama.Reply(new ModelText("hello"));

        await Session().RunAsync(conversation, useTools: true, Ct).ToListAsync(Ct);

        var (_, turns, tools) = Assert.Single(_ollama.Requests);
        Assert.Equal(["lookup", "risky"], tools.Select(tool => tool.Name));
        Assert.Equal(ContextBuilder.ToolGuidance, turns[0].Content);
    }

    [Fact]
    public async Task Model_without_tool_support_is_offered_none()
    {
        var conversation = await StartChatAsync();
        _ollama.Reply(new ModelText("hello"));

        await Session().RunAsync(conversation, useTools: false, Ct).ToListAsync(Ct);

        var (_, turns, tools) = Assert.Single(_ollama.Requests);
        Assert.Empty(tools);
        Assert.Equal([ChatRole.System, ChatRole.User], turns.Select(turn => turn.Role));
        Assert.DoesNotContain(turns, turn => turn.Content.Contains(ContextBuilder.ToolGuidance));
    }

    [Fact]
    public async Task Tool_call_the_model_wrote_out_as_text_is_run_and_not_kept_as_a_message()
    {
        var conversation = await StartChatAsync();
        _ollama
            .Reply(new ModelText("Answer: {\"name\": \"lookup\", "), new ModelText("\"parameters\": {\"text\": \"x\"}}"))
            .Reply(new ModelText("It is 42."));

        var events = await Session().RunAsync(conversation, useTools: true, Ct).ToListAsync(Ct);

        Assert.Equal(1, _lookup.Runs);
        Assert.Equal("lookup x", Assert.Single(events.OfType<ToolStarted>()).Summary);

        var stored = await _messages.ListAsync(conversation.Id, Ct);
        Assert.Equal("", stored[1].Content);
        Assert.Equal("lookup", Assert.Single(stored[1].ToolCalls).Name);
        Assert.Equal("It is 42.", stored[^1].Content);
    }

    [Fact]
    public async Task Reply_cut_at_the_length_limit_with_nothing_to_show_is_reported_as_stuck()
    {
        var conversation = await StartChatAsync();
        _ollama.Reply(new ModelDone(100, 4096, HitLengthLimit: true));

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await Session().RunAsync(conversation, useTools: true, Ct).ToListAsync(Ct));

        Assert.Equal("The model got stuck", exception.Error.Title);
        Assert.True(exception.Error.IsRetryable);
    }

    [Fact]
    public async Task Risky_tool_waits_for_the_user_and_runs_when_allowed()
    {
        var conversation = await StartChatAsync();
        var gate = new FakeGate(ApprovalDecision.AllowOnce);
        _ollama.Reply(new ModelToolCall(Call("risky"))).Reply(new ModelText("done"));

        var events = await Session(gate).RunAsync(conversation, useTools: true, Ct).ToListAsync(Ct);

        var started = Assert.Single(events.OfType<ToolStarted>());
        Assert.True(started.NeedsApproval);
        Assert.Equal([started.CallId], gate.Asked);
        Assert.Single(events.OfType<ToolRunning>());
        Assert.Equal(1, _risky.Runs);
    }

    [Fact]
    public async Task Denied_tool_does_not_run_and_the_model_is_told()
    {
        var conversation = await StartChatAsync();
        _ollama.Reply(new ModelToolCall(Call("risky"))).Reply(new ModelText("understood"));

        var events = await Session(new FakeGate(ApprovalDecision.Deny)).RunAsync(conversation, useTools: true, Ct).ToListAsync(Ct);

        Assert.Equal(0, _risky.Runs);
        Assert.Empty(events.OfType<ToolRunning>());
        Assert.Equal(ToolOutcome.Denied, Assert.Single(events.OfType<ToolFinished>()).Outcome);
        Assert.Equal(ToolResult.DeniedMessage, _ollama.Requests[1].Turns[^1].Content);
        Assert.Equal(ToolOutcome.Denied, (await _messages.ListAsync(conversation.Id, Ct))[2].ToolOutcome);
    }

    [Fact]
    public async Task Allowing_for_the_chat_stops_further_questions_for_that_tool_in_that_chat_only()
    {
        var first = await StartChatAsync();
        var second = await StartChatAsync();
        var gate = new FakeGate(ApprovalDecision.AllowForChat, ApprovalDecision.Deny);
        var session = Session(gate);
        _ollama
            .Reply(new ModelToolCall(Call("risky"))).Reply(new ModelToolCall(Call("risky"))).Reply(new ModelText("done"))
            .Reply(new ModelToolCall(Call("risky"))).Reply(new ModelText("ok"));

        await session.RunAsync(first, useTools: true, Ct).ToListAsync(Ct);
        Assert.Single(gate.Asked);
        Assert.Equal(2, _risky.Runs);

        // A different chat asks again.
        await session.RunAsync(second, useTools: true, Ct).ToListAsync(Ct);
        Assert.Equal(2, gate.Asked.Count);
        Assert.Equal(2, _risky.Runs);
    }

    [Fact]
    public async Task Allow_everything_mode_never_asks()
    {
        await _settings.SaveAsync(new AppSettings { ApprovalMode = ApprovalMode.AllowEverything }, Ct);
        var conversation = await StartChatAsync();
        var gate = new FakeGate();
        _ollama.Reply(new ModelToolCall(Call("risky"))).Reply(new ModelText("done"));

        var events = await Session(gate).RunAsync(conversation, useTools: true, Ct).ToListAsync(Ct);

        Assert.Empty(gate.Asked);
        Assert.False(Assert.Single(events.OfType<ToolStarted>()).NeedsApproval);
        Assert.Equal(1, _risky.Runs);
    }

    [Fact]
    public async Task Unknown_or_switched_off_tool_fails_without_stopping_the_reply()
    {
        await _settings.SaveAsync(new AppSettings { DisabledTools = new HashSet<string> { "lookup" } }, Ct);
        var conversation = await StartChatAsync();
        _ollama
            .Reply(new ModelToolCall(Call("made_up")), new ModelToolCall(Call("lookup")))
            .Reply(new ModelText("sorry"));

        var events = await Session().RunAsync(conversation, useTools: true, Ct).ToListAsync(Ct);

        Assert.Equal(["risky"], _ollama.Requests[0].Tools.Select(tool => tool.Name));
        Assert.All(events.OfType<ToolFinished>(), finished => Assert.Equal(ToolOutcome.Failed, finished.Outcome));
        Assert.Equal(2, events.OfType<ToolFinished>().Count());
        Assert.Equal(0, _lookup.Runs);
        Assert.Equal("sorry", ((AssistantCompleted)events[^1]).Message.Content);
    }

    [Fact]
    public async Task Arguments_that_are_not_valid_json_reach_the_tool_as_an_empty_object()
    {
        var conversation = await StartChatAsync();
        _ollama.Reply(new ModelToolCall(Call("lookup", "{not json"))).Reply(new ModelText("ok"));

        var events = await Session().RunAsync(conversation, useTools: true, Ct).ToListAsync(Ct);

        Assert.Equal(ToolOutcome.Succeeded, Assert.Single(events.OfType<ToolFinished>()).Outcome);
    }

    [Fact]
    public async Task Stopping_while_waiting_for_approval_records_an_answer_for_every_call()
    {
        var conversation = await StartChatAsync();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        _ollama.Reply(new ModelToolCall(Call("risky")), new ModelToolCall(Call("lookup")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            // The gate has no prepared answer, so the first call waits until the stop.
            await foreach (var chatEvent in Session(new FakeGate()).RunAsync(conversation, useTools: true, stop.Token))
            {
                if (chatEvent is ToolStarted)
                {
                    stop.CancelAfter(TimeSpan.FromMilliseconds(50));
                }
            }
        });

        var stored = await _messages.ListAsync(conversation.Id, Ct);
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Tool], stored.Select(message => message.Role));
        Assert.Equal(["risky", "lookup"], stored.Skip(2).Select(message => message.ToolName));
        Assert.All(stored.Skip(2), message => Assert.Equal(ToolOutcome.Failed, message.ToolOutcome));
        Assert.Equal(0, _risky.Runs + _lookup.Runs);
    }

    [Fact]
    public async Task Model_that_never_stops_calling_tools_is_cut_off()
    {
        var conversation = await StartChatAsync();
        for (var round = 0; round < ChatSession.MaxRounds; round++)
        {
            _ollama.Reply(new ModelToolCall(Call("lookup")));
        }

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await Session().RunAsync(conversation, useTools: true, Ct).ToListAsync(Ct));

        Assert.Equal(AppErrorKind.ToolFailed, exception.Error.Kind);
        Assert.Equal(ChatSession.MaxRounds, _lookup.Runs);
    }

    private static ToolCall Call(string name, string arguments = "{}") => new(name, arguments);

    private ChatSession Session(IApprovalGate? gate = null) => new(
        _ollama,
        _messages,
        new AttachmentRepository(_database),
        _settings,
        new ToolRegistry([_lookup, _risky]),
        gate ?? new FakeGate(),
        FixedClock.Default);

    private async Task<Conversation> StartChatAsync()
    {
        var conversation = await _conversations.CreateAsync("Test chat", "test-model", Ct);
        await _messages.AppendAsync(conversation.Id, new NewMessage(ChatRole.User, "question"), Ct);
        return conversation;
    }
}
