using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingCore
{
    /// <summary>
    /// Computes a handler-free ordered concurrent processing delta without committing it.
    /// </summary>
    /// <remarks>
    /// The returned delta owns pooled arrays and must be disposed by the caller after commit or rejection.
    /// </remarks>
    public RadarProcessingBatchDelta ComputeProcessingDelta(
        RadarEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (Options.Handlers.Count != 0)
        {
            throw new NotSupportedException(
                "Ordered concurrent processing deltas require a handler-free processing core.");
        }

        var invalid = ValidateBatchForProcessing(batch, cancellationToken);
        if (invalid is not null)
        {
            throw new InvalidOperationException(invalid.Validation.Message);
        }

        var route = new RadarProcessingBatchRouter(Topology).Route(batch);
        return RadarProcessingBatchDelta.Create(batch, route, sourceUniverse.SourceCount);
    }

    /// <summary>
    /// Computes a processing delta for handler delta merge without mutating committed source state.
    /// </summary>
    public RadarProcessingBatchDelta ComputeProcessingDeltaForHandlerDeltaMerge(
        RadarEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var invalid = ValidateBatchForProcessing(batch, cancellationToken);
        if (invalid is not null)
        {
            throw new InvalidOperationException(invalid.Validation.Message);
        }

        var route = new RadarProcessingBatchRouter(Topology).Route(batch);
        return RadarProcessingBatchDelta.Create(batch, route, sourceUniverse.SourceCount);
    }

    /// <summary>
    /// Validates and commits a previously computed processing delta to source state.
    /// </summary>
    /// <returns>A valid committed result, or an invalid result when state validation rejects the delta.</returns>
    public RadarProcessingResult CommitProcessingDelta(
        RadarProcessingBatchDelta delta,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delta);
        cancellationToken.ThrowIfCancellationRequested();

        if (delta.Route.TopologyVersion != Topology.Version)
        {
            throw new InvalidOperationException(
                "Processing delta topology version must match the current processing topology.");
        }

        var invalid = stateStore.ValidateDeltaForCommit(
            delta,
            Options,
            Topology.Version,
            processedBatchCount);
        if (invalid is not null)
        {
            return invalid;
        }

        stateStore.ApplyDelta(delta);
        processedBatchCount = checked(processedBatchCount + 1);

        var telemetry = Options.ExecutionMode == RadarProcessingExecutionMode.Sequential
            ? null
            : RadarProcessingTelemetry.FromRoute(Options.ExecutionMode, delta.Route);
        return Valid(telemetry, workerTelemetry);
    }

    /// <summary>
    /// Validates a previously computed processing delta before ordered commit.
    /// </summary>
    public RadarProcessingResult? ValidateProcessingDeltaForCommit(
        RadarProcessingBatchDelta delta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delta);
        cancellationToken.ThrowIfCancellationRequested();

        if (delta.Route.TopologyVersion != Topology.Version)
        {
            throw new InvalidOperationException(
                "Processing delta topology version must match the current processing topology.");
        }

        return stateStore.ValidateDeltaForCommit(
            delta,
            Options,
            Topology.Version,
            processedBatchCount);
    }

    internal RadarProcessingResult CommitValidatedProcessingDeltaWithMergedHandlerValues(
        RadarProcessingBatchDelta delta,
        IReadOnlyList<RadarProcessingHandlerDeltaValue> mergedHandlerValues,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delta);
        ArgumentNullException.ThrowIfNull(mergedHandlerValues);
        cancellationToken.ThrowIfCancellationRequested();

        if (delta.Route.TopologyVersion != Topology.Version)
        {
            throw new InvalidOperationException(
                "Processing delta topology version must match the current processing topology.");
        }

        stateStore.ApplyDeltaWithoutHandlers(delta);
        stateStore.ApplyMergedHandlerValues(mergedHandlerValues);
        processedBatchCount = checked(processedBatchCount + 1);

        var telemetry = Options.ExecutionMode == RadarProcessingExecutionMode.Sequential
            ? null
            : RadarProcessingTelemetry.FromRoute(Options.ExecutionMode, delta.Route);
        return Valid(telemetry, workerTelemetry);
    }

    /// <summary>
    /// Commits a validated processing delta together with merged handler value groups.
    /// </summary>
    public RadarProcessingResult CommitValidatedProcessingDeltaWithMergedHandlerValueGroups(
        RadarProcessingBatchDelta delta,
        IReadOnlyList<IReadOnlyList<RadarProcessingHandlerDeltaValue>> mergedHandlerValueGroups,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delta);
        ArgumentNullException.ThrowIfNull(mergedHandlerValueGroups);
        cancellationToken.ThrowIfCancellationRequested();

        if (delta.Route.TopologyVersion != Topology.Version)
        {
            throw new InvalidOperationException(
                "Processing delta topology version must match the current processing topology.");
        }

        stateStore.ApplyDeltaWithoutHandlers(delta);
        stateStore.ApplyMergedHandlerValueGroups(mergedHandlerValueGroups);
        processedBatchCount = checked(processedBatchCount + 1);

        var telemetry = Options.ExecutionMode == RadarProcessingExecutionMode.Sequential
            ? null
            : RadarProcessingTelemetry.FromRoute(Options.ExecutionMode, delta.Route);
        return Valid(telemetry, workerTelemetry);
    }
}
