using System.Globalization;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

public sealed partial record ProcessingBenchmarkRebalanceSyntheticOptions
{
    /// <summary>
    /// Parses synthetic rebalance benchmark options from CLI arguments.
    /// </summary>
    public static ProcessingBenchmarkRebalanceSyntheticOptions Parse(string[] args)
    {
        IReadOnlyList<RadarProcessingSyntheticRebalanceWorkloadKind> workloads = Array.AsReadOnly(
        [
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced
        ]);
        IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> modes = AllModes;
        var validationProfile = RadarProcessingValidationProfile.Diagnostic;
        int? quarantineTtlEvaluations = null;
        int? sustainedCoolingSampleCount = null;
        double? materialPressureChangeThreshold = null;
        var iterations = 3;
        var warmupIterations = 1;
        var executionMode = RadarProcessingExecutionMode.PartitionedBarrier;
        int? workerCount = null;
        int? queueCapacity = null;
        var orderedActiveBatchCapacity = RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--workload":
                    workloads = ParseWorkload(RequireValue(args, ref i, "--workload"));
                    break;
                case "--mode":
                    modes = ParseMode(RequireValue(args, ref i, "--mode"));
                    break;
                case "--execution":
                    executionMode = ParseExecutionMode(RequireValue(args, ref i, "--execution"));
                    break;
                case "--workers":
                    workerCount = int.Parse(RequireValue(args, ref i, "--workers"));
                    break;
                case "--queue-capacity":
                    queueCapacity = int.Parse(RequireValue(args, ref i, "--queue-capacity"));
                    break;
                case "--active-batches":
                case "--ordered-active-batches":
                    orderedActiveBatchCapacity = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--validation-profile":
                    validationProfile = ParseValidationProfile(RequireValue(args, ref i, "--validation-profile"));
                    break;
                case "--quarantine-ttl":
                case "--quarantine-ttl-evaluations":
                    quarantineTtlEvaluations = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--quarantine-sustained-cooling-samples":
                case "--quarantine-sustained-cooling-sample-count":
                    sustainedCoolingSampleCount = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--quarantine-material-pressure-change":
                case "--quarantine-material-pressure-change-threshold":
                    materialPressureChangeThreshold = double.Parse(
                        RequireValue(args, ref i, args[i]),
                        CultureInfo.InvariantCulture);
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (workerCount.HasValue && workerCount.Value <= 0)
        {
            throw new InvalidOperationException("--workers must be greater than zero.");
        }

        if (queueCapacity.HasValue && queueCapacity.Value <= 0)
        {
            throw new InvalidOperationException("--queue-capacity must be greater than zero.");
        }

        if (orderedActiveBatchCapacity <= 0)
        {
            throw new InvalidOperationException("--active-batches must be greater than zero.");
        }

        RadarProcessingAsyncExecutionOptions? asyncExecution = null;
        if (executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            asyncExecution = new RadarProcessingAsyncExecutionOptions(
                workerCount: workerCount ?? DefaultAsyncWorkerCount,
                queueCapacity: queueCapacity ?? DefaultAsyncQueueCapacity);
        }
        else if (workerCount.HasValue || queueCapacity.HasValue)
        {
            throw new InvalidOperationException("--workers and --queue-capacity require --execution async.");
        }

        var quarantineLifecycleOverrides = new ProcessingBenchmarkQuarantineLifecycleOptionOverrides(
            quarantineTtlEvaluations,
            sustainedCoolingSampleCount,
            materialPressureChangeThreshold);
        _ = quarantineLifecycleOverrides.ApplyTo(RadarProcessingQuarantineLifecycleOptions.Default);

        return new ProcessingBenchmarkRebalanceSyntheticOptions(
            workloads,
            modes,
            validationProfile,
            quarantineLifecycleOverrides,
            iterations,
            warmupIterations,
            executionMode,
            asyncExecution,
            orderedActiveBatchCapacity);
    }
}
