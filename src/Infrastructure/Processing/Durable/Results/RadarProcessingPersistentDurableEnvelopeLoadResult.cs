using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Result of loading persistent durable envelope records from an adapter.
/// </summary>
/// <remarks>
/// Compatible and empty results can be used by recovery; incompatible and failed
/// results are surfaced as adapter compatibility evidence instead of throwing
/// from product recovery flows.
/// </remarks>
public sealed class RadarProcessingPersistentDurableEnvelopeLoadResult
{
    /// <summary>
    /// Creates a durable envelope load result with immutable record evidence.
    /// </summary>
    public RadarProcessingPersistentDurableEnvelopeLoadResult(
        RadarProcessingDurableAdapterCompatibilityStatus status,
        IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord>? records = null,
        string message = "")
    {
        RadarProcessingDurableAdapterSummary.EnsureKnownCompatibilityStatus(status);
        ArgumentNullException.ThrowIfNull(message);

        Status = status;
        Records = CopyRequired(records ?? Array.Empty<RadarProcessingPersistentDurableEnvelopeRecord>());
        Message = message;
    }

    /// <summary>
    /// Compatibility status reported by the store.
    /// </summary>
    public RadarProcessingDurableAdapterCompatibilityStatus Status { get; }

    /// <summary>
    /// Current-schema records loaded from the store.
    /// </summary>
    public IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> Records { get; }

    /// <summary>
    /// Optional diagnostic message from empty, incompatible, or failed loads.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether recovery may continue with the loaded or empty store.
    /// </summary>
    public bool IsCompatible =>
        Status is RadarProcessingDurableAdapterCompatibilityStatus.Compatible or
            RadarProcessingDurableAdapterCompatibilityStatus.Empty;

    /// <summary>
    /// Indicates whether the compatible load returned any records.
    /// </summary>
    public bool HasRecords => Records.Count > 0;

    /// <summary>
    /// Creates a compatible load result with durable records.
    /// </summary>
    public static RadarProcessingPersistentDurableEnvelopeLoadResult Compatible(
        IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> records) =>
        new(RadarProcessingDurableAdapterCompatibilityStatus.Compatible, records);

    /// <summary>
    /// Creates an empty-store load result.
    /// </summary>
    public static RadarProcessingPersistentDurableEnvelopeLoadResult Empty(
        string message = "") =>
        new(RadarProcessingDurableAdapterCompatibilityStatus.Empty, message: message);

    /// <summary>
    /// Creates an incompatible-schema load result.
    /// </summary>
    public static RadarProcessingPersistentDurableEnvelopeLoadResult Incompatible(
        string message) =>
        new(RadarProcessingDurableAdapterCompatibilityStatus.Incompatible, message: message);

    /// <summary>
    /// Creates a failed-load result for malformed or inaccessible store data.
    /// </summary>
    public static RadarProcessingPersistentDurableEnvelopeLoadResult Failed(
        string message) =>
        new(RadarProcessingDurableAdapterCompatibilityStatus.Failed, message: message);

    private static IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> CopyRequired(
        IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> records)
    {
        if (records.Count == 0)
        {
            return Array.Empty<RadarProcessingPersistentDurableEnvelopeRecord>();
        }

        var copy = new RadarProcessingPersistentDurableEnvelopeRecord[records.Count];
        for (var i = 0; i < records.Count; i++)
        {
            copy[i] = records[i] ?? throw new ArgumentException(
                "Persistent durable envelope records must not contain null entries.",
                nameof(records));
        }

        return Array.AsReadOnly(copy);
    }
}
