namespace Open_Port_Explorer_WIN.Interfaces;

public interface IPortInfoService
{
    IReadOnlySet<int> SuspiciousPorts { get; }
    string GetServiceName(int port);
    string GetPortDescription(int port);
    string GetKnownProcessDescription(string processName);
}
