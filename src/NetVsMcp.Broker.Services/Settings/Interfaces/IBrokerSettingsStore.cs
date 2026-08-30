using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public interface IBrokerSettingsStore
{
    string FilePath { get; }
    BrokerSettings Load();
    void Update(Func<BrokerSettings, BrokerSettings> mutate);
}
