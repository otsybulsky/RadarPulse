using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Runtime plan that selects ordered concurrency or sequential fallback for MVP processing.
/// </summary>
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

    /// <summary>
    /// Handler output contract that drives the runtime decision.
    /// </summary>
    public RadarProcessingHandlerOutputContract HandlerOutputContract { get; }

    /// <summary>
    /// Ordered concurrency requested by the caller.
    /// </summary>
    public RadarProcessingOrderedConcurrencyOptions RequestedOrderedConcurrencyOptions { get; }

    /// <summary>
    /// Ordered concurrency that will actually be used.
    /// </summary>
    public RadarProcessingOrderedConcurrencyOptions EffectiveOrderedConcurrencyOptions { get; }

    /// <summary>
    /// Indicates whether stateful handler output forced sequential processing.
    /// </summary>
    public bool UsedSequentialFallback { get; }

    /// <summary>
    /// Human-readable reason for the selected plan.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether handler-free ordered concurrent delta processing is allowed.
    /// </summary>
    public bool AllowsOrderedConcurrentDelta =>
        HandlerOutputContract.AllowsOrderedConcurrentDelta &&
        !EffectiveOrderedConcurrencyOptions.IsSequential;

    /// <summary>
    /// Indicates whether mergeable handler delta/merge ordered processing is allowed.
    /// </summary>
    public bool AllowsOrderedConcurrentHandlerDeltaMerge =>
        HandlerOutputContract.AllowsOrderedConcurrentHandlerDeltaMerge &&
        !EffectiveOrderedConcurrencyOptions.IsSequential;

    /// <summary>
    /// Creates an MVP runtime plan from the processing core handler contract.
    /// </summary>
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
                requested,
                usedSequentialFallback: false,
                "Mergeable handler output uses ordered concurrent handler delta/merge.");
        }

        return new RadarProcessingMvpRuntimePlan(
            handlerContract,
            requested,
            requested,
            usedSequentialFallback: false,
            "Handler-free MVP processing can use the requested ordered concurrency options.");
    }
}
