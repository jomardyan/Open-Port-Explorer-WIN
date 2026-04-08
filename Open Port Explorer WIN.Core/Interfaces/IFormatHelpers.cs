using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Interfaces;

public interface IFormatHelpers
{
    string FormatDuration(double observedSeconds);
    string CreateConnectionIdentity(PortEntry entry);
}
