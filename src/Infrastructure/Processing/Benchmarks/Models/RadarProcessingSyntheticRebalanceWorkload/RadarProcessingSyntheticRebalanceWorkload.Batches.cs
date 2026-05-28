using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceWorkload
{
    private static int[][] RepeatBatch(
        int count,
        int[] sourceIds)
    {
        var result = new int[count][];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = (int[])sourceIds.Clone();
        }

        return result;
    }

    private static int[][] RepeatPattern(
        int count,
        int[][] pattern)
    {
        var result = new int[count][];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = (int[])pattern[index % pattern.Length].Clone();
        }

        return result;
    }

    private static int[][] PrependBatch(
        int[] first,
        int[][] rest)
    {
        var result = new int[rest.Length + 1][];
        result[0] = (int[])first.Clone();
        for (var index = 0; index < rest.Length; index++)
        {
            result[index + 1] = rest[index];
        }

        return result;
    }

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds,
        int batchIndex)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var eventIndex = 0; eventIndex < sourceIds.Length; eventIndex++)
        {
            var sourceId = sourceIds[eventIndex];
            events[eventIndex] = new RadarStreamEvent(
                sourceId,
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 1_000,
                messageTimestampUtcTicks: 10_000 + sourceId,
                sourceRecord: batchIndex + 1,
                sourceMessage: eventIndex + 1,
                radialSequence: checked((batchIndex * 1_000) + eventIndex),
                elevationSlot: 0,
                azimuthBucket: (ushort)sourceId,
                rangeBand: 0,
                momentId: 0,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: eventIndex,
                payloadLength: 1);
            payload[eventIndex] = (byte)(1 + ((batchIndex + eventIndex + sourceId) % 251));
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }

    private static void ApplyClassification(
        RadarProcessingHotPartitionClassifier classifier,
        InitialHotPartitionClassification classification)
    {
        switch (classification.Classification)
        {
            case RadarProcessingHotPartitionClassification.IntrinsicHot:
                classifier.ClassifyIntrinsicHot(
                    classification.PartitionId,
                    classification.ShardId,
                    evaluationSequence: 0);
                break;

            case RadarProcessingHotPartitionClassification.Quarantined:
                classifier.ClassifyQuarantined(
                    classification.PartitionId,
                    classification.ShardId,
                    evaluationSequence: 0);
                break;

            case RadarProcessingHotPartitionClassification.MovableHot:
                classifier.ClassifyMovableHot(
                    classification.PartitionId,
                    classification.ShardId,
                    evaluationSequence: 0);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(classification));
        }
    }

}
