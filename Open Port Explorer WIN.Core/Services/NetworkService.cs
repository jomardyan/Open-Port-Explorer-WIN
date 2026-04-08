using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using Open_Port_Explorer_WIN.Interfaces;
using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Services;

public sealed class NetworkService : INetworkService
{
    private readonly IPortInfoService _portInfoService;
    private readonly ConcurrentDictionary<int, ProcessInfoCacheEntry> _processInfoCache = new();
    private static readonly TimeSpan ProcessInfoCacheDuration = TimeSpan.FromSeconds(20);
    private const int MaxProcessInfoCacheEntries = 4096;

    private readonly record struct ProcessInfoCacheEntry(string Name, string Path, DateTime CachedAtUtc);

    public NetworkService(IPortInfoService portInfoService)
    {
        _portInfoService = portInfoService;
    }

    public List<PortEntry> GetPortEntries()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netstat",
            Arguments = "-ano",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start netstat. Ensure the system command is available.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(error) ? "netstat exited with a non-zero status." : error.Trim();
            throw new InvalidOperationException($"netstat failed (exit code {process.ExitCode}): {message}");
        }

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var relevantLines = lines
            .Select(static line => line.Trim())
            .Where(static trimmed =>
                trimmed.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("UDP", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var entries = new ConcurrentBag<PortEntry>();
        var processCache = new ConcurrentDictionary<int, (string Name, string Path)>();

        Parallel.ForEach(relevantLines, trimmed =>
        {
            try
            {
                var parts = trimmed.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4)
                {
                    return;
                }

                var protocol = parts[0].ToUpperInvariant();
                var localEndpoint = parts[1];
                var remoteEndpoint = parts[2];
                var state = protocol == "TCP" && parts.Length >= 5 ? parts[3].ToUpperInvariant() : "LISTENING";
                var pidToken = protocol == "TCP" && parts.Length >= 5 ? parts[4] : parts[3];

                if (!int.TryParse(pidToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                {
                    pid = -1;
                }

                var localParsed = ParseEndpoint(localEndpoint);
                var remoteParsed = ParseEndpoint(remoteEndpoint);
                var processInfo = processCache.GetOrAdd(pid, key => GetProcessInfoCached(key));

                entries.Add(new PortEntry
                {
                    Protocol = protocol,
                    State = state,
                    AddressFamily = GetAddressFamily(localParsed.Address),
                    LocalAddressRaw = localParsed.Address,
                    RemoteAddressRaw = remoteParsed.Address,
                    LocalPort = localParsed.Port,
                    RemotePort = remoteParsed.Port,
                    PortNumber = localParsed.Port,
                    ServiceName = _portInfoService.GetServiceName(localParsed.Port),
                    PortDescription = _portInfoService.GetPortDescription(localParsed.Port),
                    ProcessId = pid,
                    ProcessName = processInfo.Name,
                    ProcessPath = processInfo.Path,
                    ProcessDescription = _portInfoService.GetKnownProcessDescription(processInfo.Name)
                });
            }
            catch (FormatException)
            {
                // Skip lines with unexpected format
            }
            catch (OverflowException)
            {
                // Skip lines with out-of-range numeric values
            }
        });

        return [.. entries];
    }

    private (string Name, string Path) GetProcessInfoCached(int pid)
    {
        if (pid <= 0)
        {
            return ("N/A", string.Empty);
        }

        var now = DateTime.UtcNow;
        if (_processInfoCache.TryGetValue(pid, out var cached) && now - cached.CachedAtUtc <= ProcessInfoCacheDuration)
        {
            return (cached.Name, cached.Path);
        }

        var processInfo = GetProcessInfo(pid);
        _processInfoCache[pid] = new ProcessInfoCacheEntry(processInfo.Name, processInfo.Path, now);

        if (_processInfoCache.Count > MaxProcessInfoCacheEntries)
        {
            TrimProcessInfoCache(now);
        }

        return processInfo;
    }

    private void TrimProcessInfoCache(DateTime nowUtc)
    {
        var expirationThreshold = nowUtc - ProcessInfoCacheDuration;
        foreach (var cached in _processInfoCache)
        {
            if (cached.Value.CachedAtUtc < expirationThreshold)
            {
                _processInfoCache.TryRemove(cached.Key, out _);
            }
        }

        if (_processInfoCache.Count <= MaxProcessInfoCacheEntries)
        {
            return;
        }

        foreach (var cached in _processInfoCache.OrderBy(static pair => pair.Value.CachedAtUtc).Take(_processInfoCache.Count - MaxProcessInfoCacheEntries))
        {
            _processInfoCache.TryRemove(cached.Key, out _);
        }
    }

    public string GetAddressFamily(string address)
    {
        if (string.IsNullOrWhiteSpace(address) || address == "*")
        {
            return "Unknown";
        }

        return address.Contains(':', StringComparison.Ordinal) ? "IPv6" : "IPv4";
    }

    public (string Address, int Port) ParseEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return ("*", 0);
        }

        var value = endpoint.Trim();
        if (string.Equals(value, "*:*", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "*", StringComparison.OrdinalIgnoreCase))
        {
            return ("*", 0);
        }

        var lastColon = value.LastIndexOf(':');
        if (lastColon <= 0)
        {
            return (value.Trim('[', ']'), 0);
        }

        var address = value[..lastColon].Trim('[', ']');
        var portText = value[(lastColon + 1)..];
        if (!int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
        {
            port = 0;
        }

        return (address, port);
    }

    public (string Name, string Path) GetProcessInfo(int pid)
    {
        if (pid <= 0)
        {
            return ("N/A", string.Empty);
        }

        try
        {
            using var p = Process.GetProcessById(pid);
            var name = p.ProcessName;
            var path = string.Empty;
            try
            {
                path = p.MainModule?.FileName ?? string.Empty;
            }
            catch (InvalidOperationException)
            {
                // Process exited before module info could be read
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Access denied when reading module information
            }
            return (name, path);
        }
        catch (ArgumentException)
        {
            // Process with the specified PID is no longer running
            return ("Unavailable", string.Empty);
        }
        catch (InvalidOperationException)
        {
            // Process exited during lookup
            return ("Unavailable", string.Empty);
        }
    }

    public bool IsPublicBinding(string address)
    {
        if (string.IsNullOrWhiteSpace(address) || address == "*")
        {
            return true;
        }

        return !string.Equals(address, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(address, "::1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    public bool CanResolveHost(string address)
    {
        if (string.IsNullOrWhiteSpace(address) || address == "*" || address == "0.0.0.0" || address == "::")
        {
            return false;
        }

        return !string.Equals(address, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(address, "::1", StringComparison.OrdinalIgnoreCase);
    }

    public string ResolveHostName(string address)
    {
        try
        {
            return Dns.GetHostEntry(address).HostName;
        }
        catch (ArgumentException)
        {
            return "Unavailable";
        }
        catch (System.Net.Sockets.SocketException)
        {
            return "Unavailable";
        }
    }
}
