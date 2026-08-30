using System.Diagnostics;
using Microsoft.Win32;

namespace NetVsMcp.Broker.Services;

public interface IAutostartService
{
    bool IsSupported { get; }
    string StatusText { get; }
    bool IsEnabled();
    void SetEnabled(bool enabled);
}
