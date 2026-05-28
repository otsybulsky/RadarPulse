using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedPayloadFactoryTests
{
    private static RadarEventBatchBuilder CreateBuilder(
        byte[] payload)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: payload.Length);
        AddEvent(builder, payload);
        return builder;
    }

    private static RadarProcessingRetainedPayloadRetentionResult RetainLeasedPooledCopy(
        RadarProcessingRetainedPayloadFactory factory,
        int payloadLength)
    {
        var builder = CreateBuilderWithPayloadLength(payloadLength);
        RadarProcessingRetainedPayloadRetentionResult? result = null;

        builder.ConsumeLeased(batch =>
        {
            result = factory.Retain(
                batch,
                new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));
        });

        Assert.NotNull(result);
        Assert.True(result.IsSuccessful);
        return result;
    }

    private static RadarProcessingRetainedPayloadRetentionResult RetainLeasedPooledCopyWithEventCount(
        RadarProcessingRetainedPayloadFactory factory,
        int eventCount)
    {
        var builder = CreateBuilderWithEventCount(eventCount);
        RadarProcessingRetainedPayloadRetentionResult? result = null;

        builder.ConsumeLeased(batch =>
        {
            result = factory.Retain(
                batch,
                new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));
        });

        Assert.NotNull(result);
        Assert.True(result.IsSuccessful);
        return result;
    }

    private static RadarEventBatchBuilder CreateBuilderWithPayloadLength(
        int payloadLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(payloadLength);

        var eventCount = payloadLength == 0
            ? 0
            : ((payloadLength - 1) / ushort.MaxValue) + 1;
        var builder = new RadarEventBatchBuilder(eventCount, payloadLength);
        var remaining = payloadLength;
        var eventIndex = 0;
        while (remaining > 0)
        {
            var chunkLength = Math.Min(remaining, ushort.MaxValue);
            var payload = new byte[chunkLength];
            payload[0] = (byte)((eventIndex % 251) + 1);

            AddEvent(builder, payload);
            remaining -= chunkLength;
            eventIndex++;
        }

        return builder;
    }

    private static RadarEventBatchBuilder CreateBuilderWithEventCount(
        int eventCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eventCount);

        var builder = new RadarEventBatchBuilder(eventCount, eventCount);
        for (var i = 0; i < eventCount; i++)
        {
            AddEvent(builder, [(byte)((i % 251) + 1)]);
        }

        return builder;
    }

    private static void AddEvent(
        RadarEventBatchBuilder builder,
        byte[] payload)
    {
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId: 0,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: SourceUniverseVersion.Initial),
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: 100,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
            gateStart: 0,
            gateCount: (ushort)payload.Length,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);
    }

}
