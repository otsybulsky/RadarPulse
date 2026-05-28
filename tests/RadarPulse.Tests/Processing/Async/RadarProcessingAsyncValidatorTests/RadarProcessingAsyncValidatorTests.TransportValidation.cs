using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncValidatorTests
{
    [Fact]
    public void AsyncValidationErrorEnumValuesAreStable()
    {
        Assert.Equal(0, (int)RadarProcessingAsyncValidationError.None);
        Assert.Equal(1, (int)RadarProcessingAsyncValidationError.NonAsyncExecutionMode);
        Assert.Equal(2, (int)RadarProcessingAsyncValidationError.MissingWorkerTelemetry);
        Assert.Equal(3, (int)RadarProcessingAsyncValidationError.MissingProcessingTelemetry);
        Assert.Equal(4, (int)RadarProcessingAsyncValidationError.FailedBatchCompletion);
        Assert.Equal(5, (int)RadarProcessingAsyncValidationError.IncompleteBatchCompletion);
        Assert.Equal(6, (int)RadarProcessingAsyncValidationError.WorkerFailureNotPropagated);
        Assert.Equal(7, (int)RadarProcessingAsyncValidationError.TopologyVersionMismatch);
        Assert.Equal(8, (int)RadarProcessingAsyncValidationError.UnexpectedMigrationAfterFailedProcessing);
        Assert.Equal(9, (int)RadarProcessingAsyncValidationError.MissingWorkItem);
        Assert.Equal(10, (int)RadarProcessingAsyncValidationError.DuplicateWorkAssignment);
        Assert.Equal(11, (int)RadarProcessingAsyncValidationError.WorkItemScopeMismatch);
        Assert.Equal(12, (int)RadarProcessingAsyncValidationError.WorkItemShardOwnershipMismatch);
        Assert.Equal(13, (int)RadarProcessingAsyncValidationError.WorkItemWorkerAssignmentMismatch);
        Assert.Equal(14, (int)RadarProcessingAsyncValidationError.CompletionScopeMismatch);
        Assert.Equal(15, (int)RadarProcessingAsyncValidationError.CompletionStatusMismatch);
        Assert.Equal(16, (int)RadarProcessingAsyncValidationError.AggregationMetricMismatch);
        Assert.Equal(17, (int)RadarProcessingAsyncValidationError.TelemetryMetricMismatch);
        Assert.Equal(18, (int)RadarProcessingAsyncValidationError.DeterministicChecksumMismatch);
        Assert.Equal(19, (int)RadarProcessingAsyncValidationError.RetentionLimitExceeded);
    }

    [Fact]
    public void EssentialProfileCatchesFailedCompletionAndTopologyMismatch()
    {
        var route = CreateRoute();
        var workItems = CreateCanonicalWorkItems(route);
        var failedCompletion = CreateBatchResult(
            route,
            workItems,
            workItems[0].WorkItemId,
            RadarProcessingAsyncBatchCompletionError.WorkFailed);

        var failedResult = RadarProcessingAsyncValidator.ValidateTransport(
            route,
            workItems,
            failedCompletion,
            RadarProcessingValidationProfile.Essential);

        AssertInvalid(failedResult, RadarProcessingAsyncValidationError.FailedBatchCompletion);

        var mismatchedProcessingResult = CreateAsyncProcessingResult(
            route,
            workerTelemetryTopologyVersion: route.TopologyVersion.Next());

        var topologyResult = RadarProcessingAsyncValidator.ValidateProcessingResult(
            mismatchedProcessingResult,
            RadarProcessingValidationProfile.Essential);

        AssertInvalid(topologyResult, RadarProcessingAsyncValidationError.TopologyVersionMismatch);
    }

    [Fact]
    public void DiagnosticProfileCatchesMissingAndDuplicateWork()
    {
        var route = CreateRoute();
        var missingWorkItems = CreateWorkItems(
            route,
            new[]
            {
                new[] { 0 },
                new[] { 2, 3 }
            });
        var missingResult = RadarProcessingAsyncValidator.ValidateTransport(
            route,
            missingWorkItems,
            CreateBatchResult(route, missingWorkItems),
            RadarProcessingValidationProfile.Diagnostic,
            workerCount: 2);

        AssertInvalid(missingResult, RadarProcessingAsyncValidationError.MissingWorkItem);

        var duplicateWorkItems = CreateWorkItems(
            route,
            new[]
            {
                new[] { 0, 1 },
                new[] { 1, 2, 3 }
            });
        var duplicateResult = RadarProcessingAsyncValidator.ValidateTransport(
            route,
            duplicateWorkItems,
            CreateBatchResult(route, duplicateWorkItems),
            RadarProcessingValidationProfile.Diagnostic,
            workerCount: 2);

        AssertInvalid(duplicateResult, RadarProcessingAsyncValidationError.DuplicateWorkAssignment);
    }

    [Fact]
    public void DiagnosticProfileCatchesAssignmentOutsideShardOwnership()
    {
        var route = CreateRoute();
        var workItems = CreateWorkItems(
            route,
            new[]
            {
                new[] { 0, 2 },
                new[] { 1, 3 }
            });

        var result = RadarProcessingAsyncValidator.ValidateTransport(
            route,
            workItems,
            CreateBatchResult(route, workItems),
            RadarProcessingValidationProfile.Diagnostic,
            workerCount: 2);

        AssertInvalid(result, RadarProcessingAsyncValidationError.WorkItemShardOwnershipMismatch);
    }
}
