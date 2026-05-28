using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;


/// <summary>
/// Validates that sequential and parallel Archive II replay projection produce matching deterministic shape metrics.
/// </summary>
public sealed partial class NexradArchiveReplayShapeValidator
{
    private const int OutputBufferSize = 81920;

    private readonly IArchiveBZip2Decompressor decompressor;

    /// <summary>
    /// Creates a replay-shape validator with the default archive BZip2 decompressor.
    /// </summary>
    public NexradArchiveReplayShapeValidator()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    /// <summary>
    /// Creates a replay-shape validator with an explicit archive BZip2 decompressor.
    /// </summary>
    public NexradArchiveReplayShapeValidator(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    /// <summary>
}
