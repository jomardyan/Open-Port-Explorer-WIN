using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Interfaces;

public interface IRuleService
{
    void ApplyRules(
        PortEntry entry,
        IReadOnlySet<int> trustedPorts,
        IReadOnlySet<int> blockedPorts,
        IReadOnlySet<int> watchedPorts,
        IReadOnlySet<string> trustedProcesses,
        IReadOnlySet<string> blockedProcesses,
        IReadOnlySet<string> watchedProcesses);

    void EvaluateSuspicion(PortEntry entry);

    string TogglePortRule(
        int port,
        RuleMode mode,
        HashSet<int> watchedPorts,
        HashSet<int> trustedPorts,
        HashSet<int> blockedPorts);

    string ToggleProcessRule(
        string processName,
        RuleMode mode,
        HashSet<string> watchedProcesses,
        HashSet<string> trustedProcesses,
        HashSet<string> blockedProcesses);
}
