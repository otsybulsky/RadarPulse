using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRunReadModelTests
{
    [Fact]
    public void ReadModelProjectsSourcesAndHandlerOutputValues()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                handlers: new IRadarSourceProcessingHandler[] { new CountingHandler() }));
        var batch = CreateBatch(universe.Version, [0, 1]);

        var result = core.Process(batch);
        var run = RadarProcessingRunReadModelBuilder.FromCore(
            "run-1",
            universe,
            core);

        Assert.True(result.IsValid);
        Assert.Equal("run-1", run.RunId);
        Assert.True(run.HandlerOutputContract.RequiresSequentialFallback);
        Assert.True(run.Diagnostics.IsReady);
        Assert.Equal(2, run.Sources.Count);
        Assert.True(run.TryGetSource(1, out var sourceOne));
        Assert.NotNull(sourceOne);
        Assert.True(sourceOne!.IsActive);
        Assert.Equal(1, sourceOne.ProcessedEventCount);
        Assert.Equal(1, sourceOne.ProcessedPayloadValueCount);
        Assert.Equal(2, sourceOne.RawValueChecksum);
        Assert.Equal(1, sourceOne.Identity.AzimuthBucket);

        var events = Assert.Single(sourceOne.HandlerValues, value => value.Name == "events");
        var checksum = Assert.Single(sourceOne.HandlerValues, value => value.Name == "raw.checksum");
        var scale = Assert.Single(sourceOne.HandlerValues, value => value.Name == "last.scale");
        Assert.Equal(1, events.Int64Value);
        Assert.Equal(2, checksum.Int64Value);
        Assert.Equal(1.0, scale.DoubleValue);
    }

    [Fact]
    public void ReadModelProjectsBatchDetailsInProviderSequence()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(universe);
        var firstBatch = new RadarProcessingQueuedBatch(
            new RadarProcessingQueuedBatchSequence(0),
            CreateBatch(universe.Version, [0]));
        var secondBatch = new RadarProcessingQueuedBatch(
            new RadarProcessingQueuedBatchSequence(1),
            CreateBatch(universe.Version, [1]));

        var firstResult = core.Process(firstBatch.Batch);
        var secondResult = core.Process(secondBatch.Batch);
        var sessionResult = new RadarProcessingQueuedSessionResult(
            RadarProcessingQueuedSessionStatus.Completed,
            enqueueResults:
            [
                RadarProcessingQueuedBatchEnqueueResult.Accepted(firstBatch),
                RadarProcessingQueuedBatchEnqueueResult.Accepted(secondBatch)
            ],
            processingResults:
            [
                RadarProcessingQueuedBatchProcessingResult.Succeeded(firstBatch.Sequence, firstResult),
                RadarProcessingQueuedBatchProcessingResult.Succeeded(secondBatch.Sequence, secondResult)
            ]);

        var run = RadarProcessingRunReadModelBuilder.FromCore(
            "run-2",
            universe,
            core,
            sessionResult);

        Assert.True(run.Diagnostics.ProcessingCompletenessPassed);
        Assert.Equal(2, run.Batches.Count);
        Assert.Equal(0, run.Batches[0].ProviderSequence);
        Assert.Equal(1, run.Batches[1].ProviderSequence);
        Assert.True(run.Batches[0].WasAccepted);
        Assert.Equal(1, run.Batches[0].StreamEventCount);
        Assert.Equal(1, run.Batches[0].PayloadBytes);
        Assert.Equal(1, run.Batches[0].PayloadValueCount);
        Assert.True(run.Batches[0].IsSuccessful);
        Assert.True(run.TryGetBatch(1, out var batchOne));
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.Succeeded, batchOne!.ProcessingStatus);
    }

    [Fact]
    public void DiagnosticsExposeReadinessBlockersAndWarnings()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var readiness = new RadarProcessingDurableRuntimeReadinessSummary(
            new RadarProcessingDurableQueueSummary(
                acceptedEnvelopeCount: 1,
                failedEnvelopeCount: 1,
                oldestUncommittedSequence: new RadarProcessingQueuedBatchSequence(0),
                firstBlockingBatchId: new RadarProcessingDurableBatchId("blocked"),
                firstBlockingSequence: new RadarProcessingQueuedBatchSequence(0),
                firstBlockingState: RadarProcessingDurableEnvelopeState.Failed,
                firstBlockingReason: "validation failed"),
            releaseFailureCount: 1);

        var run = RadarProcessingRunReadModelBuilder.FromCore(
            "run-3",
            universe,
            core,
            readiness: readiness,
            warnings: ["synthetic warning"]);

        Assert.False(run.IsReady);
        Assert.Equal("validation failed", run.Diagnostics.BlockingReason);
        Assert.Equal(1, run.Diagnostics.ReleaseFailureCount);
        Assert.Equal("synthetic warning", Assert.Single(run.Diagnostics.Warnings));
    }

    [Fact]
    public void RunReadModelRejectsUnsortedBatchAndSourceShapes()
    {
        var handlerContract = RadarProcessingHandlerOutputContract.FromHandlers(null);
        var diagnostics = new RadarProcessingRunDiagnosticsReadModel(
            processingCompletenessPassed: true,
            RadarProcessingMetrics.Empty);

        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingRunReadModel(
                "bad-batches",
                handlerContract,
                diagnostics,
                batches:
                [
                    new RadarProcessingBatchReadModel(1, wasAccepted: true),
                    new RadarProcessingBatchReadModel(0, wasAccepted: true)
                ]));

        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingRunReadModel(
                "bad-sources",
                handlerContract,
                diagnostics,
                sources:
                [
                    new RadarProcessingSourceOutputReadModel(
                        new RadarProcessingSourceIdentityReadModel(
                            sourceId: 1,
                            new RadarSourceKey(0, 0, 1, 0)),
                        isActive: false,
                        processedEventCount: 0,
                        processedPayloadValueCount: 0,
                        rawValueChecksum: 0,
                        lastMessageTimestampUtcTicks: 0,
                        processingChecksum: 0)
                ]));
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
        int[] sourceIds)
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
                messageTimestampUtcTicks: 100 + i,
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

    private sealed class CountingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "counting",
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

