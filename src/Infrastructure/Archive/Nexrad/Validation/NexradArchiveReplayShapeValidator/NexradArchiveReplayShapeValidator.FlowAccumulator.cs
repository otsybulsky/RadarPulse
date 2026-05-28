using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayShapeValidator
{
    private sealed class ArchiveTwoReplayShapeFlowAccumulator
    {
        private readonly Dictionary<int, BucketAccumulator> recordBuckets = new();
        private readonly Dictionary<int, BucketAccumulator> sweepBuckets = new();
        private readonly Dictionary<int, BucketAccumulator> radialBuckets = new();
        private readonly Dictionary<int, BucketAccumulator> timeBucketBuckets = new();
        private long events;
        private long validEvents;
        private long belowThresholdEvents;
        private long rangeFoldedEvents;
        private long clutterFilterNotAppliedEvents;
        private long pointClutterFilterAppliedEvents;
        private long dualPolarizationFilteredEvents;
        private long reservedEvents;
        private long unsupportedEvents;
        private long rawValueChecksum;
        private long calibratedValueScaledChecksum;
        private ulong chronologyChecksum;

        public int CompressedRecordCount { get; private set; }

        public long CompressedBytes { get; private set; }

        public long DecompressedBytes { get; private set; }

        public void AcceptCompressedRecord(int compressedSizeBytes)
        {
            CompressedRecordCount++;
            CompressedBytes += compressedSizeBytes;
        }

        public void AcceptDecompressedBytes(long decompressedBytes) =>
            DecompressedBytes += decompressedBytes;

        public void AcceptEvent(ArchiveTwoGateMomentEvent gateMomentEvent)
        {
            events++;
            rawValueChecksum += gateMomentEvent.RawValue;
            chronologyChecksum = ArchiveTwoGateMomentChronologyChecksum.Append(chronologyChecksum, gateMomentEvent);
            AcceptBucket(recordBuckets, gateMomentEvent.SourceOrder.CompressedRecordSequenceNumber, gateMomentEvent);
            AcceptBucket(sweepBuckets, gateMomentEvent.SweepSequenceNumber, gateMomentEvent);
            AcceptBucket(radialBuckets, gateMomentEvent.RadialSequenceNumber, gateMomentEvent);
            AcceptBucket(timeBucketBuckets, GetMinuteOfDay(gateMomentEvent.MessageTimestamp), gateMomentEvent);

            switch (gateMomentEvent.Status)
            {
                case ArchiveTwoGateMomentStatus.Valid:
                    validEvents++;
                    checked
                    {
                        calibratedValueScaledChecksum += (long)Math.Round(
                            gateMomentEvent.CalibratedValue!.Value * 1_000d,
                            MidpointRounding.AwayFromZero);
                    }

                    break;
                case ArchiveTwoGateMomentStatus.BelowThreshold:
                    belowThresholdEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.RangeFolded:
                    rangeFoldedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.ClutterFilterNotApplied:
                    clutterFilterNotAppliedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.PointClutterFilterApplied:
                    pointClutterFilterAppliedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.DualPolarizationFiltered:
                    dualPolarizationFilteredEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.Reserved:
                    reservedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.Unsupported:
                    unsupportedEvents++;
                    break;
            }
        }

        public ArchiveTwoReplayShapeAnalysis Build()
        {
            var metrics = new ArchiveTwoReplayShapeValidationMetrics(
                CompressedRecordCount,
                CompressedBytes,
                DecompressedBytes,
                events,
                validEvents,
                belowThresholdEvents,
                rangeFoldedEvents,
                clutterFilterNotAppliedEvents,
                pointClutterFilterAppliedEvents,
                dualPolarizationFilteredEvents,
                reservedEvents,
                unsupportedEvents,
                rawValueChecksum,
                calibratedValueScaledChecksum,
                chronologyChecksum);
            return new ArchiveTwoReplayShapeAnalysis(
                metrics,
                BuildUnevenness("record", recordBuckets.Values),
                BuildUnevenness("sweep", sweepBuckets.Values),
                BuildUnevenness("radial", radialBuckets.Values),
                BuildUnevenness("minute", timeBucketBuckets.Values));
        }

        private static int GetMinuteOfDay(DateTimeOffset timestamp) =>
            timestamp.Hour * 60 + timestamp.Minute;

        private static void AcceptBucket(
            Dictionary<int, BucketAccumulator> buckets,
            int bucketNumber,
            ArchiveTwoGateMomentEvent gateMomentEvent)
        {
            if (!buckets.TryGetValue(bucketNumber, out var bucket))
            {
                bucket = new BucketAccumulator(bucketNumber);
                buckets.Add(bucketNumber, bucket);
            }

            bucket.Accept(gateMomentEvent);
        }

        private static ArchiveTwoReplayShapeUnevennessSummary BuildUnevenness(
            string bucketKind,
            IEnumerable<BucketAccumulator> bucketAccumulators)
        {
            var buckets = bucketAccumulators
                .Select(bucket => bucket.Build())
                .Where(bucket => bucket.Events > 0)
                .OrderBy(bucket => bucket.BucketNumber)
                .ToArray();
            if (buckets.Length == 0)
            {
                return ArchiveTwoReplayShapeUnevennessSummary.Empty(bucketKind);
            }

            return new ArchiveTwoReplayShapeUnevennessSummary(
                bucketKind,
                buckets.Length,
                buckets.MinBy(bucket => (bucket.ValidEventShare, bucket.BucketNumber))!,
                buckets.MaxBy(bucket => (bucket.ValidEventShare, -bucket.BucketNumber))!,
                buckets.MinBy(bucket => (bucket.ValidEvents, bucket.BucketNumber))!,
                buckets.MaxBy(bucket => (bucket.ValidEvents, -bucket.BucketNumber))!);
        }
    }
}
