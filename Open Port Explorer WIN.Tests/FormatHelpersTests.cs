using Open_Port_Explorer_WIN.Helpers;
using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Tests;

public class FormatHelpersTests
{
    private readonly FormatHelpers _sut = new();

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(45, "45s")]
    [InlineData(59, "59s")]
    [InlineData(60, "1m 0s")]
    [InlineData(125, "2m 5s")]
    [InlineData(3599, "59m 59s")]
    [InlineData(3600, "1h 0m")]
    [InlineData(3665, "1h 1m")]
    [InlineData(7200, "2h 0m")]
    public void FormatDuration_ReturnsExpectedFormat(double seconds, string expected)
    {
        var result = _sut.FormatDuration(seconds);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CreateConnectionIdentity_ReturnsExpectedFormat()
    {
        var entry = new PortEntry
        {
            Protocol = "TCP",
            LocalAddressRaw = "192.168.1.1",
            LocalPort = 8080,
            RemoteAddressRaw = "10.0.0.1",
            RemotePort = 443,
            ProcessId = 1234
        };

        var result = _sut.CreateConnectionIdentity(entry);
        Assert.Equal("TCP|192.168.1.1|8080|10.0.0.1|443|1234", result);
    }

    [Fact]
    public void CreateConnectionIdentity_WithEmptyFields_ReturnsExpectedFormat()
    {
        var entry = new PortEntry
        {
            Protocol = "UDP",
            LocalAddressRaw = "0.0.0.0",
            LocalPort = 53,
            RemoteAddressRaw = "",
            RemotePort = 0,
            ProcessId = 0
        };

        var result = _sut.CreateConnectionIdentity(entry);
        Assert.Equal("UDP|0.0.0.0|53||0|0", result);
    }
}
