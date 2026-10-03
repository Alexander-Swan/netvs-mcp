namespace NetVsMcp.Broker.Services;

internal sealed class BrokerShutdownPolicy
{
    private readonly Func<bool> _isShutdownAllowed;
    private readonly Action _requestShutdown;

    public BrokerShutdownPolicy(Func<bool>? isShutdownAllowed = null, Action? requestShutdown = null)
    {
        _isShutdownAllowed = isShutdownAllowed ?? (() => false);
        _requestShutdown = requestShutdown ?? (() => { });
    }

    public bool IsShutdownAllowed() => _isShutdownAllowed();

    public void RequestShutdown() => _requestShutdown();
}
