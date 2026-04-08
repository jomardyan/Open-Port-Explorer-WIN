namespace Open_Port_Explorer_WIN.Models;

public sealed class PortHistoryEntry
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
