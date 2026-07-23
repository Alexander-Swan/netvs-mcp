using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public static class BrokerToolAccessPolicy
{
    public static bool IsAllowed(BrokerCapabilityProfile activeProfile, BrokerToolCategory category)
    {
        return activeProfile switch
        {
            BrokerCapabilityProfile.Admin => true,
            BrokerCapabilityProfile.Debug => category is not BrokerToolCategory.Admin,
            BrokerCapabilityProfile.EditDirect => category is
                BrokerToolCategory.Broker or
                BrokerToolCategory.Read or
                BrokerToolCategory.EditPreview or
                BrokerToolCategory.EditDirect,
            BrokerCapabilityProfile.EditPreview => category is
                BrokerToolCategory.Broker or
                BrokerToolCategory.Read or
                BrokerToolCategory.EditPreview,
            BrokerCapabilityProfile.ReadOnly => category is
                BrokerToolCategory.Broker or
                BrokerToolCategory.Read,
            _ => category is BrokerToolCategory.Broker or BrokerToolCategory.Read
        };
    }

    public static BrokerCapabilityProfile MinimumProfile(BrokerToolCategory category)
    {
        return category switch
        {
            BrokerToolCategory.Broker => BrokerCapabilityProfile.ReadOnly,
            BrokerToolCategory.Read => BrokerCapabilityProfile.ReadOnly,
            BrokerToolCategory.EditPreview => BrokerCapabilityProfile.EditPreview,
            BrokerToolCategory.EditDirect => BrokerCapabilityProfile.EditDirect,
            BrokerToolCategory.Build => BrokerCapabilityProfile.Debug,
            BrokerToolCategory.Debug => BrokerCapabilityProfile.Debug,
            BrokerToolCategory.Project => BrokerCapabilityProfile.Admin,
            BrokerToolCategory.Test => BrokerCapabilityProfile.Debug,
            BrokerToolCategory.Admin => BrokerCapabilityProfile.Admin,
            _ => BrokerCapabilityProfile.Admin
        };
    }
}
