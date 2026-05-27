namespace RadarPulse.Infrastructure.Archive;

public interface IDiskSpaceProbe
{
    long GetAvailableBytes(string path);
}
