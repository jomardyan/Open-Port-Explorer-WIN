namespace Open_Port_Explorer_WIN.Models;

public sealed class PortEntry
{
    public int PortNumber { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string AddressFamily { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string LocalAddress => $"{LocalAddressRaw}:{LocalPort}";
    public string RemoteAddress => RemotePort > 0 ? $"{RemoteAddressRaw}:{RemotePort}" : RemoteAddressRaw;
    public string ServiceName { get; set; } = string.Empty;
    public string PortDescription { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ProcessDescription { get; set; } = string.Empty;
    public string ProcessPath { get; set; } = string.Empty;
    public bool IsSuspicious { get; set; }
    public bool IsWatched { get; set; }
    public string RuleStatus { get; set; } = "Unruled";
    public string RuleSummary { get; set; } = string.Empty;
    public string SuspicionReason { get; set; } = string.Empty;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime LastActivity { get; set; }
    public double ObservedSeconds { get; set; }
    public string ObservedDuration { get; set; } = string.Empty;
    public string RemoteHostName { get; set; } = string.Empty;

    public string LocalAddressRaw { get; set; } = string.Empty;
    public string RemoteAddressRaw { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public int RemotePort { get; set; }
}
