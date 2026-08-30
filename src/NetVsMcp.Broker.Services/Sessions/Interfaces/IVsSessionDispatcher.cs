using NetVsMcp.Contracts;
using System.Diagnostics;
using System.IO;
using StreamJsonRpc;

namespace NetVsMcp.Broker.Services;

public interface IVsSessionDispatcher
{
    Task<VsSessionDispatchResult<T>> DispatchAsync<T>(
        RoutingTarget? target,
        Func<IVisualStudioSessionRpc, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null);
}
