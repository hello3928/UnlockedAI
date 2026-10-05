using System.Globalization;
using Microsoft.Data.Sqlite;

namespace UnlockedAI.Core.Data;

/// <summary>
/// Ordered schema steps. The database's <c>user_version</c> is the number of steps already applied.
/// Never edit a released step; add a new one.
/// </summary>
internal static class Migrations
{
    private static readonly string[] Steps =
    [
        // 1: initial schema
        """
        CREATE TABLE conversations (
            id          INTEGER PRIMARY KEY,
            title       TEXT    NOT NULL,
            model       TEXT    NOT NULL,
            created_at  INTEGER NOT NULL,
            updated_at  INTEGER NOT NULL
        );
        CREATE INDEX ix_conversations_updated ON conversations (updated_at DESC);

        CREATE TABLE messages (
            id               INTEGER PRIMARY KEY,
            conversation_id  INTEGER NOT NULL REFERENCES conversations (id) ON DELETE CASCADE,
            seq              INTEGER NOT NULL,
            role             TEXT    NOT NULL,
            content          TEXT    NOT NULL,
            tool_name        TEXT,
            tool_calls_json  TEXT,
            created_at       INTEGER NOT NULL,
            UNIQUE (conversation_id, seq)
        );

        CREATE TABLE attachments (
            id              INTEGER PRIMARY KEY,
            message_id      INTEGER NOT NULL REFERENCES messages (id) ON DELETE CASCADE,
            file_name       TEXT    NOT NULL,
            kind            TEXT    NOT NULL,
            size_bytes      INTEGER NOT NULL,
            truncated       INTEGER NOT NULL DEFAULT 0,
            extracted_text  TEXT    NOT NULL
        );
        CREATE INDEX ix_attachments_message ON attachments (message_id);

        CREATE TABLE settings (
            key    TEXT PRIMARY KEY,
            value  TEXT NOT NULL
        ) WITHOUT ROWID;
        """,
    ];

    public static int LatestVersion => Steps.Length;

    public static void Apply(SqliteConnection connection)
    {
        for (var version = ReadVersion(connection); version < Steps.Length; version++)
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();

            command.CommandText = Steps[version];
            command.ExecuteNonQuery();

            // PRAGMA takes no parameters, so the number is formatted in. It is our own loop counter.
            command.CommandText = string.Create(CultureInfo.InvariantCulture, $"PRAGMA user_version = {version + 1};");
            command.ExecuteNonQuery();

            transaction.Commit();
        }
    }

    public static int ReadVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
