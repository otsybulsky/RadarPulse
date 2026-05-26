using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingMvpRuntimePlanTests
{
    [Fact]
    public void HandlerFreeCoreKeepsRequestedOrderedConcurrency()
    {
        var core = new RadarProcessingCore(
            CreateUniverse(sourceCount: 4),
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 4,
                shardCount: 2));
        var requested = new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 3);

        var plan = RadarProcessingMvpRuntimePlan.Create(core, requested);

        Assert.False(plan.UsedSequentialFallback);
        Assert.True(plan.AllowsOrderedConcurrentDelta);
        Assert.Same(requested, plan.EffectiveOrderedConcurrencyOptions);
        Assert.Equal(3, plan.EffectiveOrderedConcurrencyOptions.ActiveBatchCapacity);
        Assert.False(plan.HandlerOutputContract.HasHandlers);
    }

    [Fact]
    public void StatefulHandlerCoreUsesSequentialFallback()
    {
        var core = new RadarProcessingCore(
            CreateUniverse(sourceCount: 4),
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 4,
                shardCount: 2,
                handlers: new IRadarSourceProcessingHandler[] { new CountingHandler() }));
        var requested = new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4);

        var plan = RadarProcessingMvpRuntimePlan.Create(core, requested);

        Assert.True(plan.UsedSequentialFallback);
        Assert.False(plan.AllowsOrderedConcurrentDelta);
        Assert.Same(requested, plan.RequestedOrderedConcurrencyOptions);
        Assert.True(plan.EffectiveOrderedConcurrencyOptions.IsSequential);
        Assert.True(plan.HandlerOutputContract.RequiresSequentialFallback);
        Assert.Contains("sequential fallback", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MvpProcessingUsesSequentialFallbackForStatefulHandlers()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.AsyncShardTransport,
                partitionCount: 2,
                shardCount: 2,
                handlers: new IRadarSourceProcessingHandler[] { new CountingHandler() },
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 4)));
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunMvpProcessingAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100), cancellationToken);
                publisher.Publish(CreateBatch(universe.Version, [0, 1], messageTimestampBase: 200), cancellationToken);
                return CreatePublishResult(universe, batchCount: 2, eventCount: 4, payloadBytes: 4);
            },
            core,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4));

        Assert.True(result.Plan.UsedSequentialFallback);
        Assert.True(result.OverlapResult.IsCompleted);
        Assert.Equal(2, result.OverlapResult.Consumer.SessionResult.ProcessingResults.Count);
        Assert.All(
            result.OverlapResult.Consumer.SessionResult.ProcessingResults,
            processing => Assert.True(processing.IsSuccessful));
        Assert.Equal(2, core.GetSourceHandlerSnapshot(sourceId: 0).Values[0].Int64Value);
        Assert.Equal(2, core.GetSourceHandlerSnapshot(sourceId: 1).Values[0].Int64Value);
    }

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds,
        long messageTimestampBase)
    {
        var builder = new RadarEventBatchBuilder(
            initialEventCapacity: sourceIds.Length,
            initialPayloadCapacity: sourceIds.Length);
        for (var i = 0; i < sourceIds.Length; i++)
        {
            builder.AddEvent(
                new RadarStreamIdentity(
                    sourceIds[i],
                    radarOrdinal: 0,
                    momentId: 0,
                    elevationSlot: 0,
                    azimuthBucket: (ushort)sourceIds[i],
                    rangeBand: 0,
                    dictionaryVersion: DictionaryVersion.Initial,
                    sourceUniverseVersion: sourceUniverseVersion),
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: messageTimestampBase + i,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payload: new byte[] { (byte)(i + 1) });
        }

        return builder.Build();
    }

    private static ArchiveRadarEventBatchPublishResult CreatePublishResult(
        RadarSourceUniverse universe,
        long batchCount,
        long eventCount,
        long payloadBytes) =>
        new(
            FilePath: "synthetic",
            Decompressor: "synthetic",
            DegreeOfParallelism: 1,
            FileSizeBytes: payloadBytes,
            CompressedRecordCount: checked((int)batchCount),
            CompressedBytes: payloadBytes,
            DecompressedBytes: payloadBytes,
            StreamSchemaVersion: StreamSchemaVersion.Current,
            DictionaryVersion: DictionaryVersion.Initial,
            SourceUniverseVersion: universe.Version,
            BatchCount: batchCount,
            EventCount: eventCount,
            PayloadBytes: payloadBytes,
            PayloadValueCount: payloadBytes,
            RawValueChecksum: 0,
            DictionarySnapshot: new RadarStreamIdentityNormalizer(universe)
                .CreateDictionarySnapshot(DictionaryVersion.Initial));

    private sealed class CountingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "counting",
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0)
                });

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }
    }
}

