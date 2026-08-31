using System.Reflection;
using ModelContextProtocol.Server;
using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

/// <summary>
/// Ensures the reflected <c>get_help</c> catalog stays aligned with the method-level MCP and
/// broker-specific metadata attributes on <c>BrokerToolService</c>.
/// </summary>
public sealed class BrokerToolServiceDescriptorSyncTests
{
    private static IReadOnlyCollection<string> AttributedToolNames()
    {
        return typeof(BrokerToolService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .Where(attribute => attribute is not null)
            .Select(attribute => attribute!.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToArray();
    }

    private static IReadOnlyCollection<string> ToolDescriptorNames()
    {
        var field = typeof(BrokerToolService).GetField(
            "ToolDescriptors",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        var descriptors = (Array?)field!.GetValue(null);
        Assert.NotNull(descriptors);

        var nameProperty = descriptors!
            .GetType()
            .GetElementType()!
            .GetProperty("Name");
        Assert.NotNull(nameProperty);

        return descriptors
            .Cast<object>()
            .Select(descriptor => (string)nameProperty!.GetValue(descriptor)!)
            .ToArray();
    }

    [Fact]
    public void EveryMcpServerToolMethod_HasAToolDescriptorsEntry()
    {
        var attributed = AttributedToolNames();
        var descriptors = ToolDescriptorNames().ToHashSet(StringComparer.Ordinal);

        var missing = attributed.Where(name => !descriptors.Contains(name)).ToArray();

        Assert.True(
            missing.Length == 0,
            $"[McpServerTool] method(s) with no matching ToolDescriptors entry: {string.Join(", ", missing)}. " +
            "Add an entry to BrokerToolService.ToolDescriptors so get_help lists it correctly.");
    }

    [Fact]
    public void EveryToolDescriptorsEntry_HasAnMcpServerToolMethod()
    {
        var attributed = AttributedToolNames().ToHashSet(StringComparer.Ordinal);
        var descriptors = ToolDescriptorNames();

        var stale = descriptors.Where(name => !attributed.Contains(name)).ToArray();

        Assert.True(
            stale.Length == 0,
            $"ToolDescriptors entry/entries with no matching [McpServerTool] method: {string.Join(", ", stale)}. " +
            "Remove the stale entry, or rename it to match the method's [McpServerTool(Name = \"...\")] attribute.");
    }

    [Fact]
    public void ToolDescriptors_HasNoDuplicateNames()
    {
        var descriptors = ToolDescriptorNames();
        var duplicates = descriptors
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.True(
            duplicates.Length == 0,
            $"ToolDescriptors has duplicate entries: {string.Join(", ", duplicates)}.");
    }

    [Fact]
    public void McpServerToolMethods_HaveNoDuplicateNames()
    {
        var names = AttributedToolNames();
        var duplicates = names
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.True(
            duplicates.Length == 0,
            $"Multiple [McpServerTool] methods share the same Name: {string.Join(", ", duplicates)}.");
    }

    [Fact]
    public void EveryMcpServerToolMethod_HasBrokerToolMetadata()
    {
        var missing = typeof(BrokerToolService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Where(method => method.GetCustomAttribute<BrokerToolMetadataAttribute>() is null)
            .Select(method => method.Name)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"[McpServerTool] method(s) with no matching [BrokerToolMetadata]: {string.Join(", ", missing)}.");
    }
}
