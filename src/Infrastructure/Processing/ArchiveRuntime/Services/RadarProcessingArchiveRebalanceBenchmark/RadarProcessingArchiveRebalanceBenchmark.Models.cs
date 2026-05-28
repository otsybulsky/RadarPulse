using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private readonly record struct ArchiveIterationTelemetry(
        long ExaminedFileCount,
        long SkippedFileCount,
        long PublishedFileCount,
        long FileSizeBytes,
        long CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes,
        long BatchCount,
        long EventCount,
        long PayloadBytes,
        long PayloadValueCount,
        long RawValueChecksum,
        long TopologyVersionCount,
        long RebalanceEvaluationCount,
        long AcceptedMoveCount,
        long SkippedDecisionCount,
        long DirectHotReliefCount,
        long ColdEvacuationCount,
        long FailedMigrationCount,
        bool ValidationSucceeded,
        ulong ValidationChecksum,
        List<RadarProcessingRebalanceSkippedReason>? SkippedReasons,
        List<RadarProcessingRebalanceSkippedReasonCounter>? SkippedReasonCounters,
        List<RadarProcessingSyntheticRebalanceMovePressure>? AcceptedMovePressures,
        RadarProcessingRebalanceRetentionStats RetentionStats,
        TimeSpan ProcessingElapsed,
        long ProcessingCallbackAllocatedBytes,
        RadarProcessingProviderQueueTelemetrySummary QueueTelemetry,
        RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry,
        RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry,
        long ProcessingValidationFailedBatchCount)
    {
        public static ArchiveIterationTelemetry Empty =>
            new(
                ExaminedFileCount: 0,
                SkippedFileCount: 0,
                PublishedFileCount: 0,
                FileSizeBytes: 0,
                CompressedRecordCount: 0,
                CompressedBytes: 0,
                DecompressedBytes: 0,
                BatchCount: 0,
                EventCount: 0,
                PayloadBytes: 0,
                PayloadValueCount: 0,
                RawValueChecksum: 0,
                TopologyVersionCount: 1,
                RebalanceEvaluationCount: 0,
                AcceptedMoveCount: 0,
                SkippedDecisionCount: 0,
                DirectHotReliefCount: 0,
                ColdEvacuationCount: 0,
                FailedMigrationCount: 0,
                ValidationSucceeded: true,
                ValidationChecksum: ChecksumInitial,
                SkippedReasons: null,
                SkippedReasonCounters: null,
                AcceptedMovePressures: null,
                RetentionStats: new RadarProcessingRebalanceRetentionStats(),
                ProcessingElapsed: TimeSpan.Zero,
                ProcessingCallbackAllocatedBytes: 0,
                QueueTelemetry: RadarProcessingProviderQueueTelemetrySummary.Empty,
                RetentionTelemetry: RadarProcessingRetainedPayloadTelemetrySummary.Empty,
                OverlapTelemetry: RadarProcessingArchiveOverlapTelemetrySummary.Empty,
                ProcessingValidationFailedBatchCount: 0);

        public static ArchiveIterationTelemetry FromMetrics(
            RadarProcessingMetrics metrics,
            long topologyVersionCount,
            long rebalanceEvaluationCount = 0) =>
            Empty.WithMetrics(metrics, topologyVersionCount) with
            {
                RebalanceEvaluationCount = rebalanceEvaluationCount,
                ValidationChecksum = ComputeChecksum(
                    metrics,
                    topologyVersionCount,
                    rebalanceEvaluationCount,
                    acceptedMoveCount: 0,
                    skippedDecisionCount: 0,
                    directHotReliefCount: 0,
                    coldEvacuationCount: 0,
                    failedMigrationCount: 0,
                    validationSucceeded: true)
            };

        public static ArchiveIterationTelemetry FromRebalanceSessionResult(
            RadarProcessingRebalanceSessionResult result)
        {
            List<RadarProcessingRebalanceSkippedReason>? skippedReasons = null;
            List<RadarProcessingRebalanceSkippedReasonCounter>? skippedReasonCounters = null;
            List<RadarProcessingSyntheticRebalanceMovePressure>? movePressures = null;
            var skippedDecisionCount = 0L;
            var acceptedMoveCount = 0L;
            var directHotReliefCount = 0L;
            var coldEvacuationCount = 0L;
            var failedMigrationCount = 0L;

            AddDecision(
                result.DirectHotReliefDecision,
                ref skippedReasons,
                ref skippedReasonCounters,
                ref skippedDecisionCount);
            AddDecision(
                result.ColdEvacuationDecision,
                ref skippedReasons,
                ref skippedReasonCounters,
                ref skippedDecisionCount);

            if (result.PublishedMigration)
            {
                acceptedMoveCount = 1;
                var decision = result.RebalanceDecision ??
                               throw new InvalidDataException("Published moves require a rebalance decision.");
                movePressures = new List<RadarProcessingSyntheticRebalanceMovePressure>(capacity: 1);
                movePressures.Add(CreateMovePressure(decision));
                if (decision.MoveKind == RadarProcessingRebalanceMoveKind.DirectHotRelief)
                {
                    directHotReliefCount = 1;
                }
                else if (decision.MoveKind == RadarProcessingRebalanceMoveKind.ColdEvacuation)
                {
                    coldEvacuationCount = 1;
                }
            }

            if (result.MigrationResult is not null && !result.MigrationResult.Succeeded)
            {
                failedMigrationCount = 1;
            }

            return Empty with
            {
                RebalanceEvaluationCount = result.EvaluatedRebalance ? 1 : 0,
                AcceptedMoveCount = acceptedMoveCount,
                SkippedDecisionCount = skippedDecisionCount,
                DirectHotReliefCount = directHotReliefCount,
                ColdEvacuationCount = coldEvacuationCount,
                FailedMigrationCount = failedMigrationCount,
                ValidationSucceeded = result.Validation.IsValid && result.ProcessingResult.IsValid,
                ProcessingValidationFailedBatchCount = result.ProcessingResult.IsValid ? 0 : 1,
                SkippedReasons = skippedReasons,
                SkippedReasonCounters = skippedReasonCounters,
                AcceptedMovePressures = movePressures
            };
        }

        public ArchiveIterationTelemetry Add(ArchiveIterationTelemetry other)
        {
            var skippedReasons = SkippedReasons;
            if (other.SkippedReasons is { Count: > 0 } otherSkippedReasons)
            {
                foreach (var reason in otherSkippedReasons)
                {
                    AddSkippedReason(ref skippedReasons, reason);
                }
            }

            var skippedReasonCounters = SkippedReasonCounters;
            if (other.SkippedReasonCounters is { Count: > 0 } otherSkippedReasonCounters)
            {
                foreach (var counter in otherSkippedReasonCounters)
                {
                    AddSkippedReasonCounter(ref skippedReasonCounters, counter.Reason, counter.Count);
                }
            }

            var movePressures = AcceptedMovePressures;
            if (other.AcceptedMovePressures is { Count: > 0 } otherMovePressures)
            {
                movePressures ??= new List<RadarProcessingSyntheticRebalanceMovePressure>(
                    otherMovePressures.Count);
                movePressures.AddRange(otherMovePressures);
            }

            return this with
            {
                ExaminedFileCount = checked(ExaminedFileCount + other.ExaminedFileCount),
                SkippedFileCount = checked(SkippedFileCount + other.SkippedFileCount),
                PublishedFileCount = checked(PublishedFileCount + other.PublishedFileCount),
                FileSizeBytes = FileSizeBytes == 0 ? other.FileSizeBytes : FileSizeBytes,
                CompressedRecordCount = checked(CompressedRecordCount + other.CompressedRecordCount),
                CompressedBytes = checked(CompressedBytes + other.CompressedBytes),
                DecompressedBytes = checked(DecompressedBytes + other.DecompressedBytes),
                BatchCount = checked(BatchCount + other.BatchCount),
                EventCount = checked(EventCount + other.EventCount),
                PayloadBytes = checked(PayloadBytes + other.PayloadBytes),
                PayloadValueCount = checked(PayloadValueCount + other.PayloadValueCount),
                RawValueChecksum = checked(RawValueChecksum + other.RawValueChecksum),
                TopologyVersionCount = Math.Max(TopologyVersionCount, other.TopologyVersionCount),
                RebalanceEvaluationCount = checked(RebalanceEvaluationCount + other.RebalanceEvaluationCount),
                AcceptedMoveCount = checked(AcceptedMoveCount + other.AcceptedMoveCount),
                SkippedDecisionCount = checked(SkippedDecisionCount + other.SkippedDecisionCount),
                DirectHotReliefCount = checked(DirectHotReliefCount + other.DirectHotReliefCount),
                ColdEvacuationCount = checked(ColdEvacuationCount + other.ColdEvacuationCount),
                FailedMigrationCount = checked(FailedMigrationCount + other.FailedMigrationCount),
                ValidationSucceeded = ValidationSucceeded && other.ValidationSucceeded,
                ValidationChecksum = AppendUInt64(ValidationChecksum, other.ValidationChecksum),
                ProcessingValidationFailedBatchCount = checked(
                    ProcessingValidationFailedBatchCount + other.ProcessingValidationFailedBatchCount),
                SkippedReasons = skippedReasons,
                SkippedReasonCounters = skippedReasonCounters,
                AcceptedMovePressures = movePressures,
                RetentionStats = AddRetentionStats(RetentionStats, other.RetentionStats),
                ProcessingElapsed = ProcessingElapsed + other.ProcessingElapsed,
                ProcessingCallbackAllocatedBytes = checked(
                    ProcessingCallbackAllocatedBytes + other.ProcessingCallbackAllocatedBytes),
                QueueTelemetry = AddQueueTelemetry(QueueTelemetry, other.QueueTelemetry),
                RetentionTelemetry = AddRetentionTelemetry(RetentionTelemetry, other.RetentionTelemetry),
                OverlapTelemetry = AddOverlapTelemetry(OverlapTelemetry, other.OverlapTelemetry)
            };
        }

        public ArchiveIterationTelemetry WithPublishResult(
            RadarPulse.Domain.Archive.ArchiveRadarEventBatchPublishResult result,
            TimeSpan processingElapsed,
            long processingCallbackAllocatedBytes) =>
            this with
            {
                ExaminedFileCount = 1,
                SkippedFileCount = 0,
                PublishedFileCount = result.BatchCount > 0 ? 1 : 0,
                FileSizeBytes = result.FileSizeBytes,
                CompressedRecordCount = result.CompressedRecordCount,
                CompressedBytes = result.CompressedBytes,
                DecompressedBytes = result.DecompressedBytes,
                BatchCount = result.BatchCount,
                EventCount = result.EventCount,
                PayloadBytes = result.PayloadBytes,
                PayloadValueCount = result.PayloadValueCount,
                RawValueChecksum = result.RawValueChecksum,
                ProcessingElapsed = processingElapsed,
                ProcessingCallbackAllocatedBytes = processingCallbackAllocatedBytes
            };

        public ArchiveIterationTelemetry WithQueueTelemetry(
            RadarProcessingProviderQueueTelemetrySummary queueTelemetry)
        {
            ArgumentNullException.ThrowIfNull(queueTelemetry);

            return this with
            {
                QueueTelemetry = queueTelemetry
            };
        }

        public ArchiveIterationTelemetry WithRetentionTelemetry(
            RadarProcessingRetainedPayloadTelemetrySummary retentionTelemetry)
        {
            ArgumentNullException.ThrowIfNull(retentionTelemetry);

            return this with
            {
                RetentionTelemetry = retentionTelemetry
            };
        }

        public ArchiveIterationTelemetry WithOverlapTelemetry(
            RadarProcessingArchiveOverlapTelemetrySummary overlapTelemetry)
        {
            ArgumentNullException.ThrowIfNull(overlapTelemetry);

            return this with
            {
                OverlapTelemetry = overlapTelemetry
            };
        }

        public ArchiveIterationTelemetry WithRetentionStats(
            RadarProcessingRebalanceRetentionStats retentionStats)
        {
            ArgumentNullException.ThrowIfNull(retentionStats);

            return this with
            {
                RetentionStats = retentionStats
            };
        }

        public ArchiveIterationTelemetry WithPublishTotals(
            CacheIterationTotals totals,
            TimeSpan processingElapsed,
            long processingCallbackAllocatedBytes) =>
            this with
            {
                ExaminedFileCount = totals.ExaminedFileCount,
                SkippedFileCount = totals.SkippedFileCount,
                PublishedFileCount = totals.PublishedFileCount,
                FileSizeBytes = totals.FileSizeBytes,
                CompressedRecordCount = totals.CompressedRecordCount,
                CompressedBytes = totals.CompressedBytes,
                DecompressedBytes = totals.DecompressedBytes,
                BatchCount = totals.BatchCount,
                EventCount = totals.EventCount,
                PayloadBytes = totals.PayloadBytes,
                PayloadValueCount = totals.PayloadValueCount,
                RawValueChecksum = totals.RawValueChecksum,
                ProcessingElapsed = processingElapsed,
                ProcessingCallbackAllocatedBytes = processingCallbackAllocatedBytes
            };

        public ArchiveIterationTelemetry WithMetrics(
            RadarProcessingMetrics metrics,
            long topologyVersionCount)
        {
            var validationChecksum = ComputeChecksum(
                metrics,
                topologyVersionCount,
                RebalanceEvaluationCount,
                AcceptedMoveCount,
                SkippedDecisionCount,
                DirectHotReliefCount,
                ColdEvacuationCount,
                FailedMigrationCount,
                ValidationSucceeded);

            return this with
            {
                TopologyVersionCount = topologyVersionCount,
                ValidationChecksum = validationChecksum
            };
        }

        public bool HasSameStableTotals(ArchiveIterationTelemetry other) =>
            ExaminedFileCount == other.ExaminedFileCount &&
            SkippedFileCount == other.SkippedFileCount &&
            PublishedFileCount == other.PublishedFileCount &&
            FileSizeBytes == other.FileSizeBytes &&
            CompressedRecordCount == other.CompressedRecordCount &&
            CompressedBytes == other.CompressedBytes &&
            DecompressedBytes == other.DecompressedBytes &&
            BatchCount == other.BatchCount &&
            EventCount == other.EventCount &&
            PayloadBytes == other.PayloadBytes &&
            PayloadValueCount == other.PayloadValueCount &&
            RawValueChecksum == other.RawValueChecksum &&
            TopologyVersionCount == other.TopologyVersionCount &&
            RebalanceEvaluationCount == other.RebalanceEvaluationCount &&
            AcceptedMoveCount == other.AcceptedMoveCount &&
            SkippedDecisionCount == other.SkippedDecisionCount &&
            DirectHotReliefCount == other.DirectHotReliefCount &&
            ColdEvacuationCount == other.ColdEvacuationCount &&
            FailedMigrationCount == other.FailedMigrationCount &&
            ValidationSucceeded == other.ValidationSucceeded &&
            ValidationChecksum == other.ValidationChecksum &&
            ProcessingValidationFailedBatchCount == other.ProcessingValidationFailedBatchCount &&
            HasSameSkippedReasonCounters(SkippedReasonCounters, other.SkippedReasonCounters) &&
            HasSameRetentionStats(RetentionStats, other.RetentionStats);

        private static bool HasSameSkippedReasonCounters(
            IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter>? current,
            IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter>? other)
        {
            var currentCount = current?.Count ?? 0;
            if (currentCount != (other?.Count ?? 0))
            {
                return false;
            }

            if (currentCount == 0)
            {
                return true;
            }

            var currentSorted = current!.OrderBy(counter => counter.Reason).ToArray();
            var otherSorted = other!.OrderBy(counter => counter.Reason).ToArray();
            for (var index = 0; index < currentSorted.Length; index++)
            {
                if (currentSorted[index].Reason != otherSorted[index].Reason ||
                    currentSorted[index].Count != otherSorted[index].Count)
                {
                    return false;
                }
            }

            return true;
        }

        private static RadarProcessingRebalanceRetentionStats AddRetentionStats(
            RadarProcessingRebalanceRetentionStats current,
            RadarProcessingRebalanceRetentionStats other) =>
            new(
                Math.Max(current.RetainedDecisionCount, other.RetainedDecisionCount),
                checked(current.DroppedDecisionCount + other.DroppedDecisionCount),
                Math.Max(
                    current.RetainedLifecycleTransitionCount,
                    other.RetainedLifecycleTransitionCount),
                checked(current.DroppedLifecycleTransitionCount + other.DroppedLifecycleTransitionCount),
                Math.Max(current.RetainedAcceptedMoveCount, other.RetainedAcceptedMoveCount),
                checked(current.DroppedAcceptedMoveCount + other.DroppedAcceptedMoveCount),
                Math.Max(current.RetainedValidationFailureCount, other.RetainedValidationFailureCount),
                checked(current.DroppedValidationFailureCount + other.DroppedValidationFailureCount));

        private static bool HasSameRetentionStats(
            RadarProcessingRebalanceRetentionStats current,
            RadarProcessingRebalanceRetentionStats other) =>
            current.RetainedDecisionCount == other.RetainedDecisionCount &&
            current.DroppedDecisionCount == other.DroppedDecisionCount &&
            current.RetainedLifecycleTransitionCount == other.RetainedLifecycleTransitionCount &&
            current.DroppedLifecycleTransitionCount == other.DroppedLifecycleTransitionCount &&
            current.RetainedAcceptedMoveCount == other.RetainedAcceptedMoveCount &&
            current.DroppedAcceptedMoveCount == other.DroppedAcceptedMoveCount &&
            current.RetainedValidationFailureCount == other.RetainedValidationFailureCount &&
            current.DroppedValidationFailureCount == other.DroppedValidationFailureCount;

        private static void AddDecision(
            RadarProcessingRebalanceDecision? decision,
            ref List<RadarProcessingRebalanceSkippedReason>? skippedReasons,
            ref List<RadarProcessingRebalanceSkippedReasonCounter>? skippedReasonCounters,
            ref long skippedDecisionCount)
        {
            if (decision is null || decision.HasAcceptedMove)
            {
                return;
            }

            skippedDecisionCount = checked(skippedDecisionCount + 1);
            foreach (var reason in decision.SkippedReasons)
            {
                AddSkippedReason(ref skippedReasons, reason);
                AddSkippedReasonCounter(ref skippedReasonCounters, reason, count: 1);
            }
        }

        private static void AddSkippedReason(
            ref List<RadarProcessingRebalanceSkippedReason>? skippedReasons,
            RadarProcessingRebalanceSkippedReason reason)
        {
            skippedReasons ??= new List<RadarProcessingRebalanceSkippedReason>();
            if (!skippedReasons.Contains(reason))
            {
                skippedReasons.Add(reason);
            }
        }

        private static void AddSkippedReasonCounter(
            ref List<RadarProcessingRebalanceSkippedReasonCounter>? skippedReasonCounters,
            RadarProcessingRebalanceSkippedReason reason,
            long count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (count == 0)
            {
                return;
            }

            skippedReasonCounters ??= new List<RadarProcessingRebalanceSkippedReasonCounter>();
            for (var index = 0; index < skippedReasonCounters.Count; index++)
            {
                if (skippedReasonCounters[index].Reason != reason)
                {
                    continue;
                }

                skippedReasonCounters[index] = new RadarProcessingRebalanceSkippedReasonCounter(
                    reason,
                    checked(skippedReasonCounters[index].Count + count));
                return;
            }

            skippedReasonCounters.Add(new RadarProcessingRebalanceSkippedReasonCounter(reason, count));
        }

        private static RadarProcessingSyntheticRebalanceMovePressure CreateMovePressure(
            RadarProcessingRebalanceDecision decision) =>
            new(
                decision.MoveKind,
                decision.ProjectedPressure.SourceShardBefore.Value,
                decision.ProjectedPressure.TargetShardBefore.Value,
                decision.ProjectedPressure.SourceShardAfter.Value,
                decision.ProjectedPressure.TargetShardAfter.Value,
                decision.ExpectedRelief);

        private static ulong ComputeChecksum(
            RadarProcessingMetrics metrics,
            long topologyVersionCount,
            long rebalanceEvaluationCount,
            long acceptedMoveCount,
            long skippedDecisionCount,
            long directHotReliefCount,
            long coldEvacuationCount,
            long failedMigrationCount,
            bool validationSucceeded)
        {
            var checksum = ChecksumInitial;
            checksum = AppendInt64(checksum, metrics.ProcessedBatchCount);
            checksum = AppendInt64(checksum, metrics.ProcessedStreamEventCount);
            checksum = AppendInt64(checksum, metrics.ProcessedPayloadValueCount);
            checksum = AppendInt64(checksum, metrics.ActiveSourceCount);
            checksum = AppendInt64(checksum, metrics.RawValueChecksum);
            checksum = AppendUInt64(checksum, metrics.ProcessingChecksum);
            checksum = AppendInt64(checksum, topologyVersionCount);
            checksum = AppendInt64(checksum, rebalanceEvaluationCount);
            checksum = AppendInt64(checksum, acceptedMoveCount);
            checksum = AppendInt64(checksum, skippedDecisionCount);
            checksum = AppendInt64(checksum, directHotReliefCount);
            checksum = AppendInt64(checksum, coldEvacuationCount);
            checksum = AppendInt64(checksum, failedMigrationCount);
            return AppendInt32(checksum, validationSucceeded ? 1 : 0);
        }
    }

    private struct CacheIterationTotals
    {
        public static CacheIterationTotals Empty => new();

        public long ExaminedFileCount;
        public long SkippedFileCount;
        public long PublishedFileCount;
        public long FileSizeBytes;
        public long CompressedRecordCount;
        public long CompressedBytes;
        public long DecompressedBytes;
        public long BatchCount;
        public long EventCount;
        public long PayloadBytes;
        public long PayloadValueCount;
        public long RawValueChecksum;

        public void Add(RadarPulse.Domain.Archive.ArchiveRadarEventBatchPublishResult result)
        {
            PublishedFileCount = checked(PublishedFileCount + 1);
            FileSizeBytes = checked(FileSizeBytes + result.FileSizeBytes);
            CompressedRecordCount = checked(CompressedRecordCount + result.CompressedRecordCount);
            CompressedBytes = checked(CompressedBytes + result.CompressedBytes);
            DecompressedBytes = checked(DecompressedBytes + result.DecompressedBytes);
            BatchCount = checked(BatchCount + result.BatchCount);
            EventCount = checked(EventCount + result.EventCount);
            PayloadBytes = checked(PayloadBytes + result.PayloadBytes);
            PayloadValueCount = checked(PayloadValueCount + result.PayloadValueCount);
            RawValueChecksum = checked(RawValueChecksum + result.RawValueChecksum);
        }
    }

    private readonly record struct QueuedArchivePublishResult(
        ArchiveRadarEventBatchPublishResult PublishResult,
        RadarProcessingProviderQueueTelemetrySummary QueueTelemetry,
        RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry,
        RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry);

    private readonly record struct QueuedArchiveCachePublishResult(
        CacheIterationTotals Totals,
        RadarProcessingProviderQueueTelemetrySummary QueueTelemetry,
        RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry,
        RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry);

    private sealed record DefaultRetainedPayloadPrewarm(
        RadarProcessingRetainedPayloadFactory Factory,
        RadarProcessingRetainedPayloadPrewarmResult Result);

    private sealed class CacheArchiveFileSelection
    {
        public CacheArchiveFileSelection(
            CacheIterationTotals totals,
            IReadOnlyList<FileInfo> baseDataFiles)
        {
            Totals = totals;
            BaseDataFiles = baseDataFiles ?? throw new ArgumentNullException(nameof(baseDataFiles));
        }

        public CacheIterationTotals Totals { get; }

        public IReadOnlyList<FileInfo> BaseDataFiles { get; }
    }
}
