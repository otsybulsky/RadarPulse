namespace RadarPulse.Domain.Streaming;

public sealed partial class RadarEventBatchBuilder
{
    /// <summary>
    /// Ensures the builder can stage at least the requested number of events and payload bytes.
    /// </summary>
    public void EnsureCapacity(int eventCapacity, int payloadCapacity)
    {
        if (eventCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eventCapacity));
        }

        if (payloadCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadCapacity));
        }

        if (eventBuffer.Length < eventCapacity)
        {
            Array.Resize(ref eventBuffer, eventCapacity);
        }

        if (payloadBuffer.Length < payloadCapacity)
        {
            Array.Resize(ref payloadBuffer, payloadCapacity);
        }
    }

    /// <summary>
    /// Clears staged events and metrics while keeping the current buffer capacity for reuse.
    /// </summary>
    public void ResetRetainingCapacity()
    {
        eventCount = 0;
        payloadLength = 0;
        payloadValueCount = 0;
        rawValueChecksum = 0;
        dictionaryVersion = DictionaryVersion.Initial;
        sourceUniverseVersion = SourceUniverseVersion.Initial;
        hasSourceUniverseVersion = false;
    }

    private void EnsureEventCapacity()
    {
        if (eventCount < eventBuffer.Length)
        {
            return;
        }

        var newLength = eventBuffer.Length == 0 ? DefaultEventCapacity : checked(eventBuffer.Length * 2);
        Array.Resize(ref eventBuffer, newLength);
    }

    private void EnsurePayloadCapacity(int appendLength)
    {
        var requiredLength = checked(payloadLength + appendLength);
        if (requiredLength <= payloadBuffer.Length)
        {
            return;
        }

        var newLength = payloadBuffer.Length == 0 ? DefaultPayloadCapacity : payloadBuffer.Length;
        while (newLength < requiredLength)
        {
            newLength = checked(newLength * 2);
        }

        Array.Resize(ref payloadBuffer, newLength);
    }

    private void Reset()
    {
        eventBuffer = [];
        eventCount = 0;
        payloadBuffer = [];
        payloadLength = 0;
        payloadValueCount = 0;
        rawValueChecksum = 0;
        dictionaryVersion = DictionaryVersion.Initial;
        sourceUniverseVersion = SourceUniverseVersion.Initial;
        hasSourceUniverseVersion = false;
    }
}
