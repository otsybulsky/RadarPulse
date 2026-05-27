using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingPersistentDurableEnvelopeLoadResult
{
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

    public RadarProcessingDurableAdapterCompatibilityStatus Status { get; }

    public IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> Records { get; }

    public string Message { get; }

    public bool IsCompatible =>
        Status is RadarProcessingDurableAdapterCompatibilityStatus.Compatible or
            RadarProcessingDurableAdapterCompatibilityStatus.Empty;

    public bool HasRecords => Records.Count > 0;

    public static RadarProcessingPersistentDurableEnvelopeLoadResult Compatible(
        IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> records) =>
        new(RadarProcessingDurableAdapterCompatibilityStatus.Compatible, records);

    public static RadarProcessingPersistentDurableEnvelopeLoadResult Empty(
        string message = "") =>
        new(RadarProcessingDurableAdapterCompatibilityStatus.Empty, message: message);

    public static RadarProcessingPersistentDurableEnvelopeLoadResult Incompatible(
        string message) =>
        new(RadarProcessingDurableAdapterCompatibilityStatus.Incompatible, message: message);

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
