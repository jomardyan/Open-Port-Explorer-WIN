using Open_Port_Explorer_WIN.Services;

namespace Open_Port_Explorer_WIN.Tests;

public class NetworkServiceTests
{
    private readonly NetworkService _sut;

    public NetworkServiceTests()
    {
        _sut = new NetworkService(new PortInfoService());
    }

    [Theory]
    [InlineData("192.168.1.1", "IPv4")]
    [InlineData("10.0.0.1", "IPv4")]
    [InlineData("127.0.0.1", "IPv4")]
    [InlineData("::1", "IPv6")]
    [InlineData("fe80::1", "IPv6")]
    [InlineData("2001:db8::1", "IPv6")]
    [InlineData("*", "Unknown")]
    [InlineData("", "Unknown")]
    [InlineData(null, "Unknown")]
    public void GetAddressFamily_ReturnsExpectedResult(string? address, string expected)
    {
        var result = _sut.GetAddressFamily(address!);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("192.168.1.1:8080", "192.168.1.1", 8080)]
    [InlineData("127.0.0.1:443", "127.0.0.1", 443)]
    [InlineData("0.0.0.0:0", "0.0.0.0", 0)]
    [InlineData("[::1]:443", "::1", 443)]
    [InlineData("[fe80::1]:80", "fe80::1", 80)]
    [InlineData("*:*", "*", 0)]
    [InlineData("*", "*", 0)]
    [InlineData("", "*", 0)]
    [InlineData(null, "*", 0)]
    public void ParseEndpoint_ReturnsExpectedResult(string? endpoint, string expectedAddress, int expectedPort)
    {
        var (address, port) = _sut.ParseEndpoint(endpoint!);
        Assert.Equal(expectedAddress, address);
        Assert.Equal(expectedPort, port);
    }

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("::", true)]
    [InlineData("*", true)]
    [InlineData("", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("localhost", false)]
    public void IsPublicBinding_ReturnsExpectedResult(string address, bool expected)
    {
        var result = _sut.IsPublicBinding(address);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("google.com", true)]
    [InlineData("*", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("::", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void CanResolveHost_ReturnsExpectedResult(string? address, bool expected)
    {
        var result = _sut.CanResolveHost(address!);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "N/A", "")]
    [InlineData(-1, "N/A", "")]
    public void GetProcessInfo_InvalidPid_ReturnsNA(int pid, string expectedName, string expectedPath)
    {
        var (name, path) = _sut.GetProcessInfo(pid);
        Assert.Equal(expectedName, name);
        Assert.Equal(expectedPath, path);
    }

    [Fact]
    public void GetProcessInfo_NonexistentPid_ReturnsUnavailable()
    {
        var (name, _) = _sut.GetProcessInfo(999999);
        Assert.Equal("Unavailable", name);
    }
}
