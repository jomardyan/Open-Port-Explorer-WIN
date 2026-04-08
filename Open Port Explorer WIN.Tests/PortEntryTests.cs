using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Tests;

public class PortEntryTests
{
    [Fact]
    public void LocalAddress_CombinesRawAddressAndPort()
    {
        var entry = new PortEntry
        {
            LocalAddressRaw = "192.168.1.1",
            LocalPort = 8080
        };

        Assert.Equal("192.168.1.1:8080", entry.LocalAddress);
    }

    [Fact]
    public void RemoteAddress_WithPort_CombinesRawAddressAndPort()
    {
        var entry = new PortEntry
        {
            RemoteAddressRaw = "10.0.0.1",
            RemotePort = 443
        };

        Assert.Equal("10.0.0.1:443", entry.RemoteAddress);
    }

    [Fact]
    public void RemoteAddress_WithoutPort_ReturnsRawAddressOnly()
    {
        var entry = new PortEntry
        {
            RemoteAddressRaw = "*",
            RemotePort = 0
        };

        Assert.Equal("*", entry.RemoteAddress);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var entry = new PortEntry();

        Assert.Equal(0, entry.PortNumber);
        Assert.Equal(string.Empty, entry.Protocol);
        Assert.Equal(string.Empty, entry.AddressFamily);
        Assert.Equal(string.Empty, entry.State);
        Assert.Equal("Unruled", entry.RuleStatus);
        Assert.False(entry.IsSuspicious);
        Assert.False(entry.IsWatched);
        Assert.Equal(string.Empty, entry.ProcessName);
        Assert.Equal(0, entry.ProcessId);
    }
}
