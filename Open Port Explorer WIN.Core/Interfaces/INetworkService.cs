using Open_Port_Explorer_WIN.Models;

namespace Open_Port_Explorer_WIN.Interfaces;

public interface INetworkService
{
    List<PortEntry> GetPortEntries();
    string GetAddressFamily(string address);
    (string Address, int Port) ParseEndpoint(string endpoint);
    (string Name, string Path) GetProcessInfo(int pid);
    bool IsPublicBinding(string address);
    bool CanResolveHost(string address);
    string ResolveHostName(string address);
}
