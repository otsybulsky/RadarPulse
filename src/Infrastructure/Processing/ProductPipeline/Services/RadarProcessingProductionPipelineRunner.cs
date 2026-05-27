using RadarPulse.Application.Archive;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingProductionPipelineRunner
{
    private readonly RadarProcessingArchiveQueuedOverlapRunner runtimeRunner;

    public RadarProcessingProductionPipelineRunner(
        RadarProcessingArchiveQueuedOverlapRunner? runtimeRunner = null)
    {
        this.runtimeRunner = runtimeRunner ?? new RadarProcessingArchiveQueuedOverlapRunner();
    }

    public async ValueTask<RadarProcessingProductionPipelineRunResult> RunAsync(
        RadarProcessingProductionPipelineRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var configuration = RadarProcessingProductionPipelineProfile.Resolve(request.Options);
        if (!configuration.IsValid)
        {
            var blockedSummary = RadarProcessingProductionPipelineOperatorSummary.Create(
                configuration,
                RadarProcessingProductionPipelineRunState.Blocked,
                processingComplete: false);
            return new RadarProcessingProductionPipelineRunResult(
                request.RunId,
                configuration,
                blockedSummary,
                request.ReadModelStore,
                message: blockedSummary.FirstBlockingReason);
        }

        var core = CreateCore(request, configuration);
        try
        {
            var runtimeResult = await runtimeRunner.RunMvpProcessingAsync(
                    (publisher, token) => PublishBatches(request, publisher, token),
                    core,
                    configuration.CreateOrderedConcurrencyOptions(),
                    configuration.CreateQueuedOverlapOptions(),
                    cancellationToken)
                .ConfigureAwait(false);

            var durableReadiness = CreateReadiness(runtimeResult.OverlapResult);
            var processingComplete = ProcessingCompletenessPassed(runtimeResult.OverlapResult);
            var handlerMode = DetermineHandlerMode(runtimeResult.Plan);
            var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
                configuration,
                DetermineRunState(runtimeResult.OverlapResult),
                durableReadiness,
                retainedPressure: runtimeResult.OverlapResult.RetainedResourcePressure,
                processingComplete: processingComplete,
                handlerMode: handlerMode,
                warnings: CreateWarnings(runtimeResult));
            var readModel = RadarProcessingRunReadModelBuilder.FromCore(
                request.RunId,
                request.SourceUniverse,
                core,
                runtimeResult.OverlapResult.Consumer.SessionResult,
                durableReadiness,
                summary.Warnings,
                runtimeResult.OverlapResult.QueueTelemetry);
            request.ReadModelStore.Publish(readModel);

            return new RadarProcessingProductionPipelineRunResult(
                request.RunId,
                configuration,
                summary,
                request.ReadModelStore,
                runtimeResult,
                readModel,
                runtimeResult.OverlapResult.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
                configuration,
                RadarProcessingProductionPipelineRunState.Canceled,
                processingComplete: false);
            return new RadarProcessingProductionPipelineRunResult(
                request.RunId,
                configuration,
                summary,
                request.ReadModelStore,
                message: "Production pipeline run was canceled.");
        }
        catch (NotSupportedException exception)
        {
            var summary = RadarProcessingProductionPipelineOperatorSummary.Create(
                configuration,
                RadarProcessingProductionPipelineRunState.Blocked,
                processingComplete: false,
                hasHandlerConflict: true,
                handlerBlockingReason: exception.Message);
            return new RadarProcessingProductionPipelineRunResult(
                request.RunId,
                configuration,
                summary,
                request.ReadModelStore,
                message: exception.Message);
        }
    }

    private static RadarProcessingCore CreateCore(
        RadarProcessingProductionPipelineRunRequest request,
        RadarProcessingProductionPipelineResolvedConfiguration configuration) =>
        new(
            request.SourceUniverse,
            new RadarProcessingCoreOptions(
                configuration.ExecutionMode.Value,
                request.PartitionCount,
                request.ShardCount,
                enableValidation: true,
                request.Handlers,
                configuration.CreateAsyncExecution()));

    private static ArchiveRadarEventBatchPublishResult PublishBatches(
        RadarProcessingProductionPipelineRunRequest request,
        IArchiveRadarEventBatchPublisher publisher,
        CancellationToken cancellationToken)
    {
        long eventCount = 0;
        long payloadBytes = 0;
        long payloadValueCount = 0;
        long rawValueChecksum = 0;

        foreach (var batch in request.Batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateBatchUniverse(request, batch);
            publisher.Publish(batch, cancellationToken);
            eventCount = checked(eventCount + batch.EventCount);
            payloadBytes = checked(payloadBytes + batch.PayloadLength);
            if (batch.TryGetPayloadMetrics(out var batchPayloadValueCount, out var batchRawValueChecksum))
            {
                payloadValueCount = checked(payloadValueCount + batchPayloadValueCount);
                rawValueChecksum = checked(rawValueChecksum + batchRawValueChecksum);
            }
            else
            {
                payloadValueCount = checked(payloadValueCount + batch.PayloadLength);
                rawValueChecksum = checked(rawValueChecksum + ComputePayloadChecksum(batch.Payload.Span));
            }
        }

        return new ArchiveRadarEventBatchPublishResult(
            FilePath: $"production-pipeline:{request.RunId}",
            Decompressor: "production-pipeline",
            DegreeOfParallelism: 1,
            FileSizeBytes: payloadBytes,
            CompressedRecordCount: checked((int)request.Batches.Count),
            CompressedBytes: payloadBytes,
            DecompressedBytes: payloadBytes,
            StreamSchemaVersion: StreamSchemaVersion.Current,
            DictionaryVersion: DictionaryVersion.Initial,
            SourceUniverseVersion: request.SourceUniverse.Version,
            BatchCount: request.Batches.Count,
            EventCount: eventCount,
            PayloadBytes: payloadBytes,
            PayloadValueCount: payloadValueCount,
            RawValueChecksum: rawValueChecksum,
            DictionarySnapshot: new RadarStreamIdentityNormalizer(request.SourceUniverse)
                .CreateDictionarySnapshot(DictionaryVersion.Initial));
    }

    private static void ValidateBatchUniverse(
        RadarProcessingProductionPipelineRunRequest request,
        RadarEventBatch batch)
    {
        if (batch.SourceUniverseVersion != request.SourceUniverse.Version)
        {
            throw new ArgumentException(
                "Production pipeline batch source universe must match the request source universe.",
                nameof(request));
        }
    }

    private static long ComputePayloadChecksum(
        ReadOnlySpan<byte> payload)
    {
        long checksum = 0;
        for (var i = 0; i < payload.Length; i++)
        {
            checksum = unchecked((checksum * 31) + payload[i]);
        }

        return checksum;
    }

    private static RadarProcessingDurableRuntimeReadinessSummary CreateReadiness(
        RadarProcessingArchiveQueuedOverlapResult result)
    {
        var pressure = result.RetainedResourcePressure;
        return new RadarProcessingDurableRuntimeReadinessSummary(
            releaseFailureCount: result.ProviderResult.RetentionTelemetry.ReleaseFailedCount,
            terminalRetainedEnvelopeCount: pressure.CurrentCombinedRetainedBatchCount,
            terminalRetainedPayloadBytes: pressure.CurrentCombinedRetainedPayloadBytes);
    }

    private static bool ProcessingCompletenessPassed(
        RadarProcessingArchiveQueuedOverlapResult result)
    {
        var sessionResult = result.Consumer.SessionResult;
        var accepted = (long)sessionResult.EnqueueResults.Count(static enqueue => enqueue.IsAccepted);
        if (accepted == 0)
        {
            accepted = result.QueueTelemetry.EnqueuedBatchCount;
        }

        return result.IsCompleted &&
               sessionResult.IsCompleted &&
               accepted == sessionResult.ProcessingResults.Count(static processing => processing.IsSuccessful);
    }

    private static RadarProcessingProductionPipelineRunState DetermineRunState(
        RadarProcessingArchiveQueuedOverlapResult result)
    {
        if (result.IsCompleted)
        {
            return RadarProcessingProductionPipelineRunState.Completed;
        }

        if (result.IsCanceled)
        {
            return RadarProcessingProductionPipelineRunState.Canceled;
        }

        return result.IsFaulted
            ? RadarProcessingProductionPipelineRunState.Failed
            : RadarProcessingProductionPipelineRunState.Blocked;
    }

    private static RadarProcessingProductionPipelineHandlerMode DetermineHandlerMode(
        RadarProcessingMvpRuntimePlan plan)
    {
        if (plan.UsedSequentialFallback)
        {
            return RadarProcessingProductionPipelineHandlerMode.SnapshotSequential;
        }

        if (plan.AllowsOrderedConcurrentHandlerDeltaMerge)
        {
            return RadarProcessingProductionPipelineHandlerMode.MergeableDelta;
        }

        return RadarProcessingProductionPipelineHandlerMode.HandlerFree;
    }

    private static IReadOnlyList<string> CreateWarnings(
        RadarProcessingMvpRuntimeResult result)
    {
        if (result.Plan.UsedSequentialFallback)
        {
            return new[] { result.Plan.Message };
        }

        return Array.Empty<string>();
    }
}
