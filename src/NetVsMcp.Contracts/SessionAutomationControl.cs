namespace NetVsMcp.Contracts;

public sealed record SessionAutomationControl(string SessionId, bool Paused, string? ActiveMethod);
