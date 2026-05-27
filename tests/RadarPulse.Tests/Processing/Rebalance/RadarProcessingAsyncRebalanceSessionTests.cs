using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingAsyncRebalanceSessionTests
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

    [Fact]
    public async Task SyncAndAsyncRebalanceProduceMatchingDeterministicState()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var syncSession = CreateSyncSession(universe);
        await using var asyncSession = CreateAsyncSession(universe);
        var batch = CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]);

        var syncResult = syncSession.Process(batch);
        var asyncResult = await asyncSession.ProcessAsync(batch);

        Assert.True(syncResult.ProcessingResult.IsValid);
        Assert.True(asyncResult.ProcessingResult.IsValid);
        Assert.Equal(syncResult.ProcessingResult.Metrics, asyncResult.ProcessingResult.Metrics);
        Assert.Equal(syncSession.Core.CreateSourceSnapshots(), asyncSession.Core.CreateSourceSnapshots());
        Assert.Equal(syncResult.PressureSample!.BatchMetrics, asyncResult.PressureSample!.BatchMetrics);
        Assert.Equal(syncResult.RebalanceDecision!.Kind, asyncResult.RebalanceDecision!.Kind);
        Assert.Equal(syncResult.RebalanceDecision.MoveKind, asyncResult.RebalanceDecision.MoveKind);
        Assert.Equal(syncResult.RebalanceDecision.PartitionId, asyncResult.RebalanceDecision.PartitionId);
        Assert.Equal(syncResult.PublishedMigration, asyncResult.PublishedMigration);
        Assert.Equal(syncSession.CurrentTopology.Version, asyncSession.CurrentTopology.Version);
        for (var partitionId = 0; partitionId < universe.SourceCount; partitionId++)
        {
            Assert.Equal(
                syncSession.CurrentTopology.GetShardIdForPartition(partitionId),
                asyncSession.CurrentTopology.GetShardIdForPartition(partitionId));
        }
    }

    [Fact]
    public void SyncRebalanceProcessRejectsAsyncCore()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 1,
            shardCount: 1);
        var session = new RadarProcessingRebalanceSession(core);

        var exception = Assert.Throws<NotSupportedException>(() => session.Process(CreateEmptyBatch(universe.Version)));

        Assert.Contains("RadarProcessingAsyncRebalanceSession.ProcessAsync", exception.Message, StringComparison.Ordinal);
    }

    private static RadarProcessingRebalanceSession CreateSyncSession(
        RadarSourceUniverse universe,
        int shardCount = 2) =>
        new(
            CreateCore(
                universe,
                RadarProcessingExecutionMode.PartitionedBarrier,
                universe.SourceCount,
                shardCount),
            CreatePressureOptions(),
            CreatePressureWindow(),
            CreatePolicyState(universe.SourceCount, shardCount),
            telemetryRecorder: CreateTelemetryRecorder());

    private static RadarProcessingAsyncRebalanceSession CreateAsyncSession(
        RadarSourceUniverse universe,
        int shardCount = 2,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null)
    {
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            universe.SourceCount,
            shardCount,
            asyncExecution ?? new RadarProcessingAsyncExecutionOptions(workerCount: shardCount, queueCapacity: 1),
            handlers);
        return new RadarProcessingAsyncRebalanceSession(
            core,
            CreatePressureOptions(),
            CreatePressureWindow(),
            CreatePolicyState(universe.SourceCount, shardCount),
            telemetryRecorder: CreateTelemetryRecorder());
    }

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe,
        RadarProcessingExecutionMode mode,
        int partitionCount,
        int shardCount,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                mode,
                partitionCount,
                shardCount,
                handlers: handlers,
                asyncExecution: asyncExecution));

    private static RadarProcessingPressureOptions CreatePressureOptions() =>
        new(
            eventWeight: 1.0,
            payloadValueWeight: 0.0,
            rawValueChecksumWeight: 0.0);

    private static RadarProcessingPressureWindow CreatePressureWindow() =>
        new(
            new RadarProcessingPressureWindowOptions(
                sampleCapacity: 2,
                minimumSampleCount: 1,
                coldThreshold: 0.0,
                warmExitThreshold: 4.0,
                warmEnterThreshold: 4.5,
                hotExitThreshold: 4.75,
                hotEnterThreshold: 5.0,
                superHotExitThreshold: 9.0,
                superHotEnterThreshold: 10.0));

    private static RadarProcessingRebalancePolicyState CreatePolicyState(
        int partitionCount,
        int shardCount) =>
        new(
            partitionCount,
            shardCount,
            new RadarProcessingRebalanceOptions(
                budgetWindowEvaluationCount: 4,
                globalMoveBudgetPerWindow: 4,
                sourceShardMoveBudgetPerWindow: 4,
                targetShardReceiveBudgetPerWindow: 4,
                minimumPartitionResidencyEvaluations: 0,
                partitionMoveCooldownEvaluations: 0,
                sourceShardMoveCooldownEvaluations: 0,
                targetShardReceiveCooldownEvaluations: 0,
                minimumProjectedBenefit: 0.05));

    private static RadarProcessingRebalanceTelemetryRecorder CreateTelemetryRecorder() =>
        new(
            new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Recent,
                maxRetainedDecisions: 8,
                maxRetainedLifecycleTransitions: 8,
                maxRetainedAcceptedMoves: 8,
                maxRetainedValidationFailures: 8,
                maxRetainedWorkerBatches: 8,
                maxRetainedWorkerFailures: 8));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEmptyBatch(SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

    private static RadarEventBatch CreateEightBitBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = CreateEvent(
                sourceIds[i],
                messageTimestampUtcTicks: 100 + i,
                payloadOffset: i);
            payload[i] = (byte)(i + 1);
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }

    private static RadarEventBatch CreateMixedBatch(SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            new[]
            {
                CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100, payloadOffset: 0, gateCount: 4),
                CreateEvent(sourceId: 3, messageTimestampUtcTicks: 101, payloadOffset: 4, gateCount: 2, wordSize: RadarStreamWordSize.SixteenBit),
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 102, payloadOffset: 8, gateCount: 4),
                CreateEvent(sourceId: 5, messageTimestampUtcTicks: 103, payloadOffset: 12, gateCount: 2, wordSize: RadarStreamWordSize.SixteenBit),
                CreateEvent(sourceId: 3, messageTimestampUtcTicks: 104, payloadOffset: 16, gateCount: 4)
            },
            new byte[]
            {
                1, 2, 3, 4,
                0, 5, 1, 0,
                5, 6, 7, 8,
                2, 0, 0, 1,
                9, 10, 11, 12
            });

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks,
        int payloadOffset,
        ushort gateCount = 1,
        RadarStreamWordSize wordSize = RadarStreamWordSize.EightBit)
    {
        var payloadLength = checked(gateCount * (wordSize == RadarStreamWordSize.EightBit ? 1 : 2));
        return new RadarStreamEvent(
            sourceId,
            radarOrdinal: 0,
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: payloadOffset,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            momentId: 0,
            gateStart: 0,
            gateCount: gateCount,
            wordSize: wordSize,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: payloadOffset,
            payloadLength: payloadLength);
    }

    private sealed class BlockingHandler : IRadarSourceProcessingHandler
    {
        private readonly ManualResetEventSlim entered = new();
        private readonly ManualResetEventSlim released = new();
        private int blocked;

        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "blocking",
                int64SlotCount: 0,
                doubleSlotCount: 0);

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            if (Interlocked.Exchange(ref blocked, 1) != 0)
            {
                return;
            }

            entered.Set();
            released.Wait();
        }

        public bool WaitUntilEntered(TimeSpan timeout) =>
            entered.Wait(timeout);

        public void Release() =>
            released.Set();
    }
}
