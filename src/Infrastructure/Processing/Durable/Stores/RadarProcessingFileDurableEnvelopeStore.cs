using System.Text.Json;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// JSON-file implementation of the durable envelope persistence boundary.
/// </summary>
/// <remarks>
/// The store is the accepted local/demo durable adapter. Saves write a temporary
/// file and move it over the target path so readers do not observe partially
/// serialized durable envelope documents.
/// </remarks>
public sealed class RadarProcessingFileDurableEnvelopeStore : IRadarProcessingPersistentDurableEnvelopeStore
{
    /// <summary>
    /// Adapter kind reported for file-backed durable persistence.
    /// </summary>
    public const string Kind = "file";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    private readonly string path;

    /// <summary>
    /// Creates a file durable envelope store rooted at the supplied path.
    /// </summary>
    public RadarProcessingFileDurableEnvelopeStore(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Persistent durable store path must not be empty.", nameof(path));
        }

        this.path = Path.GetFullPath(path);
    }

    /// <summary>
    /// Stable adapter kind for file-backed persistence.
    /// </summary>
    public string AdapterKind => Kind;

    /// <summary>
    /// Current durable envelope persistence schema version.
    /// </summary>
    public int SchemaVersion => RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion;

    /// <summary>
    /// Absolute file path used by this store.
    /// </summary>
    public string StorageIdentity => path;

    /// <summary>
    /// Loads durable envelopes from the JSON file and reports empty, incompatible, or failed states.
    /// </summary>
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

    /// <summary>
    /// Atomically replaces the JSON file with the supplied current-schema records.
    /// </summary>
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

    /// <summary>
    /// Creates a durable adapter summary for product and operator diagnostics.
    /// </summary>
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
