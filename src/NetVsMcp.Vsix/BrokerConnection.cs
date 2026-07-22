using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace NetVsMcp.Vsix;

internal interface IBrokerConnection : IDisposable
{
    bool IsConnected { get; }
    Task RegisterAsync(VsRegistrationRequest request, CancellationToken cancellationToken);
    Task HeartbeatAsync(VsHeartbeatRequest request, CancellationToken cancellationToken);
    Task UnregisterAsync(string sessionId, CancellationToken cancellationToken);
}

internal interface IBrokerConnectionFactory
{
    Task<IBrokerConnection> ConnectAsync(CancellationToken cancellationToken);
}

internal sealed class NamedPipeBrokerConnectionFactory : IBrokerConnectionFactory
{
    private const int ConnectTimeoutMilliseconds = 2_000;
    private readonly string pipeName;
    private readonly object localRpcTarget;

    public NamedPipeBrokerConnectionFactory(string pipeName, object localRpcTarget)
    {
        this.pipeName = pipeName;
        this.localRpcTarget = localRpcTarget;
    }

    public async Task<IBrokerConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var stream = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            await Task.Run(() => stream.Connect(ConnectTimeoutMilliseconds), cancellationToken);
            return new JsonRpcBrokerConnection(stream, localRpcTarget);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }
}

internal sealed class JsonRpcBrokerConnection : IBrokerConnection
{
    private readonly NamedPipeClientStream stream;
    private readonly JsonRpc rpc;
    private bool disposed;

    public JsonRpcBrokerConnection(NamedPipeClientStream stream, object localRpcTarget)
    {
        this.stream = stream;
        rpc = JsonRpc.Attach(stream, localRpcTarget);
    }

    public bool IsConnected => !disposed && stream.IsConnected;

    public Task RegisterAsync(VsRegistrationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return rpc.InvokeAsync("RegisterAsync", VsSessionRegistrationWire.FromRequest(request));
    }

    public Task HeartbeatAsync(VsHeartbeatRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return HeartbeatAndUpdateAsync(request, cancellationToken);
    }

    public Task UnregisterAsync(string sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return rpc.InvokeAsync("UnregisterAsync", sessionId);
    }

    private async Task HeartbeatAndUpdateAsync(VsHeartbeatRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await rpc.InvokeAsync("UpdateAsync", VsSessionUpdateWire.FromRequest(request));
        cancellationToken.ThrowIfCancellationRequested();
        await rpc.InvokeAsync("HeartbeatAsync", request.Session.SessionId);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        rpc.Dispose();
        stream.Dispose();
    }
}
