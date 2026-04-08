using Open_Port_Explorer_WIN.Models;
using Open_Port_Explorer_WIN.Services;

namespace Open_Port_Explorer_WIN.Tests;

public class PreferencesServiceTests : IDisposable
{
    private readonly PreferencesService _sut = new();
    private readonly string _tempDir;
    private readonly string _tempDbPath;

    public PreferencesServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"OpenPortExplorerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempDbPath = Path.Combine(_tempDir, "appdata.db");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetDatabasePath_ReturnsNonEmptyPath()
    {
        var path = _sut.GetDatabasePath();
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.EndsWith("appdata.db", path);
    }

    [Fact]
    public void Load_NonexistentFile_ReturnsDefaultPreferences()
    {
        var result = _sut.Load(Path.Combine(_tempDir, "nonexistent.db"));
        Assert.NotNull(result);
        Assert.False(result.UseDarkTheme);
        Assert.Empty(result.TrustedPorts);
        Assert.Empty(result.BlockedPorts);
        Assert.Empty(result.WatchedPorts);
    }

    [Fact]
    public void Save_And_Load_RoundTrip()
    {
        _sut.Save(
            _tempDbPath,
            isDarkTheme: true,
            trustedPorts: [443, 80],
            blockedPorts: [23],
            watchedPorts: [8080],
            trustedProcesses: ["chrome"],
            blockedProcesses: ["malware"],
            watchedProcesses: ["svchost"]);

        var loaded = _sut.Load(_tempDbPath);

        Assert.True(loaded.UseDarkTheme);
        Assert.Equal([80, 443], loaded.TrustedPorts);
        Assert.Equal([23], loaded.BlockedPorts);
        Assert.Equal([8080], loaded.WatchedPorts);
        Assert.Contains("chrome", loaded.TrustedProcesses);
        Assert.Contains("malware", loaded.BlockedProcesses);
        Assert.Contains("svchost", loaded.WatchedProcesses);
    }

    [Fact]
    public void Save_CreatesDirectoryIfNeeded()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "dir", "appdata.db");

        _sut.Save(nestedPath, false, [], [], [], [], [], []);

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void Load_EmptyDatabase_ReturnsDefaultPreferences()
    {
        // Create an empty SQLite database
        using (var connection = PreferencesService.OpenConnection(_tempDbPath))
        {
            // Empty database with no tables
        }

        var result = _sut.Load(_tempDbPath);

        Assert.NotNull(result);
        Assert.False(result.UseDarkTheme);
        Assert.Empty(result.TrustedPorts);
    }

    [Fact]
    public void Save_OverwritesPreviousPreferences()
    {
        _sut.Save(_tempDbPath, true, [80], [23], [8080], ["chrome"], ["malware"], ["svchost"]);
        _sut.Save(_tempDbPath, false, [443], [], [], [], [], []);

        var loaded = _sut.Load(_tempDbPath);

        Assert.False(loaded.UseDarkTheme);
        Assert.Equal([443], loaded.TrustedPorts);
        Assert.Empty(loaded.BlockedPorts);
        Assert.Empty(loaded.WatchedPorts);
        Assert.Empty(loaded.TrustedProcesses);
        Assert.Empty(loaded.BlockedProcesses);
        Assert.Empty(loaded.WatchedProcesses);
    }

    [Fact]
    public void ApplySet_PopulatesDestination()
    {
        var destination = new HashSet<int>();
        var source = new List<int> { 1, 2, 3 };

        _sut.ApplySet(destination, source);

        Assert.Equal(3, destination.Count);
        Assert.Contains(1, destination);
        Assert.Contains(2, destination);
        Assert.Contains(3, destination);
    }

    [Fact]
    public void ApplySet_ClearsExistingItems()
    {
        var destination = new HashSet<int> { 99, 100 };
        var source = new List<int> { 1 };

        _sut.ApplySet(destination, source);

        Assert.Single(destination);
        Assert.Contains(1, destination);
        Assert.DoesNotContain(99, destination);
    }

    [Fact]
    public void ApplySet_NullSource_ClearsDestination()
    {
        var destination = new HashSet<int> { 1, 2, 3 };

        _sut.ApplySet<int>(destination, null);

        Assert.Empty(destination);
    }
}
