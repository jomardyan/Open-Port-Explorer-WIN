namespace Open_Port_Explorer_WIN.Interfaces;

public interface ILocalDataService
{
    string? GetValue(string databasePath, string key);
    void SetValue(string databasePath, string key, string value);
    bool RemoveValue(string databasePath, string key);
    IReadOnlyDictionary<string, string> GetAllValues(string databasePath);
}
