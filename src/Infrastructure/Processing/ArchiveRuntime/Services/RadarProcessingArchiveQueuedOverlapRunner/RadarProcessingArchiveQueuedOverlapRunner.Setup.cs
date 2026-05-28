using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveQueuedOverlapRunner
{
    private static RetainedPayloadPrewarmLifecycle ApplyStartupRetainedPayloadPrewarm(
        RadarProcessingArchiveQueuedOverlapOptions options)
    {
        if (!options.RetainedPayloadPrewarmOptions.Enabled)
        {
            return new RetainedPayloadPrewarmLifecycle(
                options.RetainedPayloadFactory,
                RadarProcessingRetainedPayloadPrewarmResult.None);
        }

        var factory = options.RetainedPayloadFactory ?? new RadarProcessingRetainedPayloadFactory();
        var prewarm = factory.Prewarm(
            options.RetainedPayloadPrewarmOptions.EventCount,
            options.RetainedPayloadPrewarmOptions.PayloadBytes,
            options.RetainedPayloadPrewarmOptions.RetainedBatchCount);
        return new RetainedPayloadPrewarmLifecycle(factory, prewarm);
    }

    private static RadarProcessingArchiveQueuedOverlapOptions CreateOrderedProcessingOverlapOptions(
        RadarProcessingArchiveQueuedOverlapOptions options,
        RadarProcessingOrderedConcurrencyOptions orderedConcurrencyOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(orderedConcurrencyOptions);

        var prewarm = options.RetainedPayloadPrewarmOptions;
        if (!prewarm.Enabled ||
            prewarm.RetainedBatchCount >= orderedConcurrencyOptions.ActiveBatchCapacity)
        {
            return options;
        }

        var retainedPayloadFactory = options.RetainedPayloadFactory ??
            CreateOrderedProcessingRetainedPayloadFactory(
                prewarm,
                orderedConcurrencyOptions.ActiveBatchCapacity);

        return new RadarProcessingArchiveQueuedOverlapOptions(
            options.QueueOptions,
            options.RetainedPayloadOptions,
            retainedPayloadFactory,
            new RadarProcessingRetainedPayloadPrewarmOptions(
                prewarm.EventCount,
                prewarm.PayloadBytes,
                orderedConcurrencyOptions.ActiveBatchCapacity));
    }

    private static RadarProcessingRetainedPayloadFactory CreateOrderedProcessingRetainedPayloadFactory(
        RadarProcessingRetainedPayloadPrewarmOptions prewarm,
        int activeBatchCapacity)
    {
        var retainedEventBytes = Math.Max(
            RadarProcessingRetainedEventArrayPool.DefaultMaxRetainedBytes,
            checked((long)prewarm.EventCount * RadarStreamEvent.SizeInBytes * activeBatchCapacity));
        var retainedPayloadBytes = Math.Max(
            RadarProcessingRetainedPayloadByteArrayPool.DefaultMaxRetainedBytes,
            checked((long)prewarm.PayloadBytes * activeBatchCapacity));
        return new RadarProcessingRetainedPayloadFactory(
            new RadarProcessingRetainedEventArrayPool(
                maxRetainedArrayCount: Math.Max(
                    RadarProcessingRetainedEventArrayPool.DefaultMaxRetainedArrayCount,
                    activeBatchCapacity),
                maxRetainedBytes: retainedEventBytes),
            new RadarProcessingRetainedPayloadByteArrayPool(
                maxRetainedArrayCount: Math.Max(
                    RadarProcessingRetainedPayloadByteArrayPool.DefaultMaxRetainedArrayCount,
                    activeBatchCapacity),
                maxRetainedBytes: retainedPayloadBytes));
    }

}
