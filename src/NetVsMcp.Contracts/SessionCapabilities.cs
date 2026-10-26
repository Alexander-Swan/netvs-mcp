namespace NetVsMcp.Contracts;

public sealed record SessionFeature(string Name, string Status, string Reason, string? Alternative = null);
public sealed record SessionCapabilityReport(VsSessionInfo Session, string ProtocolVersion,
    IReadOnlyList<string> Languages, IReadOnlyList<SessionFeature> Features, string? BrokerVersion = null);
