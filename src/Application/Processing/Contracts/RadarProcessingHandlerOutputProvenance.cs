namespace RadarPulse.Application.Processing;

public enum RadarProcessingHandlerOutputProvenance
{
    HandlerFreeOrderedConcurrent = 1,
    StatefulSequentialFallback = 2,
    OrderedHandlerDeltaMerge = 3,
    UnsupportedHandlerSet = 4
}
