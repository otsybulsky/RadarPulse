namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Status model used to interpret stream event payload values.
/// </summary>
public enum RadarStreamStatusModel : byte
{
    /// <summary>
    /// Archive II moment payload status model.
    /// </summary>
    ArchiveTwoMoment = 1
}
