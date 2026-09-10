using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services.Sessions.Interfaces;

public interface IVsSessionConnectionMap
{
    void AddOrUpdate(string sessionId, IVisualStudioSessionRpc connection);

    bool TryGet(string sessionId, out IVisualStudioSessionRpc connection);

    bool Remove(string sessionId);
}
