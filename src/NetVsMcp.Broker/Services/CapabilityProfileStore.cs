using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public interface ICapabilityProfileStore
{
    string FilePath { get; }
    BrokerCapabilityProfile Load(BrokerCapabilityProfile fallback);
    void Save(BrokerCapabilityProfile profile);
}

public sealed class CapabilityProfileStore : ICapabilityProfileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();

    public CapabilityProfileStore(string filePath)
    {
        FilePath = string.IsNullOrWhiteSpace(filePath)
            ? BrokerOptions.DefaultCapabilityProfileFilePath
            : filePath;
    }

    public string FilePath { get; }

    public BrokerCapabilityProfile Load(BrokerCapabilityProfile fallback)
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return fallback;
                }

                var record = JsonSerializer.Deserialize<CapabilityProfileRecord>(
                    File.ReadAllText(FilePath),
                    SerializerOptions);

                return record is null ? fallback : record.CapabilityProfile;
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return fallback;
        }
    }

    public void Save(BrokerCapabilityProfile profile)
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(new CapabilityProfileRecord(profile), SerializerOptions));
        }
    }

    private sealed record CapabilityProfileRecord(BrokerCapabilityProfile CapabilityProfile);
}
