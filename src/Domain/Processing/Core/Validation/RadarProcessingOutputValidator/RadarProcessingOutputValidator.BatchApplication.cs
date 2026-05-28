using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingOutputValidator
{
    private static RadarProcessingValidationResult ApplyBatch(
        RadarEventBatch batch,
        RadarSourceProcessingSnapshot[] expectedSnapshots)
    {
        var events = batch.Events.Span;
        var payload = batch.Payload.Span;

        for (var eventIndex = 0; eventIndex < events.Length; eventIndex++)
        {
            var streamEvent = events[eventIndex];
            if ((uint)streamEvent.SourceId >= (uint)expectedSnapshots.Length)
            {
                return RadarProcessingValidationResult.Invalid(
                    RadarProcessingValidationError.SourceIdOutsideUniverse,
                    streamEvent.SourceId,
                    eventIndex,
                    "Event SourceId is outside the supplied processing snapshots.");
            }

            var snapshot = expectedSnapshots[streamEvent.SourceId];
            if (snapshot.IsActive &&
                streamEvent.MessageTimestampUtcTicks < snapshot.LastMessageTimestampUtcTicks)
            {
                return RadarProcessingValidationResult.Invalid(
                    RadarProcessingValidationError.SourceOrderViolation,
                    streamEvent.SourceId,
                    eventIndex,
                    "Source-local events must be applied by non-decreasing message timestamp.");
            }

            var payloadMetrics = RadarProcessingPayloadReader.ComputeEventMetrics(streamEvent, payload);
            expectedSnapshots[streamEvent.SourceId] = ApplyExpectedEvent(
                snapshot,
                streamEvent,
                payloadMetrics);
        }

        return RadarProcessingValidationResult.Valid(default);
    }

    private static RadarSourceProcessingSnapshot ApplyExpectedEvent(
        RadarSourceProcessingSnapshot snapshot,
        in RadarStreamEvent streamEvent,
        RadarProcessingPayloadMetrics payloadMetrics)
    {
        var checksum = RadarSourceProcessingChecksum.AppendEvent(
            snapshot.IsActive ? snapshot.ProcessingChecksum : RadarStreamChecksum.Initial,
            streamEvent,
            payloadMetrics.PayloadValueCount,
            payloadMetrics.RawValueChecksum);

        return new RadarSourceProcessingSnapshot(
            snapshot.SourceId,
            true,
            checked(snapshot.ProcessedEventCount + 1),
            checked(snapshot.ProcessedPayloadValueCount + payloadMetrics.PayloadValueCount),
            checked(snapshot.RawValueChecksum + payloadMetrics.RawValueChecksum),
            streamEvent.MessageTimestampUtcTicks,
            checksum);
    }
}
