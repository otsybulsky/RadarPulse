namespace RadarPulse.Domain.Processing;

/// <summary>
/// Fixed-capacity FIFO window for retained diagnostic telemetry detail.
/// </summary>
/// <remarks>
/// A zero-capacity window drops every entry while still counting drops. This lets
/// callers switch to counter-only diagnostics without losing evidence that detail
/// retention was intentionally disabled.
/// </remarks>
public sealed class RadarProcessingBoundedTelemetryWindow<T>
    where T : class
{
    private readonly T?[] items;
    private int startIndex;
    private int count;

    /// <summary>
    /// Creates a bounded telemetry window.
    /// </summary>
    public RadarProcessingBoundedTelemetryWindow(
        int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        Capacity = capacity;
        items = capacity == 0
            ? Array.Empty<T?>()
            : new T?[capacity];
    }

    /// <summary>
    /// Maximum retained item count.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Current retained item count.
    /// </summary>
    public int Count => count;

    /// <summary>
    /// Number of entries dropped because capacity was full or disabled.
    /// </summary>
    public long DroppedCount { get; private set; }

    /// <summary>
    /// Indicates whether this window retains detail entries.
    /// </summary>
    public bool CanRetain => Capacity > 0;

    /// <summary>
    /// Adds an item, evicting the oldest item when capacity is full.
    /// </summary>
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

    /// <summary>
    /// Records a dropped entry without retaining detail.
    /// </summary>
    public void Drop()
    {
        DroppedCount++;
    }

    /// <summary>
    /// Returns retained items in insertion order.
    /// </summary>
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

    /// <summary>
    /// Clears retained items and drop count.
    /// </summary>
    public void Clear()
    {
        Array.Clear(items);
        startIndex = 0;
        count = 0;
        DroppedCount = 0;
    }
}
