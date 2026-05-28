using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveTwoMessageSummaryBuilder
{
    private sealed class MomentAccumulator
    {
        private bool hasMetadata;

        public int RadialCount { get; private set; }

        public long GateCount { get; private set; }

        public int MinimumGateCount { get; private set; }

        public int MaximumGateCount { get; private set; }

        public int MinimumWordSizeBits { get; private set; }

        public int MaximumWordSizeBits { get; private set; }

        public float MinimumFirstGateRangeKilometers { get; private set; }

        public float MaximumFirstGateRangeKilometers { get; private set; }

        public float MinimumGateSpacingKilometers { get; private set; }

        public float MaximumGateSpacingKilometers { get; private set; }

        public float MinimumScale { get; private set; }

        public float MaximumScale { get; private set; }

        public float MinimumOffset { get; private set; }

        public float MaximumOffset { get; private set; }

        public void Add(Type31MomentMetadata metadata)
        {
            RadialCount++;
            GateCount += metadata.GateCount;
            AcceptMetadata(
                metadata.GateCount,
                metadata.WordSizeBits,
                metadata.FirstGateRangeKilometers,
                metadata.GateSpacingKilometers,
                metadata.Scale,
                metadata.Offset);
        }

        public void Add(ArchiveTwoMomentSummary summary)
        {
            RadialCount += summary.RadialCount;
            GateCount += summary.GateCount;
            AcceptMetadata(
                summary.MinimumGateCount,
                summary.MinimumWordSizeBits,
                summary.MinimumFirstGateRangeKilometers,
                summary.MinimumGateSpacingKilometers,
                summary.MinimumScale,
                summary.MinimumOffset);

            AcceptMetadata(
                summary.MaximumGateCount,
                summary.MaximumWordSizeBits,
                summary.MaximumFirstGateRangeKilometers,
                summary.MaximumGateSpacingKilometers,
                summary.MaximumScale,
                summary.MaximumOffset);
        }

        private void AcceptMetadata(
            int gateCount,
            int wordSizeBits,
            float firstGateRangeKilometers,
            float gateSpacingKilometers,
            float scale,
            float offset)
        {
            if (!hasMetadata)
            {
                MinimumGateCount = gateCount;
                MaximumGateCount = gateCount;
                MinimumWordSizeBits = wordSizeBits;
                MaximumWordSizeBits = wordSizeBits;
                MinimumFirstGateRangeKilometers = firstGateRangeKilometers;
                MaximumFirstGateRangeKilometers = firstGateRangeKilometers;
                MinimumGateSpacingKilometers = gateSpacingKilometers;
                MaximumGateSpacingKilometers = gateSpacingKilometers;
                MinimumScale = scale;
                MaximumScale = scale;
                MinimumOffset = offset;
                MaximumOffset = offset;
                hasMetadata = true;
                return;
            }

            MinimumGateCount = Math.Min(MinimumGateCount, gateCount);
            MaximumGateCount = Math.Max(MaximumGateCount, gateCount);
            MinimumWordSizeBits = Math.Min(MinimumWordSizeBits, wordSizeBits);
            MaximumWordSizeBits = Math.Max(MaximumWordSizeBits, wordSizeBits);
            MinimumFirstGateRangeKilometers = Math.Min(MinimumFirstGateRangeKilometers, firstGateRangeKilometers);
            MaximumFirstGateRangeKilometers = Math.Max(MaximumFirstGateRangeKilometers, firstGateRangeKilometers);
            MinimumGateSpacingKilometers = Math.Min(MinimumGateSpacingKilometers, gateSpacingKilometers);
            MaximumGateSpacingKilometers = Math.Max(MaximumGateSpacingKilometers, gateSpacingKilometers);
            MinimumScale = Math.Min(MinimumScale, scale);
            MaximumScale = Math.Max(MaximumScale, scale);
            MinimumOffset = Math.Min(MinimumOffset, offset);
            MaximumOffset = Math.Max(MaximumOffset, offset);
        }
    }

    private readonly record struct Type31MomentMetadata(
        int GateCount,
        float FirstGateRangeKilometers,
        float GateSpacingKilometers,
        int WordSizeBits,
        float Scale,
        float Offset);

    private readonly record struct Type31RadialMetadata(
        int RadialStatus,
        int ElevationNumber,
        int CutSectorNumber,
        float ElevationAngleDegrees);

    private sealed class SweepAccumulator
    {
        private readonly SortedSet<string> moments = new(StringComparer.Ordinal);
        private double elevationAngleTotal;

        public SweepAccumulator(
            int sequenceNumber,
            int elevationNumber,
            int startRadialStatus,
            ArchiveTwoRadialSourceOrder firstRadial)
        {
            SequenceNumber = sequenceNumber;
            ElevationNumber = elevationNumber;
            StartRadialStatus = startRadialStatus;
            EndRadialStatus = startRadialStatus;
            FirstRadial = firstRadial;
            LastRadial = firstRadial;
        }

        public int SequenceNumber { get; }

        public int ElevationNumber { get; }

        public int MinimumCutSectorNumber { get; private set; }

        public int MaximumCutSectorNumber { get; private set; }

        public int RadialCount { get; private set; }

        public int StartRadialStatus { get; }

        public int EndRadialStatus { get; private set; }

        public float MinimumElevationAngleDegrees { get; private set; }

        public float MaximumElevationAngleDegrees { get; private set; }

        public int VolumeConstantBlockCount { get; private set; }

        public int ElevationConstantBlockCount { get; private set; }

        public int RadialConstantBlockCount { get; private set; }

        public ArchiveTwoRadialSourceOrder FirstRadial { get; }

        public ArchiveTwoRadialSourceOrder LastRadial { get; private set; }

        public static SweepAccumulator FromSummary(int sequenceNumber, ArchiveTwoSweepSummary summary)
        {
            var accumulator = new SweepAccumulator(
                sequenceNumber,
                summary.ElevationNumber,
                summary.StartRadialStatus,
                summary.FirstRadial)
            {
                RadialCount = summary.RadialCount,
                MinimumCutSectorNumber = summary.MinimumCutSectorNumber,
                MaximumCutSectorNumber = summary.MaximumCutSectorNumber,
                EndRadialStatus = summary.EndRadialStatus,
                MinimumElevationAngleDegrees = summary.MinimumElevationAngleDegrees,
                MaximumElevationAngleDegrees = summary.MaximumElevationAngleDegrees,
                elevationAngleTotal = summary.AverageElevationAngleDegrees * summary.RadialCount,
                VolumeConstantBlockCount = summary.VolumeConstantBlockCount,
                ElevationConstantBlockCount = summary.ElevationConstantBlockCount,
                RadialConstantBlockCount = summary.RadialConstantBlockCount,
                LastRadial = summary.LastRadial
            };

            foreach (var moment in summary.Moments)
            {
                accumulator.moments.Add(moment);
            }

            return accumulator;
        }

        public void AcceptRadial(Type31RadialMetadata radial, ArchiveTwoRadialSourceOrder sourceOrder)
        {
            if (RadialCount == 0)
            {
                MinimumCutSectorNumber = radial.CutSectorNumber;
                MaximumCutSectorNumber = radial.CutSectorNumber;
                MinimumElevationAngleDegrees = radial.ElevationAngleDegrees;
                MaximumElevationAngleDegrees = radial.ElevationAngleDegrees;
            }
            else
            {
                MinimumCutSectorNumber = Math.Min(MinimumCutSectorNumber, radial.CutSectorNumber);
                MaximumCutSectorNumber = Math.Max(MaximumCutSectorNumber, radial.CutSectorNumber);
                MinimumElevationAngleDegrees = Math.Min(MinimumElevationAngleDegrees, radial.ElevationAngleDegrees);
                MaximumElevationAngleDegrees = Math.Max(MaximumElevationAngleDegrees, radial.ElevationAngleDegrees);
            }

            RadialCount++;
            EndRadialStatus = radial.RadialStatus;
            LastRadial = sourceOrder;
            elevationAngleTotal += radial.ElevationAngleDegrees;
        }

        public void AcceptVolumeConstantBlock() => VolumeConstantBlockCount++;

        public void AcceptElevationConstantBlock() => ElevationConstantBlockCount++;

        public void AcceptRadialConstantBlock() => RadialConstantBlockCount++;

        public void AcceptMoment(string name) => moments.Add(name);

        public ArchiveTwoSweepSummary ToSummary() =>
            new(
                SequenceNumber,
                ElevationNumber,
                MinimumCutSectorNumber,
                MaximumCutSectorNumber,
                RadialCount,
                StartRadialStatus,
                EndRadialStatus,
                MinimumElevationAngleDegrees,
                MaximumElevationAngleDegrees,
                RadialCount == 0 ? 0 : (float)(elevationAngleTotal / RadialCount),
                VolumeConstantBlockCount,
                ElevationConstantBlockCount,
                RadialConstantBlockCount,
                moments.ToArray(),
                FirstRadial,
                LastRadial);
    }
}
