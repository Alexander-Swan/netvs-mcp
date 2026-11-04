using ModelContextProtocol.Protocol;
using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NetVsMcp.Broker.Tests;

public sealed class ReferenceResponseOutputTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    [Fact]
    public void ReferencesUseRelativeLocationsAndInheritSymbolIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), "ReferenceOutput");
        var symbol = new DocumentSymbolInfo("Calculate", "Method", Path.Combine(root, "OrderService.cs"),
            48, 20, "Shop.OrderService", "Shop");
        var external = Path.Combine(root + "External", "Caller.cs");
        var payload = new FindReferencesResult(symbol,
            [new(Path.Combine(root, "Program.cs"), 15, 24, false, symbol with { File = Path.Combine(root, "Program.cs"), Line = 15, Column = 24 }),
             new(external, 9, 1, true, symbol with { Name = "Other", ContainingType = "Shop.OtherService" })]);
        var response = BrokerToolService.ShapeInspectionOutput(ToolResponse<FindReferencesResult>.Ok(payload), false,
            solutionPath: Path.Combine(root, "Shop.slnx"));
        var result = Result(response);
        ReferenceResponseOutput.Apply(result);
        var output = Output(result);
        Assert.False(result.IsError == true);
        Assert.Null(output["success"]);
        Assert.Null(output["value"]);
        Assert.Equal("Shop.OrderService.Calculate", output["symbol"]!["name"]!.GetValue<string>());
        Assert.Equal("OrderService.cs:48:20", output["symbol"]!["location"]!.GetValue<string>());
        var references = output["references"]!.AsArray();
        Assert.Single(references[0]!.AsObject());
        Assert.Equal("Program.cs:15:24", references[0]!["location"]!.GetValue<string>());
        Assert.Equal(external + ":9:1", references[1]!["location"]!.GetValue<string>());
        Assert.True(references[1]!["isImplicit"]!.GetValue<bool>());
        Assert.Equal("Shop.OtherService.Other", references[1]!["symbol"]!["name"]!.GetValue<string>());
        Assert.Equal(Path.Combine(root, "OrderService.cs"), payload.Symbol!.File);
    }

    [Fact]
    public void FailureUsesMcpErrorAndRetainsRecoveryMetadata()
    {
        var result = Result(new ToolResponse<FindReferencesResult>(false, null, "Session unavailable",
            new Dictionary<string, string> { ["nextActions"] = "Select a session" }));
        ReferenceResponseOutput.Apply(result);
        var output = Output(result);
        Assert.True(result.IsError);
        Assert.Equal("Session unavailable", output["error"]!.GetValue<string>());
        Assert.Equal("Select a session", output["metadata"]!["nextActions"]!.GetValue<string>());
    }

    [Fact]
    public void NoSymbolReturnsAnEmptyReferenceList()
    {
        var result = Result(ToolResponse<FindReferencesResult>.Ok(new(null, [])));
        ReferenceResponseOutput.Apply(result);
        var output = Output(result);
        Assert.Null(output["symbol"]);
        Assert.Empty(output["references"]!.AsArray());
    }

    private static CallToolResult Result<T>(T response) => new()
    {
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(response, Json) }],
        StructuredContent = JsonSerializer.SerializeToElement(response, Json)
    };

    private static JsonObject Output(CallToolResult result)
    {
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var node = JsonNode.Parse(text)!;
        Assert.True(JsonNode.DeepEquals(node, JsonSerializer.SerializeToNode(result.StructuredContent)));
        return node.AsObject();
    }
}
