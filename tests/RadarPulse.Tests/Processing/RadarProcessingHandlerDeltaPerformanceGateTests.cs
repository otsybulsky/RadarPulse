using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingHandlerDeltaPerformanceGateTests
{
    [Fact]
    public async Task HandlerHeavyOrderedDeltaMergeGateMatchesSequentialFallbackAndCapturesEvidence()
    {
        const int sourceCount = 8;
        const int batchCount = 24;
        const int eventsPerBatch = 64;
        const int payloadBytesPerEvent = 4;

        var universe = CreateUniverse(sourceCount);
        var batches = Enumerable.Range(0, batchCount)
            .Select(index => CreateBatch(
                universe.Version,
                sourceCount,
                eventsPerBatch,
                payloadBytesPerEvent,
                messageTimestampBase: 10_000L * index))
            .ToArray();
        var mergeCore = CreateCore(universe, new HandlerHeavySummaryHandler());
        var sequentialCore = CreateCore(universe, new HandlerHeavySummaryHandler());
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 8, recentDetailCapacity: 32));

        var merge = await MeasureAsync(
            runner,
            universe,
            mergeCore,
            batches,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4),
            options);
        var sequential = await MeasureAsync(
            runner,
            universe,
            sequentialCore,
            batches,
            RadarProcessingOrderedConcurrencyOptions.Sequential,
            options);

        Assert.True(merge.Runtime.Plan.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.False(sequential.Runtime.Plan.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.True(merge.Runtime.OverlapResult.IsCompleted);
        Assert.True(sequential.Runtime.OverlapResult.IsCompleted);
        Assert.Equal(
            sequentialCore.CreateSourceSnapshots(),
            mergeCore.CreateSourceSnapshots());
        Assert.Equal(
            sequentialCore.CreateSourceHandlerSnapshots().SelectMany(static snapshot => snapshot.Values),
            mergeCore.CreateSourceHandlerSnapshots().SelectMany(static snapshot => snapshot.Values));
        Assert.Equal(sequentialCore.CreateMetrics(), mergeCore.CreateMetrics());
        Assert.True(merge.Elapsed > TimeSpan.Zero);
        Assert.True(sequential.Elapsed > TimeSpan.Zero);
        Assert.True(merge.AllocatedBytes >= 0);
        Assert.True(sequential.AllocatedBytes >= 0);
        Assert.True(merge.Run.Diagnostics.UsesOrderedHandlerDeltaMerge);
        Assert.True(merge.Run.Diagnostics.IsReady);
        Assert.Equal(0, merge.Run.Diagnostics.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, merge.Run.Diagnostics.CurrentCombinedRetainedPayloadBytes);
        Assert.All(merge.Run.Batches, static batch => Assert.True(batch.IsSuccessful));
    }

    private static async Task<GateMeasurement> MeasureAsync(
        RadarProcessingArchiveQueuedOverlapRunner runner,
        RadarSourceUniverse universe,
        RadarProcessingCore core,
        IReadOnlyList<RadarEventBatch> batches,
        RadarProcessingOrderedConcurrencyOptions orderedConcurrencyOptions,
        RadarProcessingArchiveQueuedOverlapOptions options)
    {
        var before = RadarProcessingBenchmarkAllocationSnapshot.Capture();
        var started = Stopwatch.GetTimestamp();
        var runtime = await runner.RunMvpProcessingAsync(
            CreateProducer(universe, batches),
            core,
            orderedConcurrencyOptions,
            options);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(before);
        var run = RadarProcessingRunReadModelBuilder.FromCore(
            orderedConcurrencyOptions.IsSequential ? "sequential" : "merge",
            universe,
            core,
            runtime.OverlapResult.Consumer.SessionResult,
            warnings: [runtime.Plan.Message],
            queueTelemetry: runtime.OverlapResult.QueueTelemetry);
        return new GateMeasurement(runtime, run, elapsed, allocatedBytes);
    }

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe,
        IRadarSourceProcessingHandler handler) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.AsyncShardTransport,
                partitionCount: SourceCountFor(universe),
                shardCount: Math.Min(4, SourceCountFor(universe)),
                handlers: new[] { handler },
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 4, queueCapacity: 8)));

    private static int SourceCountFor(RadarSourceUniverse universe) =>
        universe.SourceCount;

    private static Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> CreateProducer(
        RadarSourceUniverse universe,
        IReadOnlyList<RadarEventBatch> batches) =>
        (publisher, cancellationToken) =>
        {
            foreach (var batch in batches)
            {
                publisher.Publish(batch, cancellationToken);
            }

            var payloadBytes = batches.Sum(static batch => batch.PayloadLength);
            return new ArchiveRadarEventBatchPublishResult(
                FilePath: "handler-heavy-synthetic",
                Decompressor: "synthetic",
                DegreeOfParallelism: 1,
                FileSizeBytes: payloadBytes,
                CompressedRecordCount: batches.Count,
                CompressedBytes: payloadBytes,
                DecompressedBytes: payloadBytes,
                StreamSchemaVersion: StreamSchemaVersion.Current,
                DictionaryVersion: DictionaryVersion.Initial,
                SourceUniverseVersion: universe.Version,
                BatchCount: batches.Count,
                EventCount: batches.Sum(static batch => batch.EventCount),
                PayloadBytes: payloadBytes,
                PayloadValueCount: payloadBytes,
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
        int sourceCount,
        int eventCount,
        int payloadBytesPerEvent,
        long messageTimestampBase)
    {
        var builder = new RadarEventBatchBuilder(
            initialEventCapacity: eventCount,
            initialPayloadCapacity: checked(eventCount * payloadBytesPerEvent));
        for (var i = 0; i < eventCount; i++)
        {
            var sourceId = i % sourceCount;
            var payload = Enumerable.Range(0, payloadBytesPerEvent)
                .Select(offset => (byte)((i + offset) & 0xff))
                .ToArray();
            builder.AddEvent(
                new RadarStreamIdentity(
                    sourceId,
                    radarOrdinal: 0,
                    momentId: 0,
                    elevationSlot: 0,
                    azimuthBucket: (ushort)sourceId,
                    rangeBand: 0,
                    dictionaryVersion: DictionaryVersion.Initial,
                    sourceUniverseVersion: sourceUniverseVersion),
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: messageTimestampBase + i,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                gateStart: 0,
                gateCount: checked((ushort)payloadBytesPerEvent),
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f + (i % 3),
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payload);
        }

        return builder.Build();
    }

    private sealed class HandlerHeavySummaryHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata,
        IRadarProcessingHandlerDeltaMerger
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "handler.heavy",
                int64SlotCount: 3,
                doubleSlotCount: 0,
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
                        "handler.work",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 2)
                });

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
            RadarSourceProcessingHandlerExecutionClassification.Mergeable;

        public string HandlerName => "handler.heavy";

        public string HandlerContractVersion => "v1";

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            var work = 0L;
            for (var repeat = 0; repeat < 96; repeat++)
            {
                for (var i = 0; i < context.Payload.Length; i++)
                {
                    work = checked(work + context.Payload[i] + repeat);
                }
            }

            state.AddInt64(slotIndex: 0, value: 1);
            state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);
            state.AddInt64(slotIndex: 2, work);
        }

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
            IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
            RadarProcessingHandlerDelta delta)
        {
            var values = currentValues.ToDictionary(
                static value => (value.SourceId, value.FieldName),
                static value => value.Int64Value);
            foreach (var value in delta.Values)
            {
                var key = (value.SourceId, value.FieldName);
                values[key] = values.GetValueOrDefault(key) + value.Int64Value;
            }

            return values
                .OrderBy(static pair => pair.Key.SourceId)
                .ThenBy(static pair => pair.Key.FieldName, StringComparer.Ordinal)
                .Select(static pair => RadarProcessingHandlerDeltaValue.ForInt64(
                    pair.Key.SourceId,
                    pair.Key.FieldName,
                    pair.Value))
                .ToArray();
        }
    }

    private sealed record GateMeasurement(
        RadarProcessingMvpRuntimeResult Runtime,
        RadarProcessingRunReadModel Run,
        TimeSpan Elapsed,
        long AllocatedBytes);
}
