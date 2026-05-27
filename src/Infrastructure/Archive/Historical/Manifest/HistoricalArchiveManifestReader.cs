using System.Text.Json;
using System.Text.Json.Serialization;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class HistoricalArchiveManifestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<HistoricalArchiveManifest> ReadAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<HistoricalArchiveManifest>(
            stream,
            JsonOptions,
            cancellationToken);

        return manifest ?? throw new InvalidOperationException("Manifest file is empty or invalid.");
    }
}
