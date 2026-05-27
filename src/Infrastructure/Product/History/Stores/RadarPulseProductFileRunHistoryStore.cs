using System.Text.Json;
using System.Text.Json.Serialization;
using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Product;

/// <summary>
/// Local JSON file product run history store with schema validation and atomic persistence.
/// </summary>
public sealed class RadarPulseProductFileRunHistoryStore :
    IRadarPulseProductRunHistoryStore
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

    /// <summary>
    /// Lists loaded run summaries in persisted insertion order.
    /// </summary>
    public IReadOnlyList<RadarPulseProductRunSummary> ListRuns()
    {
        lock (sync)
        {
            if (runOrder.Count == 0)
            {
                return Array.Empty<RadarPulseProductRunSummary>();
            }

            return Array.AsReadOnly(
                runOrder
                    .Select(runId => runsById[runId].Summary)
                    .ToArray());
        }
    }

    /// <summary>
    /// Gets one loaded run detail by run id.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetRun(
        string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        lock (sync)
        {
            return runsById.TryGetValue(runId, out var detail)
                ? RadarPulseProductQueryResult<RadarPulseProductRunDetail>.FromValue(detail)
                : RadarPulseProductQueryResult<RadarPulseProductRunDetail>.NotFound(
                    $"Product run '{runId}' was not found.");
        }
    }

    /// <summary>
    /// Gets the most recently loaded or stored run detail.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetLatestRun()
    {
        lock (sync)
        {
            if (runOrder.Count == 0)
            {
                return RadarPulseProductQueryResult<RadarPulseProductRunDetail>.NotFound(
                    "No product pipeline run has been published.");
            }

            return RadarPulseProductQueryResult<RadarPulseProductRunDetail>.FromValue(
                runsById[runOrder[^1]]);
        }
    }

    /// <summary>
    /// Persists a new product run detail unless a conflicting run id already exists.
    /// </summary>
    public void Store(
        RadarPulseProductRunDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail.RunId);

        lock (sync)
        {
            ThrowIfBlocked();
            if (runsById.TryGetValue(detail.RunId, out var existing))
            {
                if (AreSameRecord(existing, detail))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Product run history already contains conflicting run '{detail.RunId}'.");
            }

            runsById.Add(detail.RunId, detail);
            runOrder.Add(detail.RunId);
            try
            {
                Persist();
            }
            catch
            {
                runsById.Remove(detail.RunId);
                runOrder.RemoveAt(runOrder.Count - 1);
                throw;
            }
        }
    }

    private void Load()
    {
        lock (sync)
        {
            if (!File.Exists(storagePath))
            {
                CreateParentDirectory();
                return;
            }

            try
            {
                var json = File.ReadAllText(storagePath);
                var file = JsonSerializer.Deserialize<PersistedHistoryFile>(json, JsonOptions);
                if (file is null)
                {
                    Block("Product run history file is empty or invalid.", rejectedCount: 1);
                    return;
                }

                if (file.SchemaVersion != CurrentSchemaVersion)
                {
                    Block(
                        $"Product run history schema version {file.SchemaVersion} is not supported.",
                        rejectedCount: Math.Max(file.Runs?.Count ?? 1, 1));
                    return;
                }

                if (file.Runs is null)
                {
                    Block("Product run history file does not contain a runs collection.", rejectedCount: 1);
                    return;
                }

                foreach (var detail in file.Runs)
                {
                    if (detail is null || string.IsNullOrWhiteSpace(detail.RunId))
                    {
                        Block("Product run history contains an invalid run record.", rejectedCount: 1);
                        return;
                    }

                    if (runsById.TryGetValue(detail.RunId, out var existing))
                    {
                        if (AreSameRecord(existing, detail))
                        {
                            continue;
                        }

                        Block(
                            $"Product run history contains conflicting duplicate run '{detail.RunId}'.",
                            rejectedCount: 1);
                        return;
                    }

                    runsById.Add(detail.RunId, detail);
                    runOrder.Add(detail.RunId);
                }
            }
            catch (JsonException exception)
            {
                Block(
                    $"Product run history JSON is invalid: {exception.Message}",
                    rejectedCount: 1);
            }
            catch (IOException exception)
            {
                Block(
                    $"Product run history could not be loaded: {exception.Message}",
                    rejectedCount: 1);
            }
            catch (UnauthorizedAccessException exception)
            {
                Block(
                    $"Product run history could not be accessed: {exception.Message}",
                    rejectedCount: 1);
            }
        }
    }

    private void Persist()
    {
        ThrowIfBlocked();
        CreateParentDirectory();
        var file = new PersistedHistoryFile(
            CurrentSchemaVersion,
            runOrder.Select(runId => runsById[runId]).ToArray());
        var json = JsonSerializer.Serialize(file, JsonOptions);
        var temporaryPath = string.Concat(
            storagePath,
            ".",
            Guid.NewGuid().ToString("N"),
            ".tmp");

        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, storagePath, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            firstBlockingReason =
                $"Product run history could not be persisted: {exception.Message}";
            throw new InvalidOperationException(firstBlockingReason, exception);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void CreateParentDirectory()
    {
        var directory = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void ThrowIfBlocked()
    {
        if (!string.IsNullOrEmpty(firstBlockingReason))
        {
            throw new InvalidOperationException(firstBlockingReason);
        }
    }

    private void Block(
        string reason,
        int rejectedCount)
    {
        firstBlockingReason = reason;
        rejectedRunCount = rejectedCount;
        runsById.Clear();
        runOrder.Clear();
    }

    private static bool AreSameRecord(
        RadarPulseProductRunDetail left,
        RadarPulseProductRunDetail right) =>
        string.Equals(
            JsonSerializer.Serialize(left, JsonOptions),
            JsonSerializer.Serialize(right, JsonOptions),
            StringComparison.Ordinal);

    private static string ResolveStoragePath(
        string storagePath,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            error = "Product run history path is required.";
            return string.Empty;
        }

        try
        {
            var fullPath = Path.GetFullPath(storagePath);
            if (Directory.Exists(fullPath))
            {
                error = "Product run history path must be a file path, not a directory.";
                return fullPath;
            }

            error = string.Empty;
            return fullPath;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Product run history path is invalid: {exception.Message}";
            return storagePath;
        }
    }

    private sealed record PersistedHistoryFile(
        int SchemaVersion,
        IReadOnlyList<RadarPulseProductRunDetail> Runs);
}
