using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsParseCacheSelection()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--date",
            "2026-05-04",
            "--radar",
            "ktlx",
            "--max-files",
            "100",
            "--mode",
            "rebalance"
        ]);

        Assert.Null(options.FilePath);
        Assert.Equal("data/nexrad", options.CachePath);
        Assert.Equal(new DateOnly(2026, 5, 4), options.Date);
        Assert.Equal("KTLX", options.RadarId);
        Assert.Equal(100, options.MaxFiles);
        Assert.Equal(
            [RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession],
            options.Modes);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsParseRetentionSettings()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--mode",
            "rebalance",
            "--retention-mode",
            "counters",
            "--max-retained-decisions",
            "4",
            "--max-retained-transitions",
            "3",
            "--max-retained-accepted-moves",
            "2",
            "--max-retained-validation-failures",
            "1"
        ]);

        Assert.Equal(
            RadarProcessingDiagnosticRetentionMode.Counters,
            options.TelemetryRetention.RetentionMode);
        Assert.Equal(4, options.TelemetryRetention.MaxRetainedDecisions);
        Assert.Equal(3, options.TelemetryRetention.MaxRetainedLifecycleTransitions);
        Assert.Equal(2, options.TelemetryRetention.MaxRetainedAcceptedMoves);
        Assert.Equal(1, options.TelemetryRetention.MaxRetainedValidationFailures);
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsParsePressureSkewSettings()
    {
        var options = global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(
        [
            "--cache",
            "data/nexrad",
            "--mode",
            "rebalance",
            "--skew-profile",
            "rotating-hot-shard",
            "--skew-factor",
            "2.5",
            "--skew-period",
            "4"
        ]);

        Assert.Equal(RadarProcessingPressureSkewProfile.RotatingHotShard, options.PressureSkew.Profile);
        Assert.Equal(2.5, options.PressureSkew.Factor);
        Assert.Equal(4, options.PressureSkew.Period);
    }

}
