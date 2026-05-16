using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingSyntheticRebalanceWorkload
{
    private readonly IReadOnlyList<RadarEventBatch> batches;
    private readonly IReadOnlyList<InitialHotPartitionClassification> initialClassifications;

    private RadarProcessingSyntheticRebalanceWorkload(
        RadarProcessingSyntheticRebalanceWorkloadKind kind,
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCoreOptions coreOptions,
        RadarProcessingPressureOptions pressureOptions,
        RadarProcessingPressureWindowOptions pressureWindowOptions,
        RadarProcessingRebalanceOptions rebalanceOptions,
        RadarEventBatch[] batches,
        InitialHotPartitionClassification[] initialClassifications)
    {
        Kind = kind;
        SourceUniverse = sourceUniverse;
        CoreOptions = coreOptions;
        PressureOptions = pressureOptions;
        PressureWindowOptions = pressureWindowOptions;
        RebalanceOptions = rebalanceOptions;
        this.batches = Array.AsReadOnly((RadarEventBatch[])batches.Clone());
        this.initialClassifications = Array.AsReadOnly(
            (InitialHotPartitionClassification[])initialClassifications.Clone());
    }

    public RadarProcessingSyntheticRebalanceWorkloadKind Kind { get; }

    public RadarSourceUniverse SourceUniverse { get; }

    public RadarProcessingCoreOptions CoreOptions { get; }

    public RadarProcessingPressureOptions PressureOptions { get; }

    public RadarProcessingPressureWindowOptions PressureWindowOptions { get; }

    public RadarProcessingRebalanceOptions RebalanceOptions { get; }

    public IReadOnlyList<RadarEventBatch> Batches => batches;

    public int SourceCount => SourceUniverse.SourceCount;

    public int PartitionCount => CoreOptions.PartitionCount;

    public int ShardCount => CoreOptions.ShardCount;

    public static RadarProcessingSyntheticRebalanceWorkload Create(
        RadarProcessingSyntheticRebalanceWorkloadKind kind) =>
        kind switch
        {
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced => CreateBalanced(),
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard => CreateSustainedHotShard(),
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition => CreateIntrinsicHotPartition(),
            RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike => CreateOscillatingSpike(),
            RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm => CreateCooldownStorm(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    public RadarProcessingRebalanceSession CreateSession()
    {
        var classifier = new RadarProcessingHotPartitionClassifier(PartitionCount);
        foreach (var classification in initialClassifications)
        {
            ApplyClassification(classifier, classification);
        }

        return new RadarProcessingRebalanceSession(
            new RadarProcessingCore(SourceUniverse, CoreOptions),
            PressureOptions,
            new RadarProcessingPressureWindow(PressureWindowOptions),
            new RadarProcessingRebalancePolicyState(PartitionCount, ShardCount, RebalanceOptions),
            classifier);
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

    private static RadarProcessingSyntheticRebalanceWorkload Create(
        RadarProcessingSyntheticRebalanceWorkloadKind kind,
        RadarProcessingPressureWindowOptions pressureWindowOptions,
        RadarProcessingRebalanceOptions rebalanceOptions,
        int[][] sourceIdsByBatch,
        InitialHotPartitionClassification[] initialClassifications)
    {
        var sourceUniverse = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: 4,
            rangeBandCount: 1);
        var batches = new RadarEventBatch[sourceIdsByBatch.Length];
        for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
        {
            batches[batchIndex] = CreateBatch(
                sourceUniverse.Version,
                sourceIdsByBatch[batchIndex],
                batchIndex);
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
            batches,
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
