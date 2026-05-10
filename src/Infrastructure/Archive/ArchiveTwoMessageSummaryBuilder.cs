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

    private readonly Dictionary<int, int> messageTypeCounts = new();
    private readonly Dictionary<string, MomentAccumulator> moments = new(StringComparer.Ordinal);
    private int messageCount;
    private int type31RadialCount;
    private long estimatedGateMomentEvents;

    public int MessageCount => messageCount;

    public int Type31RadialCount => type31RadialCount;

    public long EstimatedGateMomentEventCount => estimatedGateMomentEvents;

    public void Reset()
    {
        messageTypeCounts.Clear();
        moments.Clear();
        messageCount = 0;
        type31RadialCount = 0;
        estimatedGateMomentEvents = 0;
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
        foreach (var moment in summary.Type31.Moments)
        {
            moments.TryGetValue(moment.Name, out var accumulator);
            accumulator.RadialCount += moment.RadialCount;
            accumulator.GateCount += moment.GateCount;
            moments[moment.Name] = accumulator;
        }
    }

    public void AcceptMessage(ReadOnlySpan<byte> message)
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
            ParseType31(message[MessageHeaderLength..]);
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
                moments
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new ArchiveTwoMomentSummary(
                        pair.Key,
                        pair.Value.RadialCount,
                        pair.Value.GateCount))
                    .ToArray()));

    private void ParseType31(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < Type31DataHeaderMinimumLength)
        {
            return;
        }

        var radialLength = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(18, 2));
        var parseLength = Math.Min(payload.Length, radialLength > 0 ? radialLength : payload.Length);
        var parsePayload = payload[..parseLength];
        var blockCount = BinaryPrimitives.ReadUInt16BigEndian(parsePayload.Slice(30, 2));
        var pointerCount = Math.Min(
            Math.Min((int)blockCount, Type31MaximumDataBlockPointers),
            (parsePayload.Length - Type31DataBlockPointerOffset) / Type31DataBlockPointerLength);

        var radialHadMoment = false;
        for (var i = 0; i < pointerCount; i++)
        {
            var pointer = BinaryPrimitives.ReadInt32BigEndian(
                parsePayload.Slice(Type31DataBlockPointerOffset + i * Type31DataBlockPointerLength, Type31DataBlockPointerLength));
            if (pointer <= 0 || pointer + GenericMomentDescriptorLength > parsePayload.Length)
            {
                continue;
            }

            var block = parsePayload[pointer..];
            if (block[0] != (byte)'D')
            {
                continue;
            }

            var name = Encoding.ASCII.GetString(block.Slice(1, 3)).TrimEnd();
            if (name.Length == 0)
            {
                continue;
            }

            var gateCount = BinaryPrimitives.ReadUInt16BigEndian(block.Slice(8, 2));
            moments.TryGetValue(name, out var accumulator);
            accumulator.RadialCount++;
            accumulator.GateCount += gateCount;
            moments[name] = accumulator;
            estimatedGateMomentEvents += gateCount;
            radialHadMoment = true;
        }

        if (radialHadMoment)
        {
            type31RadialCount++;
        }
    }

    private struct MomentAccumulator
    {
        public int RadialCount;
        public long GateCount;
    }
}
