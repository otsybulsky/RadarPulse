using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveQueuedOverlapRunner
{
    private sealed record RetainedPayloadPrewarmLifecycle(
        RadarProcessingRetainedPayloadFactory? Factory,
        RadarProcessingRetainedPayloadPrewarmResult Result);
}
