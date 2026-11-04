using System.Text.Json;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    // Resolve the same session after dispatch; never guess a root from a document's directory.
    internal string? GetResponseSolutionPath(IEnumerable<KeyValuePair<string, JsonElement>>? arguments)
    {
        var args = arguments?.ToDictionary(p => p.Key, p => p.Value) ?? new();
        string? Get(string key) => args.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        var target = CreateTarget(Get("sessionId"), Get("solutionName"), Get("solutionPath"),
            workspacePath: GetInferredWorkspacePath(Get("documentPath") ?? Get("path"), Get("sessionId"), Get("solutionName"), Get("solutionPath")));
        return _runtime.Sessions.Resolve(target).Session?.SolutionPath;
    }
}
