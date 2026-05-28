using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;
using static CliFormat;

internal static class ProcessingBenchmarkCliApplication
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            return RadarPulseCliUsage.Print();
        }

        return args[0] switch
        {
            "synthetic" => BenchmarkProcessingSynthetic(args[1..]),
            "rebalance" or "rebalance-synthetic" => BenchmarkProcessingRebalanceSynthetic(args[1..]),
            "rebalance-archive" => BenchmarkProcessingRebalanceArchive(args[1..]),
            "ordered-archive-processing" => BenchmarkProcessingOrderedArchiveProcessing(args[1..]),
            _ => RadarPulseCliUsage.Print()
        };
    }


    static int BenchmarkProcessingSynthetic(string[] args)
    {
        var options = ProcessingBenchmarkSyntheticOptions.Parse(args);
        var workloadOptions = new RadarProcessingSyntheticWorkloadOptions(
            options.SourceCount,
            options.BatchCount,
            options.EventsPerBatch,
            options.PayloadValuesPerEvent);
        var result = new RadarProcessingSyntheticBenchmark().Measure(
            workloadOptions,
            options.ExecutionMode,
            options.PartitionCount,
            options.ShardCount,
            options.HandlerSet,
            options.Iterations,
            options.WarmupIterations,
            CancellationToken.None,
            options.AsyncExecution);

        ProcessingBenchmarkCliReporter.PrintProcessingBenchmarkResult(result);
        return 0;
    }

    static int BenchmarkProcessingRebalanceSynthetic(string[] args)
    {
        var options = ProcessingBenchmarkRebalanceSyntheticOptions.Parse(args);
        var benchmark = new RadarProcessingSyntheticRebalanceBenchmark();
        var printedResult = false;

        foreach (var workloadKind in options.Workloads)
        {
            var workload = RadarProcessingSyntheticRebalanceWorkload.Create(workloadKind);
            var hardeningOptions = CreateProcessingRebalanceSyntheticHardeningOptions(
                workload,
                options.ValidationProfile,
                options.QuarantineLifecycleOverrides);

            foreach (var mode in options.Modes)
            {
                if (printedResult)
                {
                    Console.WriteLine();
                }

                var result = benchmark.Measure(
                    workload,
                    mode,
                    options.Iterations,
                    options.WarmupIterations,
                    CancellationToken.None,
                    hardeningOptions,
                    options.ExecutionMode,
                    options.AsyncExecution,
                    options.OrderedActiveBatchCapacity);

                ProcessingBenchmarkCliReporter.PrintProcessingRebalanceBenchmarkResult(result);
                printedResult = true;
            }
        }

        return 0;
    }

    static RadarProcessingRebalanceHardeningOptions CreateProcessingRebalanceSyntheticHardeningOptions(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingValidationProfile validationProfile,
        ProcessingBenchmarkQuarantineLifecycleOptionOverrides quarantineLifecycleOverrides) =>
        new(
            telemetryRetention: workload.HardeningOptions.TelemetryRetention,
            quarantineLifecycle: quarantineLifecycleOverrides.ApplyTo(workload.HardeningOptions.QuarantineLifecycle),
            validationProfile: validationProfile);

    static int BenchmarkProcessingRebalanceArchive(string[] args)
    {
        var options = ProcessingBenchmarkArchiveRebalanceOptions.Parse(args);
        var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
            ArchiveBZip2Decompressors.Create(options.Decompressor));
        var hardeningOptions = new RadarProcessingRebalanceHardeningOptions(
            telemetryRetention: options.TelemetryRetention,
            quarantineLifecycle: options.QuarantineLifecycleOverrides.ApplyTo(
                RadarProcessingQuarantineLifecycleOptions.Default),
            validationProfile: options.ValidationProfile);
        var printedResult = false;

        foreach (var mode in options.Modes)
        {
            if (printedResult)
            {
                Console.WriteLine();
            }

            if (options.CachePath is not null)
            {
                var cacheResult = benchmark.MeasureCache(
                    options.CachePath,
                    options.Date,
                    options.RadarId,
                    options.MaxFiles,
                    mode,
                    options.Iterations,
                    options.WarmupIterations,
                    options.PartitionCount,
                    options.ShardCount,
                    options.Parallelism,
                    CancellationToken.None,
                    hardeningOptions,
                    options.PressureSkew,
                    options.ExecutionMode,
                    options.AsyncExecution,
                    options.ProviderMode,
                    options.ProviderQueueCapacity,
                    options.ProviderQueueTimeout,
                    options.ProviderOverlapMode,
                    options.RetentionStrategy,
                    options.ProviderQueueRetainedPayloadBytes,
                    options.OverlapConsumerDelay);
                ProcessingBenchmarkCliReporter.PrintProcessingArchiveRebalanceCacheBenchmarkResult(
                    cacheResult,
                    options);
            }
            else
            {
                var result = benchmark.MeasureFile(
                    options.FilePath ?? throw new InvalidOperationException("--file is required when --cache is not provided."),
                    mode,
                    options.Iterations,
                    options.WarmupIterations,
                    options.PartitionCount,
                    options.ShardCount,
                    options.Parallelism,
                    CancellationToken.None,
                    hardeningOptions,
                    options.PressureSkew,
                    options.ExecutionMode,
                    options.AsyncExecution,
                    options.ProviderMode,
                    options.ProviderQueueCapacity,
                    options.ProviderQueueTimeout,
                    options.ProviderOverlapMode,
                    options.RetentionStrategy,
                    options.ProviderQueueRetainedPayloadBytes,
                    options.OverlapConsumerDelay);
                ProcessingBenchmarkCliReporter.PrintProcessingArchiveRebalanceBenchmarkResult(
                    result,
                    options);
            }

            printedResult = true;
        }

        return 0;
    }

    static int BenchmarkProcessingOrderedArchiveProcessing(string[] args)
    {
        var options = ProcessingBenchmarkOrderedArchiveProcessingOptions.Parse(args);
        var benchmark = new RadarProcessingArchiveOrderedProcessingBenchmark(
            ArchiveBZip2Decompressors.Create(options.Decompressor));

        var result = options.CachePath is not null
            ? benchmark.MeasureCache(
                options.CachePath,
                options.Date,
                options.RadarId,
                options.MaxFiles,
                options.Iterations,
                options.WarmupIterations,
                options.PartitionCount,
                options.ShardCount,
                options.Parallelism,
                options.ActiveBatchCapacity,
                CancellationToken.None,
                options.HandlerSet)
            : benchmark.MeasureFile(
                options.FilePath ?? throw new InvalidOperationException("--file is required when --cache is not provided."),
                options.Iterations,
                options.WarmupIterations,
                options.PartitionCount,
                options.ShardCount,
                options.Parallelism,
                options.ActiveBatchCapacity,
                CancellationToken.None,
                options.HandlerSet);

        ProcessingBenchmarkCliReporter.PrintProcessingArchiveOrderedProcessingBenchmarkResult(result, options);
        return 0;
    }
}
