using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveOrderedProcessingBenchmark
{
    private readonly struct OrderedProcessingIterationTelemetry
    {
        private OrderedProcessingIterationTelemetry(
            CacheIterationTotals totals,
            RadarProcessingArchiveQueuedOverlapResult result,
            RadarProcessingMetrics finalMetrics,
            RadarProcessingWorkerTelemetrySummary? workerTelemetry,
            long succeededBatchCount,
            long failedProcessingBatchCount,
            long failedValidationBatchCount,
            long canceledBatchCount,
            long skippedAfterFaultBatchCount,
            bool processingSucceeded)
        {
            Decompressor = result.Producer.PublishResult?.Decompressor ?? string.Empty;
            ExaminedFiles = totals.ExaminedFiles;
            SkippedFiles = totals.SkippedFiles;
            PublishedFiles = totals.PublishedFiles;
            FileSizeBytes = totals.FileSizeBytes;
            CompressedRecordCount = totals.CompressedRecordCount;
            CompressedBytes = totals.CompressedBytes;
            DecompressedBytes = totals.DecompressedBytes;
            BatchCount = totals.BatchCount;
            EventCount = totals.EventCount;
            PayloadBytes = totals.PayloadBytes;
            PayloadValueCount = totals.PayloadValueCount;
            RawValueChecksum = totals.RawValueChecksum;
            Status = result.Status;
            ConsumerStatus = result.Consumer.Status;
            SucceededBatchCount = succeededBatchCount;
            FailedProcessingBatchCount = failedProcessingBatchCount;
            FailedValidationBatchCount = failedValidationBatchCount;
            CanceledBatchCount = canceledBatchCount;
            SkippedAfterFaultBatchCount = skippedAfterFaultBatchCount;
            FinalProcessedBatchCount = finalMetrics.ProcessedBatchCount;
            FinalProcessedStreamEventCount = finalMetrics.ProcessedStreamEventCount;
            FinalProcessedPayloadValueCount = finalMetrics.ProcessedPayloadValueCount;
            FinalRawValueChecksum = finalMetrics.RawValueChecksum;
            FinalProcessingChecksum = finalMetrics.ProcessingChecksum;
            ProcessingSucceeded = processingSucceeded;
            QueueTelemetry = result.QueueTelemetry;
            OverlapTelemetry = result.OverlapTelemetry;
            RetainedPayloadPrewarm = result.RetainedPayloadPrewarm;
            WorkerTelemetry = workerTelemetry;
        }

        public string Decompressor { get; }
        public long ExaminedFiles { get; }
        public long SkippedFiles { get; }
        public long PublishedFiles { get; }
        public long FileSizeBytes { get; }
        public long CompressedRecordCount { get; }
        public long CompressedBytes { get; }
        public long DecompressedBytes { get; }
        public long BatchCount { get; }
        public long EventCount { get; }
        public long PayloadBytes { get; }
        public long PayloadValueCount { get; }
        public long RawValueChecksum { get; }
        public RadarProcessingArchiveQueuedOverlapStatus Status { get; }
        public RadarProcessingQueuedSessionStatus ConsumerStatus { get; }
        public long SucceededBatchCount { get; }
        public long FailedProcessingBatchCount { get; }
        public long FailedValidationBatchCount { get; }
        public long CanceledBatchCount { get; }
        public long SkippedAfterFaultBatchCount { get; }
        public long FinalProcessedBatchCount { get; }
        public long FinalProcessedStreamEventCount { get; }
        public long FinalProcessedPayloadValueCount { get; }
        public long FinalRawValueChecksum { get; }
        public ulong FinalProcessingChecksum { get; }
        public bool ProcessingSucceeded { get; }
        public RadarProcessingProviderQueueTelemetrySummary QueueTelemetry { get; }
        public RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry { get; }
        public RadarProcessingRetainedPayloadPrewarmResult RetainedPayloadPrewarm { get; }
        public RadarProcessingWorkerTelemetrySummary? WorkerTelemetry { get; }

        public static OrderedProcessingIterationTelemetry FromResult(
            CacheIterationTotals totals,
            RadarProcessingArchiveQueuedOverlapResult result)
        {
            var processingResults = result.Consumer.SessionResult.ProcessingResults;
            var succeeded = 0L;
            var failedProcessing = 0L;
            var failedValidation = 0L;
            var canceled = 0L;
            var skippedAfterFault = 0L;
            RadarProcessingResult? finalProcessing = null;

            foreach (var processingResult in processingResults)
            {
                switch (processingResult.Status)
                {
                    case RadarProcessingQueuedBatchProcessingStatus.Succeeded:
                        succeeded++;
                        break;
                    case RadarProcessingQueuedBatchProcessingStatus.FailedProcessing:
                    case RadarProcessingQueuedBatchProcessingStatus.FailedMigration:
                        failedProcessing++;
                        break;
                    case RadarProcessingQueuedBatchProcessingStatus.FailedValidation:
                        failedValidation++;
                        break;
                    case RadarProcessingQueuedBatchProcessingStatus.Canceled:
                        canceled++;
                        break;
                    case RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault:
                        skippedAfterFault++;
                        break;
                    default:
                        RadarProcessingQueuedBatchProcessingResult.EnsureKnownStatus(processingResult.Status);
                        throw new ArgumentOutOfRangeException(nameof(processingResults));
                }

                if (processingResult.ProcessingResult is not null)
                {
                    finalProcessing = processingResult.ProcessingResult;
                }
            }

            var finalMetrics = finalProcessing?.Metrics ?? RadarProcessingMetrics.Empty;
            var workerTelemetry = finalProcessing?.WorkerTelemetry;
            var processingSucceeded =
                result.IsCompleted &&
                failedProcessing == 0 &&
                failedValidation == 0 &&
                canceled == 0 &&
                skippedAfterFault == 0 &&
                result.QueueTelemetry.FailedBatchCount == 0 &&
                result.QueueTelemetry.CanceledBatchCount == 0 &&
                result.QueueTelemetry.SkippedAfterFaultCount == 0 &&
                result.ProviderResult.RetentionTelemetry.ReleaseFailedCount == 0 &&
                finalMetrics.ProcessedBatchCount == totals.BatchCount &&
                finalMetrics.ProcessedStreamEventCount == totals.EventCount &&
                finalMetrics.ProcessedPayloadValueCount == totals.PayloadValueCount &&
                finalMetrics.RawValueChecksum == totals.RawValueChecksum;

            return new OrderedProcessingIterationTelemetry(
                totals,
                result,
                finalMetrics,
                workerTelemetry,
                succeeded,
                failedProcessing,
                failedValidation,
                canceled,
                skippedAfterFault,
                processingSucceeded);
        }

        public bool HasSameStableTotals(OrderedProcessingIterationTelemetry other) =>
            Decompressor == other.Decompressor &&
            ExaminedFiles == other.ExaminedFiles &&
            SkippedFiles == other.SkippedFiles &&
            PublishedFiles == other.PublishedFiles &&
            FileSizeBytes == other.FileSizeBytes &&
            CompressedRecordCount == other.CompressedRecordCount &&
            CompressedBytes == other.CompressedBytes &&
            DecompressedBytes == other.DecompressedBytes &&
            BatchCount == other.BatchCount &&
            EventCount == other.EventCount &&
            PayloadBytes == other.PayloadBytes &&
            PayloadValueCount == other.PayloadValueCount &&
            RawValueChecksum == other.RawValueChecksum &&
            Status == other.Status &&
            ConsumerStatus == other.ConsumerStatus &&
            SucceededBatchCount == other.SucceededBatchCount &&
            FailedProcessingBatchCount == other.FailedProcessingBatchCount &&
            FailedValidationBatchCount == other.FailedValidationBatchCount &&
            CanceledBatchCount == other.CanceledBatchCount &&
            SkippedAfterFaultBatchCount == other.SkippedAfterFaultBatchCount &&
            FinalProcessedBatchCount == other.FinalProcessedBatchCount &&
            FinalProcessedStreamEventCount == other.FinalProcessedStreamEventCount &&
            FinalProcessedPayloadValueCount == other.FinalProcessedPayloadValueCount &&
            FinalRawValueChecksum == other.FinalRawValueChecksum &&
            FinalProcessingChecksum == other.FinalProcessingChecksum &&
            ProcessingSucceeded == other.ProcessingSucceeded;
    }

    private struct CacheIterationTotals
    {
        public static CacheIterationTotals Empty => new();

        public long ExaminedFiles;
        public long SkippedFiles;
        public long PublishedFiles;
        public long FileSizeBytes;
        public long CompressedRecordCount;
        public long CompressedBytes;
        public long DecompressedBytes;
        public long BatchCount;
        public long EventCount;
        public long PayloadBytes;
        public long PayloadValueCount;
        public long RawValueChecksum;

        public void Add(ArchiveRadarEventBatchPublishResult result)
        {
            PublishedFiles++;
            FileSizeBytes += result.FileSizeBytes;
            CompressedRecordCount += result.CompressedRecordCount;
            CompressedBytes += result.CompressedBytes;
            DecompressedBytes += result.DecompressedBytes;
            BatchCount += result.BatchCount;
            EventCount += result.EventCount;
            PayloadBytes += result.PayloadBytes;
            PayloadValueCount += result.PayloadValueCount;
            RawValueChecksum += result.RawValueChecksum;
        }
    }

    private sealed record CacheArchiveFileSelection(
        CacheIterationTotals Totals,
        IReadOnlyList<FileInfo> BaseDataFiles);
}
