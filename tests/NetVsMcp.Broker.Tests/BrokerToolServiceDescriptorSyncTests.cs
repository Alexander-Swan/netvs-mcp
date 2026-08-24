using System.Reflection;
using ModelContextProtocol.Server;
using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

/// <summary>
/// <c>BrokerToolService.ToolDescriptors</c> is a hand-maintained array that duplicates each tool's
/// name separately from the <c>[McpServerTool]</c> attribute on the actual method.
/// Nothing in the compiler enforces the two stay in sync, so a renamed or newly added tool
/// method that isn't mirrored in <c>ToolDescriptors</c> would silently produce a wrong or
/// missing entry in <c>get_help</c>/<c>vs_get_capabilities</c> while still being callable.
/// These tests catch that drift in both directions via reflection.
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
            "Add an entry to BrokerToolService.ToolDescriptors so get_help/vs_get_capabilities list it correctly.");
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
}
