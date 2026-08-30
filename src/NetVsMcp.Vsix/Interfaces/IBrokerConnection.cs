using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NetVsMcp.Contracts;
using StreamJsonRpc;

namespace NetVsMcp.Vsix;

internal interface IBrokerConnection : IDisposable
{
    bool IsConnected { get; }
    Task RegisterAsync(VsRegistrationRequest request, CancellationToken cancellationToken);
    Task HeartbeatAsync(VsHeartbeatRequest request, CancellationToken cancellationToken);
    Task UnregisterAsync(string sessionId, CancellationToken cancellationToken);
}
