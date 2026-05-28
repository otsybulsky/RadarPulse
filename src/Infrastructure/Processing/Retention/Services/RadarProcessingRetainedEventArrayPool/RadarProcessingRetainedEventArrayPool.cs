using System.Buffers;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Array pool that retains a bounded number of large radar event arrays.
/// </summary>
/// <remarks>
/// Small arrays delegate to the fallback pool. Large arrays are retained by
/// size-bounded best fit so retained payload snapshots can reuse event buffers
/// without unbounded memory growth.
/// </remarks>
public sealed partial class RadarProcessingRetainedEventArrayPool : ArrayPool<RadarStreamEvent>
{
    /// <summary>
    /// Default threshold at which event arrays are retained by this pool.
    /// </summary>
    public const int DefaultLargeArrayThreshold = 16_384;
    /// <summary>
    /// Default maximum count of retained large event arrays.
    /// </summary>
    public const int DefaultMaxRetainedArrayCount = 8;
    /// <summary>
    /// Default maximum bytes retained by large event arrays.
    /// </summary>
    public const long DefaultMaxRetainedBytes = 128L * 1024L * 1024L;

    private readonly object sync = new();
    private readonly ArrayPool<RadarStreamEvent> fallback;
    private readonly List<RadarStreamEvent[]> retainedArrays = [];
    private long retainedBytes;
    private long rentCount;
    private long returnCount;
    private long missCount;

    /// <summary>
    /// Creates a retained event array pool with bounded large-array retention.
    /// </summary>
    public RadarProcessingRetainedEventArrayPool(
        ArrayPool<RadarStreamEvent>? fallback = null,
        int largeArrayThreshold = DefaultLargeArrayThreshold,
        int maxRetainedArrayCount = DefaultMaxRetainedArrayCount,
        long maxRetainedBytes = DefaultMaxRetainedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(largeArrayThreshold);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedArrayCount);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedBytes);

        this.fallback = fallback ?? ArrayPool<RadarStreamEvent>.Shared;
        LargeArrayThreshold = largeArrayThreshold;
        MaxRetainedArrayCount = maxRetainedArrayCount;
        MaxRetainedBytes = maxRetainedBytes;
    }

    /// <summary>
    /// Minimum requested event count retained by this pool instead of the fallback pool.
    /// </summary>
    public int LargeArrayThreshold { get; }

    /// <summary>
    /// Maximum number of retained large arrays.
    /// </summary>
    public int MaxRetainedArrayCount { get; }

    /// <summary>
    /// Maximum retained bytes across large arrays.
    /// </summary>
    public long MaxRetainedBytes { get; }
}
