using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Product;

public sealed class RadarPulseProductPipelineService
{
    private readonly object sync = new();
    private readonly Dictionary<string, RadarPulseProductRunDetail> runsById = new(StringComparer.Ordinal);
    private readonly List<string> runOrder = new();
    private readonly RadarProcessingProductionPipelineRunner runner;

    public RadarPulseProductPipelineService(
        RadarProcessingProductionPipelineRunner? runner = null)
    {
        this.runner = runner ?? new RadarProcessingProductionPipelineRunner();
    }

    public int Count
    {
        get
        {
            lock (sync)
            {
                return runsById.Count;
            }
        }
    }

    public async ValueTask<RadarPulseProductRunDetail> RunSyntheticAsync(
        RadarPulseProductPipelineSyntheticRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRunId(request.RunId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.SourceCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.BatchCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.EventsPerBatch);

        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: request.SourceCount,
            rangeBandCount: 1);
        var partitionCount = ResolvePartitionCount(request.PartitionCount, request.SourceCount);
        var shardCount = ResolveShardCount(request.ShardCount, partitionCount);
        var batches = CreateSyntheticBatches(
            universe,
            request.BatchCount,
            request.EventsPerBatch);
        var input = new RadarPulseProductInputSummary(
            RadarPulseProductInputKind.Synthetic,
            "deterministic synthetic product pipeline input",
            "synthetic",
            request.BatchCount,
            checked((long)request.BatchCount * request.EventsPerBatch));

