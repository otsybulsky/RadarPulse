using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingMvpHandlerDeltaRuntimeTests
{
    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe,
        IRadarSourceProcessingHandler handler) =>
        CreateCore(universe, new[] { handler });

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe,
        IReadOnlyList<IRadarSourceProcessingHandler> handlers) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.AsyncShardTransport,
                partitionCount: Math.Min(2, universe.SourceCount),
                shardCount: Math.Min(2, universe.SourceCount),
                handlers: handlers,
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
}
