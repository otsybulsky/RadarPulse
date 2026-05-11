using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class ArchiveTwoMessageSummaryBuilder
{
    private const int MessageHeaderLength = 16;
    private const int Type31DataHeaderMinimumLength = 72;
    private const int Type31DataBlockPointerOffset = 32;
    private const int Type31DataBlockPointerLength = 4;
    private const int Type31MaximumDataBlockPointers = 10;
    private const int GenericMomentDescriptorLength = 28;
    private const int GenericMomentDataOffset = 28;
    private const int GenericMomentWordSizeOffset = 19;

    private readonly bool decodeMomentValues;
    private readonly bool collectSweepSummaries;
    private readonly Dictionary<int, int> messageTypeCounts = new();
    private readonly Dictionary<string, MomentAccumulator> moments = new(StringComparer.Ordinal);
    private readonly List<SweepAccumulator> sweeps = new();
    private int messageCount;
    private int type31RadialCount;
    private long estimatedGateMomentEvents;
    private long decodedGateMomentValues;
    private ulong decodedGateMomentValueChecksum;
    private int volumeConstantBlockCount;
    private int elevationConstantBlockCount;
    private int radialConstantBlockCount;
    private SweepAccumulator? currentSweep;

    public ArchiveTwoMessageSummaryBuilder(
        bool decodeMomentValues = false,
        bool collectSweepSummaries = true)
    {
        this.decodeMomentValues = decodeMomentValues;
        this.collectSweepSummaries = collectSweepSummaries;
    }

    public int MessageCount => messageCount;

    public int Type31RadialCount => type31RadialCount;

    public long EstimatedGateMomentEventCount => estimatedGateMomentEvents;

    public long DecodedGateMomentValueCount => decodedGateMomentValues;

    public ulong DecodedGateMomentValueChecksum => decodedGateMomentValueChecksum;

    public void Reset()
    {
        messageTypeCounts.Clear();
        moments.Clear();
        messageCount = 0;
        type31RadialCount = 0;
        estimatedGateMomentEvents = 0;
        decodedGateMomentValues = 0;
        decodedGateMomentValueChecksum = 0;
        volumeConstantBlockCount = 0;
        elevationConstantBlockCount = 0;
        radialConstantBlockCount = 0;
        sweeps.Clear();
        currentSweep = null;
    }

    public void Add(ArchiveTwoMessageSummary summary)
    {
        messageCount += summary.MessageCount;
        foreach (var messageType in summary.MessageTypes)
        {
            messageTypeCounts.TryGetValue(messageType.MessageType, out var count);
            messageTypeCounts[messageType.MessageType] = count + messageType.Count;
        }

        type31RadialCount += summary.Type31.RadialCount;
        estimatedGateMomentEvents += summary.Type31.EstimatedGateMomentEventCount;
        volumeConstantBlockCount += summary.Type31.ConstantBlocks.VolumeCount;
        elevationConstantBlockCount += summary.Type31.ConstantBlocks.ElevationCount;
        radialConstantBlockCount += summary.Type31.ConstantBlocks.RadialCount;
        foreach (var moment in summary.Type31.Moments)
        {
            moments.TryGetValue(moment.Name, out var accumulator);
            accumulator.RadialCount += moment.RadialCount;
            accumulator.GateCount += moment.GateCount;
            moments[moment.Name] = accumulator;
        }

        if (collectSweepSummaries)
        {
            foreach (var sweep in summary.Type31.Sweeps)
            {
                sweeps.Add(SweepAccumulator.FromSummary(sweeps.Count + 1, sweep));
            }
        }
    }

    public void AcceptMessage(ReadOnlySpan<byte> message) =>
        AcceptMessage(message, default);

    public void AcceptMessage(ReadOnlySpan<byte> message, ArchiveTwoMessageSource source)
    {
        if (message.Length < MessageHeaderLength)
        {
            throw new InvalidDataException("RDA/RPG message is shorter than the 16-byte message header.");
        }

        var messageType = message[3];
        messageCount++;
        messageTypeCounts.TryGetValue(messageType, out var count);
        messageTypeCounts[messageType] = count + 1;

        if (messageType == 31)
        {
            ParseType31(message[MessageHeaderLength..], source);
        }
    }

    public ArchiveTwoMessageSummary Build() =>
        new(
            messageCount,
            messageTypeCounts
                .OrderBy(pair => pair.Key)
                .Select(pair => new ArchiveTwoMessageTypeCount(pair.Key, pair.Value))
                .ToArray(),
            new ArchiveTwoType31Summary(
                type31RadialCount,
                estimatedGateMomentEvents,
                new ArchiveTwoType31ConstantBlockSummary(
                    volumeConstantBlockCount,
                    elevationConstantBlockCount,
                    radialConstantBlockCount),
                moments
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new ArchiveTwoMomentSummary(
                        pair.Key,
                        pair.Value.RadialCount,
                        pair.Value.GateCount))
                    .ToArray(),
                collectSweepSummaries
                    ? sweeps.Select(sweep => sweep.ToSummary()).ToArray()
                    : Array.Empty<ArchiveTwoSweepSummary>()));

    private void ParseType31(ReadOnlySpan<byte> payload, ArchiveTwoMessageSource source)
    {
        if (payload.Length < Type31DataHeaderMinimumLength)
        {
            return;
        }

        var radialLength = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(18, 2));
        var parseLength = Math.Min(payload.Length, radialLength > 0 ? radialLength : payload.Length);
        var parsePayload = payload[..parseLength];
        var radial = ReadType31RadialMetadata(parsePayload);
        var sourceOrder = new ArchiveTwoRadialSourceOrder(
            source.CompressedRecordSequenceNumber,
            source.MessageSequenceNumberInRecord,
            type31RadialCount + 1);
        var sweep = AcceptRadial(radial, sourceOrder);
        var blockCount = BinaryPrimitives.ReadUInt16BigEndian(parsePayload.Slice(30, 2));
        var pointerCount = Math.Min(
            Math.Min((int)blockCount, Type31MaximumDataBlockPointers),
            (parsePayload.Length - Type31DataBlockPointerOffset) / Type31DataBlockPointerLength);

        for (var i = 0; i < pointerCount; i++)
        {
            var pointer = BinaryPrimitives.ReadInt32BigEndian(
                parsePayload.Slice(Type31DataBlockPointerOffset + i * Type31DataBlockPointerLength, Type31DataBlockPointerLength));
            if (pointer <= 0 || pointer >= parsePayload.Length)
            {
                continue;
            }

            var block = parsePayload[pointer..];
            if (block.Length < 4)
            {
                continue;
            }

            var blockName = ReadDataBlockName(block);
            if (block[0] == (byte)'R')
            {
                AcceptConstantBlock(blockName, sweep);
                continue;
            }

            if (block[0] == (byte)'D')
            {
                AcceptMomentBlock(block, blockName, sweep);
            }
        }

        type31RadialCount++;
    }

    private static Type31RadialMetadata ReadType31RadialMetadata(ReadOnlySpan<byte> payload) =>
        new(
            payload[21],
            payload[22],
            payload[23],
            ReadSingleBigEndian(payload.Slice(24, 4)));

    private SweepAccumulator? AcceptRadial(
        Type31RadialMetadata radial,
        ArchiveTwoRadialSourceOrder sourceOrder)
    {
        if (!collectSweepSummaries)
        {
            return null;
        }

        if (currentSweep is null ||
            ShouldStartNewSweep(radial, currentSweep))
        {
            currentSweep = new SweepAccumulator(
                sweeps.Count + 1,
                radial.ElevationNumber,
                radial.RadialStatus,
                sourceOrder);
            sweeps.Add(currentSweep);
        }

        currentSweep.AcceptRadial(radial, sourceOrder);
        return currentSweep;
    }

    private static bool ShouldStartNewSweep(Type31RadialMetadata radial, SweepAccumulator current) =>
        current.RadialCount > 0 &&
        (IsStartRadialStatus(radial.RadialStatus) ||
            radial.ElevationNumber != current.ElevationNumber);

    private static bool IsStartRadialStatus(int radialStatus) =>
        radialStatus is 0 or 3 or 5 or 80 or 83 or 85;

    private void AcceptConstantBlock(string blockName, SweepAccumulator? sweep)
    {
        switch (blockName)
        {
            case "VOL":
                volumeConstantBlockCount++;
                sweep?.AcceptVolumeConstantBlock();
                break;
            case "ELV":
                elevationConstantBlockCount++;
                sweep?.AcceptElevationConstantBlock();
                break;
            case "RAD":
                radialConstantBlockCount++;
                sweep?.AcceptRadialConstantBlock();
                break;
        }
    }

    private void AcceptMomentBlock(
        ReadOnlySpan<byte> block,
        string name,
        SweepAccumulator? sweep)
    {
        if (name.Length == 0 ||
            block.Length < GenericMomentDescriptorLength)
        {
            return;
        }

        var gateCount = BinaryPrimitives.ReadUInt16BigEndian(block.Slice(8, 2));
        moments.TryGetValue(name, out var accumulator);
        accumulator.RadialCount++;
        accumulator.GateCount += gateCount;
        moments[name] = accumulator;
        sweep?.AcceptMoment(name);
        estimatedGateMomentEvents += gateCount;
        if (decodeMomentValues)
        {
            DecodeMomentValues(block, gateCount);
        }
    }

    private static string ReadDataBlockName(ReadOnlySpan<byte> block) =>
        Encoding.ASCII.GetString(block.Slice(1, 3)).TrimEnd('\0', ' ');

    private static float ReadSingleBigEndian(ReadOnlySpan<byte> buffer) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(buffer));

    private void DecodeMomentValues(ReadOnlySpan<byte> block, int gateCount)
    {
        var wordSizeBits = block[GenericMomentWordSizeOffset];
        switch (wordSizeBits)
        {
            case 8:
                DecodeEightBitMomentValues(block[GenericMomentDataOffset..], gateCount);
                break;
            case 16:
                DecodeSixteenBitMomentValues(block[GenericMomentDataOffset..], gateCount);
                break;
        }
    }

    private void DecodeEightBitMomentValues(ReadOnlySpan<byte> data, int gateCount)
    {
        if (data.Length < gateCount)
        {
            return;
        }

        for (var i = 0; i < gateCount; i++)
        {
            unchecked
            {
                decodedGateMomentValueChecksum += data[i];
            }
        }

        decodedGateMomentValues += gateCount;
    }

    private void DecodeSixteenBitMomentValues(ReadOnlySpan<byte> data, int gateCount)
    {
        var requiredBytes = checked(gateCount * sizeof(ushort));
        if (data.Length < requiredBytes)
        {
            return;
        }

        for (var i = 0; i < requiredBytes; i += sizeof(ushort))
        {
            unchecked
            {
                decodedGateMomentValueChecksum += BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i, sizeof(ushort)));
            }
        }

        decodedGateMomentValues += gateCount;
    }

    private struct MomentAccumulator
    {
        public int RadialCount;
        public long GateCount;
    }

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
