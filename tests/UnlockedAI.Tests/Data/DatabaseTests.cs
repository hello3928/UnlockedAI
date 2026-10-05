using Microsoft.Data.Sqlite;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Tests.Data;

public class DatabaseTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task New_database_is_at_the_latest_schema_version()
    {
        using var database = Database.OpenInMemory();

        var version = await database.RunAsync(Migrations.ReadVersion, Ct);

        Assert.Equal(Migrations.LatestVersion, version);
    }

    [Fact]
    public async Task Reopening_a_file_keeps_its_data()
    {
        var path = Path.Combine(Path.GetTempPath(), $"unlockedai-test-{Guid.NewGuid():N}.db");
        try
        {
            using (var first = Database.Open(path))
            {
                await new ConversationRepository(first).CreateAsync("Kept", "model", Ct);
            }

            using var second = Database.Open(path);
            var summaries = await new ConversationRepository(second).ListAsync(Ct);

            Assert.Equal("Kept", Assert.Single(summaries).Title);
        }
        finally
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public async Task Database_from_an_older_version_is_upgraded_on_open()
    {
        var path = Path.Combine(Path.GetTempPath(), $"unlockedai-test-{Guid.NewGuid():N}.db");
        try
        {
            long conversationId;
            using (var old = Database.Open(path))
            {
                conversationId = (await new ConversationRepository(old).CreateAsync("Old chat", "model", Ct)).Id;

                // Put the file back to how version 1 left it.
                await old.RunAsync(
                    connection =>
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = "ALTER TABLE messages DROP COLUMN tool_outcome; PRAGMA user_version = 1;";
                        command.ExecuteNonQuery();
                    },
                    Ct);
            }

            using var upgraded = Database.Open(path);
            var messages = new MessageRepository(upgraded);
            await messages.AppendAsync(
                conversationId,
                new NewMessage(ChatRole.Tool, "result") { ToolName = "lookup", ToolOutcome = ToolOutcome.Denied },
                Ct);

            Assert.Equal(Migrations.LatestVersion, await upgraded.RunAsync(Migrations.ReadVersion, Ct));
            Assert.Equal(ToolOutcome.Denied, Assert.Single(await messages.ListAsync(conversationId, Ct)).ToolOutcome);
        }
        finally
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public async Task Foreign_keys_are_enforced()
    {
        using var database = Database.OpenInMemory();
        var messages = new MessageRepository(database);

        await Assert.ThrowsAsync<SqliteException>(
            () => messages.AppendAsync(conversationId: 999, new NewMessage(ChatRole.User, "orphan"), Ct));
    }
}
