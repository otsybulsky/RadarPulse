using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingRetainedEventArrayPool
{
    /// <summary>
    /// Number of large arrays currently retained.
    /// </summary>
    public int RetainedArrayCount
    {
        get
        {
            lock (sync)
            {
                return retainedArrays.Count;
            }
        }
    }

    /// <summary>
    /// Total event slots currently retained by large arrays.
    /// </summary>
    public long RetainedEventCount
    {
        get
        {
            lock (sync)
            {
                return retainedBytes / RadarStreamEvent.SizeInBytes;
            }
        }
    }

    /// <summary>
    /// Total bytes currently retained by large arrays.
    /// </summary>
    public long RetainedBytes
    {
        get
        {
            lock (sync)
            {
                return retainedBytes;
            }
        }
    }

    /// <summary>
    /// Number of rent requests observed by this pool.
    /// </summary>
    public long RentCount
    {
        get
        {
            lock (sync)
            {
                return rentCount;
            }
        }
    }

    /// <summary>
    /// Number of arrays returned to this pool.
    /// </summary>
    public long ReturnCount
    {
        get
        {
            lock (sync)
            {
                return returnCount;
            }
        }
    }

    /// <summary>
    /// Number of large-array rents that missed retained buffers and allocated.
    /// </summary>
    public long MissCount
    {
        get
        {
            lock (sync)
            {
                return missCount;
            }
        }
    }
}
