using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Data;

public sealed class ConversationRepository(Database database) : RepositoryBase(database)
{
    /// <summary>Most recently used first. Reads only what the sidebar shows.</summary>
    public Task<List<ConversationSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        Database.RunAsync(
            connection =>
            {
                using var command = Command(
                    connection,
                    "SELECT id, title, updated_at FROM conversations ORDER BY updated_at DESC, id DESC");
                using var reader = command.ExecuteReader();

                var summaries = new List<ConversationSummary>();
                while (reader.Read())
                {
                    summaries.Add(new ConversationSummary(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        FromUnixMs(reader.GetInt64(2))));
                }

                return summaries;
            },
            cancellationToken);

    public Task<Conversation?> GetAsync(long id, CancellationToken cancellationToken = default) =>
        Database.RunAsync(
            connection =>
            {
                using var command = Command(
                    connection,
                    "SELECT id, title, model, created_at, updated_at FROM conversations WHERE id = $id",
                    ("$id", id));
                using var reader = command.ExecuteReader();

                return reader.Read()
                    ? new Conversation(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        FromUnixMs(reader.GetInt64(3)),
                        FromUnixMs(reader.GetInt64(4)))
                    : null;
            },
            cancellationToken);

    public Task<Conversation> CreateAsync(string title, string model, CancellationToken cancellationToken = default) =>
        Database.RunAsync(
            connection =>
            {
                var now = NowUnixMs();
                using var command = Command(
                    connection,
                    """
                    INSERT INTO conversations (title, model, created_at, updated_at)
                    VALUES ($title, $model, $now, $now)
                    RETURNING id
                    """,
                    ("$title", title),
                    ("$model", model),
                    ("$now", now));

                var id = (long)command.ExecuteScalar()!;
                return new Conversation(id, title, model, FromUnixMs(now), FromUnixMs(now));
            },
            cancellationToken);

    /// <summary>Renaming does not count as activity, so the chat keeps its place in the list.</summary>
    public Task RenameAsync(long id, string title, CancellationToken cancellationToken = default) =>
        Database.RunAsync(
            connection =>
            {
                using var command = Command(
                    connection,
                    "UPDATE conversations SET title = $title WHERE id = $id",
                    ("$title", title),
                    ("$id", id));
                command.ExecuteNonQuery();
            },
            cancellationToken);

    public Task SetModelAsync(long id, string model, CancellationToken cancellationToken = default) =>
        Database.RunAsync(
            connection =>
            {
                using var command = Command(
                    connection,
                    "UPDATE conversations SET model = $model WHERE id = $id",
                    ("$model", model),
                    ("$id", id));
                command.ExecuteNonQuery();
            },
            cancellationToken);

    /// <summary>Removes the chat with its messages and attachments (cascade).</summary>
    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        Database.RunAsync(
            connection =>
            {
                using var command = Command(connection, "DELETE FROM conversations WHERE id = $id", ("$id", id));
                command.ExecuteNonQuery();
            },
            cancellationToken);
}
