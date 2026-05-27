using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Persistence boundary for durable processing envelope snapshots.
/// </summary>
/// <remarks>
/// Implementations load and save the production-shaped durable queue evidence
/// used by local recovery workflows. The contract reports adapter identity and
/// compatibility status without prescribing a database or broker backend.
/// </remarks>
public interface IRadarProcessingPersistentDurableEnvelopeStore
{
    /// <summary>
    /// Stable adapter kind reported in durable adapter summaries.
    /// </summary>
    string AdapterKind { get; }

    /// <summary>
    /// Persistent schema version supported by the store implementation.
    /// </summary>
    int SchemaVersion { get; }

    /// <summary>
    /// File path, connection id, or other storage identity for diagnostics.
    /// </summary>
    string StorageIdentity { get; }

    /// <summary>
    /// Loads persisted durable envelope records and reports compatibility.
    /// </summary>
    RadarProcessingPersistentDurableEnvelopeLoadResult Load();

    /// <summary>
    /// Replaces persisted durable envelope records with the supplied current-schema records.
    /// </summary>
    void Save(
        IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> records);

    /// <summary>
    /// Creates an adapter summary using optional queue state and compatibility evidence.
    /// </summary>
    RadarProcessingDurableAdapterSummary CreateSummary(
        RadarProcessingDurableQueueSummary? queueSummary = null,
        RadarProcessingDurableAdapterCompatibilityStatus compatibilityStatus =
            RadarProcessingDurableAdapterCompatibilityStatus.Compatible,
        string storageMessage = "");
}
