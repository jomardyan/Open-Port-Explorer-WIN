namespace Open_Port_Explorer_WIN.Models;

public sealed class ObservedConnectionState
{
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime LastActivity { get; set; }
    public string LastState { get; set; } = string.Empty;
}
