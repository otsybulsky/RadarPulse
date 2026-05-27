namespace RadarPulse.Domain.Processing;

/// <summary>
/// Supported handler snapshot field value types.
/// </summary>
public enum RadarSourceProcessingSnapshotFieldType
{
    /// <summary>
    /// Signed 64-bit integer field.
    /// </summary>
    Int64 = 0,

    /// <summary>
    /// Double-precision floating point field.
    /// </summary>
    Double
}
