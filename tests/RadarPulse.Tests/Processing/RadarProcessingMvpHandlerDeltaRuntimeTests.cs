using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingMvpHandlerDeltaRuntimeTests
{
    [Fact]
    public void AllMergeableMvpPlanUsesOrderedDeltaMergeProvenance()
    {
        var core = CreateCore(CreateUniverse(sourceCount: 2), new MergeableCountingHandler());
        var requested = new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4);

        var plan = RadarProcessingMvpRuntimePlan.Create(core, requested);

        Assert.False(plan.UsedSequentialFallback);
        Assert.False(plan.AllowsOrderedConcurrentDelta);
        Assert.True(plan.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.Same(requested, plan.EffectiveOrderedConcurrencyOptions);
        Assert.True(plan.HandlerOutputContract.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.Contains("delta/merge", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MvpProcessingUsesOrderedDeltaMergeForMergeableHandlers()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = CreateCore(universe, new MergeableCountingHandler());
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunMvpProcessingAsync(
            CreateProducer(
                universe,
                CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100),
                CreateBatch(universe.Version, [0, 1], messageTimestampBase: 200)),
            core,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4));

        Assert.False(result.Plan.UsedSequentialFallback);
        Assert.True(result.Plan.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.True(result.OverlapResult.IsCompleted);
        Assert.Equal(2, result.OverlapResult.Consumer.SessionResult.ProcessingResults.Count);
        Assert.All(
            result.OverlapResult.Consumer.SessionResult.ProcessingResults,
            processing => Assert.True(processing.IsSuccessful));
        Assert.Equal(2, core.GetSourceHandlerSnapshot(sourceId: 0).Values[0].Int64Value);
        Assert.Equal(2, core.GetSourceHandlerSnapshot(sourceId: 1).Values[0].Int64Value);
    }

    [Fact]
    public async Task OrderedDeltaMergeOutputMatchesSequentialFallbackOutput()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var mergeCore = CreateCore(universe, new MergeableCountingHandler());
        var sequentialCore = CreateCore(universe, new MergeableCountingHandler());
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var batches = new[]
        {
            CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100),
            CreateBatch(universe.Version, [1], messageTimestampBase: 200),
            CreateBatch(universe.Version, [0], messageTimestampBase: 300)
        };

        await runner.RunMvpProcessingAsync(
            CreateProducer(universe, batches),
            mergeCore,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4));
        await runner.RunMvpProcessingAsync(
            CreateProducer(universe, batches),
            sequentialCore,
            RadarProcessingOrderedConcurrencyOptions.Sequential);

        Assert.Equal(
            sequentialCore.CreateSourceSnapshots(),
            mergeCore.CreateSourceSnapshots());
        Assert.Equal(
            sequentialCore.CreateSourceHandlerSnapshots().Select(static snapshot => snapshot.Values[0].Int64Value),
            mergeCore.CreateSourceHandlerSnapshots().Select(static snapshot => snapshot.Values[0].Int64Value));
        Assert.Equal(sequentialCore.CreateMetrics(), mergeCore.CreateMetrics());
    }

    [Fact]
    public async Task UnsupportedHandlerSetFailsClosedBeforeMvpProcessingStarts()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(universe, new UnsupportedCountingHandler());
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            runner.RunMvpProcessingAsync(
                    CreateProducer(
                        universe,
                        CreateBatch(universe.Version, [0], messageTimestampBase: 100)),
                    core,
                    new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4))
                .AsTask());

        Assert.Contains("Unsupported", exception.Message, StringComparison.Ordinal);
    }

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe,
        IRadarSourceProcessingHandler handler) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.AsyncShardTransport,
                partitionCount: Math.Min(2, universe.SourceCount),
                shardCount: Math.Min(2, universe.SourceCount),
                handlers: new[] { handler },
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 4)));

    private static Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> CreateProducer(
        RadarSourceUniverse universe,
        params RadarEventBatch[] batches) =>
        (publisher, cancellationToken) =>
        {
            foreach (var batch in batches)
            {
                publisher.Publish(batch, cancellationToken);
            }

            return CreatePublishResult(
                universe,
                batchCount: batches.Length,
                eventCount: batches.Sum(static batch => batch.EventCount),
                payloadBytes: batches.Sum(static batch => batch.PayloadLength));
        };

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

    private sealed class MergeableCountingHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata,
        IRadarProcessingHandlerDeltaMerger
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

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
            RadarSourceProcessingHandlerExecutionClassification.Mergeable;

        public string HandlerName => "counting";

        public string HandlerContractVersion => "v1";

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
            IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
            RadarProcessingHandlerDelta delta)
        {
            var values = currentValues.ToDictionary(
                static value => value.SourceId,
                static value => value.Int64Value);
            foreach (var value in delta.Values)
            {
                values[value.SourceId] = values.GetValueOrDefault(value.SourceId) + value.Int64Value;
            }

            return values
                .OrderBy(static pair => pair.Key)
                .Select(static pair => RadarProcessingHandlerDeltaValue.ForInt64(
                    pair.Key,
                    "events",
                    pair.Value))
                .ToArray();
        }
    }

    private sealed class UnsupportedCountingHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "unsupported",
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0)
                });

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
            RadarSourceProcessingHandlerExecutionClassification.Unsupported;

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }
    }
}
