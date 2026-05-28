using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarSourceProcessingHandlerSlotTests
{
    private static long GetInt64(
        RadarSourceProcessingHandlerSnapshot snapshot,
        string name)
    {
        Assert.True(snapshot.TryGetValue(name, out var value));
        Assert.Equal(RadarSourceProcessingSnapshotFieldType.Int64, value.Type);
        return value.Int64Value;
    }

    private static double GetDouble(
        RadarSourceProcessingHandlerSnapshot snapshot,
        string name)
    {
        Assert.True(snapshot.TryGetValue(name, out var value));
        Assert.Equal(RadarSourceProcessingSnapshotFieldType.Double, value.Type);
        return value.DoubleValue;
    }
}
