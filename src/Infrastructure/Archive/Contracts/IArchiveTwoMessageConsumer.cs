using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public interface IArchiveTwoMessageConsumer
{
    void AcceptMessage(ReadOnlySpan<byte> message, ArchiveTwoMessageSource source);
}
