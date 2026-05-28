using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticBenchmark
{
    private static async ValueTask<ValidationRunResult> ComputeValidationChecksumAsync(
        RadarProcessingSyntheticWorkload workload,
        RadarProcessingCoreOptions coreOptions,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        RadarProcessingValidationProfile validationProfile,
        CancellationToken cancellationToken)
    {
        if (coreOptions.ExecutionMode != RadarProcessingExecutionMode.AsyncShardTransport)
        {
            var core = new RadarProcessingCore(workload.SourceUniverse, coreOptions);
            foreach (var batch in workload.Batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = core.Process(batch, cancellationToken);
                if (!result.IsValid)
                {
                    throw new InvalidDataException(result.Validation.Message);
                }
            }

            return new ValidationRunResult(ComputeStateChecksum(core), AsyncValidation: null);
        }

        var referenceOptions = CreateCoreOptions(
            RadarProcessingExecutionMode.PartitionedBarrier,
            coreOptions.PartitionCount,
            coreOptions.ShardCount,
            handlerSet);
        var referenceCore = new RadarProcessingCore(workload.SourceUniverse, referenceOptions);
        var asyncCore = new RadarProcessingCore(workload.SourceUniverse, coreOptions);
        RadarProcessingResult? referenceResult = null;
        RadarProcessingResult? asyncResult = null;

        await using (var asyncSession = new RadarProcessingAsyncCoreSession(asyncCore))
        {
            foreach (var batch in workload.Batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                referenceResult = referenceCore.Process(batch, cancellationToken);
                asyncResult = await asyncSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
                if (!referenceResult.IsValid)
                {
                    throw new InvalidDataException(referenceResult.Validation.Message);
                }

                if (!asyncResult.IsValid)
                {
                    throw new InvalidDataException(asyncResult.Validation.Message);
                }
            }
        }

        if (referenceResult is null || asyncResult is null)
        {
            throw new InvalidOperationException("Processing benchmark workload has no batches.");
        }

        var asyncValidation = RadarProcessingAsyncValidator.ValidateBenchmarkComparison(
            referenceResult,
            asyncResult,
            referenceCore.CreateSourceSnapshots(),
            asyncCore.CreateSourceSnapshots(),
            validationProfile);
        if (!asyncValidation.IsValid)
        {
            throw new InvalidDataException(asyncValidation.Message);
        }

        if (!asyncValidation.HasComparisonChecksums)
        {
            asyncValidation = RadarProcessingAsyncValidationResult.Valid(
                RadarProcessingValidationProfile.Benchmark,
                referenceCore.CreateMetrics().ProcessingChecksum,
                asyncCore.CreateMetrics().ProcessingChecksum);
        }

        return new ValidationRunResult(ComputeStateChecksum(asyncCore), asyncValidation);
    }

    private static ulong ComputeStateChecksum(
        RadarProcessingCore core)
    {
        var metrics = core.CreateMetrics();
        var checksum = ChecksumInitial;
        checksum = AppendInt64(checksum, metrics.ProcessedBatchCount);
        checksum = AppendInt64(checksum, metrics.ProcessedStreamEventCount);
        checksum = AppendInt64(checksum, metrics.ProcessedPayloadValueCount);
        checksum = AppendInt64(checksum, metrics.ActiveSourceCount);
        checksum = AppendInt64(checksum, metrics.RawValueChecksum);
        checksum = AppendUInt64(checksum, metrics.ProcessingChecksum);

        foreach (var snapshot in core.CreateSourceHandlerSnapshots())
        {
            checksum = AppendInt32(checksum, snapshot.SourceId);
            foreach (var value in snapshot.Values)
            {
                checksum = AppendStringOrdinal(checksum, value.Name);
                checksum = AppendInt32(checksum, (int)value.Type);
                checksum = value.Type switch
                {
                    RadarSourceProcessingSnapshotFieldType.Int64 =>
                        AppendInt64(checksum, value.Int64Value),
                    RadarSourceProcessingSnapshotFieldType.Double =>
                        AppendUInt64(checksum, (ulong)BitConverter.DoubleToInt64Bits(value.DoubleValue)),
                    _ => throw new InvalidOperationException("Unsupported handler snapshot value type.")
                };
            }
        }

        return checksum;
    }
}
