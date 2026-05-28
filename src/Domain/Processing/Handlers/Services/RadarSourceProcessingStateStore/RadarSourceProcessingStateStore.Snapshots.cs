namespace RadarPulse.Domain.Processing;

public sealed partial class RadarSourceProcessingStateStore
{
    /// <summary>
    /// Returns the processing snapshot for one source.
    /// </summary>
    public RadarSourceProcessingSnapshot GetSnapshot(int sourceId)
    {
        EnsureSourceId(sourceId);

        return new RadarSourceProcessingSnapshot(
            sourceId,
            activeSources[sourceId],
            processedEventCounts[sourceId],
            processedPayloadValueCounts[sourceId],
            rawValueChecksums[sourceId],
            lastMessageTimestampUtcTicks[sourceId],
            processingChecksums[sourceId]);
    }

    /// <summary>
    /// Creates processing snapshots for all sources in source-id order.
    /// </summary>
    public RadarSourceProcessingSnapshot[] CreateSnapshots()
    {
        var snapshots = new RadarSourceProcessingSnapshot[SourceCount];
        for (var sourceId = 0; sourceId < snapshots.Length; sourceId++)
        {
            snapshots[sourceId] = GetSnapshot(sourceId);
        }

        return snapshots;
    }

    /// <summary>
    /// Returns exported handler values for one source.
    /// </summary>
    public RadarSourceProcessingHandlerSnapshot GetHandlerSnapshot(int sourceId)
    {
        EnsureSourceId(sourceId);

        var values = new RadarSourceProcessingSnapshotValue[handlerSlotLayout.SnapshotFieldCount];
        var valueIndex = 0;
        foreach (var assignment in handlerSlotLayout.Assignments)
        {
            foreach (var field in assignment.Descriptor.SnapshotFields)
            {
                values[valueIndex++] = field.Type switch
                {
                    RadarSourceProcessingSnapshotFieldType.Int64 =>
                        RadarSourceProcessingSnapshotValue.FromInt64(
                            field.Name,
                            ReadInt64HandlerSlot(sourceId, assignment.Int64SlotOffset + field.SlotIndex)),
                    RadarSourceProcessingSnapshotFieldType.Double =>
                        RadarSourceProcessingSnapshotValue.FromDouble(
                            field.Name,
                            ReadDoubleHandlerSlot(sourceId, assignment.DoubleSlotOffset + field.SlotIndex)),
                    _ => throw new InvalidOperationException("Unsupported handler snapshot field type.")
                };
            }
        }

        return new RadarSourceProcessingHandlerSnapshot(sourceId, values);
    }

    /// <summary>
    /// Creates handler snapshots for all sources in source-id order.
    /// </summary>
    public RadarSourceProcessingHandlerSnapshot[] CreateHandlerSnapshots()
    {
        var snapshots = new RadarSourceProcessingHandlerSnapshot[SourceCount];
        for (var sourceId = 0; sourceId < snapshots.Length; sourceId++)
        {
            snapshots[sourceId] = GetHandlerSnapshot(sourceId);
        }

        return snapshots;
    }
}
