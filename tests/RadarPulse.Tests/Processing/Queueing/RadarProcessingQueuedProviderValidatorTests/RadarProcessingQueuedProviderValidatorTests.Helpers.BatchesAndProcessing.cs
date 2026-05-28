using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderValidatorTests
{
    private static RadarProcessingQueuedBatchEnqueueResult CreateAccepted(long sequence) =>
        RadarProcessingQueuedBatchEnqueueResult.Accepted(
            new RadarProcessingQueuedBatch(
                new RadarProcessingQueuedBatchSequence(sequence),
                CreateOwnedBatch((byte)(sequence + 1))));

    private static RadarEventBatch CreateOwnedBatch(byte firstPayloadValue)
    {
        var builder = CreateSingleEventBuilder(firstPayloadValue);
        return builder.Build();
    }

    private static RadarEventBatchBuilder CreateSingleEventBuilder(byte firstPayloadValue = 1)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 2);
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId: 0,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: SourceUniverseVersion.Initial),
            volumeTimestampUtcTicks: 1,
            messageTimestampUtcTicks: 2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [firstPayloadValue, (byte)(firstPayloadValue + 1)]);
        return builder;
    }

    private static RadarProcessingResult CreateProcessingResult(
        ulong checksum = 10,
        RadarProcessingTopologyVersion? topologyVersion = null,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry = null)
    {
        var metrics = new RadarProcessingMetrics(
            ProcessedBatchCount: 1,
            ProcessedStreamEventCount: 1,
            ProcessedPayloadValueCount: 2,
            ActiveSourceCount: 1,
            RawValueChecksum: 3,
            ProcessingChecksum: checksum);

        return new RadarProcessingResult(
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            metrics,
            RadarProcessingValidationResult.Valid(metrics),
            topologyVersion: topologyVersion,
            workerTelemetry: workerTelemetry);
    }

    private static RadarProcessingWorkerTelemetrySummary CreateFailedWorkerTelemetry() =>
        new(
            new RadarProcessingWorkerTelemetryCounters(
                dispatchedBatchCount: 1,
                completedBatchCount: 1,
                failedBatchCount: 1),
            workerCount: 1,
            queueCapacity: 1,
            Array.Empty<RadarProcessingRecentWorkerBatch>(),
            Array.Empty<RadarProcessingRecentWorkerFailure>(),
            new RadarProcessingWorkerRetentionStats());
}
