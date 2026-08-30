using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NetVsMcp.Contracts;
using StreamJsonRpc;

namespace NetVsMcp.Vsix;

internal interface IBrokerConnectionFactory
{
    Task<IBrokerConnection> ConnectAsync(CancellationToken cancellationToken);
}
