using UnlockedAI.Core.Data;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Tests.Data;

public sealed class MessageRepositoryTests : IDisposable
{
    private readonly Database _database = Database.OpenInMemory();
    private readonly ConversationRepository _conversations;
    private readonly MessageRepository _messages;

    public MessageRepositoryTests()
    {
        _conversations = new ConversationRepository(_database);
        _messages = new MessageRepository(_database);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Messages_are_numbered_in_order_per_conversation()
    {
        var first = await _conversations.CreateAsync("A", "m", Ct);
        var second = await _conversations.CreateAsync("B", "m", Ct);

        await _messages.AppendAsync(first.Id, new NewMessage(ChatRole.User, "one"), Ct);
        await _messages.AppendAsync(second.Id, new NewMessage(ChatRole.User, "other chat"), Ct);
        await _messages.AppendAsync(first.Id, new NewMessage(ChatRole.Assistant, "two"), Ct);

        var loaded = await _messages.ListAsync(first.Id, Ct);

        Assert.Equal([0, 1], loaded.Select(message => message.Seq));
        Assert.Equal(["one", "two"], loaded.Select(message => message.Content));
        Assert.Equal(0, Assert.Single(await _messages.ListAsync(second.Id, Ct)).Seq);
    }

    [Fact]
    public async Task Tool_calls_and_tool_results_round_trip()
    {
        var conversation = await _conversations.CreateAsync("Tools", "m", Ct);
        var call = new ToolCall("web_search", """{"query":"weather in Sydney"}""");

        await _messages.AppendAsync(
            conversation.Id,
            new NewMessage(ChatRole.Assistant, "") { ToolCalls = [call] },
            Ct);
        await _messages.AppendAsync(
            conversation.Id,
            new NewMessage(ChatRole.Tool, "Sunny, 24 degrees") { ToolName = "web_search", ToolOutcome = ToolOutcome.Succeeded },
            Ct);

        var loaded = await _messages.ListAsync(conversation.Id, Ct);

        Assert.Equal(call, Assert.Single(loaded[0].ToolCalls));
        Assert.Null(loaded[0].ToolName);
        Assert.Null(loaded[0].ToolOutcome);
        Assert.Equal(ChatRole.Tool, loaded[1].Role);
        Assert.Equal("web_search", loaded[1].ToolName);
        Assert.Equal(ToolOutcome.Succeeded, loaded[1].ToolOutcome);
        Assert.Empty(loaded[1].ToolCalls);
    }

    [Fact]
    public async Task Attachment_metadata_loads_with_messages_and_text_loads_separately()
    {
        var conversation = await _conversations.CreateAsync("Files", "m", Ct);

        var saved = await _messages.AppendAsync(
            conversation.Id,
            new NewMessage(ChatRole.User, "summarise these")
            {
                Attachments =
                [
                    new NewAttachment("report.pdf", "pdf", 2048, true, "page one text"),
                    new NewAttachment("notes.md", "text", 12, false, "# Notes"),
                ],
            },
            Ct);

        var loaded = Assert.Single(await _messages.ListAsync(conversation.Id, Ct));
        Assert.Equal(saved.Attachments, loaded.Attachments);
        Assert.Equal(["report.pdf", "notes.md"], loaded.Attachments.Select(attachment => attachment.FileName));
        Assert.True(loaded.Attachments[0].Truncated);

        var texts = await new AttachmentRepository(_database).ListTextAsync(conversation.Id, Ct);
        Assert.Equal(["page one text", "# Notes"], texts.Select(text => text.Text));
        Assert.All(texts, text => Assert.Equal(saved.Id, text.MessageId));
    }
}
