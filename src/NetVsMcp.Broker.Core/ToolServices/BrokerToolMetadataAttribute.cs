using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
internal sealed class BrokerToolMetadataAttribute : Attribute
{
    public BrokerToolMetadataAttribute(
        BrokerToolCategory category,
        bool requiresVisualStudioSession = true)
    {
        Category = category;
        RequiresVisualStudioSession = requiresVisualStudioSession;
    }

    public BrokerToolCategory Category { get; }

    public bool RequiresVisualStudioSession { get; }
}
