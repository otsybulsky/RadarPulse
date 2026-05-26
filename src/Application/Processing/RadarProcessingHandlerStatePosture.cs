namespace RadarPulse.Application.Processing;

public enum RadarProcessingHandlerStatePosture
{
    HandlerFreeOrderedConcurrent = 1,
    StatefulSnapshotSequentialFallback = 2,
    MergeableHandlerDeltaMergeEligible = 3,
    UnsupportedHandlerSet = 4
}
