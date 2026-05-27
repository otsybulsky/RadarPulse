namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingDirectHotReliefPlanner
{
    public RadarProcessingRebalanceDecision Plan(
        long decisionId,
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier = null,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decisionId);
        ArgumentNullException.ThrowIfNull(pressureWindow);
        ArgumentNullException.ThrowIfNull(policyState);

        if (!pressureWindow.IsRebalanceEligible)
        {
            return NoAction(
                decisionId,
                pressureWindow,
                policyState,
                [RadarProcessingRebalanceSkippedReason.NoSustainedPressure]);
        }

        EnsureCompatibleShape(pressureWindow, policyState, hotPartitionClassifier, quarantineLifecycleTracker);

        var hotShards = pressureWindow.Shards
            .Where(shard => shard.IsHot)
            .OrderByDescending(shard => shard.AverageScore.Value)
            .ThenBy(shard => shard.ShardId)
            .ToArray();
        if (hotShards.Length == 0)
        {
            return NoAction(
                decisionId,
                pressureWindow,
                policyState,
                [RadarProcessingRebalanceSkippedReason.NoHotShard]);
        }

        var coldTargets = pressureWindow.Shards
            .Where(shard => shard.Band == RadarProcessingPressureBand.Cold)
            .OrderBy(shard => shard.AverageScore.Value)
            .ThenBy(shard => shard.ShardId)
            .ToArray();
        if (coldTargets.Length == 0)
        {
            return NoAction(
                decisionId,
                pressureWindow,
                policyState,
                [RadarProcessingRebalanceSkippedReason.NoColdTargetShard]);
        }

        var skippedClassificationReasons = new List<RadarProcessingRebalanceSkippedReason>();
        var candidates = CreateCandidateEvaluations(
            pressureWindow,
            hotShards,
            coldTargets,
            hotPartitionClassifier,
            quarantineLifecycleTracker,
            skippedClassificationReasons);
        if (candidates.Count == 0)
        {
            if (skippedClassificationReasons.Count > 0)
            {
                return NoAction(
                    decisionId,
                    pressureWindow,
                    policyState,
                    skippedClassificationReasons);
            }

            return NoAction(
                decisionId,
                pressureWindow,
                policyState,
                [RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget]);
        }

        var selected = candidates
            .Where(candidate => candidate.TargetSkippedReason is null)
            .OrderByDescending(candidate => candidate.Candidate.ExpectedRelief)
            .ThenByDescending(candidate => candidate.PartitionPressure.Value)
            .ThenBy(candidate => candidate.Candidate.PartitionId)
            .ThenBy(candidate => candidate.Candidate.TargetShardId)
            .FirstOrDefault();

        if (selected is null)
        {
            var rejected = candidates
                .OrderByDescending(candidate => candidate.Candidate.ExpectedRelief)
                .ThenByDescending(candidate => candidate.PartitionPressure.Value)
                .ThenBy(candidate => candidate.Candidate.PartitionId)
                .ThenBy(candidate => candidate.Candidate.TargetShardId)
                .First();
            var classificationReason = RecordRejectedDirectCandidate(
                pressureWindow,
                policyState,
                hotPartitionClassifier,
                quarantineLifecycleTracker,
                rejected);

            return RadarProcessingRebalanceDecision.RejectedCandidate(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                rejected.Candidate,
                [
                    RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget,
                    rejected.TargetSkippedReason!.Value,
                    classificationReason
                ]);
        }

        if (selected.Candidate.ExpectedRelief <= 0.0)
        {
            return RadarProcessingRebalanceDecision.RejectedCandidate(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                selected.Candidate,
                [RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit]);
        }

        RecordMovableDirectCandidate(
            pressureWindow,
            policyState,
            hotPartitionClassifier,
            quarantineLifecycleTracker,
            selected);

        var policyResult = policyState.EvaluateMove(selected.Candidate.ToPolicyInput());
        return policyResult.IsAllowed
            ? RadarProcessingRebalanceDecision.AcceptedMove(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                selected.Candidate)
            : RadarProcessingRebalanceDecision.RejectedCandidate(
                decisionId,
                policyState.EvaluationSequence,
                pressureWindow.LatestTopologyVersion,
                pressureWindow.SampleCount,
                selected.Candidate,
                policyResult);
    }

    private static List<CandidateEvaluation> CreateCandidateEvaluations(
        RadarProcessingPressureWindow pressureWindow,
        IReadOnlyCollection<RadarProcessingShardPressureState> hotShards,
        IReadOnlyCollection<RadarProcessingShardPressureState> coldTargets,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker,
        List<RadarProcessingRebalanceSkippedReason> skippedClassificationReasons)
    {
        var result = new List<CandidateEvaluation>();

        foreach (var sourceShard in hotShards)
        {
            var sourcePartitions = pressureWindow.Partitions
                .Where(partition => partition.ShardId == sourceShard.ShardId)
                .Where(partition => partition.AverageScore.Value > 0.0)
                .OrderByDescending(partition => partition.AverageScore.Value)
                .ThenBy(partition => partition.PartitionId);

            foreach (var partition in sourcePartitions)
            {
                if ((hotPartitionClassifier is not null || quarantineLifecycleTracker is not null) &&
                    TrySkipClassifiedPartition(
                        hotPartitionClassifier,
                        quarantineLifecycleTracker,
                        partition.PartitionId,
                        skippedClassificationReasons))
                {
                    continue;
                }

                foreach (var targetShard in coldTargets)
                {
                    if (targetShard.ShardId == sourceShard.ShardId)
                    {
                        continue;
                    }

                    result.Add(CreateCandidateEvaluation(pressureWindow, sourceShard, partition, targetShard));
                }
            }
        }

        return result;
    }

    private static CandidateEvaluation CreateCandidateEvaluation(
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingShardPressureState sourceShard,
        RadarProcessingPartitionPressureState partition,
        RadarProcessingShardPressureState targetShard)
    {
        var partitionPressure = partition.AverageScore.Value;
        var projectedSource = new RadarProcessingPressureScore(
            Math.Max(0.0, sourceShard.AverageScore.Value - partitionPressure));
        var projectedTarget = new RadarProcessingPressureScore(
            targetShard.AverageScore.Value + partitionPressure);
        var projectedMaxPressure = Math.Max(projectedSource.Value, projectedTarget.Value);
        var expectedRelief = Math.Max(0.0, sourceShard.AverageScore.Value - projectedMaxPressure);
        var projectedTargetBand = pressureWindow.Options.Classify(projectedTarget, targetShard.Band);
        var targetSkippedReason = projectedTargetBand switch
        {
            RadarProcessingPressureBand.Warm => RadarProcessingRebalanceSkippedReason.TargetWouldBecomeWarm,
            RadarProcessingPressureBand.Hot or RadarProcessingPressureBand.SuperHot =>
                RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot,
            _ => (RadarProcessingRebalanceSkippedReason?)null
        };

        return new CandidateEvaluation(
            new RadarProcessingRebalanceCandidate(
                RadarProcessingRebalanceMoveKind.DirectHotRelief,
                partition.PartitionId,
                sourceShard.ShardId,
                targetShard.ShardId,
                new RadarProcessingProjectedPressure(
                    sourceShard.AverageScore,
                    targetShard.AverageScore,
                    projectedSource,
                    projectedTarget),
                expectedRelief),
            partition,
            partition.AverageScore,
            targetSkippedReason);
    }

    private static RadarProcessingRebalanceDecision NoAction(
        long decisionId,
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        IReadOnlyCollection<RadarProcessingRebalanceSkippedReason> skippedReasons) =>
        RadarProcessingRebalanceDecision.NoAction(
            decisionId,
            policyState.EvaluationSequence,
            pressureWindow.LatestTopologyVersion,
            pressureWindow.SampleCount,
            skippedReasons);

    private static void EnsureCompatibleShape(
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker)
    {
        if (pressureWindow.Partitions.Count != policyState.PartitionCount)
        {
            throw new ArgumentException(
                "Pressure window partition count must match rebalance policy state.",
                nameof(pressureWindow));
        }

        if (pressureWindow.Shards.Count != policyState.ShardCount)
        {
            throw new ArgumentException(
                "Pressure window shard count must match rebalance policy state.",
                nameof(pressureWindow));
        }

        if (hotPartitionClassifier is not null &&
            pressureWindow.Partitions.Count != hotPartitionClassifier.PartitionCount)
        {
            throw new ArgumentException(
                "Pressure window partition count must match hot partition classifier state.",
                nameof(pressureWindow));
        }

        if (quarantineLifecycleTracker is not null &&
            pressureWindow.Partitions.Count != quarantineLifecycleTracker.PartitionCount)
        {
            throw new ArgumentException(
                "Pressure window partition count must match quarantine lifecycle state.",
                nameof(pressureWindow));
        }
    }

    private static bool TrySkipClassifiedPartition(
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker,
        int partitionId,
        List<RadarProcessingRebalanceSkippedReason> skippedClassificationReasons)
    {
        if (quarantineLifecycleTracker is not null)
        {
            return TrySkipLifecyclePartition(
                quarantineLifecycleTracker,
                partitionId,
                skippedClassificationReasons);
        }

        if (hotPartitionClassifier is null)
        {
            return false;
        }

        var state = hotPartitionClassifier.GetPartition(partitionId);
        if (!state.BlocksDirectMove)
        {
            return false;
        }

        var reason = state.Classification switch
        {
            RadarProcessingHotPartitionClassification.IntrinsicHot =>
                RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            RadarProcessingHotPartitionClassification.Quarantined =>
                RadarProcessingRebalanceSkippedReason.PartitionQuarantined,
            _ => throw new InvalidOperationException("Only blocked hot partition classifications can be skipped.")
        };

        if (!skippedClassificationReasons.Contains(reason))
        {
            skippedClassificationReasons.Add(reason);
        }

        return true;
    }

    private static bool TrySkipLifecyclePartition(
        RadarProcessingQuarantineLifecycleTracker quarantineLifecycleTracker,
        int partitionId,
        List<RadarProcessingRebalanceSkippedReason> skippedClassificationReasons)
    {
        var state = quarantineLifecycleTracker.GetPartition(partitionId);
        if (!state.BlocksDirectMove)
        {
            return false;
        }

        var reason = state.EffectiveClassification switch
        {
            RadarProcessingQuarantineEffectiveClassification.IntrinsicHot =>
                RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            RadarProcessingQuarantineEffectiveClassification.Quarantined =>
                RadarProcessingRebalanceSkippedReason.PartitionQuarantined,
            _ => throw new InvalidOperationException("Only blocked quarantine lifecycle classifications can be skipped.")
        };

        if (!skippedClassificationReasons.Contains(reason))
        {
            skippedClassificationReasons.Add(reason);
        }

        return true;
    }

    private static void RecordMovableDirectCandidate(
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker,
        CandidateEvaluation selected)
    {
        hotPartitionClassifier?.ClassifyMovableHot(
            selected.Candidate.PartitionId,
            selected.Candidate.SourceShardId,
            policyState.EvaluationSequence);
        quarantineLifecycleTracker?.RecordPartitionEvidence(
            selected.Partition,
            policyState.EvaluationSequence,
            pressureWindow.LatestTopologyVersion,
            RadarProcessingHotPartitionClassification.MovableHot);
    }

    private static RadarProcessingRebalanceSkippedReason RecordRejectedDirectCandidate(
        RadarProcessingPressureWindow pressureWindow,
        RadarProcessingRebalancePolicyState policyState,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker,
        CandidateEvaluation rejected)
    {
        var lifecycleState = quarantineLifecycleTracker?.GetPartition(rejected.Candidate.PartitionId);
        var observedClassification = lifecycleState?.IsRetryEligible == true
            ? RadarProcessingHotPartitionClassification.Quarantined
            : RadarProcessingHotPartitionClassification.IntrinsicHot;

        if (observedClassification == RadarProcessingHotPartitionClassification.Quarantined)
        {
            hotPartitionClassifier?.ClassifyQuarantined(
                rejected.Candidate.PartitionId,
                rejected.Candidate.SourceShardId,
                policyState.EvaluationSequence);
        }
        else
        {
            hotPartitionClassifier?.ClassifyIntrinsicHot(
                rejected.Candidate.PartitionId,
                rejected.Candidate.SourceShardId,
                policyState.EvaluationSequence);
        }

        quarantineLifecycleTracker?.RecordPartitionEvidence(
            rejected.Partition,
            policyState.EvaluationSequence,
            pressureWindow.LatestTopologyVersion,
            observedClassification);

        return observedClassification == RadarProcessingHotPartitionClassification.Quarantined
            ? RadarProcessingRebalanceSkippedReason.PartitionQuarantined
            : RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot;
    }

    private sealed record CandidateEvaluation(
        RadarProcessingRebalanceCandidate Candidate,
        RadarProcessingPartitionPressureState Partition,
        RadarProcessingPressureScore PartitionPressure,
        RadarProcessingRebalanceSkippedReason? TargetSkippedReason);
}
