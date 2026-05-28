using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Measures archive replay with rebalance processing across provider and execution modes.
/// </summary>
public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private const ulong ChecksumInitial = 14_695_981_039_346_656_037UL;
    private const ulong ChecksumPrime = 1_099_511_628_211UL;
    private const int MaxAutoSizedCacheRadarOrdinalCount = 256;

    private readonly IArchiveBZip2Decompressor decompressor;

    /// <summary>
    /// Creates a benchmark with the default archive decompressor.
    /// </summary>
    public RadarProcessingArchiveRebalanceBenchmark()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    /// <summary>
    /// Creates a benchmark with an explicit archive decompressor.
    /// </summary>
    public RadarProcessingArchiveRebalanceBenchmark(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    /// <summary>
    /// Measures archive rebalance processing over one local archive file.
    /// </summary>
}
