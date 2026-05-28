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
        private string? filePath;
        private string? cachePath;
        private DateOnly? date;
        private string? radarId;
        private int maxFiles = 20;
        private bool maxFilesWasProvided;
        private IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> modes = Array.AsReadOnly(
        [
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession
        ]);
        private int partitionCount = 24;
        private int shardCount = 4;
        private int iterations = 1;
        private int warmupIterations;
        private int parallelism = 1;
        private string decompressor = ArchiveBZip2Decompressors.DefaultName;
        private RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic;
        private int? quarantineTtlEvaluations;
        private int? sustainedCoolingSampleCount;
        private double? materialPressureChangeThreshold;
        private RadarProcessingDiagnosticRetentionMode retentionMode =
            RadarProcessingTelemetryRetentionOptions.Default.RetentionMode;
        private int maxRetainedDecisions = RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedDecisions;
        private int maxRetainedTransitions =
            RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedLifecycleTransitions;
        private int maxRetainedAcceptedMoves = RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedAcceptedMoves;
        private int maxRetainedValidationFailures =
            RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedValidationFailures;
        private RadarProcessingPressureSkewProfile skewProfile = RadarProcessingPressureSkewOptions.None.Profile;
        private double skewFactor = RadarProcessingPressureSkewOptions.None.Factor;
        private int skewPeriod = RadarProcessingPressureSkewOptions.None.Period;
        private RadarProcessingArchiveProviderMode providerMode = RadarProcessingArchiveProviderMode.BlockingBorrowed;
        private bool providerModeWasProvided;
        private RadarProcessingQueuedProviderOverlapMode providerOverlapMode = RadarProcessingQueuedProviderOverlapMode.None;
        private bool providerOverlapModeWasProvided;
        private RadarProcessingRetainedPayloadStrategy retentionStrategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy;
        private bool retentionStrategyWasProvided;
        private long? queueRetainedPayloadBytes;
        private bool queueRetainedPayloadBytesWasProvided;
        private TimeSpan? queueTimeout;
        private ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput =
            ProcessingBenchmarkProviderQueueTelemetryOutput.Summary;
        private bool queueTelemetryWasProvided;
        private ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput =
            ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary;
        private bool overlapTelemetryWasProvided;
        private TimeSpan overlapConsumerDelay = TimeSpan.Zero;
        private bool overlapConsumerDelayWasProvided;
        private RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier;
        private bool executionModeWasProvided;
        private int? workerCount;
        private bool workerCountWasProvided;
        private int? queueCapacity;
        private bool queueCapacityWasProvided;
        private ProcessingBenchmarkOptionValueSource providerModeSource;
        private ProcessingBenchmarkOptionValueSource providerOverlapModeSource;
        private ProcessingBenchmarkOptionValueSource retentionStrategySource;
        private ProcessingBenchmarkOptionValueSource queueCapacitySource;
        private ProcessingBenchmarkOptionValueSource queueRetainedPayloadBytesSource;
        private ProcessingBenchmarkOptionValueSource queueTelemetrySource;
        private ProcessingBenchmarkOptionValueSource overlapTelemetrySource;
        private ProcessingBenchmarkOptionValueSource overlapConsumerDelaySource;
        private ProcessingBenchmarkOptionValueSource executionModeSource;
        private ProcessingBenchmarkOptionValueSource workerCountSource;
    }
}
