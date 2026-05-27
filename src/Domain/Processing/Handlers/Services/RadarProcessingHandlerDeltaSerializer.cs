namespace RadarPulse.Domain.Processing;

public static class RadarProcessingHandlerDeltaSerializer
{
    public static RadarProcessingSerializedHandlerDelta Serialize(
        RadarProcessingHandlerDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        return new RadarProcessingSerializedHandlerDelta(
            delta.SchemaVersion,
            delta.HandlerName,
            delta.HandlerContractVersion,
            delta.ProviderSequence,
            delta.DurableBatchId,
            delta.EventCount,
            delta.SourceCount,
            delta.PayloadValueCount,
            delta.InputChecksum,
            delta.DeltaId,
            delta.Values);
    }

    public static RadarProcessingHandlerDeltaSerializationResult Deserialize(
        RadarProcessingSerializedHandlerDelta serialized)
    {
        ArgumentNullException.ThrowIfNull(serialized);

        if (serialized.SchemaVersion != RadarProcessingHandlerDelta.CurrentSchemaVersion)
        {
            return RadarProcessingHandlerDeltaSerializationResult.Failed(
                $"Unsupported handler delta schema version {serialized.SchemaVersion}.");
        }

        try
        {
            return RadarProcessingHandlerDeltaSerializationResult.Succeeded(
                new RadarProcessingHandlerDelta(
                    serialized.HandlerName,
                    serialized.HandlerContractVersion,
                    serialized.ProviderSequence,
                    serialized.DurableBatchId,
                    serialized.EventCount,
                    serialized.SourceCount,
                    serialized.PayloadValueCount,
                    serialized.InputChecksum,
                    serialized.DeltaId,
                    serialized.Values,
                    serialized.SchemaVersion));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return RadarProcessingHandlerDeltaSerializationResult.Failed(exception.Message);
        }
    }
}
