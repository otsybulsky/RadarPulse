namespace RadarPulse.Domain.Processing;

internal interface IRadarProcessingRetainedPayloadReleaseOwner
{
    RadarProcessingRetainedPayloadReleaseResult Release();
}
