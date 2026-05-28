using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Presentation;

public sealed partial class RadarPulseCliRebalanceBenchmarkTests
{
    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsRejectMissingAndConflictingInputs()
    {
        AssertArchiveOptionsThrows<InvalidOperationException>("--mode", "static");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--file",
            "data/nexrad/sample",
            "--cache",
            "data/nexrad");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--file",
            "data/nexrad/sample",
            "--partitions",
            "2",
            "--shards",
            "4");
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsRejectUnknownOrOutOfRangeValues()
    {
        AssertArchiveOptionsThrows<ArgumentException>("--cache", "data/nexrad", "--retention-mode", "forever");
        AssertArchiveOptionsThrows<ArgumentException>("--cache", "data/nexrad", "--validation-profile", "verbose");
        AssertArchiveOptionsThrows<ArgumentOutOfRangeException>(
            "--cache",
            "data/nexrad",
            "--quarantine-material-pressure-change",
            "-0.1");
        AssertArchiveOptionsThrows<ArgumentOutOfRangeException>(
            "--cache",
            "data/nexrad",
            "--max-retained-decisions",
            "-1");
        AssertArchiveOptionsThrows<ArgumentException>("--cache", "data/nexrad", "--skew-profile", "random");
        AssertArchiveOptionsThrows<ArgumentOutOfRangeException>("--cache", "data/nexrad", "--skew-period", "0");
        AssertArchiveOptionsThrows<ArgumentException>("--cache", "data/nexrad", "--provider", "leased");
        AssertArchiveOptionsThrows<ArgumentException>("--cache", "data/nexrad", "--queue-telemetry", "verbose");
        AssertArchiveOptionsThrows<ArgumentException>("--cache", "data/nexrad", "--provider-overlap", "threaded");
        AssertArchiveOptionsThrows<ArgumentException>("--cache", "data/nexrad", "--retention-strategy", "arena");
        AssertArchiveOptionsThrows<ArgumentException>("--cache", "data/nexrad", "--overlap-telemetry", "verbose");
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsRejectBlockingBorrowedProviderQueueSettings()
    {
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "blocking-borrowed",
            "--queue-capacity",
            "2");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "blocking-borrowed",
            "--queue-timeout-ms",
            "10");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "blocking-borrowed",
            "--provider-overlap",
            "producer-consumer");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "blocking-borrowed",
            "--retention-strategy",
            "snapshot-copy");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "blocking-borrowed",
            "--queue-retained-bytes",
            "4096");
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsRejectQueuedOwnedProviderRetentionSettings()
    {
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "queued-owned",
            "--queue-timeout-ms",
            "0");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "queued-owned",
            "--queue-retained-bytes",
            "0");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "queued-owned",
            "--retention-strategy",
            "builder-transfer");
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsRejectOverlapTelemetryWithoutProducerConsumerOverlap()
    {
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "queued-owned",
            "--overlap-telemetry",
            "summary");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "queued-owned",
            "--provider-overlap",
            "producer-consumer",
            "--overlap-consumer-delay-ms",
            "0");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "queued-owned",
            "--overlap-consumer-delay-ms",
            "10");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--provider",
            "blocking-borrowed",
            "--overlap-consumer-delay-ms",
            "10");
    }

    [Fact]
    public void ArchiveRebalanceBenchmarkOptionsRejectInvalidExecutionSettings()
    {
        AssertArchiveOptionsThrows<ArgumentException>("--cache", "data/nexrad", "--execution", "parallel");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--execution",
            "async",
            "--workers",
            "0");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--execution",
            "async",
            "--queue-capacity",
            "0");
        AssertArchiveOptionsThrows<InvalidOperationException>(
            "--cache",
            "data/nexrad",
            "--execution",
            "sync",
            "--workers",
            "2");
    }

    private static void AssertArchiveOptionsThrows<TException>(params string[] args)
        where TException : Exception
    {
        Assert.Throws<TException>(() => global::ProcessingBenchmarkArchiveRebalanceOptions.Parse(args));
    }
}
