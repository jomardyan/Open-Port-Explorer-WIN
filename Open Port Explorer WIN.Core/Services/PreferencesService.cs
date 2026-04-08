using System.IO;
using Microsoft.Data.Sqlite;
using Open_Port_Explorer_WIN.Interfaces;
using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Services;

public sealed class PreferencesService : IPreferencesService
{
    public string GetDatabasePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenPortExplorerWin",
            "appdata.db");
    }

    public AppPreferences Load(string databasePath)
    {
        if (!File.Exists(databasePath))
        {
            return new AppPreferences();
        }

        try
        {
            using var connection = OpenConnection(databasePath);

            if (!TableExists(connection, "Preferences"))
            {
                return new AppPreferences();
            }

            var preferences = new AppPreferences();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT Key, Value FROM Preferences";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var key = reader.GetString(0);
                    var value = reader.GetString(1);

                    if (key == "UseDarkTheme")
                    {
                        preferences.UseDarkTheme = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            if (TableExists(connection, "PortRules"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Port, Category FROM PortRules ORDER BY Port";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var port = reader.GetInt32(0);
                    var category = reader.GetString(1);

                    switch (category)
                    {
                        case "Trusted":
                            preferences.TrustedPorts.Add(port);
                            break;
                        case "Blocked":
                            preferences.BlockedPorts.Add(port);
                            break;
                        case "Watched":
                            preferences.WatchedPorts.Add(port);
                            break;
                    }
                }
            }

            if (TableExists(connection, "ProcessRules"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT ProcessName, Category FROM ProcessRules ORDER BY ProcessName";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var processName = reader.GetString(0);
                    var category = reader.GetString(1);

                    switch (category)
                    {
                        case "Trusted":
                            preferences.TrustedProcesses.Add(processName);
                            break;
                        case "Blocked":
                            preferences.BlockedProcesses.Add(processName);
                            break;
                        case "Watched":
                            preferences.WatchedProcesses.Add(processName);
                            break;
                    }
                }
            }

            return preferences;
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException($"Database is corrupted or contains invalid data: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Access denied when reading database '{databasePath}': {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Unable to read database '{databasePath}': {ex.Message}", ex);
        }
    }

    public void Save(
        string databasePath,
        bool isDarkTheme,
        IEnumerable<int> trustedPorts,
        IEnumerable<int> blockedPorts,
        IEnumerable<int> watchedPorts,
        IEnumerable<string> trustedProcesses,
        IEnumerable<string> blockedProcesses,
        IEnumerable<string> watchedProcesses)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            using var connection = OpenConnection(databasePath);
            EnsureSchema(connection);

            using var transaction = connection.BeginTransaction();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM Preferences";
                cmd.ExecuteNonQuery();
            }

            InsertPreference(connection, "UseDarkTheme", isDarkTheme ? "true" : "false");

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM PortRules";
                cmd.ExecuteNonQuery();
            }

            foreach (var port in trustedPorts.Order())
            {
                InsertPortRule(connection, port, "Trusted");
            }

            foreach (var port in blockedPorts.Order())
            {
                InsertPortRule(connection, port, "Blocked");
            }

            foreach (var port in watchedPorts.Order())
            {
                InsertPortRule(connection, port, "Watched");
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM ProcessRules";
                cmd.ExecuteNonQuery();
            }

            foreach (var process in trustedProcesses.Order(StringComparer.OrdinalIgnoreCase))
            {
                InsertProcessRule(connection, process, "Trusted");
            }

            foreach (var process in blockedProcesses.Order(StringComparer.OrdinalIgnoreCase))
            {
                InsertProcessRule(connection, process, "Blocked");
            }

            foreach (var process in watchedProcesses.Order(StringComparer.OrdinalIgnoreCase))
            {
                InsertProcessRule(connection, process, "Watched");
            }

            transaction.Commit();
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Access denied when saving database '{databasePath}': {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Unable to write database '{databasePath}': {ex.Message}", ex);
        }
    }

    public void ApplySet<T>(ISet<T> destination, IEnumerable<T>? source)
    {
        destination.Clear();
        if (source is null)
        {
            return;
        }

        foreach (var item in source)
        {
            destination.Add(item);
        }
    }

    internal static SqliteConnection OpenConnection(string databasePath)
    {
        var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        return connection;
    }

    internal static void EnsureSchema(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Preferences (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS PortRules (
                Port INTEGER NOT NULL,
                Category TEXT NOT NULL,
                PRIMARY KEY (Port, Category)
            );
            CREATE TABLE IF NOT EXISTS ProcessRules (
                ProcessName TEXT NOT NULL,
                Category TEXT NOT NULL,
                PRIMARY KEY (ProcessName, Category)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
        cmd.Parameters.AddWithValue("@name", tableName);
        var result = cmd.ExecuteScalar();
        return Convert.ToInt64(result) > 0;
    }

    private static void InsertPreference(SqliteConnection connection, string key, string value)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Preferences (Key, Value) VALUES (@key, @value)";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

    private static void InsertPortRule(SqliteConnection connection, int port, string category)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO PortRules (Port, Category) VALUES (@port, @category)";
        cmd.Parameters.AddWithValue("@port", port);
        cmd.Parameters.AddWithValue("@category", category);
        cmd.ExecuteNonQuery();
    }

    private static void InsertProcessRule(SqliteConnection connection, string processName, string category)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO ProcessRules (ProcessName, Category) VALUES (@processName, @category)";
        cmd.Parameters.AddWithValue("@processName", processName);
        cmd.Parameters.AddWithValue("@category", category);
        cmd.ExecuteNonQuery();
    }
}
