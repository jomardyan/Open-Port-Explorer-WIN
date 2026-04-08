using Open_Port_Explorer_WIN.Interfaces;
using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Services;

public sealed class RuleService : IRuleService
{
    private readonly IPortInfoService _portInfoService;
    private readonly INetworkService _networkService;

    public RuleService(IPortInfoService portInfoService, INetworkService networkService)
    {
        _portInfoService = portInfoService;
        _networkService = networkService;
    }

    public void ApplyRules(
        PortEntry entry,
        IReadOnlySet<int> trustedPorts,
        IReadOnlySet<int> blockedPorts,
        IReadOnlySet<int> watchedPorts,
        IReadOnlySet<string> trustedProcesses,
        IReadOnlySet<string> blockedProcesses,
        IReadOnlySet<string> watchedProcesses)
    {
        var trusted = false;
        var blocked = false;
        var watched = false;
        var reasonFlags = RuleReasonFlags.None;

        if (trustedPorts.Contains(entry.PortNumber))
        {
            trusted = true;
            reasonFlags |= RuleReasonFlags.TrustedPort;
        }

        if (blockedPorts.Contains(entry.PortNumber))
        {
            blocked = true;
            reasonFlags |= RuleReasonFlags.BlockedPort;
        }

        if (watchedPorts.Contains(entry.PortNumber))
        {
            watched = true;
            reasonFlags |= RuleReasonFlags.WatchedPort;
        }

        if (!string.IsNullOrWhiteSpace(entry.ProcessName))
        {
            if (trustedProcesses.Contains(entry.ProcessName))
            {
                trusted = true;
                reasonFlags |= RuleReasonFlags.TrustedProcess;
            }

            if (blockedProcesses.Contains(entry.ProcessName))
            {
                blocked = true;
                reasonFlags |= RuleReasonFlags.BlockedProcess;
            }

            if (watchedProcesses.Contains(entry.ProcessName))
            {
                watched = true;
                reasonFlags |= RuleReasonFlags.WatchedProcess;
            }
        }

        entry.IsWatched = watched;
        entry.RuleStatus = blocked ? "Blocked" : trusted ? "Trusted" : watched ? "Watched" : "Unruled";
        entry.RuleSummary = FormatRuleReasons(reasonFlags);
    }

    public void EvaluateSuspicion(PortEntry entry)
    {
        var flags = SuspicionFlags.None;

        if (_portInfoService.SuspiciousPorts.Contains(entry.PortNumber))
        {
            flags |= SuspicionFlags.SensitivePort;
        }

        if (string.Equals(entry.State, "LISTENING", StringComparison.OrdinalIgnoreCase) && _networkService.IsPublicBinding(entry.LocalAddressRaw))
        {
            flags |= SuspicionFlags.PublicListener;
        }

        if (string.Equals(entry.RuleStatus, "Blocked", StringComparison.OrdinalIgnoreCase))
        {
            flags |= SuspicionFlags.BlockedRule;
        }

        if (string.Equals(entry.ProcessName, "Unavailable", StringComparison.OrdinalIgnoreCase) && entry.ProcessId > 0)
        {
            flags |= SuspicionFlags.ProcessUnavailable;
        }

        entry.IsSuspicious = flags != SuspicionFlags.None && !string.Equals(entry.RuleStatus, "Trusted", StringComparison.OrdinalIgnoreCase);
        entry.SuspicionReason = flags == SuspicionFlags.None ? entry.RuleSummary : FormatSuspicionReasons(flags);
    }

    public string TogglePortRule(
        int port,
        RuleMode mode,
        HashSet<int> watchedPorts,
        HashSet<int> trustedPorts,
        HashSet<int> blockedPorts)
    {
        return mode switch
        {
            RuleMode.Watched => ToggleMembership(watchedPorts, port, $"Watching port {port}.", $"Stopped watching port {port}."),
            RuleMode.Trusted => ToggleExclusiveMembership(trustedPorts, blockedPorts, port, $"Trusted port {port}.", $"Removed trusted rule for port {port}."),
            RuleMode.Blocked => ToggleExclusiveMembership(blockedPorts, trustedPorts, port, $"Blocked port {port}.", $"Removed blocked rule for port {port}."),
            _ => string.Empty
        };
    }

