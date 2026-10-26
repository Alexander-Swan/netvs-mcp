namespace NetVsMcp.Contracts;

public static class VsRpcErrorCodes
{
    public const int UnhandledCapability = -32050;
    public const int StaleDebuggerContext = -32051;
    public const int StaleAction = -32052;
    public const int FeatureUnavailable = -32053;
    public const int HistoryUnavailable = -32054;
    public const int InspectionUnavailable = -32055;
    public const int InvalidRequest = -32602; // JSON-RPC Invalid params.
}
