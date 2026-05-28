using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

internal sealed record ProductPipelineDemoOptions(
    string RunId,
    int SourceCount,
    int BatchCount,
    int EventsPerBatch,
    int PartitionCount,
    int ShardCount,
    RadarPulseProductHandlerSet HandlerSet,
    RadarPulseProductPipelineOptions PipelineOptions)
{
    /// <summary>
    /// Parses product pipeline synthetic demo options from CLI arguments.
    /// </summary>
    public static ProductPipelineDemoOptions Parse(string[] args)
    {
        var runId = "product-demo";
        var sourceCount = 2;
        var batchCount = 2;
        var eventsPerBatch = 2;
        var partitionCount = 0;
        var shardCount = 0;
        var handlerSet = RadarPulseProductHandlerSet.None;
        int? workerCount = null;
        int? workerQueueCapacity = null;
        int? providerQueueCapacity = null;
        int? orderedActiveBatchCapacity = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--run-id":
                    runId = RequireValue(args, ref i, "--run-id");
                    break;
                case "--sources":
                    sourceCount = int.Parse(RequireValue(args, ref i, "--sources"));
                    break;
                case "--batches":
                    batchCount = int.Parse(RequireValue(args, ref i, "--batches"));
                    break;
                case "--events-per-batch":
                    eventsPerBatch = int.Parse(RequireValue(args, ref i, "--events-per-batch"));
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--handlers":
                    handlerSet = ProductPipelineCliOptionParsing.ParseHandlerSet(RequireValue(args, ref i, "--handlers"));
                    break;
                case "--workers":
                    workerCount = int.Parse(RequireValue(args, ref i, "--workers"));
                    break;
                case "--worker-queue-capacity":
                    workerQueueCapacity = int.Parse(RequireValue(args, ref i, "--worker-queue-capacity"));
                    break;
                case "--provider-queue-capacity":
                    providerQueueCapacity = int.Parse(RequireValue(args, ref i, "--provider-queue-capacity"));
                    break;
                case "--active-batches":
                    orderedActiveBatchCapacity = int.Parse(RequireValue(args, ref i, "--active-batches"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        ProductPipelineCliOptionParsing.ValidatePositive(sourceCount, "--sources");
        ProductPipelineCliOptionParsing.ValidatePositive(batchCount, "--batches");
        ProductPipelineCliOptionParsing.ValidatePositive(eventsPerBatch, "--events-per-batch");
        ProductPipelineCliOptionParsing.ValidateNonNegative(partitionCount, "--partitions");
        ProductPipelineCliOptionParsing.ValidateNonNegative(shardCount, "--shards");
        ProductPipelineCliOptionParsing.ValidateOptionalPositive(workerCount, "--workers");
        ProductPipelineCliOptionParsing.ValidateOptionalPositive(workerQueueCapacity, "--worker-queue-capacity");
        ProductPipelineCliOptionParsing.ValidateOptionalPositive(providerQueueCapacity, "--provider-queue-capacity");
        ProductPipelineCliOptionParsing.ValidateOptionalPositive(orderedActiveBatchCapacity, "--active-batches");

        return new ProductPipelineDemoOptions(
            runId,
            sourceCount,
            batchCount,
            eventsPerBatch,
            partitionCount,
            shardCount,
            handlerSet,
            new RadarPulseProductPipelineOptions(
                WorkerCount: workerCount,
                WorkerQueueCapacity: workerQueueCapacity,
                ProviderQueueCapacity: providerQueueCapacity,
                OrderedActiveBatchCapacity: orderedActiveBatchCapacity));
    }

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

internal sealed record ProductPipelineArchiveOptions(
    string RunId,
    string FilePath,
    int Parallelism,
    int PartitionCount,
    int ShardCount,
    string Decompressor,
    RadarPulseProductHandlerSet HandlerSet,
    RadarPulseProductPipelineOptions PipelineOptions)
{
    /// <summary>
    /// Parses product pipeline archive run options from CLI arguments.
    /// </summary>
    public static ProductPipelineArchiveOptions Parse(string[] args)
    {
        var runId = "product-archive";
        string? filePath = null;
        var parallelism = 1;
        var partitionCount = 0;
        var shardCount = 0;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        var handlerSet = RadarPulseProductHandlerSet.None;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--run-id":
                    runId = RequireValue(args, ref i, "--run-id");
                    break;
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                case "--handlers":
                    handlerSet = ProductPipelineCliOptionParsing.ParseHandlerSet(RequireValue(args, ref i, "--handlers"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("--file is required.");
        }

        ProductPipelineCliOptionParsing.ValidatePositive(parallelism, "--parallelism");
        ProductPipelineCliOptionParsing.ValidateNonNegative(partitionCount, "--partitions");
        ProductPipelineCliOptionParsing.ValidateNonNegative(shardCount, "--shards");
        ArchiveBZip2Decompressors.Create(decompressor);

        return new ProductPipelineArchiveOptions(
            runId,
            filePath,
            parallelism,
            partitionCount,
            shardCount,
            decompressor,
            handlerSet,
            new RadarPulseProductPipelineOptions());
    }

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

internal static class ProductPipelineCliOptionParsing
{
    /// <summary>
    /// Parses the product pipeline handler set option.
    /// </summary>
    public static RadarPulseProductHandlerSet ParseHandlerSet(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "none" => RadarPulseProductHandlerSet.None,
            "counter-checksum" => RadarPulseProductHandlerSet.CounterChecksum,
            "counter-checksum-heavy" => RadarPulseProductHandlerSet.CounterChecksumHeavy,
            "snapshot-counting" => RadarPulseProductHandlerSet.SnapshotCounting,
            "unsupported" => RadarPulseProductHandlerSet.Unsupported,
            _ => throw new ArgumentException($"Unknown product handler set: {value}.")
        };

    /// <summary>
    /// Validates that an integer option is positive.
    /// </summary>
    public static void ValidatePositive(int value, string option)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"{option} must be greater than zero.");
        }
    }

    /// <summary>
    /// Validates that an integer option is non-negative.
    /// </summary>
    public static void ValidateNonNegative(int value, string option)
    {
        if (value < 0)
        {
            throw new InvalidOperationException($"{option} cannot be negative.");
        }
    }

    /// <summary>
    /// Validates that an optional integer option is positive when supplied.
    /// </summary>
    public static void ValidateOptionalPositive(int? value, string option)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"{option} must be greater than zero.");
        }
    }
}
