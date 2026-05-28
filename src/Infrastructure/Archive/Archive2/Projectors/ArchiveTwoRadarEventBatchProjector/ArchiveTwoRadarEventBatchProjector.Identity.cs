using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

internal sealed partial class ArchiveTwoRadarEventBatchProjector : IArchiveTwoMessageConsumer
{
    private RadarStreamIdentity ResolveIdentity(
        ReadOnlySpan<byte> momentNameUtf8,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        var momentCode = GetMomentCode(momentNameUtf8);
        if (!identityCacheByMomentCode.TryGetValue(momentCode, out var dimensions))
        {
            return ResolveIdentityAndCacheDimensions(
                momentCode,
                momentNameUtf8,
                elevationSlot,
                azimuthBucket,
                rangeBand);
        }

        return CreateIdentityFromCachedDimensions(
            dimensions,
            elevationSlot,
            azimuthBucket,
            rangeBand);
    }

    private RadarStreamIdentity ResolveIdentityAndCacheDimensions(
        int momentCode,
        ReadOnlySpan<byte> momentNameUtf8,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        var result = identityNormalizer.TryNormalize(
            radarIdUtf8,
            momentNameUtf8,
            elevationSlot,
            azimuthBucket,
            rangeBand);
        if (result.IsResolved)
        {
            CacheIdentityDimensions(momentCode, result.Identity);
            if (result.Identity.DictionaryVersion.Value > currentDictionaryVersion.Value)
            {
                currentDictionaryVersion = result.Identity.DictionaryVersion;
            }

            return result.Identity;
        }

        throw new InvalidDataException($"Failed to normalize radar stream identity: {result.Error}.");
    }

    private void CacheIdentityDimensions(int momentCode, RadarStreamIdentity identity)
    {
        if (identityCacheByMomentCode.ContainsKey(momentCode))
        {
            return;
        }

        identityCacheByMomentCode.Add(
            momentCode,
            new CachedIdentityDimensions(
                identity.RadarOrdinal,
                identity.MomentId,
                sourceUniverse.GetRadarSourceBlockStart(identity.RadarOrdinal)));
    }

    private RadarStreamIdentity CreateIdentityFromCachedDimensions(
        CachedIdentityDimensions dimensions,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        if ((uint)elevationSlot >= (uint)elevationSlotCount ||
            (uint)azimuthBucket >= (uint)azimuthBucketCount ||
            (uint)rangeBand >= (uint)rangeBandCount)
        {
            throw new InvalidDataException("Radar stream source dimensions are outside the source universe.");
        }

        if (elevationSlot > ushort.MaxValue ||
            azimuthBucket > ushort.MaxValue ||
            rangeBand > ushort.MaxValue)
        {
            throw new InvalidDataException("Radar stream source dimensions exceed the stream event range.");
        }

        var sourceId =
            dimensions.RadarSourceBlockStart +
            (elevationSlot * sourcesPerElevationSlot) +
            (azimuthBucket * sourcesPerAzimuthBucket) +
            rangeBand;

        return new RadarStreamIdentity(
            sourceId,
            dimensions.RadarOrdinal,
            dimensions.MomentId,
            checked((ushort)elevationSlot),
            checked((ushort)azimuthBucket),
            checked((ushort)rangeBand),
            currentDictionaryVersion,
            sourceUniverseVersion);
    }
}
