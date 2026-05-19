using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingAsyncCoreSession : IAsyncDisposable
{
    private readonly RadarProcessingCore core;
    private readonly RadarProcessingAsyncWorkerGroup workerGroup;
    private readonly RadarProcessingWorkerTelemetryRecorder workerTelemetryRecorder;
    private readonly RadarProcessingAsyncCompletionAggregator completionAggregator = new();
    private readonly bool ownsWorkerGroup;
    private int started;
    private int disposed;

    public RadarProcessingAsyncCoreSession(
        RadarProcessingCore core,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder = null)
        : this(
            core,
            CreateOwnedWorkerGroup(core),
            workerTelemetryRecorder,
            ownsWorkerGroup: true)
    {
    }

    public RadarProcessingAsyncCoreSession(
        RadarProcessingCore core,
        RadarProcessingAsyncWorkerGroup workerGroup,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder = null,
        bool ownsWorkerGroup = false)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(workerGroup);

        if (core.Options.ExecutionMode != RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new ArgumentException(
                "Async core session requires async shard transport core options.",
                nameof(core));
        }

        this.core = core;
        this.workerGroup = workerGroup;
        this.workerTelemetryRecorder = workerTelemetryRecorder ?? new RadarProcessingWorkerTelemetryRecorder();
        this.ownsWorkerGroup = ownsWorkerGroup;
    }

    public RadarProcessingCore Core => core;

    public RadarProcessingAsyncWorkerGroup WorkerGroup => workerGroup;

    public RadarProcessingWorkerTelemetryRecorder WorkerTelemetryRecorder => workerTelemetryRecorder;

    public async ValueTask<RadarProcessingResult> ProcessAsync(
        RadarEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(batch);

        var invalid = core.ValidateBatchForProcessing(batch, cancellationToken);
        if (invalid is not null)
        {
            return ValidateAsyncResult(invalid);
        }

        EnsureWorkerGroupStarted();

        RadarProcessingResult? firstInvalidWorkResult = null;
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(workerGroup, () => core.Topology);
        var dispatchStarted = Stopwatch.GetTimestamp();
        var dispatchResult = await dispatcher.DispatchAsync(
            core.CreateMetrics().ProcessedBatchCount + 1,
            batch,
            (borrowedBatch, route, workItem, workCancellationToken) =>
            {
                var completion = core.ProcessAsyncShardWorkItem(
                    borrowedBatch,
                    route,
                    workItem,
                    workCancellationToken,
                    out var invalidWorkResult);
                if (invalidWorkResult is not null)
                {
                    Interlocked.CompareExchange(ref firstInvalidWorkResult, invalidWorkResult, null);
                }

                return ValueTask.FromResult(completion);
            },
            cancellationToken).ConfigureAwait(false);
        var dispatchTime = Stopwatch.GetElapsedTime(dispatchStarted);

        var aggregationStarted = Stopwatch.GetTimestamp();
        var aggregation = completionAggregator.Aggregate(dispatchResult);
        var aggregationTime = Stopwatch.GetElapsedTime(aggregationStarted);
        workerTelemetryRecorder.RecordDispatch(dispatchResult, dispatchTime, aggregationTime);
        var workerTelemetry = workerTelemetryRecorder.CreateSummary();

        if (firstInvalidWorkResult is not null)
        {
            return ValidateAsyncResult(CreateInvalidResultWithCurrentMetrics(firstInvalidWorkResult, workerTelemetry));
        }

        if (!aggregation.IsSuccess)
        {
            return ValidateAsyncResult(aggregation
                .CreateProcessingResult(core.CreateMetrics())
                .WithWorkerTelemetry(workerTelemetry));
        }

        return ValidateAsyncResult(core.CompleteAsyncBatch(aggregation.Telemetry!, workerTelemetry));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        if (ownsWorkerGroup)
        {
            await workerGroup.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void EnsureWorkerGroupStarted()
    {
        if (Volatile.Read(ref started) != 0)
        {
            return;
        }

        if (Interlocked.Exchange(ref started, 1) != 0)
        {
            return;
        }

        if (workerGroup.Status.State == RadarProcessingWorkerGroupState.NotStarted)
        {
            var startedResult = workerGroup.Start();
            if (!startedResult.IsSuccess)
            {
                throw new InvalidOperationException("Async worker group could not be started.");
            }
        }

        if (!workerGroup.Status.CanAcceptDispatch)
        {
            throw new InvalidOperationException("Async worker group cannot accept dispatch.");
        }
    }

    private static RadarProcessingAsyncWorkerGroup CreateOwnedWorkerGroup(
        RadarProcessingCore core)
    {
        ArgumentNullException.ThrowIfNull(core);

        return new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(core.Options.AsyncExecution));
    }

    private RadarProcessingResult CreateInvalidResultWithCurrentMetrics(
        RadarProcessingResult invalidResult,
        RadarProcessingWorkerTelemetrySummary workerTelemetry)
    {
        var metrics = core.CreateMetrics();
        return new RadarProcessingResult(
            invalidResult.ExecutionMode,
            invalidResult.PartitionCount,
            invalidResult.ShardCount,
            metrics,
            RadarProcessingValidationResult.Invalid(
                invalidResult.Validation.Error,
                invalidResult.Validation.SourceId,
                invalidResult.Validation.EventIndex,
                invalidResult.Validation.Message,
                metrics,
                invalidResult.Validation.ExpectedMetrics),
            invalidResult.Telemetry,
            invalidResult.TopologyVersion,
            workerTelemetry);
    }

    private static RadarProcessingResult ValidateAsyncResult(
        RadarProcessingResult result)
    {
        var validation = RadarProcessingAsyncValidator.ValidateProcessingResult(
            result,
            RadarProcessingValidationProfile.Essential);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return result;
    }
}
