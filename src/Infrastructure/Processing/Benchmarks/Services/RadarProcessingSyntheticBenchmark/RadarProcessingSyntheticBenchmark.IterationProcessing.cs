using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticBenchmark
{
    private static async ValueTask<ProcessingIterationResult> ProcessAndValidateIterationAsync(
        RadarProcessingCore core,
        RadarProcessingAsyncCoreSession? asyncSession,
        RadarProcessingSyntheticWorkload workload,
        RadarProcessingCoreOptions coreOptions,
        IterationTotals? expectedIteration,
        CancellationToken cancellationToken)
    {
        var before = core.CreateMetrics();
        RadarProcessingResult? lastResult = null;

        foreach (var batch in workload.Batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = asyncSession is null
                ? core.Process(batch, cancellationToken)
                : await asyncSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
            if (!result.IsValid)
            {
                throw new InvalidDataException(result.Validation.Message);
            }

            if ((coreOptions.ExecutionMode == RadarProcessingExecutionMode.PartitionedBarrier ||
                 coreOptions.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport) &&
                result.Telemetry is null)
            {
                throw new InvalidDataException("Partitioned or async processing benchmark did not produce telemetry.");
            }

            if (coreOptions.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
            {
                var asyncValidation = RadarProcessingAsyncValidator.ValidateProcessingResult(
                    result,
                    RadarProcessingValidationProfile.Benchmark);
                if (!asyncValidation.IsValid)
                {
                    throw new InvalidDataException(asyncValidation.Message);
                }
            }

            lastResult = result;
        }

        if (lastResult is null)
        {
            throw new InvalidOperationException("Processing benchmark workload has no batches.");
        }

        var after = core.CreateMetrics();
        var iterationTotals = IterationTotals.Create(before, after);
        if (iterationTotals.BatchCount != workload.BatchesPerIteration ||
            iterationTotals.EventCount != workload.EventsPerIteration ||
            iterationTotals.PayloadValueCount != workload.PayloadValuesPerIteration ||
            iterationTotals.RawValueChecksum != workload.RawValueChecksumPerIteration)
        {
            throw new InvalidDataException("Processing benchmark iteration totals do not match the workload contract.");
        }

        if (expectedIteration.HasValue && !expectedIteration.Value.HasSameTotals(iterationTotals))
        {
            throw new InvalidDataException("Processing benchmark produced inconsistent iteration totals.");
        }

        return new ProcessingIterationResult(iterationTotals, lastResult);
    }
}
