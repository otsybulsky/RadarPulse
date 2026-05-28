using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

public sealed partial record ProcessingBenchmarkArchiveRebalanceOptions
{
    private sealed partial class ParseState
    {
        public ProcessingBenchmarkArchiveRebalanceOptions ToOptions()
        {
            var asyncExecution = CreateAsyncExecutionOptions();
            var providerQueueCapacity = providerMode == RadarProcessingArchiveProviderMode.QueuedOwned
                ? queueCapacity ?? 1
                : 1;

            ArchiveBZip2Decompressors.Create(decompressor);
            var telemetryRetention = new RadarProcessingTelemetryRetentionOptions(
                retentionMode,
                maxRetainedDecisions,
                maxRetainedTransitions,
                maxRetainedAcceptedMoves,
                maxRetainedValidationFailures);
            var pressureSkew = new RadarProcessingPressureSkewOptions(
                skewProfile,
                skewFactor,
                skewPeriod);
            var quarantineLifecycleOverrides = new ProcessingBenchmarkQuarantineLifecycleOptionOverrides(
                quarantineTtlEvaluations,
                sustainedCoolingSampleCount,
                materialPressureChangeThreshold);
            _ = quarantineLifecycleOverrides.ApplyTo(RadarProcessingQuarantineLifecycleOptions.Default);

            return new ProcessingBenchmarkArchiveRebalanceOptions(
                filePath,
                cachePath,
                date,
                radarId,
                maxFiles,
                modes,
                partitionCount,
                shardCount,
                iterations,
                warmupIterations,
                parallelism,
                decompressor,
                validationProfile,
                quarantineLifecycleOverrides,
                telemetryRetention,
                pressureSkew,
                providerMode,
                providerQueueCapacity,
                queueTimeout,
                providerOverlapMode,
                retentionStrategy,
                queueRetainedPayloadBytes,
                overlapConsumerDelay,
                queueTelemetryOutput,
                overlapTelemetryOutput,
                executionMode,
                asyncExecution,
                new ProcessingBenchmarkArchiveRebalanceOptionProvenance(
                    providerModeSource,
                    providerOverlapModeSource,
                    retentionStrategySource,
                    queueCapacitySource,
                    queueRetainedPayloadBytesSource,
                    queueTelemetrySource,
                    overlapTelemetrySource,
                    overlapConsumerDelaySource,
                    executionModeSource,
                    workerCountSource));
        }

        private RadarProcessingAsyncExecutionOptions? CreateAsyncExecutionOptions() =>
            executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? new RadarProcessingAsyncExecutionOptions(
                    workerCount: workerCount ?? shardCount,
                    queueCapacity: queueCapacity ?? 1)
                : null;
    }
}
