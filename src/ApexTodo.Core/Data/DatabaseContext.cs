using Microsoft.Data.Sqlite;

namespace ApexTodo.Core.Data;

public class DatabaseContext : IDisposable
{
    private readonly string _connectionString;

    public DatabaseContext(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private void Initialize()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS todos (
                id TEXT PRIMARY KEY,
                text TEXT NOT NULL,
                completed INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                completed_at TEXT,
                sort_order INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        // Migration: add due_at column if not exists
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "PRAGMA table_info(todos)";
        using var reader = checkCmd.ExecuteReader();
        bool hasDueAt = false;
        while (reader.Read())
        {
            if (reader.GetString(1) == "due_at")
            {
                hasDueAt = true;
                break;
            }
        }
        reader.Close();

        if (!hasDueAt)
        {
            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE todos ADD COLUMN due_at TEXT";
            alterCmd.ExecuteNonQuery();
        }
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public void Dispose()
    {
        // SqliteConnection is lightweight, each usage creates/disposes its own
    }
}