        return await RunBatchesAsync(
                request.RunId,
                universe,
                batches,
                partitionCount,
                shardCount,
                request.HandlerSet,
                request.Options,
                input,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<RadarPulseProductRunDetail> RunArchiveFileAsync(
        RadarPulseProductPipelineArchiveFileRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRunId(request.RunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FilePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Parallelism);

        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        var publisher = new CapturingArchiveRadarEventBatchPublisher();
        var archiveOptions = new ArchiveRadarEventBatchPublishOptions(
            sourceUniverse,
            request.Parallelism);
        var archivePublisher = new NexradArchiveRadarEventBatchPublisher(
            ArchiveBZip2Decompressors.Create(request.Decompressor));
        var archiveResult = archivePublisher.PublishFile(
            request.FilePath,
            publisher,
            archiveOptions,
            cancellationToken);
        var batches = publisher.Batches;
        if (batches.Count == 0)
        {
            throw new InvalidOperationException("Archive file did not produce any RadarEventBatch input.");
        }

        var partitionCount = ResolvePartitionCount(
            request.PartitionCount,
            sourceUniverse.SourceCount);
        var shardCount = ResolveShardCount(request.ShardCount, partitionCount);
        var input = new RadarPulseProductInputSummary(
            RadarPulseProductInputKind.ArchiveFile,
            "NEXRAD archive file product pipeline input",
            Path.GetFullPath(request.FilePath),
            batches.Count,
            archiveResult.EventCount);

        return await RunBatchesAsync(
                request.RunId,
                sourceUniverse,
                batches,
                partitionCount,
                shardCount,
                request.HandlerSet,
                request.Options,
                input,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public IReadOnlyList<RadarPulseProductRunSummary> ListRuns()
    {
        lock (sync)
        {
            if (runOrder.Count == 0)
            {
                return Array.Empty<RadarPulseProductRunSummary>();
            }

            return Array.AsReadOnly(
                runOrder
                    .Select(runId => runsById[runId].Summary)
                    .ToArray());
        }
    }

    public RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetRun(
        string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        lock (sync)
        {
            return runsById.TryGetValue(runId, out var detail)
                ? RadarPulseProductQueryResult<RadarPulseProductRunDetail>.FromValue(detail)
                : RadarPulseProductQueryResult<RadarPulseProductRunDetail>.NotFound(
                    $"Product run '{runId}' was not found.");
        }
    }

    public RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetLatestRun()
    {
        lock (sync)
        {
            if (runOrder.Count == 0)
            {
                return RadarPulseProductQueryResult<RadarPulseProductRunDetail>.NotFound(
                    "No product pipeline run has been published.");
            }

            return RadarPulseProductQueryResult<RadarPulseProductRunDetail>.FromValue(
                runsById[runOrder[^1]]);
        }
    }

    private async ValueTask<RadarPulseProductRunDetail> RunBatchesAsync(
        string runId,
        RadarSourceUniverse sourceUniverse,
        IReadOnlyCollection<RadarEventBatch> batches,
        int partitionCount,
        int shardCount,
        RadarPulseProductHandlerSet handlerSet,
        RadarPulseProductPipelineOptions? options,
        RadarPulseProductInputSummary input,
        CancellationToken cancellationToken)
    {
        var productionRequest = new RadarProcessingProductionPipelineRunRequest(
            runId,
            sourceUniverse,
            batches,
            partitionCount,
            shardCount,
            CreateHandlers(handlerSet),
            CreateProductionOptions(options));
        var productionResult = await runner.RunAsync(productionRequest, cancellationToken)
            .ConfigureAwait(false);
        var detail = RadarPulseProductPipelineMapper.ToProductRunDetail(
            productionResult,
            input);
        Store(detail);
        return detail;
    }

    private void Store(
        RadarPulseProductRunDetail detail)
    {
        lock (sync)
        {
            if (!runsById.ContainsKey(detail.RunId))
            {
                runOrder.Add(detail.RunId);
            }

            runsById[detail.RunId] = detail;
        }
    }

    private static RadarProcessingProductionPipelineOptions? CreateProductionOptions(
        RadarPulseProductPipelineOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        return new RadarProcessingProductionPipelineOptions(
            workerCount: options.WorkerCount,
            workerQueueCapacity: options.WorkerQueueCapacity,
            providerQueueCapacity: options.ProviderQueueCapacity,
            retainedPayloadBytes: options.RetainedPayloadBytes,
            orderedActiveBatchCapacity: options.OrderedActiveBatchCapacity,
            workloadBatchLimit: options.WorkloadBatchLimit,
            silentBorrowedProviderFallback: options.SilentBorrowedProviderFallback);
    }

    private static IReadOnlyCollection<IRadarSourceProcessingHandler> CreateHandlers(
        RadarPulseProductHandlerSet handlerSet) =>
        handlerSet switch
        {
            RadarPulseProductHandlerSet.None => Array.Empty<IRadarSourceProcessingHandler>(),
            RadarPulseProductHandlerSet.CounterChecksum =>
                RadarProcessingBenchmarkHandlers.Create(RadarProcessingBenchmarkHandlerSet.CounterChecksum),
            RadarPulseProductHandlerSet.CounterChecksumHeavy =>
                RadarProcessingBenchmarkHandlers.Create(RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy),
            RadarPulseProductHandlerSet.SnapshotCounting =>
                new IRadarSourceProcessingHandler[] { new SnapshotCountingHandler() },
            RadarPulseProductHandlerSet.Unsupported =>
                new IRadarSourceProcessingHandler[] { new UnsupportedProductHandler() },
            _ => throw new ArgumentOutOfRangeException(nameof(handlerSet))
        };

    private static IReadOnlyList<RadarEventBatch> CreateSyntheticBatches(
        RadarSourceUniverse universe,
        int batchCount,
        int eventsPerBatch)
    {
        var batches = new RadarEventBatch[batchCount];
        for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            batches[batchIndex] = CreateSyntheticBatch(
                universe,
                eventsPerBatch,
                messageTimestampBase: 100 + (batchIndex * 1000));
        }

        return Array.AsReadOnly(batches);
    }

    private static RadarEventBatch CreateSyntheticBatch(
        RadarSourceUniverse universe,
        int eventsPerBatch,
        long messageTimestampBase)
    {
        var builder = new RadarEventBatchBuilder(
            initialEventCapacity: eventsPerBatch,
            initialPayloadCapacity: eventsPerBatch);
        for (var i = 0; i < eventsPerBatch; i++)
        {
            var sourceId = i % universe.SourceCount;
            builder.AddEvent(
                new RadarStreamIdentity(
                    sourceId,
                    radarOrdinal: 0,
                    momentId: 0,
                    elevationSlot: 0,
                    azimuthBucket: (ushort)sourceId,
                    rangeBand: 0,
                    dictionaryVersion: DictionaryVersion.Initial,
                    sourceUniverseVersion: universe.Version),
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: messageTimestampBase + i,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payload: new byte[] { (byte)(i + 1) });
        }

        return builder.Build();
    }

    private static void ValidateRunId(
        string runId) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

    private static int ResolvePartitionCount(
        int requested,
        int sourceCount)
    {
        if (requested == 0)
        {
            return Math.Max(sourceCount, 1);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(requested);
        return requested;
    }

    private static int ResolveShardCount(
        int requested,
        int partitionCount)
    {
        if (requested == 0)
        {
            return Math.Max(1, Math.Min(partitionCount, 4));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(requested);
        if (requested > partitionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requested),
                requested,
                "Shard count must be less than or equal to partition count.");
        }

        return requested;
    }

    private sealed class CapturingArchiveRadarEventBatchPublisher : IArchiveRadarEventBatchPublisher
    {
        private readonly List<RadarEventBatch> batches = new();

        public IReadOnlyList<RadarEventBatch> Batches => batches;

        public void Publish(
            RadarEventBatch batch,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(batch);
            cancellationToken.ThrowIfCancellationRequested();
            batches.Add(batch.ToOwnedSnapshot());
        }
    }

    private sealed class SnapshotCountingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "product-snapshot-count",
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "product.events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0)
                });

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }
    }

    private sealed class UnsupportedProductHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "product-unsupported",
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "product.unsupported",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0)
                });

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
            RadarSourceProcessingHandlerExecutionClassification.Unsupported;

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }
    }
}
