using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedPayloadFactoryTests
{
    [Fact]
    public void BuilderTransferIsExplicitlyUnsupportedUntilTransferSemanticsAreProven()
    {
        var factory = new RadarProcessingRetainedPayloadFactory();
        var batch = CreateBuilder([1]).Build();

        var result = factory.Retain(
            batch,
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.BuilderTransfer));

        Assert.False(result.IsSuccessful);
        Assert.Equal(RadarProcessingRetainedPayloadRetentionStatus.UnsupportedStrategy, result.Status);
        Assert.Null(result.Batch);
        Assert.Null(result.Resource);
        Assert.Contains("not implemented", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetentionCanBeCanceledBeforeCopyStarts()
    {
        var factory = new RadarProcessingRetainedPayloadFactory();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = factory.Retain(
            CreateBuilder([1]).Build(),
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy),
            cancellation.Token);

        Assert.Equal(RadarProcessingRetainedPayloadRetentionStatus.Canceled, result.Status);
        Assert.False(result.IsSuccessful);
        Assert.Null(result.Batch);
        Assert.Null(result.Resource);
    }

    [Fact]
    public void PooledCopyFailureReturnsRentedStorageBeforeReportingFailure()
    {
        var eventPool = new TrackingArrayPool<RadarStreamEvent>();
        var payloadPool = new ThrowingArrayPool<byte>();
        var factory = new RadarProcessingRetainedPayloadFactory(eventPool, payloadPool);
        var builder = CreateBuilder([1, 2]);
        RadarProcessingRetainedPayloadRetentionResult? result = null;

        builder.ConsumeLeased(batch =>
        {
            result = factory.Retain(
                batch,
                new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));
        });

        Assert.NotNull(result);
        Assert.Equal(RadarProcessingRetainedPayloadRetentionStatus.FailedCopy, result.Status);
        Assert.Null(result.Batch);
        Assert.Null(result.Resource);
        Assert.Equal(1, eventPool.RentCount);
        Assert.Equal(1, eventPool.ReturnCount);
    }

}
