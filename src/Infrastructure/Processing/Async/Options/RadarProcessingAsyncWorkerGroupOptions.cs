using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingAsyncWorkerGroupOptions
{
    public static RadarProcessingAsyncWorkerGroupOptions Default { get; } = new();

    public RadarProcessingAsyncWorkerGroupOptions(
        RadarProcessingAsyncExecutionOptions? execution = null)
    {
        Execution = execution ?? RadarProcessingAsyncExecutionOptions.Default;
    }

    public RadarProcessingAsyncExecutionOptions Execution { get; }

    public int WorkerCount => Execution.WorkerCount;

    public int QueueCapacity => Execution.QueueCapacity;
}
