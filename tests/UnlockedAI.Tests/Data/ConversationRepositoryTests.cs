using UnlockedAI.Core.Data;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Tests.Data;

public sealed class ConversationRepositoryTests : IDisposable
{
    private readonly Database _database = Database.OpenInMemory();
    private readonly ConversationRepository _conversations;
    private readonly MessageRepository _messages;

    public ConversationRepositoryTests()
    {
        _conversations = new ConversationRepository(_database);
        _messages = new MessageRepository(_database);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Created_conversation_can_be_read_back()
    {
        var created = await _conversations.CreateAsync("First chat", "some-model", Ct);

        var loaded = await _conversations.GetAsync(created.Id, Ct);

        Assert.Equal(created, loaded);
    }

    [Fact]
    public async Task Missing_conversation_reads_as_null()
    {
        Assert.Null(await _conversations.GetAsync(42, Ct));
    }

    [Fact]
    public async Task List_puts_the_most_recently_used_chat_first()
    {
        var older = await _conversations.CreateAsync("Older", "m", Ct);
        var newer = await _conversations.CreateAsync("Newer", "m", Ct);

        // Timestamps are whole milliseconds; make sure the new message lands on a later one.
        await Task.Delay(5, Ct);
        await _messages.AppendAsync(older.Id, new NewMessage(ChatRole.User, "hello again"), Ct);

        var order = (await _conversations.ListAsync(Ct)).Select(summary => summary.Id);

        Assert.Equal([older.Id, newer.Id], order);
    }

    [Fact]
    public async Task Rename_changes_the_title_without_reordering()
    {
        var first = await _conversations.CreateAsync("First", "m", Ct);
        await Task.Delay(5, Ct);
        var second = await _conversations.CreateAsync("Second", "m", Ct);

        await Task.Delay(5, Ct);
        await _conversations.RenameAsync(first.Id, "Renamed", Ct);

        var summaries = await _conversations.ListAsync(Ct);
        Assert.Equal([second.Id, first.Id], summaries.Select(summary => summary.Id));
        Assert.Equal("Renamed", summaries[1].Title);
    }

    [Fact]
    public async Task SetModel_is_stored()
    {
        var conversation = await _conversations.CreateAsync("Chat", "old-model", Ct);

        await _conversations.SetModelAsync(conversation.Id, "new-model", Ct);

        Assert.Equal("new-model", (await _conversations.GetAsync(conversation.Id, Ct))!.Model);
    }

    [Fact]
    public async Task Delete_removes_messages_and_attachments_too()
    {
        var conversation = await _conversations.CreateAsync("Doomed", "m", Ct);
        await _messages.AppendAsync(
            conversation.Id,
            new NewMessage(ChatRole.User, "see file")
            {
                Attachments = [new NewAttachment("notes.txt", "text", 5, false, "hello")],
            },
            Ct);

        await _conversations.DeleteAsync(conversation.Id, Ct);

        Assert.Empty(await _conversations.ListAsync(Ct));
        Assert.Empty(await _messages.ListAsync(conversation.Id, Ct));
        Assert.Empty(await new AttachmentRepository(_database).ListTextAsync(conversation.Id, Ct));
    }
}
