using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

public sealed record ProcessingBenchmarkOrderedArchiveProcessingOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    int PartitionCount,
    int ShardCount,
    int ActiveBatchCapacity,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor,
    RadarProcessingBenchmarkHandlerSet HandlerSet,
    ProcessingBenchmarkProviderQueueTelemetryOutput QueueTelemetryOutput =
        ProcessingBenchmarkProviderQueueTelemetryOutput.Summary,
    ProcessingBenchmarkProviderOverlapTelemetryOutput OverlapTelemetryOutput =
        ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary)
{
    /// <summary>
    /// Parses ordered archive processing benchmark options from CLI arguments.
    /// </summary>
    public static ProcessingBenchmarkOrderedArchiveProcessingOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        var maxFilesWasProvided = false;
        var partitionCount = 24;
        var shardCount = 4;
        var activeBatchCapacity = RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity;
        var iterations = 1;
        var warmupIterations = 0;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        var handlerSet = RadarProcessingBenchmarkHandlerSet.None;
        var queueTelemetryOutput = ProcessingBenchmarkProviderQueueTelemetryOutput.Summary;
        var overlapTelemetryOutput = ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    maxFilesWasProvided = true;
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--active-batches":
                case "--active-batch-capacity":
                    activeBatchCapacity = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                case "--handlers":
                    handlerSet = ParseHandlerSet(RequireValue(args, ref i, "--handlers"));
                    break;
                case "--queue-telemetry":
                    queueTelemetryOutput = ParseQueueTelemetryOutput(RequireValue(args, ref i, "--queue-telemetry"));
                    break;
                case "--overlap-telemetry":
                    overlapTelemetryOutput = ParseOverlapTelemetryOutput(
                        RequireValue(args, ref i, "--overlap-telemetry"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null || maxFilesWasProvided))
        {
            throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
        }

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

        if (activeBatchCapacity <= 0)
        {
            throw new InvalidOperationException("--active-batches must be greater than zero.");
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

        ArchiveBZip2Decompressors.Create(decompressor);
        RadarProcessingBenchmarkHandlers.EnsureKnown(handlerSet);
        _ = new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity);

        return new ProcessingBenchmarkOrderedArchiveProcessingOptions(
            filePath,
            cachePath,
            date,
            radarId,
            maxFiles,
            partitionCount,
            shardCount,
            activeBatchCapacity,
            iterations,
            warmupIterations,
            parallelism,
            decompressor,
            handlerSet,
            queueTelemetryOutput,
            overlapTelemetryOutput);
    }

    private static RadarProcessingBenchmarkHandlerSet ParseHandlerSet(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" => RadarProcessingBenchmarkHandlerSet.None,
            "counter-checksum" => RadarProcessingBenchmarkHandlerSet.CounterChecksum,
            "counter-checksum-heavy" or "counter-checksum+heavy" or "standard-heavy" =>
                RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy,
            _ => throw new ArgumentException($"Unknown ordered archive processing handler set: {value}")
        };

    private static ProcessingBenchmarkProviderQueueTelemetryOutput ParseQueueTelemetryOutput(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => ProcessingBenchmarkProviderQueueTelemetryOutput.None,
            "summary" => ProcessingBenchmarkProviderQueueTelemetryOutput.Summary,
            "recent" or "details" => ProcessingBenchmarkProviderQueueTelemetryOutput.Recent,
            _ => throw new ArgumentException($"Unknown ordered archive processing queue telemetry mode: {value}")
        };

    private static ProcessingBenchmarkProviderOverlapTelemetryOutput ParseOverlapTelemetryOutput(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => ProcessingBenchmarkProviderOverlapTelemetryOutput.None,
            "summary" => ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary,
            "recent" or "details" => ProcessingBenchmarkProviderOverlapTelemetryOutput.Recent,
            _ => throw new ArgumentException($"Unknown ordered archive processing overlap telemetry mode: {value}")
        };

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

/// <summary>
/// Selects how provider queue telemetry is written by processing benchmark CLI commands.
