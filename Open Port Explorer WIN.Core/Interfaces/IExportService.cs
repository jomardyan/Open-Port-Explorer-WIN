using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Interfaces;

public interface IExportService
{
    void WriteCsv(IReadOnlyList<PortEntry> entries, string filePath);
    void WriteJson(IReadOnlyList<PortEntry> entries, string filePath);
    void WriteActivityLog(IReadOnlyList<PortHistoryEntry> history, string filePath);
}
