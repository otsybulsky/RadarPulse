using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayPublishSession
{
    private sealed record ArchiveReplayRecordMetadata(
        IReadOnlyList<ArchiveReplayRadialMetadata> Radials);

    private readonly record struct ArchiveReplayRadialMetadata(
        int RadialStatus,
        int ElevationNumber);

    private sealed class ArchiveReplayRecordMetadataCollector : IArchiveTwoMessageConsumer
    {
        private const int MessageHeaderLength = 16;
        private const int Type31DataHeaderMinimumLength = 72;
        private readonly List<ArchiveReplayRadialMetadata> radials = new();

        public void Reset() => radials.Clear();

        public ArchiveReplayRecordMetadata Build() => new(radials.ToArray());

        public void AcceptMessage(ReadOnlySpan<byte> message, ArchiveTwoMessageSource source)
        {
            if (message.Length < MessageHeaderLength || message[3] != 31)
            {
                return;
            }

            var payload = message[MessageHeaderLength..];
            if (payload.Length < Type31DataHeaderMinimumLength)
            {
                return;
            }

            radials.Add(new ArchiveReplayRadialMetadata(
                payload[21],
                payload[22]));
        }
    }

}
