using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingHandlerDeltaPerformanceGateTests
{
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

    private sealed record GateMeasurement(
        RadarProcessingMvpRuntimeResult Runtime,
        RadarProcessingRunReadModel Run,
        TimeSpan Elapsed,
        long AllocatedBytes);
}
