using System.Text.Json;
using System.Text.Json.Serialization;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class HistoricalArchiveCacheMetadataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public string GetMetadataPath(string localPath) => $"{localPath}.metadata.json";

    public bool HasMetadata(string localPath) => File.Exists(GetMetadataPath(localPath));

    public bool Matches(string localPath, HistoricalArchiveFile file)
    {
        if (!File.Exists(localPath) || new FileInfo(localPath).Length != file.SizeBytes)
        {
            return false;
        }

        var metadataPath = GetMetadataPath(localPath);
        if (!File.Exists(metadataPath))
        {
            return true;
        }

        try
        {
            var metadata = JsonSerializer.Deserialize<HistoricalArchiveCacheMetadata>(
                File.ReadAllText(metadataPath),
                JsonOptions);

            return metadata is not null &&
                string.Equals(metadata.ArchivePath, file.ArchivePath, StringComparison.Ordinal) &&
                metadata.SizeBytes == file.SizeBytes &&
                metadata.LastModified == file.LastModified;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public async Task WriteAsync(
        string localPath,
        HistoricalArchiveFile file,
        CancellationToken cancellationToken)
    {
        var metadata = new HistoricalArchiveCacheMetadata(
            file.ArchivePath,
            file.SizeBytes,
            file.LastModified,
            DateTimeOffset.UtcNow);

        var metadataPath = GetMetadataPath(localPath);
        var tempPath = $"{metadataPath}.part";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, metadata, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, metadataPath, overwrite: true);
    }
}
