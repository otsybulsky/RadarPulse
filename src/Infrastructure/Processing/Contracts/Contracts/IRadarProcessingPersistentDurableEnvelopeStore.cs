using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public interface IRadarProcessingPersistentDurableEnvelopeStore
{
    string AdapterKind { get; }

    int SchemaVersion { get; }

    string StorageIdentity { get; }

    RadarProcessingPersistentDurableEnvelopeLoadResult Load();

    void Save(
        IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> records);

    RadarProcessingDurableAdapterSummary CreateSummary(
        RadarProcessingDurableQueueSummary? queueSummary = null,
        RadarProcessingDurableAdapterCompatibilityStatus compatibilityStatus =
            RadarProcessingDurableAdapterCompatibilityStatus.Compatible,
        string storageMessage = "");
}
