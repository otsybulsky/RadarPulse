using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Product;

/// <summary>
/// Product service that composes accepted production-pipeline execution with run history.
/// </summary>
/// <remarks>
/// The service is intentionally a product adapter over existing processing and
/// archive infrastructure. It creates product vocabulary requests, stores the
/// resulting run detail, and exposes query/control helpers without changing the
/// accepted backend runtime semantics.
/// </remarks>
public sealed class RadarPulseProductPipelineService
{
    private readonly IRadarPulseProductRunHistoryStore historyStore;
    private readonly RadarProcessingProductionPipelineRunner runner;

    /// <summary>
    /// Creates a service with optional runner and history-store dependencies.
    /// </summary>
    /// <remarks>
    /// The default constructor path uses the accepted production pipeline runner
    /// and process-local in-memory history, which keeps direct tests and CLI
    /// experimentation deterministic.
    /// </remarks>
    public RadarPulseProductPipelineService(
        RadarProcessingProductionPipelineRunner? runner = null,
        IRadarPulseProductRunHistoryStore? historyStore = null)
    {
        this.runner = runner ?? new RadarProcessingProductionPipelineRunner();
        this.historyStore = historyStore ?? new RadarPulseProductInMemoryRunHistoryStore();
    }

    /// <summary>
    /// Creates a service backed by deterministic local file history.
    /// </summary>
    /// <remarks>
    /// This is the accepted local product demo persistence path. It is not a
    /// database-backed or cross-machine history adapter.
    /// </remarks>
    public static RadarPulseProductPipelineService CreateWithFileHistory(
        string historyPath,
        RadarProcessingProductionPipelineRunner? runner = null) =>
        new(
            runner,
            new RadarPulseProductFileRunHistoryStore(historyPath));

    /// <summary>
    /// Number of run details currently visible through the configured history store.
    /// </summary>
    public int Count => historyStore.Count;

    /// <summary>
    /// Current readiness and load posture for the configured history store.
    /// </summary>
    public RadarPulseProductRunHistoryReadiness HistoryReadiness =>
        historyStore.Readiness;

    /// <summary>
    /// Runs the accepted production-shaped pipeline over deterministic synthetic input.
    /// </summary>
    /// <remarks>
    /// This is the primary local demo path. It generates owned archive-shaped
    /// batches from the requested source and event counts, then persists the
    /// resulting product run detail.
    /// </remarks>
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

    /// <summary>
    /// Runs the accepted production-shaped pipeline over one local NEXRAD archive file.
    /// </summary>
    /// <remarks>
    /// The archive publisher projects the local file into owned RadarEventBatch
    /// input before processing. The method rejects archive files that produce no
    /// batches because the product run would have no inspectable read model.
    /// </remarks>
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

    /// <summary>
    /// Lists compact product run summaries from the configured history store.
    /// </summary>
    public IReadOnlyList<RadarPulseProductRunSummary> ListRuns()
        => historyStore.ListRuns();

