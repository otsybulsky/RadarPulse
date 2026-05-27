namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Runs one synthetic rebalance workload and returns per-batch evidence.
/// </summary>
public sealed class RadarProcessingSyntheticRebalanceWorkloadRunner
{
    /// <summary>
    /// Processes workload batches through a rebalance session.
    /// </summary>
    public RadarProcessingSyntheticRebalanceWorkloadResult Run(
        RadarProcessingSyntheticRebalanceWorkload workload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workload);

        var session = workload.CreateSession();
        var initialTopologyVersion = session.CurrentTopology.Version;
        var steps = new List<RadarPulse.Domain.Processing.RadarProcessingRebalanceSessionResult>(
            workload.Batches.Count);

        foreach (var batch in workload.Batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            steps.Add(session.Process(batch, cancellationToken));
        }

        return new RadarProcessingSyntheticRebalanceWorkloadResult(
            workload.Kind,
            workload.SourceCount,
            workload.PartitionCount,
            workload.ShardCount,
            initialTopologyVersion,
            session.CurrentTopology.Version,
            steps,
            session.QuarantineLifecycleTracker.Partitions);
    }
}
