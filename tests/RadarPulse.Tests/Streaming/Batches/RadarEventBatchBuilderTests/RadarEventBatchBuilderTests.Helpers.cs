using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed partial class RadarEventBatchBuilderTests
{
    private static RadarStreamIdentity CreateIdentity(
        int sourceId,
        ushort radarOrdinal = 0,
        ushort momentId = 0,
        ushort elevationSlot = 0,
        ushort azimuthBucket = 0,
        ushort rangeBand = 0,
        DictionaryVersion? dictionaryVersion = null,
        SourceUniverseVersion? sourceUniverseVersion = null) =>
        new(
            sourceId,
            radarOrdinal,
            momentId,
            elevationSlot,
            azimuthBucket,
            rangeBand,
            dictionaryVersion ?? new DictionaryVersion(3),
            sourceUniverseVersion ?? SourceUniverseVersion.Initial);
}
