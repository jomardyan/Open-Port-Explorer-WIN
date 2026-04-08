using System.IO;
using Microsoft.Data.Sqlite;
using Open_Port_Explorer_WIN.Interfaces;

namespace Open_Port_Explorer_WIN.Services;

public sealed class LocalDataService : ILocalDataService
{
    public string? GetValue(string databasePath, string key)
    {
        if (!File.Exists(databasePath))
        {
            return null;
        }

        try
        {
            using var connection = PreferencesService.OpenConnection(databasePath);

            if (!TableExists(connection))
            {
                return null;
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Value FROM LocalData WHERE Key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            var result = cmd.ExecuteScalar();
            return result as string;
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException($"Database error when reading key '{key}': {ex.Message}", ex);
        }
    }

    public void SetValue(string databasePath, string key, string value)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            using var connection = PreferencesService.OpenConnection(databasePath);
            EnsureSchema(connection);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO LocalData (Key, Value) VALUES (@key, @value)
                ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value
                """;
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", value);
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException($"Database error when writing key '{key}': {ex.Message}", ex);
        }
    }

    public bool RemoveValue(string databasePath, string key)
    {
        if (!File.Exists(databasePath))
        {
            return false;
        }

        try
        {
            using var connection = PreferencesService.OpenConnection(databasePath);

            if (!TableExists(connection))
            {
                return false;
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM LocalData WHERE Key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            return cmd.ExecuteNonQuery() > 0;
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException($"Database error when removing key '{key}': {ex.Message}", ex);
        }
    }

    public IReadOnlyDictionary<string, string> GetAllValues(string databasePath)
    {
        if (!File.Exists(databasePath))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            using var connection = PreferencesService.OpenConnection(databasePath);

            if (!TableExists(connection))
            {
                return new Dictionary<string, string>();
            }

            var result = new Dictionary<string, string>();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Key, Value FROM LocalData ORDER BY Key";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result[reader.GetString(0)] = reader.GetString(1);
            }

            return result;
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException($"Database error when reading all values: {ex.Message}", ex);
        }
    }

    internal static void EnsureSchema(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS LocalData (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='LocalData'";
        var result = cmd.ExecuteScalar();
        return Convert.ToInt64(result) > 0;
    }
}
