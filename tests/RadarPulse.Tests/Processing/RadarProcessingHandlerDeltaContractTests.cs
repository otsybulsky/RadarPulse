using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingHandlerDeltaContractTests
{
    [Fact]
    public void ValidDeltaCarriesDeterministicIdentityAndBatchMetadata()
    {
        var durableBatchId = new RadarProcessingDurableBatchId("batch-1");
        var delta = CreateDelta(durableBatchId: durableBatchId);

        Assert.Equal("analytics", delta.HandlerName);
        Assert.Equal("v1", delta.HandlerContractVersion);
        Assert.Equal(new RadarProcessingQueuedBatchSequence(3), delta.ProviderSequence);
        Assert.Equal(durableBatchId, delta.DurableBatchId);
        Assert.Equal(12, delta.EventCount);
        Assert.Equal(2, delta.SourceCount);
        Assert.Equal(64, delta.PayloadValueCount);
        Assert.Equal(1234, delta.InputChecksum);
        Assert.Equal(RadarProcessingHandlerDelta.CurrentSchemaVersion, delta.SchemaVersion);
        Assert.Equal(
            RadarProcessingHandlerDelta.CreateId(
                "analytics",
                "v1",
                new RadarProcessingQueuedBatchSequence(3),
                durableBatchId,
                eventCount: 12,
                sourceCount: 2,
                payloadValueCount: 64,
                inputChecksum: 1234),
            delta.DeltaId);
        Assert.Equal(2, delta.Values.Count);
    }

    [Fact]
    public void DeltaRejectsInvalidIdentityCountersAndDuplicateValues()
    {
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingHandlerDelta.Create(
                "",
                "v1",
                RadarProcessingQueuedBatchSequence.Initial,
                durableBatchId: null,
                eventCount: 1,
                sourceCount: 1,
                payloadValueCount: 1,
                inputChecksum: 1));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingHandlerDelta.Create(
                "analytics",
                "v1",
                RadarProcessingQueuedBatchSequence.Initial,
                durableBatchId: null,
                eventCount: -1,
                sourceCount: 1,
                payloadValueCount: 1,
                inputChecksum: 1));

        Assert.Throws<ArgumentException>(() =>
            RadarProcessingHandlerDelta.Create(
                "analytics",
                "v1",
                RadarProcessingQueuedBatchSequence.Initial,
                durableBatchId: null,
                eventCount: 1,
                sourceCount: 1,
                payloadValueCount: 1,
                inputChecksum: 1,
                values:
                [
                    RadarProcessingHandlerDeltaValue.ForInt64(0, "events", 1),
                    RadarProcessingHandlerDeltaValue.ForInt64(0, "events", 2)
                ]));
    }

    [Fact]
    public void DeltaRejectsMismatchedId()
    {
        var expected = RadarProcessingHandlerDelta.CreateId(
            "analytics",
            "v1",
            RadarProcessingQueuedBatchSequence.Initial,
            durableBatchId: null,
            eventCount: 1,
            sourceCount: 1,
            payloadValueCount: 1,
            inputChecksum: 1);

        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingHandlerDelta(
                "analytics",
                "v2",
                RadarProcessingQueuedBatchSequence.Initial,
                durableBatchId: null,
                eventCount: 1,
                sourceCount: 1,
                payloadValueCount: 1,
                inputChecksum: 1,
                expected));
    }

    [Fact]
    public void SerializationRoundtripPreservesIdempotencyKeyAndPayloadValues()
    {
        var delta = CreateDelta();

        var serialized = RadarProcessingHandlerDeltaSerializer.Serialize(delta);
        var result = RadarProcessingHandlerDeltaSerializer.Deserialize(serialized);

        Assert.True(result.IsSuccessful);
        Assert.Equal(string.Empty, result.Diagnostic);
        Assert.Equal(delta.DeltaId, result.Delta!.DeltaId);
        Assert.Equal(delta.HandlerName, result.Delta.HandlerName);
        Assert.Equal(delta.ProviderSequence, result.Delta.ProviderSequence);
        Assert.Equal(delta.Values, result.Delta.Values);
    }

    [Fact]
    public void UnsupportedSerializedVersionFailsClosedWithDiagnostic()
    {
        var delta = CreateDelta();
        var serialized = new RadarProcessingSerializedHandlerDelta(
            schemaVersion: RadarProcessingHandlerDelta.CurrentSchemaVersion + 1,
            delta.HandlerName,
            delta.HandlerContractVersion,
            delta.ProviderSequence,
            delta.DurableBatchId,
            delta.EventCount,
            delta.SourceCount,
            delta.PayloadValueCount,
            delta.InputChecksum,
            delta.DeltaId,
            delta.Values);

        var result = RadarProcessingHandlerDeltaSerializer.Deserialize(serialized);

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Delta);
        Assert.Contains("Unsupported handler delta schema version", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void RetryingDeltaComputeForSameHandlerAndBatchProducesEquivalentIdentity()
    {
        var first = CreateDelta();
        var retry = CreateDelta();

        Assert.Equal(first.DeltaId, retry.DeltaId);
        Assert.Equal(first.Values, retry.Values);
    }

    private static RadarProcessingHandlerDelta CreateDelta(
        RadarProcessingDurableBatchId? durableBatchId = null) =>
        RadarProcessingHandlerDelta.Create(
            "analytics",
            "v1",
            new RadarProcessingQueuedBatchSequence(3),
            durableBatchId,
            eventCount: 12,
            sourceCount: 2,
            payloadValueCount: 64,
            inputChecksum: 1234,
            values:
            [
                RadarProcessingHandlerDeltaValue.ForInt64(0, "events", 7),
                RadarProcessingHandlerDeltaValue.ForDouble(1, "max.scale", 1.5)
            ]);
}
