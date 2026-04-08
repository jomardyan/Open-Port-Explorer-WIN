using System.Globalization;
using Open_Port_Explorer_WIN.Interfaces;
using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Helpers;

public sealed class FormatHelpers : IFormatHelpers
{
    public string FormatDuration(double observedSeconds)
    {
        var duration = TimeSpan.FromSeconds(observedSeconds);
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.Minutes}m {duration.Seconds}s";
        }

        return $"{duration.Seconds}s";
    }

    public string CreateConnectionIdentity(PortEntry entry)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{entry.Protocol}|{entry.LocalAddressRaw}|{entry.LocalPort}|{entry.RemoteAddressRaw}|{entry.RemotePort}|{entry.ProcessId}");
    }
}
