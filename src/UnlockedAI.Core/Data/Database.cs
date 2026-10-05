using Microsoft.Data.Sqlite;

namespace UnlockedAI.Core.Data;

/// <summary>
/// Owns the single SQLite connection for the app's lifetime. All access goes through
/// <see cref="RunAsync{T}"/>, which runs one piece of work at a time off the calling thread.
/// </summary>
public sealed class Database : IDisposable
{
    // Roughly 2 MB of page cache. A chat history never needs more, and SQLite's default grows with use.
    private const string Pragmas =
        "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA foreign_keys = ON; PRAGMA cache_size = -2000;";

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Database(SqliteConnection connection) => _connection = connection;

    public static Database Open(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
        };
        return Create(connectionString.ToString());
    }

    public static Database OpenInMemory() => Create("Data Source=:memory:");

    public async Task<T> RunAsync<T>(Func<SqliteConnection, T> work, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => work(_connection), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task RunAsync(Action<SqliteConnection> work, CancellationToken cancellationToken = default) =>
        RunAsync(
            connection =>
            {
                work(connection);
                return true;
            },
            cancellationToken);

    public void Dispose()
    {
        _connection.Dispose();
        _gate.Dispose();
    }

    private static Database Create(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();
            using (var pragmas = connection.CreateCommand())
            {
                pragmas.CommandText = Pragmas;
                pragmas.ExecuteNonQuery();
            }

            Migrations.Apply(connection);
            return new Database(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
