using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    internal Task<ToolResponse<SessionCapabilityReport>> SessionCapabilities(string? sessionId = null,
        string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath,
            async (connection, ct) => (await connection.SessionCapabilitiesAsync(ct)) with { BrokerVersion = _runtime.GetStatus().Version },
            cancellationToken, unsupportedByVsixMessage: "Session capability discovery requires an updated VSIX; update it before relying on feature negotiation.");
}
