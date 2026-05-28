namespace RadarPulse.Infrastructure.Processing;

internal sealed class RadarProcessingOwnedBatchQueueRetainedByteBudget(long? maxRetainedPayloadBytes)
{
    public bool HasCapacity(
        long pendingPayloadBytes,
        long payloadBytes)
    {
        if (!maxRetainedPayloadBytes.HasValue)
        {
            return true;
        }

        return pendingPayloadBytes <= maxRetainedPayloadBytes.Value - payloadBytes;
    }

    public string? TryCreateOversizedMessage(long payloadBytes)
    {
        if (!maxRetainedPayloadBytes.HasValue ||
            payloadBytes <= maxRetainedPayloadBytes.Value)
        {
            return null;
        }

        return $"Queued batch retained payload bytes {payloadBytes} exceed configured retained payload byte budget {maxRetainedPayloadBytes.Value}.";
    }

    public string CreateExhaustedMessage(
        long pendingPayloadBytes,
        long payloadBytes) =>
        maxRetainedPayloadBytes.HasValue
            ? $"Provider queue retained payload byte budget is exhausted. Pending retained payload bytes: {pendingPayloadBytes}; batch payload bytes: {payloadBytes}; budget: {maxRetainedPayloadBytes.Value}."
            : string.Empty;

    public string CreateTimedOutMessage(long payloadBytes) =>
        maxRetainedPayloadBytes.HasValue
            ? $"Timed out waiting for provider queue retained payload byte budget. Batch payload bytes: {payloadBytes}; budget: {maxRetainedPayloadBytes.Value}."
            : string.Empty;
}
