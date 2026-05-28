using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingPersistentDurableEnvelopeStoreTests
{
    [Fact]
    public void PersistentLoadResultAndAdapterSummaryExposeCompatibility()
    {
        var records = new[]
        {
            CreateRecord("batch-a")
        };
        var compatible = RadarProcessingPersistentDurableEnvelopeLoadResult.Compatible(records);
        var empty = RadarProcessingPersistentDurableEnvelopeLoadResult.Empty("store not found");
        var incompatible = RadarProcessingPersistentDurableEnvelopeLoadResult.Incompatible("schema 2 is unsupported");
        var failed = RadarProcessingPersistentDurableEnvelopeLoadResult.Failed("invalid json");
        var summary = new RadarProcessingDurableAdapterSummary(
            adapterKind: "file",
            schemaVersion: RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion,
            storageIdentity: "durable.json",
            compatibilityStatus: incompatible.Status,
            storageMessage: incompatible.Message,
            queueSummary: new RadarProcessingDurableQueueSummary(acceptedEnvelopeCount: 1));

        Assert.True(compatible.IsCompatible);
        Assert.True(compatible.HasRecords);
        Assert.True(empty.IsCompatible);
        Assert.False(empty.HasRecords);
        Assert.False(incompatible.IsCompatible);
        Assert.False(failed.IsCompatible);
        Assert.Equal(RadarProcessingDurableAdapterCompatibilityStatus.Incompatible, summary.CompatibilityStatus);
        Assert.Equal("file", summary.AdapterKind);
        Assert.Equal("durable.json", summary.StorageIdentity);
        Assert.True(summary.HasStorageMessage);
        Assert.Equal(1, summary.QueueSummary.AcceptedEnvelopeCount);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingDurableAdapterSummary(
                "file",
                1,
                "durable.json",
                (RadarProcessingDurableAdapterCompatibilityStatus)255));
    }

    [Fact]
    public void PersistentStoreContractCanCreateAdapterSummary()
    {
        var store = new StubPersistentDurableEnvelopeStore();
        var summary = store.CreateSummary(
            new RadarProcessingDurableQueueSummary(pendingEnvelopeCount: 1),
            RadarProcessingDurableAdapterCompatibilityStatus.Empty,
            "empty store");

        Assert.Equal("stub", store.AdapterKind);
        Assert.Equal(RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion, store.SchemaVersion);
        Assert.Equal("memory://stub", store.StorageIdentity);
        Assert.Equal(RadarProcessingDurableAdapterCompatibilityStatus.Empty, summary.CompatibilityStatus);
        Assert.Equal(1, summary.QueueSummary.PendingEnvelopeCount);
    }
}
