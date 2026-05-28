using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableEnvelopeQueue
{
    public int CancelOpen(
        string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (sync)
        {
            var canceled = 0;
            foreach (var entry in bySequence.Values)
            {
                if (entry.State is not RadarProcessingDurableEnvelopeState.Pending and
                    not RadarProcessingDurableEnvelopeState.Claimed and
                    not RadarProcessingDurableEnvelopeState.Completed)
                {
                    continue;
                }

                entry.State = RadarProcessingDurableEnvelopeState.Canceled;
                entry.Message = message;
                entry.CompletedTimestamp = Stopwatch.GetTimestamp();
                canceled++;
            }

            if (canceled > 0)
            {
                PersistLocked();
            }

            return canceled;
        }
    }

    /// <summary>
    /// Releases all canceled envelopes after cleanup.
    /// </summary>
    /// <returns>The number of canceled envelopes transitioned to released.</returns>
    public int ReleaseCanceled(
        string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (sync)
        {
            var released = 0;
            foreach (var entry in bySequence.Values)
            {
                if (entry.State != RadarProcessingDurableEnvelopeState.Canceled)
                {
                    continue;
                }

                entry.State = RadarProcessingDurableEnvelopeState.Released;
                entry.Message = message;
                entry.ReleasedTimestamp = Stopwatch.GetTimestamp();
                released++;
            }

            if (released > 0)
            {
                PersistLocked();
            }

            return released;
        }
    }
}
