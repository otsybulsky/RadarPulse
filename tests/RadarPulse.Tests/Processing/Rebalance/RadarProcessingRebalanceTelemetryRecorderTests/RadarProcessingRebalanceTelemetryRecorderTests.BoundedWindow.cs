using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryRecorderTests
{
    [Fact]
    public void BoundedWindowKeepsLatestItemsAndCountsDroppedItems()
    {
        var window = new RadarProcessingBoundedTelemetryWindow<string>(capacity: 2);

        window.Add("first");
        window.Add("second");
        var firstSnapshot = window.Snapshot();
        window.Add("third");

        Assert.Equal(2, window.Count);
        Assert.Equal(1, window.DroppedCount);
        Assert.Equal(new[] { "first", "second" }, firstSnapshot);
        Assert.Equal(new[] { "second", "third" }, window.Snapshot());
    }

    [Fact]
    public void BoundedWindowSupportsCountersOnlyCapacity()
    {
        var window = new RadarProcessingBoundedTelemetryWindow<string>(capacity: 0);

        window.Add("first");
        window.Add("second");

        Assert.Equal(0, window.Count);
        Assert.Equal(2, window.DroppedCount);
        Assert.Empty(window.Snapshot());
    }

    [Fact]
    public void BoundedWindowRejectsInvalidInputAndCanReset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingBoundedTelemetryWindow<string>(capacity: -1));

        var window = new RadarProcessingBoundedTelemetryWindow<string>(capacity: 1);

        Assert.Throws<ArgumentNullException>(() => window.Add(null!));

        window.Add("first");
        window.Add("second");
        window.Clear();

        Assert.Equal(0, window.Count);
        Assert.Equal(0, window.DroppedCount);
        Assert.Empty(window.Snapshot());
    }
}
