using Microsoft.Data.Sqlite;

namespace UnlockedAI.Core.Data;

/// <summary>Shared plumbing for repositories: the database handle, command building and time conversion.</summary>
public abstract class RepositoryBase(Database database)
{
    protected Database Database { get; } = database;

    /// <summary>Builds a command with named parameters. Null values are stored as SQL NULL.</summary>
    protected static SqliteCommand Command(
        SqliteConnection connection,
        string sql,
        params ReadOnlySpan<(string Name, object? Value)> parameters)
    {
        // CreateCommand enlists in the connection's open transaction, if there is one.
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }

    protected static long NowUnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    protected static DateTimeOffset FromUnixMs(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value);
}
