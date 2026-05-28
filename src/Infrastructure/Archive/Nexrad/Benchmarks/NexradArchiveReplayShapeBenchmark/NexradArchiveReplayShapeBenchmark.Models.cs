using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayShapeBenchmark
{
    private sealed record ArchiveTwoReplayShapeRecordMetadata(
        IReadOnlyList<ArchiveTwoReplayShapeRadialMetadata> Radials);

    private readonly record struct ArchiveTwoReplayShapeRadialMetadata(
        int RadialStatus,
        int ElevationNumber);

    private sealed class ArchiveTwoReplayShapeRecordMetadataCollector : IArchiveTwoMessageConsumer
    {
        private const int MessageHeaderLength = 16;
        private const int Type31DataHeaderMinimumLength = 72;
        private readonly List<ArchiveTwoReplayShapeRadialMetadata> radials = new();

        public void Reset() => radials.Clear();

        public ArchiveTwoReplayShapeRecordMetadata Build() =>
            new(radials.ToArray());

        public void AcceptMessage(ReadOnlySpan<byte> message, ArchiveTwoMessageSource source)
        {
            if (message.Length < MessageHeaderLength || message[3] != 31)
            {
                return;
            }

            var payload = message[MessageHeaderLength..];
            if (payload.Length < Type31DataHeaderMinimumLength)
            {
                return;
            }

            radials.Add(new ArchiveTwoReplayShapeRadialMetadata(
                payload[21],
                payload[22]));
        }
    }

    private sealed class ArchiveTwoReplayShapeIterationMeasurement
    {
        public int CompressedRecordCount;
        public long CompressedBytes;
        public long DecompressedBytes;
        public long Events;
        public long ValidEvents;
        public long BelowThresholdEvents;
        public long RangeFoldedEvents;
        public long ClutterFilterNotAppliedEvents;
        public long PointClutterFilterAppliedEvents;
        public long DualPolarizationFilteredEvents;
        public long ReservedEvents;
        public long UnsupportedEvents;
        public long RawValueChecksum;
        public long CalibratedValueScaledChecksum;
        public ulong ChronologyChecksum;
        public double MinimumCalibratedValue;
        public double MaximumCalibratedValue;
        public double MinimumRangeKilometers;
        public double MaximumRangeKilometers;

        public bool HasSameTotals(ArchiveTwoReplayShapeIterationMeasurement other) =>
            CompressedRecordCount == other.CompressedRecordCount &&
            CompressedBytes == other.CompressedBytes &&
            DecompressedBytes == other.DecompressedBytes &&
            Events == other.Events &&
            ValidEvents == other.ValidEvents &&
            BelowThresholdEvents == other.BelowThresholdEvents &&
            RangeFoldedEvents == other.RangeFoldedEvents &&
            ClutterFilterNotAppliedEvents == other.ClutterFilterNotAppliedEvents &&
            PointClutterFilterAppliedEvents == other.PointClutterFilterAppliedEvents &&
            DualPolarizationFilteredEvents == other.DualPolarizationFilteredEvents &&
            ReservedEvents == other.ReservedEvents &&
            UnsupportedEvents == other.UnsupportedEvents &&
            RawValueChecksum == other.RawValueChecksum &&
            CalibratedValueScaledChecksum == other.CalibratedValueScaledChecksum &&
            ChronologyChecksum == other.ChronologyChecksum &&
            MinimumCalibratedValue.Equals(other.MinimumCalibratedValue) &&
            MaximumCalibratedValue.Equals(other.MaximumCalibratedValue) &&
            MinimumRangeKilometers.Equals(other.MinimumRangeKilometers) &&
            MaximumRangeKilometers.Equals(other.MaximumRangeKilometers);

        public void AddOrdered(ArchiveTwoReplayShapeIterationMeasurement other)
        {
            if (other.Events == 0)
            {
                CompressedRecordCount += other.CompressedRecordCount;
                CompressedBytes += other.CompressedBytes;
                DecompressedBytes += other.DecompressedBytes;
                return;
            }

            var hadEvents = Events > 0;
            var hadValidEvents = ValidEvents > 0;

            CompressedRecordCount += other.CompressedRecordCount;
            CompressedBytes += other.CompressedBytes;
            DecompressedBytes += other.DecompressedBytes;
            RawValueChecksum += other.RawValueChecksum;
            CalibratedValueScaledChecksum += other.CalibratedValueScaledChecksum;
            ChronologyChecksum = CombineChronologyChecksums(
                ChronologyChecksum,
                other.ChronologyChecksum,
                other.Events);

            if (!hadEvents)
            {
                MinimumRangeKilometers = other.MinimumRangeKilometers;
                MaximumRangeKilometers = other.MaximumRangeKilometers;
            }
            else
            {
                MinimumRangeKilometers = Math.Min(MinimumRangeKilometers, other.MinimumRangeKilometers);
                MaximumRangeKilometers = Math.Max(MaximumRangeKilometers, other.MaximumRangeKilometers);
            }

            if (other.ValidEvents > 0)
            {
                if (!hadValidEvents)
                {
                    MinimumCalibratedValue = other.MinimumCalibratedValue;
                    MaximumCalibratedValue = other.MaximumCalibratedValue;
                }
                else
                {
                    MinimumCalibratedValue = Math.Min(MinimumCalibratedValue, other.MinimumCalibratedValue);
                    MaximumCalibratedValue = Math.Max(MaximumCalibratedValue, other.MaximumCalibratedValue);
                }
            }

            Events += other.Events;
            ValidEvents += other.ValidEvents;
            BelowThresholdEvents += other.BelowThresholdEvents;
            RangeFoldedEvents += other.RangeFoldedEvents;
            ClutterFilterNotAppliedEvents += other.ClutterFilterNotAppliedEvents;
            PointClutterFilterAppliedEvents += other.PointClutterFilterAppliedEvents;
            DualPolarizationFilteredEvents += other.DualPolarizationFilteredEvents;
            ReservedEvents += other.ReservedEvents;
            UnsupportedEvents += other.UnsupportedEvents;
        }

        public void AcceptEvent(ArchiveTwoGateMomentEvent gateMomentEvent)
        {
            Events++;
            RawValueChecksum += gateMomentEvent.RawValue;
            AcceptRange(gateMomentEvent.RangeKilometers);
            AcceptChronology(gateMomentEvent);

            switch (gateMomentEvent.Status)
            {
                case ArchiveTwoGateMomentStatus.Valid:
                    ValidEvents++;
                    AcceptCalibratedValue(gateMomentEvent.CalibratedValue!.Value);
                    break;
                case ArchiveTwoGateMomentStatus.BelowThreshold:
                    BelowThresholdEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.RangeFolded:
                    RangeFoldedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.ClutterFilterNotApplied:
                    ClutterFilterNotAppliedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.PointClutterFilterApplied:
                    PointClutterFilterAppliedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.DualPolarizationFiltered:
                    DualPolarizationFilteredEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.Reserved:
                    ReservedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.Unsupported:
                    UnsupportedEvents++;
                    break;
            }
        }

        private void AcceptChronology(ArchiveTwoGateMomentEvent gateMomentEvent)
        {
            unchecked
            {
                ChronologyChecksum = ArchiveTwoGateMomentChronologyChecksum.Append(ChronologyChecksum, gateMomentEvent);
            }
        }

        private void AcceptCalibratedValue(double value)
        {
            if (ValidEvents == 1)
            {
                MinimumCalibratedValue = value;
                MaximumCalibratedValue = value;
            }
            else
            {
                MinimumCalibratedValue = Math.Min(MinimumCalibratedValue, value);
                MaximumCalibratedValue = Math.Max(MaximumCalibratedValue, value);
            }

            checked
            {
                CalibratedValueScaledChecksum += (long)Math.Round(value * 1_000d, MidpointRounding.AwayFromZero);
            }
        }

        private void AcceptRange(float rangeKilometers)
        {
            if (Events == 1)
            {
                MinimumRangeKilometers = rangeKilometers;
                MaximumRangeKilometers = rangeKilometers;
                return;
            }

            MinimumRangeKilometers = Math.Min(MinimumRangeKilometers, rangeKilometers);
            MaximumRangeKilometers = Math.Max(MaximumRangeKilometers, rangeKilometers);
        }
    }

}
