using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingQueuedProviderValidator
{
    private static RadarProcessingQueuedProviderValidationResult ValidateEssential(
        RadarProcessingQueuedSessionResult result,
        RadarProcessingQueuedProviderValidationProfile profile,
        RadarProcessingQueuedProviderValidationContext? context)
    {
        var acceptedSequences = new HashSet<long>();
        long? previousProviderSequence = null;
        var expectedProviderSequence = 0L;

        foreach (var enqueue in result.EnqueueResults)
        {
            RadarProcessingQueuedBatchEnqueueResult.EnsureKnownStatus(enqueue.Status);
            if (!enqueue.IsAccepted)
            {
                continue;
            }

            var batch = enqueue.Batch ??
                        throw new InvalidOperationException("Accepted enqueue results must carry a queued batch.");
            if (batch.Batch.Lifetime != RadarEventBatchLifetime.Owned)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.NonOwnedQueuedBatch,
                    "Queued provider batches must own their payload.",
                    profile,
                    context: context);
            }

            var sequence = batch.Sequence.Value;
            if (sequence > expectedProviderSequence)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.ProviderSequenceGap,
                    "Accepted provider sequence ids must be contiguous.",
                    profile,
                    context: context,
                    expectedCount: expectedProviderSequence,
                    actualCount: sequence);
            }

            if (previousProviderSequence.HasValue && sequence <= previousProviderSequence.Value)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.ProviderSequenceRegression,
                    "Accepted provider sequence ids must increase monotonically.",
                    profile,
                    context: context);
            }

            previousProviderSequence = sequence;
            acceptedSequences.Add(sequence);
            expectedProviderSequence = checked(sequence + 1);
        }

        var processedSequences = new HashSet<long>();
        long? previousProcessingSequence = null;
        RadarProcessingTopologyVersion? previousTopologyVersion = null;
        foreach (var processing in result.ProcessingResults)
        {
            RadarProcessingQueuedBatchProcessingResult.EnsureKnownStatus(processing.Status);
            var sequence = processing.Sequence.Value;
            if (previousProcessingSequence.HasValue && sequence <= previousProcessingSequence.Value)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.ProcessingSequenceRegression,
                    "Processed provider sequence ids must increase monotonically.",
                    profile,
                    context: context);
            }

            previousProcessingSequence = sequence;
            processedSequences.Add(sequence);

            if (processing.TopologyVersion is not { } topologyVersion)
            {
                continue;
            }

            if (previousTopologyVersion.HasValue &&
                topologyVersion.Value < previousTopologyVersion.Value.Value)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.TopologyVersionRegression,
                    "Queued processing topology versions must not regress.",
                    profile,
                    context: context);
            }

            previousTopologyVersion = topologyVersion;
            if (result.FinalTopologyVersion.HasValue &&
                topologyVersion.Value > result.FinalTopologyVersion.Value.Value)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.TopologyVersionRegression,
                    "Final queued topology version must cover every processed topology version.",
                    profile,
                    context: context);
            }
        }

        if (!result.IsCanceled)
        {
            foreach (var sequence in acceptedSequences.OrderBy(static sequence => sequence))
            {
                if (!processedSequences.Contains(sequence))
                {
                    var nextProcessedSequence = processedSequences
                        .Where(processedSequence => processedSequence > sequence)
                        .DefaultIfEmpty(-1)
                        .Min();
                    if (nextProcessedSequence >= 0)
                    {
                        return Invalid(
                            RadarProcessingQueuedProviderValidationError.ProcessingSequenceGap,
                            "Processed provider sequence ids must be contiguous.",
                            profile,
                            context: context,
                            expectedCount: sequence,
                            actualCount: nextProcessedSequence);
                    }

                    return Invalid(
                        RadarProcessingQueuedProviderValidationError.MissingCompletionForAcceptedBatch,
                        "Every accepted provider sequence requires a processing result unless the session is canceled.",
                        profile,
                        context: context);
                }
            }

            foreach (var sequence in processedSequences.OrderBy(static sequence => sequence))
            {
                if (!acceptedSequences.Contains(sequence))
                {
                    return Invalid(
                        RadarProcessingQueuedProviderValidationError.ProcessingSequenceGap,
                        "Processed provider sequence ids must not include unaccepted provider sequence ids.",
                        profile,
                        context: context,
                        expectedCount: expectedProviderSequence,
                        actualCount: sequence);
                }
            }
        }

        return Valid(profile, context);
    }

}
