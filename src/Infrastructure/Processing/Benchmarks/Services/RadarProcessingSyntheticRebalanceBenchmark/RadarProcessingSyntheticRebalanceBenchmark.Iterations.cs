using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceBenchmark
{
    private static ValueTask<IterationTelemetry> RunIterationAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        int orderedActiveBatchCapacity,
        CancellationToken cancellationToken) =>
        mode switch
        {
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance =>
                RunStaticIterationAsync(
                    workload,
                    executionMode,
                    asyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    cancellationToken),
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly =>
                RunPressureSamplingIterationAsync(
                    workload,
                    executionMode,
                    asyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    cancellationToken),
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession =>
                RunRebalanceSessionIterationAsync(
                    workload,
                    hardeningOptions,
                    executionMode,
                    asyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    cancellationToken),
            RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession =>
                RunOrderedRebalanceSessionIterationAsync(
                    workload,
                    hardeningOptions,
                    executionMode,
                    asyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    orderedActiveBatchCapacity,
                    cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    private static async ValueTask<IterationTelemetry> RunStaticIterationAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        CancellationToken cancellationToken)
    {
        var coreOptions = workload.CreateCoreOptions(executionMode, asyncExecution);
        var core = new RadarProcessingCore(workload.SourceUniverse, coreOptions);
        RadarProcessingAsyncCoreSession? asyncSession = null;
        try
        {
            asyncSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? CreateAsyncCoreSession(core, workerTelemetryRecorder, workerGroup)
                : null;
            foreach (var batch in workload.Batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = asyncSession is null
                    ? core.Process(batch, cancellationToken)
                    : await asyncSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
                EnsureValidProcessingResult(result);
            }
        }
        finally
        {
            if (asyncSession is not null)
            {
                await asyncSession.DisposeAsync().ConfigureAwait(false);
            }
        }

        return IterationTelemetry.FromMetrics(
            core.CreateMetrics(),
            topologyVersionCount: 1);
    }

    private static async ValueTask<IterationTelemetry> RunPressureSamplingIterationAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        CancellationToken cancellationToken)
    {
        var coreOptions = workload.CreateCoreOptions(executionMode, asyncExecution);
        var core = new RadarProcessingCore(workload.SourceUniverse, coreOptions);
        var pressureWindow = new RadarProcessingPressureWindow(workload.PressureWindowOptions);
        var evaluationCount = 0L;
        RadarProcessingAsyncCoreSession? asyncSession = null;

        try
        {
            asyncSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? CreateAsyncCoreSession(core, workerTelemetryRecorder, workerGroup)
                : null;
            foreach (var batch in workload.Batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = asyncSession is null
                    ? core.Process(batch, cancellationToken)
                    : await asyncSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
                EnsureValidProcessingResult(result);
                var telemetry = result.Telemetry ??
                                throw new InvalidDataException("Pressure sampling benchmark requires telemetry.");
                pressureWindow.AddSample(RadarProcessingPressureSample.FromTelemetry(telemetry, workload.PressureOptions));
                evaluationCount = checked(evaluationCount + 1);
            }
        }
        finally
        {
            if (asyncSession is not null)
            {
                await asyncSession.DisposeAsync().ConfigureAwait(false);
            }
        }

        return IterationTelemetry.FromMetrics(
            core.CreateMetrics(),
            topologyVersionCount: 1,
            rebalanceEvaluationCount: evaluationCount);
    }

    private static async ValueTask<IterationTelemetry> RunRebalanceSessionIterationAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        CancellationToken cancellationToken)
    {
        var session = workload.CreateSession(hardeningOptions, executionMode, asyncExecution);
        var initialTopologyVersion = session.CurrentTopology.Version;
        var telemetry = IterationTelemetry.Empty;
        RadarProcessingAsyncRebalanceSession? asyncSession = null;

        try
        {
            asyncSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? new RadarProcessingAsyncRebalanceSession(
                    session,
                    CreateAsyncCoreSession(session.Core, workerTelemetryRecorder, workerGroup),
                    ownsAsyncCoreSession: true)
                : null;
            foreach (var batch in workload.Batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = asyncSession is null
                    ? session.Process(batch, cancellationToken)
                    : await asyncSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
                telemetry = telemetry.Add(result);
            }
        }
        finally
        {
            if (asyncSession is not null)
            {
                await asyncSession.DisposeAsync().ConfigureAwait(false);
            }
        }

        var metrics = session.Core.CreateMetrics();
        return telemetry.WithMetrics(
            metrics,
            session.CurrentTopology.Version.Value - initialTopologyVersion.Value + 1);
    }

    private static RadarProcessingAsyncCoreSession CreateAsyncCoreSession(
        RadarProcessingCore core,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup) =>
        workerGroup is null
            ? new RadarProcessingAsyncCoreSession(core, workerTelemetryRecorder)
            : new RadarProcessingAsyncCoreSession(
                core,
                workerGroup,
                workerTelemetryRecorder,
                ownsWorkerGroup: false);

    private static void EnsureValidProcessingResult(RadarProcessingResult result)
    {
        if (!result.IsValid)
        {
            throw new InvalidDataException(result.Validation.Message);
        }

        if (result.Telemetry is null)
        {
            throw new InvalidDataException("Synthetic rebalance benchmark requires partitioned telemetry.");
        }

        if (result.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            var asyncValidation = RadarProcessingAsyncValidator.ValidateProcessingResult(
                result,
                RadarProcessingValidationProfile.Benchmark);
            if (!asyncValidation.IsValid)
            {
                throw new InvalidDataException(asyncValidation.Message);
            }
        }
    }
}
