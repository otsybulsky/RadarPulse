using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public static partial class RadarProcessingProductionPipelineProfile
{
    private static void ValidateProviderMode(
        RadarProcessingArchiveProviderMode value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.ProviderMode),
                "Production pipeline provider mode must remain queued-owned.");
        }
    }

    private static void ValidateProviderOverlapMode(
        RadarProcessingQueuedProviderOverlapMode value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingQueuedProviderOverlapMode.ProducerConsumer)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.ProviderOverlapMode),
                "Production pipeline provider overlap must remain producer-consumer.");
        }
    }

    private static void ValidateRetentionStrategy(
        RadarProcessingRetainedPayloadStrategy value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingRetainedPayloadStrategy.PooledCopy)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.RetentionStrategy),
                "Production pipeline retention strategy must remain pooled-copy.");
        }
    }

    private static void ValidateExecutionMode(
        RadarProcessingExecutionMode value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingExecutionMode.AsyncShardTransport)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.ExecutionMode),
                "Production pipeline execution mode must remain async shard transport.");
        }
    }

    private static void ValidateDurableAdapterKind(
        RadarProcessingProductionPipelineDurableAdapterKind value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingProductionPipelineDurableAdapterKind.File)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.DurableAdapterKind),
                "Milestone 027 accepts only the file durable adapter kind.");
        }
    }

    private static void ValidateHandlerMode(
        RadarProcessingProductionPipelineHandlerMode value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingProductionPipelineHandlerMode.Auto and
            not RadarProcessingProductionPipelineHandlerMode.HandlerFree and
            not RadarProcessingProductionPipelineHandlerMode.MergeableDelta and
            not RadarProcessingProductionPipelineHandlerMode.SnapshotSequential)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.HandlerMode),
                "Unknown production pipeline handler mode.");
        }
    }

    private static void ValidatePositive<T>(
        T value,
        string option,
        FirstInvalid invalid)
        where T : struct, IComparable<T>
    {
        if (value.CompareTo(default) <= 0)
        {
            invalid.Set(option, "Production pipeline numeric options must be positive.");
        }
    }

    private static void ValidateNullablePositive(
        int? value,
        string option,
        FirstInvalid invalid)
    {
        if (value.HasValue && value.Value <= 0)
        {
            invalid.Set(option, "Production pipeline workload limits must be positive when specified.");
        }
    }

    private sealed class FirstInvalid
    {
        public string? Option { get; private set; }

        public string? Reason { get; private set; }

        public bool HasValue => Option is not null;

        public void Set(string option, string reason)
        {
            if (HasValue)
            {
                return;
            }

            Option = option;
            Reason = reason;
        }
    }
}