    public string ToggleProcessRule(
        string processName,
        RuleMode mode,
        HashSet<string> watchedProcesses,
        HashSet<string> trustedProcesses,
        HashSet<string> blockedProcesses)
    {
        return mode switch
        {
            RuleMode.Watched => ToggleMembership(watchedProcesses, processName, $"Watching process {processName}.", $"Stopped watching process {processName}."),
            RuleMode.Trusted => ToggleExclusiveMembership(trustedProcesses, blockedProcesses, processName, $"Trusted process {processName}.", $"Removed trusted rule for process {processName}."),
            RuleMode.Blocked => ToggleExclusiveMembership(blockedProcesses, trustedProcesses, processName, $"Blocked process {processName}.", $"Removed blocked rule for process {processName}."),
            _ => string.Empty
        };
    }

    private static string ToggleMembership<T>(ISet<T> set, T value, string onMessage, string offMessage)
    {
        return set.Remove(value) ? offMessage : AddAndReturn(set, value, onMessage);
    }

    private static string ToggleExclusiveMembership<T>(ISet<T> primary, ISet<T> secondary, T value, string onMessage, string offMessage)
    {
        if (primary.Remove(value))
        {
            return offMessage;
        }

        secondary.Remove(value);
        primary.Add(value);
        return onMessage;
    }

    private static string AddAndReturn<T>(ISet<T> set, T value, string message)
    {
        set.Add(value);
        return message;
    }

    [Flags]
    private enum RuleReasonFlags
    {
        None = 0,
        TrustedPort = 1,
        BlockedPort = 2,
        WatchedPort = 4,
        TrustedProcess = 8,
        BlockedProcess = 16,
        WatchedProcess = 32
    }

    private static string FormatRuleReasons(RuleReasonFlags flags)
    {
        if (flags == RuleReasonFlags.None)
        {
            return "No rule applied";
        }

        // Fast path for single-flag cases (most common)
        if ((flags & (flags - 1)) == 0)
        {
            return flags switch
            {
                RuleReasonFlags.TrustedPort => "Trusted port",
                RuleReasonFlags.BlockedPort => "Blocked port",
                RuleReasonFlags.WatchedPort => "Watched port",
                RuleReasonFlags.TrustedProcess => "Trusted process",
                RuleReasonFlags.BlockedProcess => "Blocked process",
                RuleReasonFlags.WatchedProcess => "Watched process",
                _ => "No rule applied"
            };
        }

        var parts = (stackalloc int[6]);  // indices into reason labels
        string[] labels = ["Trusted port", "Blocked port", "Watched port", "Trusted process", "Blocked process", "Watched process"];
        var count = 0;
        if ((flags & RuleReasonFlags.TrustedPort) != 0) parts[count++] = 0;
        if ((flags & RuleReasonFlags.BlockedPort) != 0) parts[count++] = 1;
        if ((flags & RuleReasonFlags.WatchedPort) != 0) parts[count++] = 2;
        if ((flags & RuleReasonFlags.TrustedProcess) != 0) parts[count++] = 3;
        if ((flags & RuleReasonFlags.BlockedProcess) != 0) parts[count++] = 4;
        if ((flags & RuleReasonFlags.WatchedProcess) != 0) parts[count++] = 5;
        var result = new string[count];
        for (var i = 0; i < count; i++) result[i] = labels[parts[i]];
        return string.Join(", ", result);
    }

    [Flags]
    private enum SuspicionFlags
    {
        None = 0,
        SensitivePort = 1,
        PublicListener = 2,
        BlockedRule = 4,
        ProcessUnavailable = 8
    }

    private static string FormatSuspicionReasons(SuspicionFlags flags)
    {
        if ((flags & (flags - 1)) == 0)
        {
            return flags switch
            {
                SuspicionFlags.SensitivePort => "Sensitive service port",
                SuspicionFlags.PublicListener => "Listening on a non-loopback interface",
                SuspicionFlags.BlockedRule => "Matches a blocked rule",
                SuspicionFlags.ProcessUnavailable => "Process metadata unavailable",
                _ => string.Empty
            };
        }

        var parts = (stackalloc int[4]);
        string[] labels = ["Sensitive service port", "Listening on a non-loopback interface", "Matches a blocked rule", "Process metadata unavailable"];
        var count = 0;
        if ((flags & SuspicionFlags.SensitivePort) != 0) parts[count++] = 0;
        if ((flags & SuspicionFlags.PublicListener) != 0) parts[count++] = 1;
        if ((flags & SuspicionFlags.BlockedRule) != 0) parts[count++] = 2;
        if ((flags & SuspicionFlags.ProcessUnavailable) != 0) parts[count++] = 3;
        var result = new string[count];
        for (var i = 0; i < count; i++) result[i] = labels[parts[i]];
        return string.Join("; ", result);
    }
}
