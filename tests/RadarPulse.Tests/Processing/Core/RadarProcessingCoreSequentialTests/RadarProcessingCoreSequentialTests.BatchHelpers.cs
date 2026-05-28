using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingCoreSequentialTests
{
    private static RadarSourceUniverse CreateUniverse(
        int sourceCount,
        SourceUniverseVersion? version = null) =>
        new(
            version ?? SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEmptyBatch(
        SourceUniverseVersion sourceUniverseVersion,
        StreamSchemaVersion? streamSchemaVersion = null) =>
        CreateBatch(
            sourceUniverseVersion,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>(),
            streamSchemaVersion);

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        RadarStreamEvent[] events,
        byte[] payload,
        StreamSchemaVersion? streamSchemaVersion = null) =>
        new(
            streamSchemaVersion ?? StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
}
