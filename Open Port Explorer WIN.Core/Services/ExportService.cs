using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Open_Port_Explorer_WIN.Interfaces;
using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Services;

public sealed class ExportService : IExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public void WriteCsv(IReadOnlyList<PortEntry> entries, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Port,Service,PortDescription,Protocol,State,Rule,LocalAddress,RemoteAddress,PID,ProcessName,ProcessDescription,FirstSeen,LastChange,ObservedDuration,Suspicious,Reason");

        foreach (var entry in entries)
        {
            sb.AppendLine(string.Join(',',
                entry.PortNumber,
                CsvEscape(entry.ServiceName),
                CsvEscape(entry.PortDescription),
                CsvEscape(entry.Protocol),
                CsvEscape(entry.State),
                CsvEscape(entry.RuleStatus),
                CsvEscape(entry.LocalAddress),
                CsvEscape(entry.RemoteAddress),
                entry.ProcessId,
                CsvEscape(entry.ProcessName),
                CsvEscape(entry.ProcessDescription),
                CsvEscape(entry.FirstSeen.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                CsvEscape(entry.LastActivity.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                CsvEscape(entry.ObservedDuration),
                entry.IsSuspicious,
                CsvEscape(entry.SuspicionReason)));
        }

        try
        {
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access denied when writing CSV to '{filePath}': {ex.Message}", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new IOException($"Directory not found for CSV export path '{filePath}': {ex.Message}", ex);
        }
    }

    public void WriteJson(IReadOnlyList<PortEntry> entries, string filePath)
    {
        try
        {
            var json = JsonSerializer.Serialize(entries, JsonOptions);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access denied when writing JSON to '{filePath}': {ex.Message}", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new IOException($"Directory not found for JSON export path '{filePath}': {ex.Message}", ex);
        }
    }

    public void WriteActivityLog(IReadOnlyList<PortHistoryEntry> history, string filePath)
    {
        var lines = history
            .OrderBy(static h => h.Timestamp)
            .Select(static h => $"{h.Timestamp:yyyy-MM-dd HH:mm:ss} [{h.EventType}] {h.Details}")
            .ToArray();

        try
        {
            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access denied when writing activity log to '{filePath}': {ex.Message}", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new IOException($"Directory not found for activity log path '{filePath}': {ex.Message}", ex);
        }
    }

    internal static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
