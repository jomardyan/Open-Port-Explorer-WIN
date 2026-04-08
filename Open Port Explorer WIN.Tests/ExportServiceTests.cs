using Open_Port_Explorer_WIN.Models;
using Open_Port_Explorer_WIN.Services;

namespace Open_Port_Explorer_WIN.Tests;

public class ExportServiceTests : IDisposable
{
    private readonly ExportService _sut = new();
    private readonly string _tempDir;

    public ExportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ExportTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("with,comma", "\"with,comma\"")]
    [InlineData("with\"quote", "\"with\"\"quote\"")]
    [InlineData("both,and\"", "\"both,and\"\"\"")]
    [InlineData("", "")]
    [InlineData("no special chars", "no special chars")]
    public void CsvEscape_ReturnsExpectedResult(string input, string expected)
    {
        var result = ExportService.CsvEscape(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void WriteCsv_CreatesFileWithHeaders()
    {
        var entries = new List<PortEntry>
        {
            new()
            {
                PortNumber = 80,
                ServiceName = "HTTP",
                PortDescription = "Web traffic",
                Protocol = "TCP",
                State = "LISTENING",
                RuleStatus = "Trusted",
                LocalAddressRaw = "0.0.0.0",
                LocalPort = 80,
                RemoteAddressRaw = "*",
                RemotePort = 0,
                ProcessId = 1234,
                ProcessName = "nginx",
                ProcessDescription = "Web server"
            }
        };

        var filePath = Path.Combine(_tempDir, "test.csv");
        _sut.WriteCsv(entries, filePath);

        Assert.True(File.Exists(filePath));
        var content = File.ReadAllText(filePath);
        Assert.StartsWith("Port,Service,PortDescription,Protocol,State,Rule", content);
        Assert.Contains("80", content);
        Assert.Contains("HTTP", content);
        Assert.Contains("nginx", content);
    }

    [Fact]
    public void WriteJson_CreatesValidJsonFile()
    {
        var entries = new List<PortEntry>
        {
            new() { PortNumber = 443, Protocol = "TCP", State = "LISTENING" }
        };

        var filePath = Path.Combine(_tempDir, "test.json");
        _sut.WriteJson(entries, filePath);

        Assert.True(File.Exists(filePath));
        var content = File.ReadAllText(filePath);
        Assert.Contains("443", content);
        Assert.Contains("TCP", content);
    }

    [Fact]
    public void WriteActivityLog_CreatesFileWithEntries()
    {
        var history = new List<PortHistoryEntry>
        {
            new() { Timestamp = new DateTime(2024, 1, 1, 12, 0, 0), EventType = "Opened", Details = "Port 80 opened" },
            new() { Timestamp = new DateTime(2024, 1, 1, 12, 5, 0), EventType = "Closed", Details = "Port 80 closed" }
        };

        var filePath = Path.Combine(_tempDir, "activity.txt");
        _sut.WriteActivityLog(history, filePath);

        Assert.True(File.Exists(filePath));
        var lines = File.ReadAllLines(filePath);
        Assert.Equal(2, lines.Length);
        Assert.Contains("Port 80 opened", lines[0]);
        Assert.Contains("Port 80 closed", lines[1]);
    }
}
