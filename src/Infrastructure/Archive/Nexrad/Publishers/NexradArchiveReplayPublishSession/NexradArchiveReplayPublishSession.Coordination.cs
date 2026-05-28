using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayPublishSession
{
    private void EnsureRecordCapacity(int recordCount)
    {
        if (metadataByRecord.Length < recordCount)
        {
            Array.Resize(ref metadataByRecord, recordCount);
            Array.Resize(ref startingStatesByRecord, recordCount);
            Array.Resize(ref accumulatorsByRecord, recordCount);
            Array.Resize(ref compressedRecordCountsByRecord, recordCount);
            Array.Resize(ref compressedBytesByRecord, recordCount);
            Array.Resize(ref decompressedBytesByRecord, recordCount);
        }

        for (var i = 0; i < recordCount; i++)
        {
            accumulatorsByRecord[i] ??= new ArchiveReplayEventAccumulator();
            compressedRecordCountsByRecord[i] = 0;
            compressedBytesByRecord[i] = 0;
            decompressedBytesByRecord[i] = 0;
        }
    }

    private static void RunParallelRecordPass(
        IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> records,
        SafeFileHandle fileHandle,
        ConcurrentStack<ArchiveReplaySessionWorker> availableWorkers,
        ParallelOptions options,
        Action<ArchiveReplaySessionWorker, ArchiveTwoCompressedRecordDescriptor, byte[]> processRecord)
    {
        try
        {
            Parallel.For(
                0,
                records.Count,
                options,
                recordIndex =>
                {
                    options.CancellationToken.ThrowIfCancellationRequested();
                    var record = records[recordIndex];
                    if (!availableWorkers.TryPop(out var worker))
                    {
                        throw new InvalidOperationException("No archive replay worker was available.");
                    }

                    try
                    {
                        var compressedPayloadBuffer = worker.EnsureCompressedPayloadBuffer(record.CompressedSizeBytes);
                        ArchiveTwoFileReader.ReadExactly(
                            fileHandle,
                            compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                            record.PayloadOffset);
                        ArchiveTwoFileReader.ValidateBZip2Signature(
                            compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                            record.ControlWordOffset);

                        processRecord(worker, record, compressedPayloadBuffer);
                    }
                    finally
                    {
                        availableWorkers.Push(worker);
                    }
                });
        }
        catch (AggregateException ex)
        {
            ThrowSingleInnerExceptionWhenUseful(ex);
            throw;
        }
    }

    private ArchiveReplayPublishResult BuildOrderedResult(FileInfo fileInfo, int recordCount)
    {
        totalAccumulator.Reset();
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;

        for (var i = 0; i < recordCount; i++)
        {
            compressedRecordCount += compressedRecordCountsByRecord[i];
            compressedBytes += compressedBytesByRecord[i];
            decompressedBytes += decompressedBytesByRecord[i];
            totalAccumulator.AddOrdered(accumulatorsByRecord[i]);
        }

        return totalAccumulator.BuildResult(
            fileInfo.FullName,
            decompressor.Name,
            DegreeOfParallelism,
            fileInfo.Length,
            compressedRecordCount,
            compressedBytes,
            decompressedBytes);
    }

    private static void BuildStartingProjectorStates(
        IReadOnlyList<ArchiveReplayRecordMetadata> metadataByRecord,
        Span<ArchiveTwoGateMomentProjectorState> states,
        int recordCount)
    {
        var state = default(ArchiveTwoGateMomentProjectorState);
        for (var i = 0; i < recordCount; i++)
        {
            states[i] = state;
            foreach (var radial in metadataByRecord[i].Radials)
            {
                state = ArchiveTwoGateMomentEventProjector.AdvanceState(
                    state,
                    radial.RadialStatus,
                    radial.ElevationNumber,
                    out _);
            }
        }
    }

    private static void ThrowSingleInnerExceptionWhenUseful(AggregateException exception)
    {
        var flattened = exception.Flatten();
        if (flattened.InnerExceptions.Count != 1)
        {
            return;
        }

        var innerException = flattened.InnerExceptions[0];
        if (innerException is InvalidDataException or IOException)
        {
            ExceptionDispatchInfo.Capture(innerException).Throw();
        }
    }

    private static bool MatchesRadar(FileInfo fileInfo, string? radarId)
    {
        if (radarId is null)
        {
            return true;
        }

        return fileInfo.Name.StartsWith(radarId, StringComparison.OrdinalIgnoreCase) ||
            fileInfo.DirectoryName?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => string.Equals(segment, radarId, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool MatchesDate(FileInfo fileInfo, DateOnly? date)
    {
        if (date is null)
        {
            return true;
        }

        return TryReadDateFromFileName(fileInfo.Name, out var fileNameDate) && fileNameDate == date ||
            PathContainsDate(fileInfo.FullName, date.Value);
    }

    private static bool TryReadDateFromFileName(string fileName, out DateOnly date)
    {
        date = default;
        if (fileName.Length < 12)
        {
            return false;
        }

        var dateText = fileName.AsSpan(4, 8);
        if (!int.TryParse(dateText[..4], out var year) ||
            !int.TryParse(dateText.Slice(4, 2), out var month) ||
            !int.TryParse(dateText.Slice(6, 2), out var day))
        {
            return false;
        }

        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool PathContainsDate(string path, DateOnly date)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i <= segments.Length - 3; i++)
        {
            if (string.Equals(segments[i], date.Year.ToString("0000"), StringComparison.Ordinal) &&
                string.Equals(segments[i + 1], date.Month.ToString("00"), StringComparison.Ordinal) &&
                string.Equals(segments[i + 2], date.Day.ToString("00"), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
}
