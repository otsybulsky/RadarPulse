using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveRadarEventBatchPublisher
{
    private static int EstimateInitialPayloadCapacity(FileInfo fileInfo)
    {
        var estimated = fileInfo.Length * EstimatedPayloadBytesPerCompressedByte;
        if (estimated <= 0)
        {
            return DefaultInitialPayloadCapacity;
        }

        estimated = Math.Max(estimated, DefaultInitialPayloadCapacity);
        return checked((int)Math.Min(estimated, MaxInitialPayloadCapacity));
    }

    private static int EstimateInitialEventCapacity(
        int? compressedRecordCount,
        int rangeBandCount,
        int initialPayloadCapacity)
    {
        long estimated = Math.Max(
            DefaultInitialEventCapacity,
            initialPayloadCapacity / EstimatedPayloadBytesPerStreamEvent);
        if (compressedRecordCount is { } recordCount)
        {
            estimated = Math.Max(
                estimated,
                (long)recordCount * Math.Max(rangeBandCount, 1) * EstimatedEventsPerCompressedRecord);
        }

        return checked((int)Math.Min(estimated, MaxInitialEventCapacity));
    }

    private static FileInfo GetExistingFileInfo(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        return fileInfo;
    }
}
