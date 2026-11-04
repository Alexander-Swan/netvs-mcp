using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetVsMcp.Broker.Services;

internal static class ReferenceResponseOutput
{
    internal static void Apply(CallToolResult result)
    {
        foreach (var text in result.Content.OfType<TextContentBlock>())
        {
            try
            {
                var node = JsonNode.Parse(text.Text);
                if (Project(node, out var output, out var error))
                {
                    text.Text = output!.ToJsonString();
                    if (error) result.IsError = true;
                }
            }
            catch (JsonException) { }
        }
        if (result.StructuredContent is { } structured &&
            Project(JsonSerializer.SerializeToNode(structured), out var projected, out var failed))
        {
            result.StructuredContent = JsonSerializer.SerializeToElement(projected);
            if (failed) result.IsError = true;
        }
    }

    private static bool Project(JsonNode? node, out JsonObject? output, out bool error)
    {
        output = null;
        error = false;
        if (node is not JsonObject envelope || envelope["success"] is not JsonValue success ||
            !success.TryGetValue<bool>(out var ok)) return false;
        if (!ok)
        {
            error = true;
            output = new JsonObject { ["error"] = envelope["message"]?.DeepClone() ?? JsonValue.Create("Reference lookup failed.") };
            if (envelope["metadata"] is { } metadata) output["metadata"] = metadata.DeepClone();
            return true;
        }
        if (envelope["value"] is not JsonObject value || value["references"] is not JsonArray references) return false;
        var symbol = value["symbol"] as JsonObject;
        var projectedReferences = new JsonArray();
        foreach (var reference in references.OfType<JsonObject>())
        {
            var entry = new JsonObject();
            AddLocation(entry, reference);
            if (reference["isImplicit"]?.GetValue<bool>() == true) entry["isImplicit"] = true;
            if (reference["symbol"] is JsonObject related && !SameSymbol(symbol, related))
                entry["symbol"] = Symbol(related);
            projectedReferences.Add(entry);
        }
        output = new JsonObject { ["symbol"] = symbol is null ? null : Symbol(symbol), ["references"] = projectedReferences };
        return true;
    }

    private static bool SameSymbol(JsonObject? left, JsonObject right)
        => left is not null && new[] { "name", "kind", "containingType", "containingNamespace" }
            .All(key => JsonNode.DeepEquals(left[key], right[key]));

    private static JsonObject Symbol(JsonObject source)
    {
        var name = source["name"]?.GetValue<string>() ?? "";
        var type = source["containingType"]?.GetValue<string>();
        var ns = source["containingNamespace"]?.GetValue<string>();
        // Roslyn supplies a namespace-qualified containing type already.
        var container = string.IsNullOrEmpty(type) ? ns : type;
        if (!string.IsNullOrEmpty(ns) && !string.IsNullOrEmpty(type) && !type.StartsWith(ns + ".", StringComparison.Ordinal))
            container = ns + "." + type;
        if (!string.IsNullOrEmpty(container)) name = container + "." + name;
        var result = new JsonObject { ["name"] = name, ["kind"] = source["kind"]?.DeepClone() };
        AddLocation(result, source);
        return result;
    }

    private static void AddLocation(JsonObject target, JsonObject source)
    {
        if (source["file"] is JsonValue file)
            target["location"] = $"{file.GetValue<string>()}:{source["line"]?.GetValue<int>() ?? 0}:{source["column"]?.GetValue<int>() ?? 0}";
    }
}
