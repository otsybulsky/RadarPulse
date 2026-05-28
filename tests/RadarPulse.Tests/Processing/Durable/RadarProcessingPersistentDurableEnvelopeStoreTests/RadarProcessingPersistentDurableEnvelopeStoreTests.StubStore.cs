using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingPersistentDurableEnvelopeStoreTests
{
    private sealed class StubPersistentDurableEnvelopeStore : IRadarProcessingPersistentDurableEnvelopeStore
    {
        public string AdapterKind => "stub";

        public int SchemaVersion => RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion;

        public string StorageIdentity => "memory://stub";

        public RadarProcessingPersistentDurableEnvelopeLoadResult Load() =>
            RadarProcessingPersistentDurableEnvelopeLoadResult.Empty();

        public void Save(
            IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> records)
        {
            ArgumentNullException.ThrowIfNull(records);
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
    }
}
