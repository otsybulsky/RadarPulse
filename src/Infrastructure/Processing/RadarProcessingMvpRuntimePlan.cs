using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingMvpRuntimePlan
{
    private RadarProcessingMvpRuntimePlan(
        RadarProcessingHandlerOutputContract handlerOutputContract,
        RadarProcessingOrderedConcurrencyOptions requestedOrderedConcurrencyOptions,
        RadarProcessingOrderedConcurrencyOptions effectiveOrderedConcurrencyOptions,
        bool usedSequentialFallback,
        string message)
    {
        ArgumentNullException.ThrowIfNull(handlerOutputContract);
        ArgumentNullException.ThrowIfNull(requestedOrderedConcurrencyOptions);
        ArgumentNullException.ThrowIfNull(effectiveOrderedConcurrencyOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        HandlerOutputContract = handlerOutputContract;
        RequestedOrderedConcurrencyOptions = requestedOrderedConcurrencyOptions;
        EffectiveOrderedConcurrencyOptions = effectiveOrderedConcurrencyOptions;
        UsedSequentialFallback = usedSequentialFallback;
        Message = message;
    }

    public RadarProcessingHandlerOutputContract HandlerOutputContract { get; }

    public RadarProcessingOrderedConcurrencyOptions RequestedOrderedConcurrencyOptions { get; }

    public RadarProcessingOrderedConcurrencyOptions EffectiveOrderedConcurrencyOptions { get; }

    public bool UsedSequentialFallback { get; }

    public string Message { get; }

    public bool AllowsOrderedConcurrentDelta =>
        HandlerOutputContract.AllowsOrderedConcurrentDelta &&
        !EffectiveOrderedConcurrencyOptions.IsSequential;

    public static RadarProcessingMvpRuntimePlan Create(
        RadarProcessingCore core,
        RadarProcessingOrderedConcurrencyOptions? requestedOrderedConcurrencyOptions = null)
    {
        ArgumentNullException.ThrowIfNull(core);

        var requested = requestedOrderedConcurrencyOptions ??
                        RadarProcessingRuntimeArchiveBaseline.OrderedConcurrencyOptions;
        var handlerContract = RadarProcessingHandlerOutputContract.FromOptions(core.Options);
        if (handlerContract.IsUnsupported)
        {
            return new RadarProcessingMvpRuntimePlan(
                handlerContract,
                requested,
                RadarProcessingOrderedConcurrencyOptions.Sequential,
                usedSequentialFallback: false,
                handlerContract.FirstBlockingReason ??
                "Unsupported handler output blocks MVP processing.");
        }

        if (handlerContract.RequiresSequentialFallback)
        {
            return new RadarProcessingMvpRuntimePlan(
                handlerContract,
                requested,
                RadarProcessingOrderedConcurrencyOptions.Sequential,
                usedSequentialFallback: true,
                "Stateful handler output uses committed snapshots; MVP processing uses sequential fallback until handler delta/merge exists.");
        }

        if (handlerContract.AllowsOrderedConcurrentHandlerDeltaMerge)
        {
            return new RadarProcessingMvpRuntimePlan(
                handlerContract,
                requested,
                RadarProcessingOrderedConcurrencyOptions.Sequential,
                usedSequentialFallback: true,
                "Mergeable handler output is classified, but MVP processing uses sequential fallback until ordered handler delta/merge runtime integration exists.");
        }

        return new RadarProcessingMvpRuntimePlan(
            handlerContract,
            requested,
            requested,
            usedSequentialFallback: false,
            "Handler-free MVP processing can use the requested ordered concurrency options.");
    }
}
