using System.Text.Json;
using System.Text.Json.Serialization;
using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Product;

/// <summary>
/// Local JSON file product run history store with schema validation and atomic persistence.
/// </summary>
public sealed partial class RadarPulseProductFileRunHistoryStore : IRadarPulseProductRunHistoryStore
{
    /// <summary>
    /// Current persisted product run history schema version.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object sync = new();
    private readonly Dictionary<string, RadarPulseProductRunDetail> runsById = new(StringComparer.Ordinal);
    private readonly List<string> runOrder = new();
    private readonly string storagePath;
    private int rejectedRunCount;
    private string firstBlockingReason = string.Empty;

    /// <summary>
    /// Creates a file-backed history store and loads existing persisted runs when the file is valid.
    /// </summary>
    public RadarPulseProductFileRunHistoryStore(
        string storagePath)
    {
        this.storagePath = ResolveStoragePath(storagePath, out var pathError);
        if (!string.IsNullOrEmpty(pathError))
        {
            firstBlockingReason = pathError;
            rejectedRunCount = 1;
            return;
        }

        Load();
    }

    /// <summary>
    /// Gets the number of loaded product runs.
    /// </summary>
    public int Count
    {
        get
        {
            lock (sync)
            {
                return runsById.Count;
            }
        }
    }

    /// <summary>
    /// Gets current file history readiness, including blocking load or persistence errors.
    /// </summary>
    public RadarPulseProductRunHistoryReadiness Readiness
    {
        get
        {
            lock (sync)
            {
                return new RadarPulseProductRunHistoryReadiness(
                    RadarPulseProductRunHistoryStorageKind.LocalFile,
                    IsReady: string.IsNullOrEmpty(firstBlockingReason),
                    StorageIdentity: storagePath,
                    SchemaVersion: CurrentSchemaVersion,
                    LoadedRunCount: runsById.Count,
                    RejectedRunCount: rejectedRunCount,
                    FirstBlockingReason: firstBlockingReason,
                    Warnings: Array.Empty<string>());
            }
        }
    }
}