    /// <summary>
    /// Attempts to load one product run detail by run id.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetRun(
        string runId)
        => historyStore.TryGetRun(runId);

    /// <summary>
    /// Attempts to load the latest product run detail from history.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetLatestRun()
        => historyStore.TryGetLatestRun();

    /// <summary>
    /// Lists all provider batches captured for a product run.
    /// </summary>
    public RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductBatch>> ListBatches(
        string runId)
    {
        var run = TryGetRun(runId);
        return run.Found
            ? RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductBatch>>.FromValue(run.Value!.Batches)
            : RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductBatch>>.NotFound(run.Message);
    }

    /// <summary>
    /// Looks up one batch by provider sequence within a product run.
    /// </summary>
    /// <remarks>
    /// Provider sequence is the stable ordering key from the accepted ordered
    /// commit path, so this lookup does not depend on list position.
    /// </remarks>
    public RadarPulseProductQueryResult<RadarPulseProductBatch> TryGetBatch(
        string runId,
        long providerSequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(providerSequence);

        var run = TryGetRun(runId);
        if (!run.Found)
        {
            return RadarPulseProductQueryResult<RadarPulseProductBatch>.NotFound(run.Message);
        }

        foreach (var batch in run.Value!.Batches)
        {
            if (batch.ProviderSequence == providerSequence)
            {
                return RadarPulseProductQueryResult<RadarPulseProductBatch>.FromValue(batch);
            }
        }

        return RadarPulseProductQueryResult<RadarPulseProductBatch>.NotFound(
            $"Product run '{runId}' does not contain batch sequence {providerSequence}.");
    }

    /// <summary>
    /// Lists all processed source read models for a product run.
    /// </summary>
    public RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductSource>> ListSources(
        string runId)
    {
        var run = TryGetRun(runId);
        return run.Found
            ? RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductSource>>.FromValue(run.Value!.Sources)
            : RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductSource>>.NotFound(run.Message);
    }

    /// <summary>
    /// Looks up one processed source by dense source id within a product run.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductSource> TryGetSource(
        string runId,
        int sourceId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);

        var run = TryGetRun(runId);
        if (!run.Found)
        {
            return RadarPulseProductQueryResult<RadarPulseProductSource>.NotFound(run.Message);
        }

        foreach (var source in run.Value!.Sources)
        {
            if (source.Identity.SourceId == sourceId)
            {
                return RadarPulseProductQueryResult<RadarPulseProductSource>.FromValue(source);
            }
        }

        return RadarPulseProductQueryResult<RadarPulseProductSource>.NotFound(
            $"Product run '{runId}' does not contain source {sourceId}.");
    }

    /// <summary>
    /// Looks up one exported handler output field for a source in a product run.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductHandlerOutput> TryGetHandlerOutput(
        string runId,
        int sourceId,
        string fieldName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        var source = TryGetSource(runId, sourceId);
        if (!source.Found)
        {
            return RadarPulseProductQueryResult<RadarPulseProductHandlerOutput>.NotFound(source.Message);
        }

        foreach (var value in source.Value!.HandlerValues)
        {
            if (string.Equals(value.Name, fieldName, StringComparison.Ordinal))
            {
                return RadarPulseProductQueryResult<RadarPulseProductHandlerOutput>.FromValue(value);
            }
        }

        return RadarPulseProductQueryResult<RadarPulseProductHandlerOutput>.NotFound(
            $"Product run '{runId}' source {sourceId} does not contain handler field '{fieldName}'.");
    }

    /// <summary>
    /// Returns diagnostic evidence for a product run when diagnostics were captured.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductDiagnostics> TryGetDiagnostics(
        string runId)
    {
        var run = TryGetRun(runId);
        if (!run.Found)
        {
            return RadarPulseProductQueryResult<RadarPulseProductDiagnostics>.NotFound(run.Message);
        }

        return run.Value!.Diagnostics is { } diagnostics
            ? RadarPulseProductQueryResult<RadarPulseProductDiagnostics>.FromValue(diagnostics)
            : RadarPulseProductQueryResult<RadarPulseProductDiagnostics>.NotFound(
                $"Product run '{runId}' does not have diagnostics.");
    }

    /// <summary>
    /// Returns capacity and completeness evidence for a product run.
    /// </summary>
    public RadarPulseProductQueryResult<RadarPulseProductCapacityEvidence> TryGetCapacityEvidence(
        string runId)
    {
        var run = TryGetRun(runId);
        return run.Found
            ? RadarPulseProductQueryResult<RadarPulseProductCapacityEvidence>.FromValue(
                run.Value!.CapacityEvidence)
            : RadarPulseProductQueryResult<RadarPulseProductCapacityEvidence>.NotFound(run.Message);
    }

    /// <summary>
    /// Applies a product control action against recoverable durable pipeline state.
    /// </summary>
    /// <remarks>
    /// The method reconstructs the source universe and accepted handler/options
    /// contour from the request, then delegates to the production pipeline control
    /// coordinator. It returns the control summary in product vocabulary.
    /// </remarks>
    public async ValueTask<RadarPulseProductQueryResult<RadarPulseProductControlSummary>> ApplyControlAsync(
        RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRunId(request.RunId);
        if (string.IsNullOrWhiteSpace(request.DurableStorePath))
        {
            return RadarPulseProductQueryResult<RadarPulseProductControlSummary>.NotFound(
                "Product control requires a durable store path.");
        }

        var sourceCount = request.SourceCount;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceCount);
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);
        var partitionCount = ResolvePartitionCount(request.PartitionCount, sourceCount);
        var shardCount = ResolveShardCount(request.ShardCount, partitionCount);
        var recoveryRequest = new RadarProcessingProductionPipelineRecoveryRequest(
            request.RunId,
            universe,
            request.DurableStorePath,
            partitionCount,
            shardCount,
            CreateHandlers(request.HandlerSet),
            CreateProductionOptions(request.Options));
        var coordinator = new RadarProcessingProductionPipelineControlCoordinator();
        var result = request.Action switch
        {
            RadarPulseProductControlAction.StopAccepting =>
                coordinator.StopAccepting(recoveryRequest),
            RadarPulseProductControlAction.DrainAccepted =>
                await coordinator.DrainAcceptedAsync(recoveryRequest, cancellationToken)
                    .ConfigureAwait(false),
            RadarPulseProductControlAction.CancelOpenAndRelease =>
                coordinator.CancelOpenAndRelease(
                    recoveryRequest,
                    string.IsNullOrWhiteSpace(request.Message)
                        ? "Product control canceled open pipeline work."
                        : request.Message),
            RadarPulseProductControlAction.RejectUnsafeFallback =>
                coordinator.RejectUnsafeFallback(
                    recoveryRequest,
                    string.IsNullOrWhiteSpace(request.Message)
                        ? "Product control rejected unsafe fallback."
                        : request.Message),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };

        return RadarPulseProductQueryResult<RadarPulseProductControlSummary>.FromValue(
            RadarPulseProductPipelineMapper.ToProductControlSummary(result));
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
        historyStore.Store(detail);
        return detail;
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
