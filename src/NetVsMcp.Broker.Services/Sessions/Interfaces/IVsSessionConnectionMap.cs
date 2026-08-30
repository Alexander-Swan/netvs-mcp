using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public interface IVsSessionConnectionMap
{
    void AddOrUpdate(string sessionId, IVisualStudioSessionRpc connection);

    bool TryGet(string sessionId, out IVisualStudioSessionRpc connection);

    bool Remove(string sessionId);
}
