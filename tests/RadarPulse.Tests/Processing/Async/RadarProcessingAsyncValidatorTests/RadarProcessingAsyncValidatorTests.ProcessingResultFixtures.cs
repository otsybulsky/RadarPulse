using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncValidatorTests
{
    private static RadarProcessingResult CreateAsyncProcessingResult(
        RadarProcessingBatchRoute route,
        RadarProcessingTopologyVersion? workerTelemetryTopologyVersion = null)
    {
        var metrics = new RadarProcessingMetrics(
            ProcessedBatchCount: 1,
            ProcessedStreamEventCount: 0,
            ProcessedPayloadValueCount: 0,
            ActiveSourceCount: 0,
            RawValueChecksum: 0,
            ProcessingChecksum: 0);
        var workerTelemetry = new RadarProcessingWorkerTelemetrySummary(
            new RadarProcessingWorkerTelemetryCounters(
                dispatchedBatchCount: 1,
                completedBatchCount: 1,
                submittedWorkItemCount: route.ShardCount,
                acceptedWorkItemCount: route.ShardCount,
                completedWorkItemCount: route.ShardCount,
                succeededWorkItemCount: route.ShardCount),
            workerCount: route.ShardCount,
            queueCapacity: 1,
            new[]
            {
                new RadarProcessingRecentWorkerBatch(
                    batchSequence: 1,
                    topologyVersion: workerTelemetryTopologyVersion ?? route.TopologyVersion,
                    workerCount: route.ShardCount,
                    queueCapacity: 1,
                    submittedWorkItemCount: route.ShardCount,
                    acceptedWorkItemCount: route.ShardCount,
                    completedWorkItemCount: route.ShardCount,
                    succeededWorkItemCount: route.ShardCount,
                    failedWorkItemCount: 0,
                    canceledWorkItemCount: 0,
                    isSuccessful: true,
                    isRejected: false,
                    timedOut: false)
            },
            Array.Empty<RadarProcessingRecentWorkerFailure>(),
            new RadarProcessingWorkerRetentionStats(retainedBatchCount: 1));

        return new RadarProcessingResult(
            RadarProcessingExecutionMode.AsyncShardTransport,
            route.PartitionCount,
            route.ShardCount,
            metrics,
            RadarProcessingValidationResult.Valid(metrics),
            RadarProcessingTelemetry.FromRoute(RadarProcessingExecutionMode.AsyncShardTransport, route),
            route.TopologyVersion,
            workerTelemetry);
    }
}
