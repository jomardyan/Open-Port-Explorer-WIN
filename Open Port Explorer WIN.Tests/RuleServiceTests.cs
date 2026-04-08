using Open_Port_Explorer_WIN.Models;
using Open_Port_Explorer_WIN.Services;

namespace Open_Port_Explorer_WIN.Tests;

public class RuleServiceTests
{
    private readonly RuleService _sut;

    public RuleServiceTests()
    {
        var portInfoService = new PortInfoService();
        var networkService = new NetworkService(portInfoService);
        _sut = new RuleService(portInfoService, networkService);
    }

    [Fact]
    public void ApplyRules_TrustedPort_SetsRuleStatusToTrusted()
    {
        var entry = new PortEntry { PortNumber = 443, ProcessName = "chrome" };
        var trustedPorts = new HashSet<int> { 443 };

        _sut.ApplyRules(entry, trustedPorts, new HashSet<int>(), new HashSet<int>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("Trusted", entry.RuleStatus);
        Assert.Contains("Trusted port", entry.RuleSummary);
    }

    [Fact]
    public void ApplyRules_BlockedPort_SetsRuleStatusToBlocked()
    {
        var entry = new PortEntry { PortNumber = 80, ProcessName = "nginx" };
        var blockedPorts = new HashSet<int> { 80 };

        _sut.ApplyRules(entry, new HashSet<int>(), blockedPorts, new HashSet<int>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("Blocked", entry.RuleStatus);
        Assert.Contains("Blocked port", entry.RuleSummary);
    }

    [Fact]
    public void ApplyRules_WatchedPort_SetsRuleStatusToWatched()
    {
        var entry = new PortEntry { PortNumber = 8080, ProcessName = "java" };
        var watchedPorts = new HashSet<int> { 8080 };

        _sut.ApplyRules(entry, new HashSet<int>(), new HashSet<int>(), watchedPorts, new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("Watched", entry.RuleStatus);
        Assert.True(entry.IsWatched);
        Assert.Contains("Watched port", entry.RuleSummary);
    }

    [Fact]
    public void ApplyRules_UnruledEntry_SetsDefaultStatus()
    {
        var entry = new PortEntry { PortNumber = 12345, ProcessName = "test" };

        _sut.ApplyRules(entry, new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("Unruled", entry.RuleStatus);
        Assert.Equal("No rule applied", entry.RuleSummary);
    }

    [Fact]
    public void ApplyRules_BlockedTakesPriorityOverTrusted()
    {
        var entry = new PortEntry { PortNumber = 80, ProcessName = "test" };
        var trustedPorts = new HashSet<int> { 80 };
        var blockedPorts = new HashSet<int> { 80 };

        _sut.ApplyRules(entry, trustedPorts, blockedPorts, new HashSet<int>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("Blocked", entry.RuleStatus);
    }

    [Fact]
    public void ApplyRules_TrustedProcess_SetsRuleStatus()
    {
        var entry = new PortEntry { PortNumber = 12345, ProcessName = "chrome" };
        var trustedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chrome" };

        _sut.ApplyRules(entry, new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), trustedProcesses, new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("Trusted", entry.RuleStatus);
        Assert.Contains("Trusted process", entry.RuleSummary);
    }

    [Fact]
    public void EvaluateSuspicion_SensitivePort_FlagsSuspicious()
    {
        var entry = new PortEntry
        {
            PortNumber = 22,
            State = "LISTENING",
            LocalAddressRaw = "127.0.0.1",
            RuleStatus = "Unruled",
            ProcessName = "sshd",
            ProcessId = 100,
            RuleSummary = "No rule applied"
        };

        _sut.EvaluateSuspicion(entry);

        Assert.True(entry.IsSuspicious);
        Assert.Contains("Sensitive service port", entry.SuspicionReason);
    }

    [Fact]
    public void EvaluateSuspicion_PublicBinding_FlagsSuspicious()
    {
        var entry = new PortEntry
        {
            PortNumber = 8080,
            State = "LISTENING",
            LocalAddressRaw = "0.0.0.0",
            RuleStatus = "Unruled",
            ProcessName = "java",
            ProcessId = 100,
            RuleSummary = "No rule applied"
        };

        _sut.EvaluateSuspicion(entry);

        Assert.True(entry.IsSuspicious);
        Assert.Contains("Listening on a non-loopback interface", entry.SuspicionReason);
    }

    [Fact]
    public void EvaluateSuspicion_TrustedEntry_NeverSuspicious()
    {
        var entry = new PortEntry
        {
            PortNumber = 22,
            State = "LISTENING",
            LocalAddressRaw = "0.0.0.0",
            RuleStatus = "Trusted",
            ProcessName = "sshd",
            ProcessId = 100,
            RuleSummary = "Trusted port"
        };

        _sut.EvaluateSuspicion(entry);

        Assert.False(entry.IsSuspicious);
    }

    [Fact]
    public void EvaluateSuspicion_BlockedEntry_FlagsSuspicious()
    {
        var entry = new PortEntry
        {
            PortNumber = 12345,
            State = "ESTABLISHED",
            LocalAddressRaw = "127.0.0.1",
            RuleStatus = "Blocked",
            ProcessName = "test",
            ProcessId = 100,
            RuleSummary = "Blocked port"
        };

        _sut.EvaluateSuspicion(entry);

        Assert.True(entry.IsSuspicious);
        Assert.Contains("Matches a blocked rule", entry.SuspicionReason);
    }

    [Fact]
    public void EvaluateSuspicion_UnavailableProcess_FlagsSuspicious()
    {
        var entry = new PortEntry
        {
            PortNumber = 12345,
            State = "ESTABLISHED",
            LocalAddressRaw = "127.0.0.1",
            RuleStatus = "Unruled",
            ProcessName = "Unavailable",
            ProcessId = 100,
            RuleSummary = "No rule applied"
        };

        _sut.EvaluateSuspicion(entry);

        Assert.True(entry.IsSuspicious);
        Assert.Contains("Process metadata unavailable", entry.SuspicionReason);
    }

    [Fact]
    public void TogglePortRule_WatchMode_TogglesOnAndOff()
    {
        var watched = new HashSet<int>();
        var trusted = new HashSet<int>();
        var blocked = new HashSet<int>();

        var result1 = _sut.TogglePortRule(80, RuleMode.Watched, watched, trusted, blocked);
        Assert.Contains("Watching port 80", result1);
        Assert.Contains(80, watched);

        var result2 = _sut.TogglePortRule(80, RuleMode.Watched, watched, trusted, blocked);
        Assert.Contains("Stopped watching port 80", result2);
        Assert.DoesNotContain(80, watched);
    }

    [Fact]
    public void TogglePortRule_TrustMode_RemovesFromBlocked()
    {
        var watched = new HashSet<int>();
        var trusted = new HashSet<int>();
        var blocked = new HashSet<int> { 80 };

        var result = _sut.TogglePortRule(80, RuleMode.Trusted, watched, trusted, blocked);
        Assert.Contains("Trusted port 80", result);
        Assert.Contains(80, trusted);
        Assert.DoesNotContain(80, blocked);
    }

    [Fact]
    public void TogglePortRule_BlockMode_RemovesFromTrusted()
    {
        var watched = new HashSet<int>();
        var trusted = new HashSet<int> { 443 };
        var blocked = new HashSet<int>();

        var result = _sut.TogglePortRule(443, RuleMode.Blocked, watched, trusted, blocked);
        Assert.Contains("Blocked port 443", result);
        Assert.Contains(443, blocked);
        Assert.DoesNotContain(443, trusted);
    }

    [Fact]
    public void ToggleProcessRule_WatchMode_TogglesOnAndOff()
    {
        var watched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trusted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result1 = _sut.ToggleProcessRule("chrome", RuleMode.Watched, watched, trusted, blocked);
        Assert.Contains("Watching process chrome", result1);
        Assert.Contains("chrome", watched);

        var result2 = _sut.ToggleProcessRule("chrome", RuleMode.Watched, watched, trusted, blocked);
        Assert.Contains("Stopped watching process chrome", result2);
        Assert.DoesNotContain("chrome", watched);
    }

    [Fact]
    public void ToggleProcessRule_TrustMode_RemovesFromBlocked()
    {
        var watched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trusted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "nginx" };

        var result = _sut.ToggleProcessRule("nginx", RuleMode.Trusted, watched, trusted, blocked);
        Assert.Contains("Trusted process nginx", result);
        Assert.Contains("nginx", trusted);
        Assert.DoesNotContain("nginx", blocked);
    }
}
