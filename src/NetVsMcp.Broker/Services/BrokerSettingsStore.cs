using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetVsMcp.Broker.Services;

/// <summary>Persisted, user-configurable broker settings that override the compiled-in defaults.</summary>
public sealed record BrokerSettings(
    int? Port = null,
    string? LogsDirectory = null,
    string? SessionsDirectory = null);

public interface IBrokerSettingsStore
{
    string FilePath { get; }
    BrokerSettings Load();
    void Update(Func<BrokerSettings, BrokerSettings> mutate);
}

public sealed class BrokerSettingsStore : IBrokerSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();

    public BrokerSettingsStore(string filePath)
    {
        FilePath = string.IsNullOrWhiteSpace(filePath)
            ? BrokerOptions.DefaultSettingsFilePath
            : filePath;
    }

    public string FilePath { get; }

    public BrokerSettings Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return new BrokerSettings();
                }

                return JsonSerializer.Deserialize<BrokerSettings>(File.ReadAllText(FilePath), SerializerOptions)
                    ?? new BrokerSettings();
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

            return new BrokerSettings();
        }
    }

    public void Update(Func<BrokerSettings, BrokerSettings> mutate)
    {
        lock (_gate)
        {
            var updated = mutate(Load());

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(updated, SerializerOptions));
        }
    }
}
