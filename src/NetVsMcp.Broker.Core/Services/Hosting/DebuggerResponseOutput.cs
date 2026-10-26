using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetVsMcp.Broker.Services;

internal static class DebuggerResponseOutput
{
    // Shape only public MCP output. RPC contracts and in-memory pause guards retain full state.
    internal static void DeduplicateState(CallToolResult result)
    {
        foreach (var text in result.Content.OfType<TextContentBlock>())
        {
            try
            {
                var node = JsonNode.Parse(text.Text);
                if (RemoveInheritedState(node, null)) text.Text = node!.ToJsonString();
            }
            catch (JsonException)
            {
                // Plain-text tool results are not debugger payloads.
            }
        }

        if (result.StructuredContent is { } structured)
        {
            var node = JsonSerializer.SerializeToNode(structured);
            if (RemoveInheritedState(node, null))
                result.StructuredContent = JsonSerializer.SerializeToElement(node);
        }
    }

    private static bool RemoveInheritedState(JsonNode? node, JsonObject? inherited)
    {
        var changed = false;
        if (node is JsonObject obj)
        {
            var current = inherited;
            if (obj["state"] is JsonObject state && state["mode"] is JsonValue mode &&
                mode.TryGetValue<string>(out var name) &&
                name is "dbgDesignMode" or "dbgRunMode" or "dbgBreakMode" or "dbgModalBlock")
            {
                current = state;
                if (inherited is not null && InheritsState(state, inherited))
                {
                    obj.Remove("state");
                    changed = true;
                }
            }

            foreach (var property in obj)
                if (property.Key != "state") changed |= RemoveInheritedState(property.Value, current);
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array) changed |= RemoveInheritedState(item, inherited);
        }
        return changed;
    }

    private static bool InheritsState(JsonObject state, JsonObject inherited)
    {
        if (JsonNode.DeepEquals(inherited, state)) return true;
        if (state.ContainsKey("eventCursor") || !inherited.ContainsKey("eventCursor")) return false;
        var withoutCursor = (JsonObject)inherited.DeepClone();
        withoutCursor.Remove("eventCursor");
        return JsonNode.DeepEquals(withoutCursor, state);
    }
}
