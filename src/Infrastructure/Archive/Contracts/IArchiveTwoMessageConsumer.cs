using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Consumes complete RDA/RPG messages decoded from Archive II compressed records.
/// </summary>
public interface IArchiveTwoMessageConsumer
{
    /// <summary>
    /// Accepts one complete message with its compressed-record source position and timestamp metadata.
    /// </summary>
    void AcceptMessage(ReadOnlySpan<byte> message, ArchiveTwoMessageSource source);
}
