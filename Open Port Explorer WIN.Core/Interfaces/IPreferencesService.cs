using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Interfaces;

public interface IPreferencesService
{
    string GetDatabasePath();
    AppPreferences Load(string databasePath);

    void Save(
        string databasePath,
        bool isDarkTheme,
        IEnumerable<int> trustedPorts,
        IEnumerable<int> blockedPorts,
        IEnumerable<int> watchedPorts,
        IEnumerable<string> trustedProcesses,
        IEnumerable<string> blockedProcesses,
        IEnumerable<string> watchedProcesses);

    void ApplySet<T>(ISet<T> destination, IEnumerable<T>? source);
}
