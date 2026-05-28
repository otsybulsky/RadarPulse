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
        public void Validate()
        {
            ValidateArchiveSelection();
            ValidateBasicCounts();
            ValidateQueueingOptions();
            ValidateProviderOverlapOptions();
            ValidateExecutionOptions();
        }

        private void ValidateArchiveSelection()
        {
            if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
            {
                throw new InvalidOperationException("Provide exactly one of --file or --cache.");
            }

            if (!string.IsNullOrWhiteSpace(filePath) &&
                (date is not null || radarId is not null || maxFilesWasProvided))
            {
                throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
            }
        }

        private void ValidateBasicCounts()
        {
            if (maxFiles <= 0)
            {
                throw new InvalidOperationException("--max-files must be greater than zero.");
            }

            if (partitionCount <= 0)
            {
                throw new InvalidOperationException("--partitions must be greater than zero.");
            }

            if (shardCount <= 0)
            {
                throw new InvalidOperationException("--shards must be greater than zero.");
            }

            if (partitionCount < shardCount)
            {
                throw new InvalidOperationException("--partitions must be greater than or equal to --shards.");
            }

            if (iterations <= 0)
            {
                throw new InvalidOperationException("--iterations must be greater than zero.");
            }

            if (warmupIterations < 0)
            {
                throw new InvalidOperationException("--warmup-iterations cannot be negative.");
            }

            if (parallelism <= 0)
            {
                throw new InvalidOperationException("--parallelism must be greater than zero.");
            }
        }

        private void ValidateQueueingOptions()
        {
            if (workerCount.HasValue && workerCount.Value <= 0)
            {
                throw new InvalidOperationException("--workers must be greater than zero.");
            }

            if (queueCapacity.HasValue && queueCapacity.Value <= 0)
            {
                throw new InvalidOperationException("--queue-capacity must be greater than zero.");
            }

            if (queueTimeout.HasValue &&
                queueTimeout.Value <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("--queue-timeout-ms must be greater than zero.");
            }

            if (queueTimeout.HasValue &&
                providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
            {
                throw new InvalidOperationException("--queue-timeout-ms requires --provider queued-owned.");
            }

            if (queueRetainedPayloadBytes.HasValue &&
                queueRetainedPayloadBytes.Value <= 0)
            {
                throw new InvalidOperationException("--queue-retained-bytes must be greater than zero.");
            }

            if (queueRetainedPayloadBytes.HasValue &&
                providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
            {
                throw new InvalidOperationException("--queue-retained-bytes requires --provider queued-owned.");
            }
        }

        private void ValidateProviderOverlapOptions()
        {
            if (providerOverlapMode != RadarProcessingQueuedProviderOverlapMode.None &&
                providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
            {
                throw new InvalidOperationException("--provider-overlap requires --provider queued-owned.");
            }

            if (retentionStrategyWasProvided &&
                providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
            {
                throw new InvalidOperationException("--retention-strategy requires --provider queued-owned.");
            }

            if (providerMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
                retentionStrategy == RadarProcessingRetainedPayloadStrategy.BuilderTransfer)
            {
                throw new InvalidOperationException("--retention-strategy builder-transfer is not supported yet.");
            }

            if (overlapTelemetryWasProvided &&
                overlapTelemetryOutput != ProcessingBenchmarkProviderOverlapTelemetryOutput.None &&
                providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.None)
            {
                throw new InvalidOperationException("--overlap-telemetry requires --provider-overlap producer-consumer.");
            }

            if (overlapConsumerDelayWasProvided &&
                overlapConsumerDelay <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("--overlap-consumer-delay-ms must be greater than zero.");
            }

            if (overlapConsumerDelayWasProvided &&
                (providerMode != RadarProcessingArchiveProviderMode.QueuedOwned ||
                 providerOverlapMode != RadarProcessingQueuedProviderOverlapMode.ProducerConsumer))
            {
                throw new InvalidOperationException(
                    "--overlap-consumer-delay-ms requires --provider queued-owned --provider-overlap producer-consumer.");
            }
        }

        private void ValidateExecutionOptions()
        {
            if (executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
            {
                return;
            }

            if (workerCount.HasValue)
            {
                throw new InvalidOperationException("--workers and --queue-capacity require --execution async.");
            }

            if (queueCapacity.HasValue &&
                providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
            {
                throw new InvalidOperationException("--queue-capacity requires --execution async or --provider queued-owned.");
            }
        }
    }
}
