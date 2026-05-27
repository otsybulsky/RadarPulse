using System.Text.Json;
using System.Text.Json.Serialization;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class HistoricalArchiveManifestWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task WriteAsync(
        HistoricalArchiveManifest manifest,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
    }
}
