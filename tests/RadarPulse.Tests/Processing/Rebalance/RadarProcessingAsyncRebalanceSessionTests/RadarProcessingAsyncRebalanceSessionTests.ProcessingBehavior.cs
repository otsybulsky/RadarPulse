using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncRebalanceSessionTests
{
    [Fact]
    public async Task AsyncRebalanceSessionProcessesBatchAndCarriesWorkerTelemetry()
    {
        var universe = CreateUniverse(sourceCount: 4);
        await using var session = CreateAsyncSession(universe);

        var result = await session.ProcessAsync(CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]));

        Assert.True(result.ProcessingResult.IsValid);
        Assert.True(result.Validation.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, result.ProcessingResult.ExecutionMode);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, result.ProcessingResult.TopologyVersion);
        Assert.NotNull(result.PressureSample);
        Assert.Equal(result.ProcessingResult.TopologyVersion, result.PressureSample.TopologyVersion);
        Assert.Equal(result.ProcessingResult.TopologyVersion, result.RebalanceDecision!.TopologyVersion);
        Assert.True(result.PublishedMigration);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), session.CurrentTopology.Version);
        Assert.Equal(result.ProcessingResult.TopologyVersion, result.MigrationResult!.PreviousTopologyVersion);
        Assert.Equal(session.CurrentTopology.Version, result.MigrationResult.CurrentTopologyVersion);
        Assert.NotNull(result.WorkerTelemetry);
        Assert.Equal(1, result.WorkerTelemetry.Counters.DispatchedBatchCount);
        Assert.Equal(1, result.WorkerTelemetry.Counters.CompletedBatchCount);
        Assert.Equal(0, result.WorkerTelemetry.Counters.FailedBatchCount);
        Assert.Equal(1, result.TelemetrySummary.Counters.EvaluationCount);
        Assert.Equal(1, result.TelemetrySummary.Counters.AcceptedMoveCount);
        Assert.Equal(RadarProcessingValidationProfile.Diagnostic, result.ValidationProfile);
    }

    [Fact]
    public async Task AcceptedMigrationPublishesOnlyAfterAsyncWorkersComplete()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var blockingHandler = new BlockingHandler();
        await using var session = CreateAsyncSession(
            universe,
            handlers: new IRadarSourceProcessingHandler[] { blockingHandler });

        var processTask = session
            .ProcessAsync(CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]))
            .AsTask();

        try
        {
            Assert.True(blockingHandler.WaitUntilEntered(TimeSpan.FromSeconds(5)));
            Assert.Equal(RadarProcessingTopologyVersion.Initial, session.CurrentTopology.Version);

            blockingHandler.Release();
            var result = await processTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(result.ProcessingResult.IsValid);
            Assert.True(result.PublishedMigration);
            Assert.NotNull(result.WorkerTelemetry);
            Assert.Equal(1, result.WorkerTelemetry.Counters.CompletedBatchCount);
            Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), session.CurrentTopology.Version);
        }
        finally
        {
            blockingHandler.Release();
            await processTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task FailedAsyncProcessingSkipsRebalancePlanningAndPublication()
    {
        var universe = CreateUniverse(sourceCount: 6);
        await using var session = CreateAsyncSession(
            universe,
            shardCount: 3,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 1, queueCapacity: 1));

        var result = await session.ProcessAsync(CreateMixedBatch(universe.Version));

        Assert.False(result.ProcessingResult.IsValid);
        Assert.True(result.Validation.IsValid);
        Assert.Null(result.PressureSample);
        Assert.Null(result.DirectHotReliefDecision);
        Assert.Null(result.ColdEvacuationDecision);
        Assert.Null(result.MigrationResult);
        Assert.False(result.EvaluatedRebalance);
        Assert.False(result.PublishedMigration);
        Assert.Equal(0, result.TelemetrySummary.Counters.EvaluationCount);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, session.CurrentTopology.Version);
        Assert.NotNull(result.WorkerTelemetry);
        Assert.Equal(1, result.WorkerTelemetry.Counters.FailedBatchCount);
        Assert.Equal(1, result.WorkerTelemetry.Counters.RejectedDispatchCount);
    }
}
