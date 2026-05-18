using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingSyntheticRebalanceWorkload
{
    private const int RetentionStressBatchCount = 16;
    private const int RetentionStressDecisionLimit = 4;

    private readonly IReadOnlyList<RadarEventBatch> batches;
    private readonly IReadOnlyList<InitialHotPartitionClassification> initialClassifications;

    private RadarProcessingSyntheticRebalanceWorkload(
        RadarProcessingSyntheticRebalanceWorkloadKind kind,
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCoreOptions coreOptions,
        RadarProcessingPressureOptions pressureOptions,
        RadarProcessingPressureWindowOptions pressureWindowOptions,
        RadarProcessingRebalanceOptions rebalanceOptions,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        RadarEventBatch[] batches,
        long eventsPerIteration,
        long payloadValuesPerIteration,
        long rawValueChecksumPerIteration,
        InitialHotPartitionClassification[] initialClassifications)
    {
        Kind = kind;
        SourceUniverse = sourceUniverse;
        CoreOptions = coreOptions;
        PressureOptions = pressureOptions;
        PressureWindowOptions = pressureWindowOptions;
        RebalanceOptions = rebalanceOptions;
        HardeningOptions = hardeningOptions;
        this.batches = Array.AsReadOnly((RadarEventBatch[])batches.Clone());
        EventsPerIteration = eventsPerIteration;
        PayloadValuesPerIteration = payloadValuesPerIteration;
        RawValueChecksumPerIteration = rawValueChecksumPerIteration;
        this.initialClassifications = Array.AsReadOnly(
            (InitialHotPartitionClassification[])initialClassifications.Clone());
    }

    public RadarProcessingSyntheticRebalanceWorkloadKind Kind { get; }

    public RadarSourceUniverse SourceUniverse { get; }

    public RadarProcessingCoreOptions CoreOptions { get; }

    public RadarProcessingPressureOptions PressureOptions { get; }

    public RadarProcessingPressureWindowOptions PressureWindowOptions { get; }

    public RadarProcessingRebalanceOptions RebalanceOptions { get; }

    public RadarProcessingRebalanceHardeningOptions HardeningOptions { get; }

    public IReadOnlyList<RadarEventBatch> Batches => batches;

    public int SourceCount => SourceUniverse.SourceCount;

    public int PartitionCount => CoreOptions.PartitionCount;

    public int ShardCount => CoreOptions.ShardCount;

    public long BatchesPerIteration => batches.Count;

    public long EventsPerIteration { get; }

    public long PayloadValuesPerIteration { get; }

    public long RawValueChecksumPerIteration { get; }

    public static RadarProcessingSyntheticRebalanceWorkload Create(
        RadarProcessingSyntheticRebalanceWorkloadKind kind) =>
        kind switch
        {
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced => CreateBalanced(),
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard => CreateSustainedHotShard(),
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition => CreateIntrinsicHotPartition(),
            RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike => CreateOscillatingSpike(),
            RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm => CreateCooldownStorm(),
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry => CreateQuarantineTtlRetry(),
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear =>
                CreateQuarantineSustainedCoolingClear(),
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry =>
                CreateQuarantinePressureChangeRetry(),
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry =>
                CreateQuarantineRetryReentry(),
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear =>
                CreateQuarantineSuccessfulReliefClear(),
            RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard => CreateLongNoHotShard(),
            RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection =>
                CreateLongCooldownRejection(),
            RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection =>
                CreateLongUnsafeTargetRejection(),
            RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons =>
                CreateLongMixedSkippedReasons(),
            RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention =>
                CreateCountersOnlyRetention(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    public RadarProcessingRebalanceSession CreateSession(
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null)
    {
        var classifier = new RadarProcessingHotPartitionClassifier(PartitionCount);
        foreach (var classification in initialClassifications)
        {
            ApplyClassification(classifier, classification);
        }

        var effectiveHardeningOptions = hardeningOptions ?? HardeningOptions;
        return new RadarProcessingRebalanceSession(
            new RadarProcessingCore(SourceUniverse, CoreOptions),
            PressureOptions,
            new RadarProcessingPressureWindow(PressureWindowOptions),
            new RadarProcessingRebalancePolicyState(PartitionCount, ShardCount, RebalanceOptions),
            classifier,
            hardeningOptions: effectiveHardeningOptions);
    }

    private static RadarProcessingSyntheticRebalanceWorkload CreateBalanced() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced,
            CreateStandardWindowOptions(minimumSampleCount: 1),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 1, 2, 3],
                [0, 1, 2, 3]
            ],
            []);

    private static RadarProcessingSyntheticRebalanceWorkload CreateSustainedHotShard() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard,
            CreateStandardWindowOptions(minimumSampleCount: 1),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 1, 1],
                [0, 0, 0, 0, 1, 1]
            ],
            []);

    private static RadarProcessingSyntheticRebalanceWorkload CreateIntrinsicHotPartition() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition,
            CreateStandardWindowOptions(minimumSampleCount: 1),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1]
            ],
            [
                new InitialHotPartitionClassification(
                    PartitionId: 1,
                    ShardId: 0,
                    RadarProcessingHotPartitionClassification.Quarantined)
            ]);

    private static RadarProcessingSyntheticRebalanceWorkload CreateOscillatingSpike() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike,
            new RadarProcessingPressureWindowOptions(
                sampleCapacity: 3,
                minimumSampleCount: 3,
                coldThreshold: 0.0,
                warmExitThreshold: 6.0,
                warmEnterThreshold: 6.5,
                hotExitThreshold: 6.75,
                hotEnterThreshold: 7.0,
                superHotExitThreshold: 12.0,
                superHotEnterThreshold: 14.0),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 0],
                [2, 3],
                [0, 0, 0, 0, 0, 0, 0, 0, 0]
            ],
            []);

    private static RadarProcessingSyntheticRebalanceWorkload CreateCooldownStorm() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm,
            new RadarProcessingPressureWindowOptions(
                sampleCapacity: 1,
                minimumSampleCount: 1,
                coldThreshold: 0.0,
                warmExitThreshold: 4.0,
                warmEnterThreshold: 4.5,
                hotExitThreshold: 4.75,
                hotEnterThreshold: 5.0,
                superHotExitThreshold: 9.0,
                superHotEnterThreshold: 10.0),
            new RadarProcessingRebalanceOptions(
                budgetWindowEvaluationCount: 4,
                globalMoveBudgetPerWindow: 1,
                sourceShardMoveBudgetPerWindow: 4,
                targetShardReceiveBudgetPerWindow: 4,
                minimumPartitionResidencyEvaluations: 0,
                partitionMoveCooldownEvaluations: 4,
                sourceShardMoveCooldownEvaluations: 0,
                targetShardReceiveCooldownEvaluations: 0,
                minimumProjectedBenefit: 0.05),
            [
                [0, 0, 0, 0, 1, 1],
                [0, 0, 0, 0, 2, 2]
            ],
            []);

    private static RadarProcessingSyntheticRebalanceWorkload CreateQuarantineTtlRetry() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0],
                [0]
            ],
            [
                CreateInitialQuarantine()
            ],
            CreateLifecycleHardeningOptions(
                quarantineTtlEvaluations: 1,
                sustainedCoolingSampleCount: 5,
                materialPressureChangeThreshold: 1.0));

    private static RadarProcessingSyntheticRebalanceWorkload CreateQuarantineSustainedCoolingClear() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0],
                [],
                []
            ],
            [
                CreateInitialQuarantine()
            ],
            CreateLifecycleHardeningOptions(
                quarantineTtlEvaluations: 10,
                sustainedCoolingSampleCount: 2,
                materialPressureChangeThreshold: 2.0));

    private static RadarProcessingSyntheticRebalanceWorkload CreateQuarantinePressureChangeRetry() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0],
                [0, 0, 0]
            ],
            [
                CreateInitialQuarantine()
            ],
            CreateLifecycleHardeningOptions(
                quarantineTtlEvaluations: 10,
                sustainedCoolingSampleCount: 5,
                materialPressureChangeThreshold: 0.25));

    private static RadarProcessingSyntheticRebalanceWorkload CreateQuarantineRetryReentry() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0, 0]
            ],
            [
                CreateInitialQuarantine()
            ],
            CreateLifecycleHardeningOptions(
                quarantineTtlEvaluations: 1,
                sustainedCoolingSampleCount: 5,
                materialPressureChangeThreshold: 1.0));

    private static RadarProcessingSyntheticRebalanceWorkload CreateQuarantineSuccessfulReliefClear() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0],
                [0, 0, 0, 0, 1, 1]
            ],
            [
                CreateInitialQuarantine()
            ],
            CreateLifecycleHardeningOptions(
                quarantineTtlEvaluations: 1,
                sustainedCoolingSampleCount: 5,
                materialPressureChangeThreshold: 1.0));

    private static RadarProcessingSyntheticRebalanceWorkload CreateLongNoHotShard() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            RepeatBatch(RetentionStressBatchCount, [0, 1, 2, 3]),
            [],
            CreateRetentionStressHardeningOptions());

    private static RadarProcessingSyntheticRebalanceWorkload CreateLongCooldownRejection() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection,
            CreateImmediateWindowOptions(),
            CreateCooldownRejectionOptions(),
            PrependBatch(
                [0, 0, 0, 0, 1, 1],
                RepeatBatch(RetentionStressBatchCount - 1, [0, 0, 0, 0, 2, 2])),
            [],
            CreateRetentionStressHardeningOptions());

    private static RadarProcessingSyntheticRebalanceWorkload CreateLongUnsafeTargetRejection() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            RepeatPattern(
                RetentionStressBatchCount,
                [
                    [0, 0, 0, 0, 0, 0, 0, 0],
                    [1, 1, 1, 1, 1, 1, 1, 1],
                    [2, 2, 2, 2, 2, 2, 2, 2],
                    [3, 3, 3, 3, 3, 3, 3, 3]
                ]),
            [],
            CreateRetentionStressHardeningOptions());

    private static RadarProcessingSyntheticRebalanceWorkload CreateLongMixedSkippedReasons() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons,
            CreateImmediateWindowOptions(),
            CreateCooldownRejectionOptions(),
            RepeatPattern(
                RetentionStressBatchCount,
                [
                    [0, 1, 2, 3],
                    [0, 0, 0, 0, 1, 1],
                    [0, 0, 0, 0, 2, 2],
                    [3, 3, 3, 3, 3, 3, 3, 3]
                ]),
            [],
            CreateRetentionStressHardeningOptions());

    private static RadarProcessingSyntheticRebalanceWorkload CreateCountersOnlyRetention() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            RepeatBatch(RetentionStressBatchCount, [0, 1, 2, 3]),
            [],
            CreateRetentionStressHardeningOptions(RadarProcessingDiagnosticRetentionMode.Counters));

    private static RadarProcessingSyntheticRebalanceWorkload Create(
        RadarProcessingSyntheticRebalanceWorkloadKind kind,
        RadarProcessingPressureWindowOptions pressureWindowOptions,
        RadarProcessingRebalanceOptions rebalanceOptions,
        int[][] sourceIdsByBatch,
        InitialHotPartitionClassification[] initialClassifications,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null)
    {
        var sourceUniverse = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: 4,
            rangeBandCount: 1);
        var batches = new RadarEventBatch[sourceIdsByBatch.Length];
        var eventsPerIteration = 0L;
        var payloadValuesPerIteration = 0L;
        var rawValueChecksumPerIteration = 0L;
        for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
        {
            var batch = CreateBatch(
                sourceUniverse.Version,
                sourceIdsByBatch[batchIndex],
                batchIndex);
            var metrics = RadarEventBatchMetrics.Compute(batch);

            batches[batchIndex] = batch;
            eventsPerIteration = checked(eventsPerIteration + metrics.EventCount);
            payloadValuesPerIteration = checked(payloadValuesPerIteration + metrics.PayloadValueCount);
            rawValueChecksumPerIteration = checked(rawValueChecksumPerIteration + metrics.RawValueChecksum);
        }

        return new RadarProcessingSyntheticRebalanceWorkload(
            kind,
            sourceUniverse,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 4,
                shardCount: 2),
            new RadarProcessingPressureOptions(
                eventWeight: 1.0,
                payloadValueWeight: 0.0,
                rawValueChecksumWeight: 0.0),
            pressureWindowOptions,
            rebalanceOptions,
            hardeningOptions ?? RadarProcessingRebalanceHardeningOptions.Default,
            batches,
            eventsPerIteration,
            payloadValuesPerIteration,
            rawValueChecksumPerIteration,
            initialClassifications);
    }

    private static RadarProcessingPressureWindowOptions CreateStandardWindowOptions(
        int minimumSampleCount) =>
        new(
            sampleCapacity: Math.Max(2, minimumSampleCount),
            minimumSampleCount: minimumSampleCount,
            coldThreshold: 0.0,
            warmExitThreshold: 4.0,
            warmEnterThreshold: 4.5,
            hotExitThreshold: 4.75,
            hotEnterThreshold: 5.0,
            superHotExitThreshold: 9.0,
            superHotEnterThreshold: 10.0);

    private static RadarProcessingPressureWindowOptions CreateImmediateWindowOptions() =>
        new(
            sampleCapacity: 1,
            minimumSampleCount: 1,
            coldThreshold: 0.0,
            warmExitThreshold: 4.0,
            warmEnterThreshold: 4.5,
            hotExitThreshold: 4.75,
            hotEnterThreshold: 5.0,
            superHotExitThreshold: 9.0,
            superHotEnterThreshold: 10.0);

    private static RadarProcessingRebalanceOptions CreateRelaxedRebalanceOptions() =>
        new(
            budgetWindowEvaluationCount: 4,
            globalMoveBudgetPerWindow: 4,
            sourceShardMoveBudgetPerWindow: 4,
            targetShardReceiveBudgetPerWindow: 4,
            minimumPartitionResidencyEvaluations: 0,
            partitionMoveCooldownEvaluations: 0,
            sourceShardMoveCooldownEvaluations: 0,
            targetShardReceiveCooldownEvaluations: 0,
            minimumProjectedBenefit: 0.05);

    private static RadarProcessingRebalanceOptions CreateCooldownRejectionOptions() =>
        new(
            budgetWindowEvaluationCount: RetentionStressBatchCount * 4,
            globalMoveBudgetPerWindow: 1,
            sourceShardMoveBudgetPerWindow: 4,
            targetShardReceiveBudgetPerWindow: 4,
            minimumPartitionResidencyEvaluations: 0,
            partitionMoveCooldownEvaluations: RetentionStressBatchCount * 4,
            sourceShardMoveCooldownEvaluations: 0,
            targetShardReceiveCooldownEvaluations: 0,
            minimumProjectedBenefit: 0.05);

    private static RadarProcessingRebalanceHardeningOptions CreateLifecycleHardeningOptions(
        int quarantineTtlEvaluations,
        int sustainedCoolingSampleCount,
        double materialPressureChangeThreshold) =>
        new(
            quarantineLifecycle: new RadarProcessingQuarantineLifecycleOptions(
                quarantineTtlEvaluations,
                sustainedCoolingSampleCount,
                materialPressureChangeThreshold));

    private static RadarProcessingRebalanceHardeningOptions CreateRetentionStressHardeningOptions(
        RadarProcessingDiagnosticRetentionMode retentionMode = RadarProcessingDiagnosticRetentionMode.Recent) =>
        new(
            telemetryRetention: new RadarProcessingTelemetryRetentionOptions(
                retentionMode,
                maxRetainedDecisions: RetentionStressDecisionLimit,
                maxRetainedLifecycleTransitions: RetentionStressDecisionLimit,
                maxRetainedAcceptedMoves: RetentionStressDecisionLimit,
                maxRetainedValidationFailures: RetentionStressDecisionLimit));

    private static InitialHotPartitionClassification CreateInitialQuarantine() =>
        new(
            PartitionId: 0,
            ShardId: 0,
            RadarProcessingHotPartitionClassification.Quarantined);

    private static int[][] RepeatBatch(
        int count,
        int[] sourceIds)
    {
        var result = new int[count][];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = (int[])sourceIds.Clone();
        }

        return result;
    }

    private static int[][] RepeatPattern(
        int count,
        int[][] pattern)
    {
        var result = new int[count][];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = (int[])pattern[index % pattern.Length].Clone();
        }

        return result;
    }

    private static int[][] PrependBatch(
        int[] first,
        int[][] rest)
    {
        var result = new int[rest.Length + 1][];
        result[0] = (int[])first.Clone();
        for (var index = 0; index < rest.Length; index++)
        {
            result[index + 1] = rest[index];
        }

        return result;
    }

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds,
        int batchIndex)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var eventIndex = 0; eventIndex < sourceIds.Length; eventIndex++)
        {
            var sourceId = sourceIds[eventIndex];
            events[eventIndex] = new RadarStreamEvent(
                sourceId,
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 1_000,
                messageTimestampUtcTicks: 10_000 + sourceId,
                sourceRecord: batchIndex + 1,
                sourceMessage: eventIndex + 1,
                radialSequence: checked((batchIndex * 1_000) + eventIndex),
                elevationSlot: 0,
                azimuthBucket: (ushort)sourceId,
                rangeBand: 0,
                momentId: 0,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: eventIndex,
                payloadLength: 1);
            payload[eventIndex] = (byte)(1 + ((batchIndex + eventIndex + sourceId) % 251));
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }

    private static void ApplyClassification(
        RadarProcessingHotPartitionClassifier classifier,
        InitialHotPartitionClassification classification)
    {
        switch (classification.Classification)
        {
            case RadarProcessingHotPartitionClassification.IntrinsicHot:
                classifier.ClassifyIntrinsicHot(
                    classification.PartitionId,
                    classification.ShardId,
                    evaluationSequence: 0);
                break;

            case RadarProcessingHotPartitionClassification.Quarantined:
                classifier.ClassifyQuarantined(
                    classification.PartitionId,
                    classification.ShardId,
                    evaluationSequence: 0);
                break;

            case RadarProcessingHotPartitionClassification.MovableHot:
                classifier.ClassifyMovableHot(
                    classification.PartitionId,
                    classification.ShardId,
                    evaluationSequence: 0);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(classification));
        }
    }

    private readonly record struct InitialHotPartitionClassification(
        int PartitionId,
        int ShardId,
        RadarProcessingHotPartitionClassification Classification);
}
