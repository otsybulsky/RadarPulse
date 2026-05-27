using System.Text.Json;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingFileDurableEnvelopeStore : IRadarProcessingPersistentDurableEnvelopeStore
{
    public const string Kind = "file";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    private readonly string path;

    public RadarProcessingFileDurableEnvelopeStore(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Persistent durable store path must not be empty.", nameof(path));
        }

        this.path = Path.GetFullPath(path);
    }

    public string AdapterKind => Kind;

    public int SchemaVersion => RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion;

    public string StorageIdentity => path;

    public RadarProcessingPersistentDurableEnvelopeLoadResult Load()
    {
        if (!File.Exists(path))
        {
            return RadarProcessingPersistentDurableEnvelopeLoadResult.Empty(
                $"Persistent durable store '{path}' does not exist.");
        }

        try
        {
            using var stream = File.OpenRead(path);
            var persisted = JsonSerializer.Deserialize<PersistentDurableEnvelopeFile>(stream, JsonOptions);
            if (persisted is null)
            {
                return RadarProcessingPersistentDurableEnvelopeLoadResult.Failed(
                    $"Persistent durable store '{path}' did not contain a readable document.");
            }

            if (persisted.SchemaVersion != SchemaVersion)
            {
                return RadarProcessingPersistentDurableEnvelopeLoadResult.Incompatible(
                    $"Persistent durable store schema {persisted.SchemaVersion} is not supported by schema {SchemaVersion}.");
            }

            var records = persisted.Records ?? Array.Empty<RadarProcessingPersistentDurableEnvelopeRecord>();
            foreach (var record in records)
            {
                if (record is null)
                {
                    return RadarProcessingPersistentDurableEnvelopeLoadResult.Failed(
                        $"Persistent durable store '{path}' contains a null envelope record.");
                }

                if (!record.IsCurrentSchema)
                {
                    return RadarProcessingPersistentDurableEnvelopeLoadResult.Incompatible(
                        $"Persistent durable envelope schema {record.SchemaVersion} is not supported by schema {SchemaVersion}.");
                }
            }

            return records.Length == 0
                ? RadarProcessingPersistentDurableEnvelopeLoadResult.Empty()
                : RadarProcessingPersistentDurableEnvelopeLoadResult.Compatible(Array.AsReadOnly(records));
        }
        catch (JsonException exception)
        {
            return RadarProcessingPersistentDurableEnvelopeLoadResult.Failed(exception.Message);
        }
        catch (IOException exception)
        {
            return RadarProcessingPersistentDurableEnvelopeLoadResult.Failed(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return RadarProcessingPersistentDurableEnvelopeLoadResult.Failed(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return RadarProcessingPersistentDurableEnvelopeLoadResult.Failed(exception.Message);
        }
    }

    public void Save(
        IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var copy = new RadarProcessingPersistentDurableEnvelopeRecord[records.Count];
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i] ?? throw new ArgumentException(
                "Persistent durable envelope records must not contain null entries.",
                nameof(records));
            if (!record.IsCurrentSchema)
            {
                throw new InvalidOperationException(
                    $"Persistent durable envelope schema {record.SchemaVersion} is not supported by schema {SchemaVersion}.");
            }

            copy[i] = record;
        }

        var document = new PersistentDurableEnvelopeFile(SchemaVersion, copy);
        var tempPath = string.Concat(path, ".", Guid.NewGuid().ToString("N"), ".tmp");
        try
        {
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, document, JsonOptions);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public RadarProcessingDurableAdapterSummary CreateSummary(
        RadarProcessingDurableQueueSummary? queueSummary = null,
        RadarProcessingDurableAdapterCompatibilityStatus compatibilityStatus =
            RadarProcessingDurableAdapterCompatibilityStatus.Compatible,
        string storageMessage = "") =>
        new(
            AdapterKind,
            SchemaVersion,
            StorageIdentity,
            compatibilityStatus,
            storageMessage,
            queueSummary);

    private sealed class PersistentDurableEnvelopeFile
    {
        public PersistentDurableEnvelopeFile(
            int schemaVersion,
            RadarProcessingPersistentDurableEnvelopeRecord[]? records)
        {
            SchemaVersion = schemaVersion;
            Records = records ?? Array.Empty<RadarProcessingPersistentDurableEnvelopeRecord>();
        }

        public int SchemaVersion { get; }

        public RadarProcessingPersistentDurableEnvelopeRecord[] Records { get; }
    }
}
