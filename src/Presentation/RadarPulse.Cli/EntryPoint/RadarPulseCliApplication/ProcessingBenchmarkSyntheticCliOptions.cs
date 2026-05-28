using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

internal sealed record ProcessingBenchmarkSyntheticOptions(
    RadarProcessingExecutionMode ExecutionMode,
    int SourceCount,
    int BatchCount,
    int EventsPerBatch,
    int PayloadValuesPerEvent,
    int PartitionCount,
    int ShardCount,
    RadarProcessingBenchmarkHandlerSet HandlerSet,
    int Iterations,
    int WarmupIterations,
    RadarProcessingAsyncExecutionOptions? AsyncExecution)
{
    /// <summary>
    /// Parses synthetic processing benchmark options from CLI arguments.
    /// </summary>
    public static ProcessingBenchmarkSyntheticOptions Parse(string[] args)
    {
        var executionMode = RadarProcessingExecutionMode.Sequential;
        var sourceCount = 16;
        var batchCount = 4;
        var eventsPerBatch = 1024;
        var payloadValuesPerEvent = 4;
        var partitionCount = 1;
        var shardCount = 1;
        var handlerSet = RadarProcessingBenchmarkHandlerSet.None;
        var iterations = 3;
        var warmupIterations = 1;
        int? workerCount = null;
        int? queueCapacity = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mode":
                    executionMode = ParseExecutionMode(RequireValue(args, ref i, "--mode"));
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
                case "--payload-values":
                    payloadValuesPerEvent = int.Parse(RequireValue(args, ref i, "--payload-values"));
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--workers":
                    workerCount = int.Parse(RequireValue(args, ref i, "--workers"));
                    break;
                case "--queue-capacity":
                    queueCapacity = int.Parse(RequireValue(args, ref i, "--queue-capacity"));
                    break;
                case "--handlers":
                    handlerSet = ParseHandlerSet(RequireValue(args, ref i, "--handlers"));
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

        new RadarProcessingSyntheticWorkloadOptions(
            sourceCount,
            batchCount,
            eventsPerBatch,
            payloadValuesPerEvent).Validate();

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

        if (partitionCount > sourceCount)
        {
            throw new InvalidOperationException("--partitions must be less than or equal to --sources.");
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

        RadarProcessingAsyncExecutionOptions? asyncExecution = null;
        if (executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            asyncExecution = new RadarProcessingAsyncExecutionOptions(
                workerCount: workerCount ?? shardCount,
                queueCapacity: queueCapacity ?? 1);
        }
        else if (workerCount.HasValue || queueCapacity.HasValue)
        {
            throw new InvalidOperationException("--workers and --queue-capacity require --mode async.");
        }

        return new ProcessingBenchmarkSyntheticOptions(
            executionMode,
            sourceCount,
            batchCount,
            eventsPerBatch,
            payloadValuesPerEvent,
            partitionCount,
            shardCount,
            handlerSet,
            iterations,
            warmupIterations,
            asyncExecution);
    }

    private static RadarProcessingExecutionMode ParseExecutionMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "sequential" => RadarProcessingExecutionMode.Sequential,
            "partitioned" or "partitioned-barrier" => RadarProcessingExecutionMode.PartitionedBarrier,
            "async" or "async-partitioned" or "async-shard" or "async-shard-transport" =>
                RadarProcessingExecutionMode.AsyncShardTransport,
            _ => throw new ArgumentException($"Unknown processing mode: {value}")
        };

    private static RadarProcessingBenchmarkHandlerSet ParseHandlerSet(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" => RadarProcessingBenchmarkHandlerSet.None,
            "counter-checksum" => RadarProcessingBenchmarkHandlerSet.CounterChecksum,
            "counter-checksum-heavy" or "counter-checksum+heavy" or "standard-heavy" =>
                RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy,
            _ => throw new ArgumentException($"Unknown processing benchmark handler set: {value}")
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
/// Optional CLI overrides for quarantine lifecycle settings used by processing benchmark commands.
