using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayPublisher
{
    private static void ValidateOptions(ArchiveReplayPublishOptions options)
    {
        if (options.DegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ArchiveReplayPublishOptions.DegreeOfParallelism),
                "Degree of parallelism must be greater than zero.");
        }
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

    private IReadOnlyList<ArchiveReplayWorker> CreateWorkers(
        int degreeOfParallelism,
        string radarId,
        DateTimeOffset volumeTimestamp)
    {
        var workers = new ArchiveReplayWorker[degreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveReplayWorker(
                decompressor.CreateSession(),
                radarId,
                volumeTimestamp);
        }

        return workers;
    }

    private static void RunParallelRecordPass(
        IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> records,
        SafeFileHandle fileHandle,
        ConcurrentStack<ArchiveReplayWorker> availableWorkers,
        ParallelOptions options,
        Action<ArchiveReplayWorker, ArchiveTwoCompressedRecordDescriptor, byte[]> processRecord)
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

    private static void ScheduleBufferedRecord(
        IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> records,
        IReadOnlyList<ArchiveTwoGateMomentProjectorState> startingStatesByRecord,
        SafeFileHandle fileHandle,
        ConcurrentStack<ArchiveReplayWorker> availableWorkers,
        Dictionary<int, Task<ArchiveReplayBufferedRecord>> inFlight,
        int recordIndex,
        CancellationToken cancellationToken)
    {
        var record = records[recordIndex];
        inFlight.Add(
            recordIndex,
            Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
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

                        var events = new List<ArchiveTwoGateMomentEvent>();
                        worker.ResetProjection(events.Add, startingStatesByRecord[record.Index]);
                        var decompressedBytes = worker.ProjectRecordContinuing(
                            compressedPayloadBuffer,
                            record.CompressedSizeBytes,
                            record.Index + 1);
                        return new ArchiveReplayBufferedRecord(
                            CompressedRecordCount: 1,
                            record.CompressedSizeBytes,
                            decompressedBytes,
                            events);
                    }
                    finally
                    {
                        availableWorkers.Push(worker);
                    }
                },
                cancellationToken));
    }

    private ArchiveReplayPublishResult BuildOrderedResult(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveReplayRecordMeasurement> measurementsByRecord)
    {
        var accumulator = new ArchiveReplayEventAccumulator();
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;
        foreach (var measurement in measurementsByRecord)
        {
            compressedRecordCount += measurement.CompressedRecordCount;
            compressedBytes += measurement.CompressedBytes;
            decompressedBytes += measurement.DecompressedBytes;
            accumulator.AddOrdered(measurement.Accumulator);
        }

        return accumulator.BuildResult(
            fileInfo.FullName,
            decompressor.Name,
            degreeOfParallelism,
            fileInfo.Length,
            compressedRecordCount,
            compressedBytes,
            decompressedBytes);
    }

    private static ArchiveTwoGateMomentProjectorState[] BuildStartingProjectorStates(
        IReadOnlyList<ArchiveReplayRecordMetadata> metadataByRecord)
    {
        var states = new ArchiveTwoGateMomentProjectorState[metadataByRecord.Count];
        var state = default(ArchiveTwoGateMomentProjectorState);
        for (var i = 0; i < metadataByRecord.Count; i++)
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

        return states;
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

    private static void WaitForInFlightTasks(IEnumerable<Task<ArchiveReplayBufferedRecord>> tasks)
    {
        try
        {
            Task.WaitAll(tasks.ToArray());
        }
        catch
        {
            // The original task exception is observed by the ordered drain path.
        }
    }

}
