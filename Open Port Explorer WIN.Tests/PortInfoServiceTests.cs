using Open_Port_Explorer_WIN.Services;

namespace Open_Port_Explorer_WIN.Tests;

public class PortInfoServiceTests
{
    private readonly PortInfoService _sut = new();

    [Theory]
    [InlineData(22, "SSH")]
    [InlineData(80, "HTTP")]
    [InlineData(443, "HTTPS")]
    [InlineData(3306, "MySQL")]
    [InlineData(5432, "PostgreSQL")]
    [InlineData(6379, "Redis")]
    [InlineData(27017, "MongoDB")]
    [InlineData(3389, "RDP")]
    [InlineData(21, "FTP Control")]
    [InlineData(53, "DNS")]
    [InlineData(25, "SMTP")]
    public void GetServiceName_WellKnownPorts_ReturnsExpected(int port, string expected)
    {
        var result = _sut.GetServiceName(port);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetServiceName_UnknownPort_ReturnsEmpty()
    {
        var result = _sut.GetServiceName(99999);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetServiceName_EphemeralRange_ReturnsDynamic()
    {
        var result = _sut.GetServiceName(50000);
        Assert.Equal("Dynamic/Ephemeral", result);
    }

    [Theory]
    [InlineData(22, "Secure Shell remote login and tunneling.")]
    [InlineData(80, "Unencrypted web traffic (HTTP).")]
    [InlineData(443, "Encrypted web traffic (HTTPS).")]
    [InlineData(3389, "Remote Desktop Protocol.")]
    public void GetPortDescription_WellKnownPorts_ReturnsExpected(int port, string expected)
    {
        var result = _sut.GetPortDescription(port);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetPortDescription_UnknownPort_ReturnsDefault()
    {
        var result = _sut.GetPortDescription(99999);
        Assert.Equal("No known description for this port.", result);
    }

    [Theory]
    [InlineData("chrome", "Google Chrome browser process.")]
    [InlineData("chrome.exe", "Google Chrome browser process.")]
    [InlineData("svchost", "Windows Service Host process.")]
    [InlineData("sqlservr", "Microsoft SQL Server engine.")]
    public void GetKnownProcessDescription_KnownProcesses_ReturnsExpected(string processName, string expected)
    {
        var result = _sut.GetKnownProcessDescription(processName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("N/A", "Process information not available.")]
    [InlineData("", "Process information not available.")]
    [InlineData(null, "Process information not available.")]
    public void GetKnownProcessDescription_InvalidNames_ReturnsNotAvailable(string? processName, string expected)
    {
        var result = _sut.GetKnownProcessDescription(processName!);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetKnownProcessDescription_UnknownProcess_ReturnsDefault()
    {
        var result = _sut.GetKnownProcessDescription("totally_unknown_process_xyz");
        Assert.Equal("No known description for this process.", result);
    }

    [Fact]
    public void SuspiciousPorts_ContainsExpectedPorts()
    {
        Assert.Contains(22, _sut.SuspiciousPorts);
        Assert.Contains(23, _sut.SuspiciousPorts);
        Assert.Contains(445, _sut.SuspiciousPorts);
        Assert.Contains(3389, _sut.SuspiciousPorts);
        Assert.Contains(27017, _sut.SuspiciousPorts);
    }

    [Fact]
    public void SuspiciousPorts_DoesNotContainCommonSafePorts()
    {
        Assert.DoesNotContain(80, _sut.SuspiciousPorts);
        Assert.DoesNotContain(443, _sut.SuspiciousPorts);
        Assert.DoesNotContain(8080, _sut.SuspiciousPorts);
    }
}
