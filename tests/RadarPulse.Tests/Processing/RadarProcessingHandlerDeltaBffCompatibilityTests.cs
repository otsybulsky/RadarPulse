using RadarPulse.Application.Archive;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingHandlerDeltaBffCompatibilityTests
{
    [Fact]
    public async Task BffRunDetailExposesMergedHandlerOutputThroughExistingReadModels()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = CreateCore(universe, new MergeableCountingHandler());
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var runtime = await runner.RunMvpProcessingAsync(
            CreateProducer(
                universe,
                CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100),
                CreateBatch(universe.Version, [0, 1], messageTimestampBase: 200)),
            core,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4));
        var store = new RadarProcessingBffReadModelStore();
        var run = RadarProcessingRunReadModelBuilder.FromCore(
            "merge-run",
            universe,
            core,
            runtime.OverlapResult.Consumer.SessionResult);

        store.Publish(run);

        Assert.True(store.TryGetHandlerOutput("merge-run", sourceId: 0, "events", out var output));
        Assert.Equal(2, output!.Int64Value);
        Assert.True(store.TryGetHandlerOutputContract("merge-run", out var contract));
        var handler = Assert.Single(contract!.Handlers);
        Assert.Equal(RadarSourceProcessingHandlerExecutionClassification.Mergeable, handler.ExecutionClassification);
        Assert.True(store.TryGetDiagnostics("merge-run", out var diagnostics));
        Assert.True(diagnostics!.UsesOrderedHandlerDeltaMerge);
        Assert.False(diagnostics.UsesSequentialHandlerFallback);
        Assert.True(diagnostics.IsReady);
    }

    [Fact]
    public void SnapshotOnlyHandlerOutputReportsSequentialFallbackProvenance()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                handlers: new IRadarSourceProcessingHandler[] { new SnapshotCountingHandler() }));
        var result = core.Process(CreateBatch(universe.Version, [0], messageTimestampBase: 100));

        var run = RadarProcessingRunReadModelBuilder.FromCore(
            "snapshot-run",
            universe,
            core);

        Assert.True(result.IsValid);
        Assert.True(run.HandlerOutputContract.RequiresSequentialFallback);
        Assert.True(run.Diagnostics.UsesSequentialHandlerFallback);
        Assert.False(run.Diagnostics.UsesOrderedHandlerDeltaMerge);
        Assert.True(run.Diagnostics.IsReady);
    }

    [Fact]
    public void UnsupportedHandlerDeltaWorkAppearsAsReadinessDiagnostic()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(universe, new UnsupportedCountingHandler());

        var run = RadarProcessingRunReadModelBuilder.FromCore(
            "blocked-run",
            universe,
            core);

        Assert.True(run.HandlerOutputContract.IsUnsupported);
        Assert.Equal(
            RadarProcessingHandlerOutputProvenance.UnsupportedHandlerSet,
            run.Diagnostics.HandlerOutputProvenance);
        Assert.False(run.Diagnostics.IsReady);
        Assert.Contains("Unsupported handler", run.Diagnostics.BlockingReason, StringComparison.Ordinal);
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

            return new ArchiveRadarEventBatchPublishResult(
                FilePath: "synthetic",
                Decompressor: "synthetic",
                DegreeOfParallelism: 1,
                FileSizeBytes: batches.Sum(static batch => batch.PayloadLength),
                CompressedRecordCount: batches.Length,
                CompressedBytes: batches.Sum(static batch => batch.PayloadLength),
                DecompressedBytes: batches.Sum(static batch => batch.PayloadLength),
                StreamSchemaVersion: StreamSchemaVersion.Current,
                DictionaryVersion: DictionaryVersion.Initial,
                SourceUniverseVersion: universe.Version,
                BatchCount: batches.Length,
                EventCount: batches.Sum(static batch => batch.EventCount),
                PayloadBytes: batches.Sum(static batch => batch.PayloadLength),
                PayloadValueCount: batches.Sum(static batch => batch.PayloadLength),
                RawValueChecksum: 0,
                DictionarySnapshot: new RadarStreamIdentityNormalizer(universe)
                    .CreateDictionarySnapshot(DictionaryVersion.Initial));
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

    private sealed class SnapshotCountingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "snapshot",
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
