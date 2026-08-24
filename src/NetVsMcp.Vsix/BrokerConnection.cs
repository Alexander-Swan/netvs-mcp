using System;
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
    private static readonly TimeSpan RpcTimeout = TimeSpan.FromSeconds(15);

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
        return InvokeAndValidateAsync(
            "RegisterAsync",
            cancellationToken,
            VsContractMapping.ToRegistration(request));
    }

    public Task HeartbeatAsync(VsHeartbeatRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return HeartbeatAndUpdateAsync(request, cancellationToken);
    }

    public Task UnregisterAsync(string sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return InvokeAndValidateAsync("UnregisterAsync", cancellationToken, sessionId);
    }

    private async Task HeartbeatAndUpdateAsync(VsHeartbeatRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await InvokeAndValidateAsync(
            "UpdateAsync",
            cancellationToken,
            VsContractMapping.ToUpdate(request));
        cancellationToken.ThrowIfCancellationRequested();
        await InvokeAndValidateAsync("HeartbeatAsync", cancellationToken, request.Session.SessionId);
    }

    private async Task InvokeAndValidateAsync(
        string targetName,
        CancellationToken cancellationToken,
        params object?[] arguments)
    {
        var response = await WithTimeoutAsync(
            rpc.InvokeAsync<ToolResponse>(targetName, arguments),
            targetName,
            cancellationToken);

        if (!response.Success)
        {
            throw new InvalidOperationException(response.Message ?? $"Broker RPC '{targetName}' failed.");
        }
    }

    private static async Task<T> WithTimeoutAsync<T>(
        Task<T> operation,
        string targetName,
        CancellationToken cancellationToken)
    {
#pragma warning disable VSTHRD003 // RPC tasks are started by StreamJsonRpc; this helper bounds how long the lifecycle waits on them.
        var timeout = Task.Delay(RpcTimeout, cancellationToken);
        var completed = await Task.WhenAny(operation, timeout).ConfigureAwait(false);

        if (completed == operation)
        {
            return await operation.ConfigureAwait(false);
        }
#pragma warning restore VSTHRD003

        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException($"Broker RPC '{targetName}' timed out after {RpcTimeout.TotalSeconds:0} seconds.");
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
