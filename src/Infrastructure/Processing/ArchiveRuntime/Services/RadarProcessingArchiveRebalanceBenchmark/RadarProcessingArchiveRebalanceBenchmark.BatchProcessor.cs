using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private sealed class ArchiveRebalanceBatchProcessor : IArchiveRadarEventBatchPublisher, IDisposable
    {
        private readonly RadarProcessingSyntheticRebalanceBenchmarkMode mode;
        private readonly RadarProcessingCore? core;
        private readonly RadarProcessingAsyncCoreSession? asyncCoreSession;
        private readonly RadarProcessingPressureWindow? pressureWindow;
        private readonly RadarProcessingRebalanceSession? rebalanceSession;
        private readonly RadarProcessingAsyncRebalanceSession? asyncRebalanceSession;
        private readonly RadarProcessingPressureSkewTransformer? pressureSkewTransformer;
        private readonly System.Diagnostics.Stopwatch processingStopwatch = new();
        private ArchiveIterationTelemetry telemetry = ArchiveIterationTelemetry.Empty;
        private long processingCallbackAllocatedBytes;
        private bool disposed;

        public ArchiveRebalanceBatchProcessor(
            RadarSourceUniverse sourceUniverse,
            RadarProcessingSyntheticRebalanceBenchmarkMode mode,
            int partitionCount,
            int shardCount,
            RadarProcessingRebalanceHardeningOptions hardeningOptions,
            RadarProcessingPressureSkewOptions pressureSkewOptions,
            RadarProcessingExecutionMode executionMode,
            RadarProcessingAsyncExecutionOptions? asyncExecution,
            RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
            RadarProcessingAsyncWorkerGroup? workerGroup)
        {
            ArgumentNullException.ThrowIfNull(hardeningOptions);
            ArgumentNullException.ThrowIfNull(pressureSkewOptions);

            this.mode = mode;
            pressureSkewTransformer = pressureSkewOptions.IsEnabled
                ? new RadarProcessingPressureSkewTransformer(pressureSkewOptions)
                : null;
            var coreOptions = new RadarProcessingCoreOptions(
                executionMode,
                partitionCount,
                shardCount,
                asyncExecution: asyncExecution);

            switch (mode)
            {
                case RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance:
                    core = new RadarProcessingCore(sourceUniverse, coreOptions);
                    asyncCoreSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                        ? CreateAsyncCoreSession(core, workerTelemetryRecorder, workerGroup)
                        : null;
                    break;
                case RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly:
                    core = new RadarProcessingCore(sourceUniverse, coreOptions);
                    asyncCoreSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                        ? CreateAsyncCoreSession(core, workerTelemetryRecorder, workerGroup)
                        : null;
                    pressureWindow = new RadarProcessingPressureWindow(
                        new RadarProcessingPressureWindowOptions(
                            sampleCapacity: 8,
                            minimumSampleCount: 1));
                    break;
                case RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession:
                    var rebalanceCore = new RadarProcessingCore(sourceUniverse, coreOptions);
                    rebalanceSession = new RadarProcessingRebalanceSession(
                        rebalanceCore,
                        pressureWindow: new RadarProcessingPressureWindow(
                            new RadarProcessingPressureWindowOptions(
                                sampleCapacity: 8,
                                minimumSampleCount: 1)),
                        policyState: new RadarProcessingRebalancePolicyState(
                            partitionCount,
                            shardCount,
                            new RadarProcessingRebalanceOptions(
                                budgetWindowEvaluationCount: 8,
                                globalMoveBudgetPerWindow: 1,
                                sourceShardMoveBudgetPerWindow: 1,
                                targetShardReceiveBudgetPerWindow: 1,
                                minimumPartitionResidencyEvaluations: 0,
                                partitionMoveCooldownEvaluations: 4,
                                sourceShardMoveCooldownEvaluations: 1,
                                targetShardReceiveCooldownEvaluations: 1)),
                        hardeningOptions: hardeningOptions,
                        pressureSkewOptions: pressureSkewOptions);
                    asyncRebalanceSession = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                        ? new RadarProcessingAsyncRebalanceSession(
                            rebalanceSession,
                            CreateAsyncCoreSession(rebalanceCore, workerTelemetryRecorder, workerGroup),
                            ownsAsyncCoreSession: true)
                        : null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public void Publish(RadarEventBatch batch, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
            processingStopwatch.Start();
            try
            {
                telemetry = mode switch
                {
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance =>
                        telemetry.Add(ProcessStatic(batch, cancellationToken)),
                    RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly =>
                        telemetry.Add(ProcessPressureSampling(batch, cancellationToken)),
                    RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession =>
                        telemetry.Add(ProcessRebalanceSession(batch, cancellationToken)),
                    _ => throw new ArgumentOutOfRangeException(nameof(mode))
                };
            }
            finally
            {
                processingStopwatch.Stop();
                processingCallbackAllocatedBytes = checked(
                    processingCallbackAllocatedBytes +
                    RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore));
            }
        }

        public ArchiveIterationTelemetry BuildTelemetry(
            RadarPulse.Domain.Archive.ArchiveRadarEventBatchPublishResult publishResult) =>
            telemetry.WithPublishResult(
                publishResult,
                processingStopwatch.Elapsed,
                processingCallbackAllocatedBytes)
                .WithRetentionStats(CreateRetentionStats());

        public ArchiveIterationTelemetry BuildTelemetry(
            CacheIterationTotals totals) =>
            telemetry.WithPublishTotals(
                totals,
                processingStopwatch.Elapsed,
                processingCallbackAllocatedBytes)
                .WithRetentionStats(CreateRetentionStats());

        private ArchiveIterationTelemetry ProcessStatic(
            RadarEventBatch batch,
            CancellationToken cancellationToken)
        {
            var candidateCore = core ?? throw new InvalidOperationException("Static processing core was not initialized.");
            var result = asyncCoreSession is null
                ? candidateCore.Process(batch, cancellationToken)
                : asyncCoreSession.ProcessAsync(batch, cancellationToken).AsTask().GetAwaiter().GetResult();
            EnsureValidProcessingResult(result);
            return ArchiveIterationTelemetry.FromMetrics(
                candidateCore.CreateMetrics(),
                topologyVersionCount: 1);
        }

        private ArchiveIterationTelemetry ProcessPressureSampling(
            RadarEventBatch batch,
            CancellationToken cancellationToken)
        {
            var candidateCore = core ?? throw new InvalidOperationException("Pressure sampling core was not initialized.");
            var candidatePressureWindow = pressureWindow ??
                                          throw new InvalidOperationException("Pressure window was not initialized.");
            var result = asyncCoreSession is null
                ? candidateCore.Process(batch, cancellationToken)
                : asyncCoreSession.ProcessAsync(batch, cancellationToken).AsTask().GetAwaiter().GetResult();
            EnsureValidProcessingResult(result);
            var telemetryResult = result.Telemetry ??
                                  throw new InvalidDataException("Archive pressure sampling requires telemetry.");
            var pressureSample = RadarProcessingPressureSample.FromTelemetry(telemetryResult);
            var effectivePressureSample = pressureSkewTransformer?.Apply(
                pressureSample,
                telemetry.RebalanceEvaluationCount + 1,
                candidatePressureWindow.Options) ?? pressureSample;
            candidatePressureWindow.AddSample(effectivePressureSample);
            return ArchiveIterationTelemetry.FromMetrics(
                candidateCore.CreateMetrics(),
                topologyVersionCount: 1,
                rebalanceEvaluationCount: 1);
        }

        private ArchiveIterationTelemetry ProcessRebalanceSession(
            RadarEventBatch batch,
            CancellationToken cancellationToken)
        {
            var session = rebalanceSession ??
                          throw new InvalidOperationException("Rebalance session was not initialized.");
            var initialTopologyVersion = session.CurrentTopology.Version;
            var result = asyncRebalanceSession is null
                ? session.Process(batch, cancellationToken)
                : asyncRebalanceSession.ProcessAsync(batch, cancellationToken).AsTask().GetAwaiter().GetResult();
            var metrics = session.Core.CreateMetrics();
            return ArchiveIterationTelemetry.FromRebalanceSessionResult(result)
                .WithMetrics(
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
                throw new InvalidDataException("Archive rebalance benchmark requires partitioned telemetry.");
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

        private RadarProcessingRebalanceRetentionStats CreateRetentionStats() =>
            rebalanceSession?.TelemetryRecorder.CreateSummary().RetentionStats ??
            new RadarProcessingRebalanceRetentionStats();

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (asyncRebalanceSession is not null)
            {
                asyncRebalanceSession.DisposeAsync().AsTask().GetAwaiter().GetResult();
                return;
            }

            if (asyncCoreSession is not null)
            {
                asyncCoreSession.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }
}
