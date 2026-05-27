namespace RadarPulse.Domain.Archive;

/// <summary>
/// Recognized NEXRAD archive file categories during cache inspection.
/// </summary>
public enum NexradArchiveFileKind
{
    /// <summary>
    /// File signature is not recognized by the archive inspector.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Archive II base-data volume with an AR2V volume header.
    /// </summary>
    ArchiveTwoBaseData = 1,

    /// <summary>
    /// MDM or compressed stream object that is not parsed as an Archive II base-data volume.
    /// </summary>
    MdmOrCompressedStream = 2
}

