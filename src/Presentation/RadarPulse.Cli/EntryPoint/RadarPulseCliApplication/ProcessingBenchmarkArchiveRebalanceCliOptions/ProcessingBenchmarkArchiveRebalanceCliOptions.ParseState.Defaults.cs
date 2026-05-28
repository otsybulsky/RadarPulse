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
        public void ApplyRolloutDefaults()
        {
            providerModeSource = CurrentDefaultOrExplicit(providerModeWasProvided);
            providerOverlapModeSource = CurrentDefaultOrExplicit(providerOverlapModeWasProvided);
            retentionStrategySource = CurrentDefaultOrExplicit(retentionStrategyWasProvided);
            queueCapacitySource = CurrentDefaultOrExplicit(queueCapacityWasProvided);
            queueRetainedPayloadBytesSource = CurrentDefaultOrExplicit(queueRetainedPayloadBytesWasProvided);
            queueTelemetrySource = CurrentDefaultOrExplicit(queueTelemetryWasProvided);
            overlapTelemetrySource = CurrentDefaultOrExplicit(overlapTelemetryWasProvided);
            overlapConsumerDelaySource = CurrentDefaultOrExplicit(overlapConsumerDelayWasProvided);
            executionModeSource = CurrentDefaultOrExplicit(executionModeWasProvided);
            workerCountSource = CurrentDefaultOrExplicit(workerCountWasProvided);

            if (providerModeWasProvided)
            {
                return;
            }

            providerMode = RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode;
            providerModeSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;

            if (!providerOverlapModeWasProvided)
            {
                providerOverlapMode = RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode;
                providerOverlapModeSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!retentionStrategyWasProvided)
            {
                retentionStrategy = RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy;
                retentionStrategySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!queueCapacityWasProvided)
            {
                queueCapacity = DefaultRolloutProviderQueueCapacity;
                queueCapacitySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!queueRetainedPayloadBytesWasProvided)
            {
                queueRetainedPayloadBytes = DefaultRolloutRetainedPayloadBytes;
                queueRetainedPayloadBytesSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!queueTelemetryWasProvided)
            {
                queueTelemetrySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!overlapTelemetryWasProvided)
            {
                overlapTelemetrySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!overlapConsumerDelayWasProvided)
            {
                overlapConsumerDelaySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!executionModeWasProvided)
            {
                executionMode = RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode;
                executionModeSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!workerCountWasProvided &&
                executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
            {
                workerCount = DefaultRolloutWorkerCount;
                workerCountSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }
        }
    }
}
