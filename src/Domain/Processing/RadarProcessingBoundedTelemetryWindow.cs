namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingBoundedTelemetryWindow<T>
    where T : class
{
    private readonly T?[] items;
    private int startIndex;
    private int count;

    public RadarProcessingBoundedTelemetryWindow(
        int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        Capacity = capacity;
        items = capacity == 0
            ? Array.Empty<T?>()
            : new T?[capacity];
    }

    public int Capacity { get; }

    public int Count => count;

    public long DroppedCount { get; private set; }

    public bool CanRetain => Capacity > 0;

    public void Add(
        T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Capacity == 0)
        {
            DroppedCount++;
            return;
        }

        if (count < Capacity)
        {
            var insertIndex = (startIndex + count) % Capacity;
            items[insertIndex] = item;
            count++;
            return;
        }

        items[startIndex] = item;
        startIndex = (startIndex + 1) % Capacity;
        DroppedCount++;
    }

    public void Drop()
    {
        DroppedCount++;
    }

    public IReadOnlyList<T> Snapshot()
    {
        if (count == 0)
        {
            return Array.Empty<T>();
        }

        var snapshot = new T[count];

        for (var index = 0; index < count; index++)
        {
            var sourceIndex = (startIndex + index) % Capacity;
            snapshot[index] = items[sourceIndex]!;
        }

        return Array.AsReadOnly(snapshot);
    }

    public void Clear()
    {
        Array.Clear(items);
        startIndex = 0;
        count = 0;
        DroppedCount = 0;
    }
}
