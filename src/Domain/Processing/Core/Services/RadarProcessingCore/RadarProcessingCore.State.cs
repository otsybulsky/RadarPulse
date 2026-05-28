namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingCore
{
    /// <summary>
    /// Gets the current processing snapshot for one source id.
    /// </summary>
    public RadarSourceProcessingSnapshot GetSourceSnapshot(int sourceId) =>
        stateStore.GetSnapshot(sourceId);

    /// <summary>
    /// Creates an ordered snapshot of every source state.
    /// </summary>
    public RadarSourceProcessingSnapshot[] CreateSourceSnapshots() =>
        stateStore.CreateSnapshots();

    /// <summary>
    /// Gets the current handler snapshot for one source id.
    /// </summary>
    public RadarSourceProcessingHandlerSnapshot GetSourceHandlerSnapshot(int sourceId) =>
        stateStore.GetHandlerSnapshot(sourceId);

    /// <summary>
    /// Creates an ordered snapshot of every source handler state.
    /// </summary>
    public RadarSourceProcessingHandlerSnapshot[] CreateSourceHandlerSnapshots() =>
        stateStore.CreateHandlerSnapshots();

    /// <summary>
    /// Creates cumulative metrics from the current source state and processed batch count.
    /// </summary>
    public RadarProcessingMetrics CreateMetrics() =>
        stateStore.CreateMetrics(processedBatchCount);

    /// <summary>
    /// Captures source state for a topology partition.
    /// </summary>
    public RadarProcessingPartitionStateSnapshot CapturePartitionState(
        RadarProcessingPartitionAssignment partition) =>
        RadarProcessingPartitionStateSnapshot.Capture(partition, stateStore);
}
