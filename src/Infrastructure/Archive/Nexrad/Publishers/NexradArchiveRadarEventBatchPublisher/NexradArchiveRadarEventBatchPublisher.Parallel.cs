using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveRadarEventBatchPublisher
{
    private ArchiveRadarEventBatchPublishResult PublishFileParallel(
        FileInfo fileInfo,
        IArchiveRadarEventBatchPublisher publisher,
        ArchiveRadarEventBatchPublishOptions options,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var countingPublisher = publisher as ArchiveRadarEventBatchCountingPublisher ??
            new ArchiveRadarEventBatchCountingPublisher(publisher);
        var initialPayloadCapacity = EstimateInitialPayloadCapacity(fileInfo);
        var projector = new ArchiveTwoRadarEventBatchProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            options.SourceUniverse,
            EstimateInitialEventCapacity(
                records.Count,
                options.SourceUniverse.RangeBandCount,
                initialPayloadCapacity),
            initialPayloadCapacity);
        var scanner = new ArchiveTwoMessageStreamScanner(projector);
        var workers = CreateWorkers(options.DegreeOfParallelism);
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;
        var inFlight = new Dictionary<int, Task<ArchiveRadarEventBatchDecompressedRecord>>();
        var availableWorkers = new ConcurrentStack<ArchiveRadarEventBatchWorker>(workers);

        try
        {
            using var fileHandle = File.OpenHandle(
                fileInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileOptions.RandomAccess);
            var nextRecordToSchedule = 0;

            while (nextRecordToSchedule < records.Count &&
                inFlight.Count < options.DegreeOfParallelism)
            {
                ScheduleDecompressedRecord(
                    records,
                    fileHandle,
                    availableWorkers,
                    inFlight,
                    nextRecordToSchedule,
                    cancellationToken);
                nextRecordToSchedule++;
            }

            for (var recordIndex = 0; recordIndex < records.Count; recordIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var recordResult = inFlight[recordIndex].GetAwaiter().GetResult();
                inFlight.Remove(recordIndex);

                try
                {
                    compressedRecordCount += recordResult.CompressedRecordCount;
                    compressedBytes += recordResult.CompressedBytes;
                    decompressedBytes += recordResult.DecompressedBytes;
                    scanner.Reset(recordIndex + 1);
                    scanner.Append(recordResult.DecompressedPayload);
                    scanner.Complete();
                }
                finally
                {
                    recordResult.Dispose();
                    availableWorkers.Push(recordResult.Worker);
                }

                if (nextRecordToSchedule < records.Count)
                {
                    ScheduleDecompressedRecord(
                        records,
                        fileHandle,
                        availableWorkers,
                        inFlight,
                        nextRecordToSchedule,
                        cancellationToken);
                    nextRecordToSchedule++;
                }
            }

            var batch = projector.BuildBatch();
            if (batch.EventCount > 0)
            {
                countingPublisher.Publish(batch, cancellationToken);
            }

            return countingPublisher.BuildResult(
                fileInfo.FullName,
                decompressor.Name,
                options.DegreeOfParallelism,
                fileInfo.Length,
                compressedRecordCount,
                compressedBytes,
                decompressedBytes,
                projector.DictionarySnapshot);
        }
        finally
        {
            WaitForInFlightTasks(inFlight.Values, availableWorkers);
            foreach (var worker in workers)
            {
                worker.Dispose();
            }
        }
    }

    private IReadOnlyList<ArchiveRadarEventBatchWorker> CreateWorkers(int degreeOfParallelism)
    {
        var workers = new ArchiveRadarEventBatchWorker[degreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveRadarEventBatchWorker(decompressor.CreateSession());
        }

        return workers;
    }

    private static void ScheduleDecompressedRecord(
        IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> records,
        SafeFileHandle fileHandle,
        ConcurrentStack<ArchiveRadarEventBatchWorker> availableWorkers,
        Dictionary<int, Task<ArchiveRadarEventBatchDecompressedRecord>> inFlight,
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
                        throw new InvalidOperationException("No archive radar event batch worker was available.");
                    }

                    try
                    {
                        return worker.DecompressRecord(record, fileHandle);
                    }
                    catch
                    {
                        availableWorkers.Push(worker);
                        throw;
                    }
                },
                cancellationToken));
    }

    private static void WaitForInFlightTasks(
        IEnumerable<Task<ArchiveRadarEventBatchDecompressedRecord>> tasks,
        ConcurrentStack<ArchiveRadarEventBatchWorker> availableWorkers)
    {
        foreach (var task in tasks.ToArray())
        {
            try
            {
                var record = task.GetAwaiter().GetResult();
                record.Dispose();
                availableWorkers.Push(record.Worker);
            }
            catch
            {
                // The ordered drain path observes the original task exception.
            }
        }
    }
}
