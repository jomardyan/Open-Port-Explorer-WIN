using Open_Port_Explorer_WIN.Services;

namespace Open_Port_Explorer_WIN.Tests;

public class LocalDataServiceTests : IDisposable
{
    private readonly LocalDataService _sut = new();
    private readonly string _tempDir;
    private readonly string _tempDbPath;

    public LocalDataServiceTests()
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
    public void GetValue_NonexistentDatabase_ReturnsNull()
    {
        var result = _sut.GetValue(Path.Combine(_tempDir, "nonexistent.db"), "key");
        Assert.Null(result);
    }

    [Fact]
    public void SetValue_And_GetValue_RoundTrip()
    {
        _sut.SetValue(_tempDbPath, "theme", "dark");

        var result = _sut.GetValue(_tempDbPath, "theme");

        Assert.Equal("dark", result);
    }

    [Fact]
    public void SetValue_OverwritesExistingValue()
    {
        _sut.SetValue(_tempDbPath, "lang", "en");
        _sut.SetValue(_tempDbPath, "lang", "fr");

        var result = _sut.GetValue(_tempDbPath, "lang");

        Assert.Equal("fr", result);
    }

    [Fact]
    public void GetValue_MissingKey_ReturnsNull()
    {
        _sut.SetValue(_tempDbPath, "exists", "yes");

        var result = _sut.GetValue(_tempDbPath, "missing");

        Assert.Null(result);
    }

    [Fact]
    public void RemoveValue_ExistingKey_ReturnsTrue()
    {
        _sut.SetValue(_tempDbPath, "key", "value");

        var removed = _sut.RemoveValue(_tempDbPath, "key");

        Assert.True(removed);
        Assert.Null(_sut.GetValue(_tempDbPath, "key"));
    }

    [Fact]
    public void RemoveValue_MissingKey_ReturnsFalse()
    {
        _sut.SetValue(_tempDbPath, "other", "value");

        var removed = _sut.RemoveValue(_tempDbPath, "missing");

        Assert.False(removed);
    }

    [Fact]
    public void RemoveValue_NonexistentDatabase_ReturnsFalse()
    {
        var removed = _sut.RemoveValue(Path.Combine(_tempDir, "nonexistent.db"), "key");

        Assert.False(removed);
    }

    [Fact]
    public void GetAllValues_ReturnsAllStoredEntries()
    {
        _sut.SetValue(_tempDbPath, "a", "1");
        _sut.SetValue(_tempDbPath, "b", "2");
        _sut.SetValue(_tempDbPath, "c", "3");

        var all = _sut.GetAllValues(_tempDbPath);

        Assert.Equal(3, all.Count);
        Assert.Equal("1", all["a"]);
        Assert.Equal("2", all["b"]);
        Assert.Equal("3", all["c"]);
    }

    [Fact]
    public void GetAllValues_NonexistentDatabase_ReturnsEmptyDictionary()
    {
        var all = _sut.GetAllValues(Path.Combine(_tempDir, "nonexistent.db"));

        Assert.Empty(all);
    }

    [Fact]
    public void SetValue_CreatesDirectoryIfNeeded()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "dir", "appdata.db");

        _sut.SetValue(nestedPath, "key", "value");

        Assert.True(File.Exists(nestedPath));
        Assert.Equal("value", _sut.GetValue(nestedPath, "key"));
    }
}
