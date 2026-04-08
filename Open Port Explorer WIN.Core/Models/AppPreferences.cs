namespace Open_Port_Explorer_WIN.Models;

public sealed class AppPreferences
{
    public bool UseDarkTheme { get; set; }
    public List<int> TrustedPorts { get; set; } = [];
    public List<int> BlockedPorts { get; set; } = [];
    public List<int> WatchedPorts { get; set; } = [];
    public List<string> TrustedProcesses { get; set; } = [];
    public List<string> BlockedProcesses { get; set; } = [];
    public List<string> WatchedProcesses { get; set; } = [];
}
