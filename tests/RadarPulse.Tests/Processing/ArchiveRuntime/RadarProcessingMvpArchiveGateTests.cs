using RadarPulse.Application.Processing;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingMvpArchiveGateTests
{
    [Fact]
    public async Task ArchiveShapedMvpWorkloadPublishesBffReadyHandlerOutputs()
    {
        var universe = CreateUniverse(sourceCount: 3);
        var core = RadarProcessingRuntimeArchiveBaseline.CreateCore(
            universe,
            partitionCount: 3,
            shardCount: 3,
            handlers: new IRadarSourceProcessingHandler[] { new MvpSummaryHandler() });
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var store = new RadarProcessingBffReadModelStore();

        var runtime = await runner.RunMvpProcessingAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100), cancellationToken);
                publisher.Publish(CreateBatch(universe.Version, [1, 2], messageTimestampBase: 200), cancellationToken);
                return CreatePublishResult(universe, batchCount: 2, eventCount: 4, payloadBytes: 4);
            },
            core,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4),
            new RadarProcessingArchiveQueuedOverlapOptions(
                new RadarProcessingProviderQueueOptions(capacity: 4, recentDetailCapacity: 16)));

        var run = RadarProcessingRunReadModelBuilder.FromCore(
            "mvp-archive-gate",
            universe,
            core,
            runtime.OverlapResult.Consumer.SessionResult,
            warnings: [runtime.Plan.Message],
            queueTelemetry: runtime.OverlapResult.QueueTelemetry);
        store.Publish(run);

        Assert.True(runtime.Plan.UsedSequentialFallback);
        Assert.True(runtime.OverlapResult.IsCompleted);
        Assert.True(run.Diagnostics.ProcessingCompletenessPassed);
        Assert.True(run.Diagnostics.IsReady);
        Assert.Equal(0, run.Diagnostics.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, run.Diagnostics.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(2, run.Batches.Count);
        Assert.All(run.Batches, batch => Assert.True(batch.IsSuccessful));
        Assert.True(store.TryGetLatestRun(out var latest));
        Assert.Equal("mvp-archive-gate", latest!.RunId);
        Assert.True(store.TryGetHandlerOutput("mvp-archive-gate", sourceId: 1, "events", out var events));
        Assert.Equal(2, events!.Int64Value);
        Assert.True(store.TryGetHandlerOutput("mvp-archive-gate", sourceId: 1, "payload.values", out var payloadValues));
        Assert.Equal(2, payloadValues!.Int64Value);
        Assert.True(store.TryGetDiagnostics("mvp-archive-gate", out var diagnostics));
        Assert.True(diagnostics!.IsReady);
        Assert.Contains("sequential fallback", Assert.Single(diagnostics.Warnings), StringComparison.Ordinal);
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
            FilePath: "synthetic-mvp-gate",
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

    private sealed class MvpSummaryHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "mvp.summary",
                int64SlotCount: 3,
                doubleSlotCount: 1,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "payload.values",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 1),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "raw.checksum",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 2),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "last.scale",
                        RadarSourceProcessingSnapshotFieldType.Double,
                        slotIndex: 0)
                });

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
            state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);
            state.AddInt64(slotIndex: 2, context.PayloadMetrics.RawValueChecksum);
            state.SetDouble(slotIndex: 0, context.StreamEvent.Scale);
        }
    }
}

