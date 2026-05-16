namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingRebalancePolicyState
{
    private readonly long[] partitionResidencyStartSequences;
    private readonly long[] partitionLastMoveSequences;
    private readonly long[] sourceShardLastMoveSequences;
    private readonly long[] targetShardLastReceiveSequences;
    private readonly int[] sourceShardMoveCounts;
    private readonly int[] targetShardReceiveCounts;
    private long budgetWindowStartSequence;
    private int globalMoveCount;

    public RadarProcessingRebalancePolicyState(
        int partitionCount,
        int shardCount,
        RadarProcessingRebalanceOptions? options = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);

        Options = options ?? RadarProcessingRebalanceOptions.Default;
        PartitionCount = partitionCount;
        ShardCount = shardCount;
        partitionResidencyStartSequences = new long[partitionCount];
        partitionLastMoveSequences = CreateUninitializedSequenceArray(partitionCount);
        sourceShardLastMoveSequences = CreateUninitializedSequenceArray(shardCount);
        targetShardLastReceiveSequences = CreateUninitializedSequenceArray(shardCount);
        sourceShardMoveCounts = new int[shardCount];
        targetShardReceiveCounts = new int[shardCount];
    }

    public RadarProcessingRebalanceOptions Options { get; }

    public int PartitionCount { get; }

    public int ShardCount { get; }

    public long EvaluationSequence { get; private set; }

    public RadarProcessingRebalanceBudget GlobalMoveBudget =>
        new(Options.GlobalMoveBudgetPerWindow, globalMoveCount);

    public void AdvanceEvaluation()
    {
        EvaluationSequence = checked(EvaluationSequence + 1);
        if (EvaluationSequence - budgetWindowStartSequence >= Options.BudgetWindowEvaluationCount)
        {
            ResetBudgets();
        }
    }

    public RadarProcessingRebalancePolicyResult EvaluateMove(
        RadarProcessingRebalanceMovePolicyInput input)
    {
        EnsureInputShape(input);

        var rejections = new List<RadarProcessingRebalancePolicyRejection>();

        if (!GetPartitionResidency(input.PartitionId).IsSatisfied)
        {
            rejections.Add(RadarProcessingRebalancePolicyRejection.PartitionBelowMinimumResidency);
        }

        if (GetPartitionCooldown(input.PartitionId).IsActive)
        {
            rejections.Add(RadarProcessingRebalancePolicyRejection.PartitionInCooldown);
        }

        if (GetSourceShardCooldown(input.SourceShardId).IsActive)
        {
            rejections.Add(RadarProcessingRebalancePolicyRejection.SourceShardInCooldown);
        }

        if (GetTargetShardCooldown(input.TargetShardId).IsActive)
        {
            rejections.Add(RadarProcessingRebalancePolicyRejection.TargetShardInCooldown);
        }

        if (GlobalMoveBudget.IsExhausted)
        {
            rejections.Add(RadarProcessingRebalancePolicyRejection.GlobalMoveBudgetExhausted);
        }

        if (GetSourceShardMoveBudget(input.SourceShardId).IsExhausted)
        {
            rejections.Add(RadarProcessingRebalancePolicyRejection.SourceShardMoveBudgetExhausted);
        }

        if (GetTargetShardReceiveBudget(input.TargetShardId).IsExhausted)
        {
            rejections.Add(RadarProcessingRebalancePolicyRejection.TargetShardReceiveBudgetExhausted);
        }

        if (input.ProjectedBenefit < Options.MinimumProjectedBenefit)
        {
            rejections.Add(RadarProcessingRebalancePolicyRejection.InsufficientProjectedBenefit);
        }

        if (input.TargetProjectedPressure.Value > Options.TargetHeadroomThreshold)
        {
            rejections.Add(RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded);
        }

        return rejections.Count == 0
            ? RadarProcessingRebalancePolicyResult.Allowed(input)
            : RadarProcessingRebalancePolicyResult.Rejected(input, rejections);
    }

    public RadarProcessingRebalancePolicyResult RecordAcceptedMove(
        RadarProcessingRebalanceMovePolicyInput input)
    {
        var result = EvaluateMove(input);
        if (!result.IsAllowed)
        {
            return result;
        }

        globalMoveCount = checked(globalMoveCount + 1);
        sourceShardMoveCounts[input.SourceShardId] = checked(sourceShardMoveCounts[input.SourceShardId] + 1);
        targetShardReceiveCounts[input.TargetShardId] = checked(targetShardReceiveCounts[input.TargetShardId] + 1);
        partitionResidencyStartSequences[input.PartitionId] = EvaluationSequence;
        partitionLastMoveSequences[input.PartitionId] = EvaluationSequence;
        sourceShardLastMoveSequences[input.SourceShardId] = EvaluationSequence;
        targetShardLastReceiveSequences[input.TargetShardId] = EvaluationSequence;

        return result;
    }

    public RadarProcessingPartitionResidency GetPartitionResidency(int partitionId)
    {
        EnsurePartitionId(partitionId);

        return new RadarProcessingPartitionResidency(
            partitionId,
            EvaluationSequence - partitionResidencyStartSequences[partitionId],
            Options.MinimumPartitionResidencyEvaluations);
    }

    public RadarProcessingPartitionCooldown GetPartitionCooldown(int partitionId)
    {
        EnsurePartitionId(partitionId);

        return new RadarProcessingPartitionCooldown(
            partitionId,
            CalculateRemainingCooldown(
                partitionLastMoveSequences[partitionId],
                Options.PartitionMoveCooldownEvaluations));
    }

    public RadarProcessingShardCooldown GetSourceShardCooldown(int shardId)
    {
        EnsureShardId(shardId);

        return new RadarProcessingShardCooldown(
            shardId,
            CalculateRemainingCooldown(
                sourceShardLastMoveSequences[shardId],
                Options.SourceShardMoveCooldownEvaluations));
    }

    public RadarProcessingShardCooldown GetTargetShardCooldown(int shardId)
    {
        EnsureShardId(shardId);

        return new RadarProcessingShardCooldown(
            shardId,
            CalculateRemainingCooldown(
                targetShardLastReceiveSequences[shardId],
                Options.TargetShardReceiveCooldownEvaluations));
    }

    public RadarProcessingRebalanceBudget GetSourceShardMoveBudget(int shardId)
    {
        EnsureShardId(shardId);

        return new RadarProcessingRebalanceBudget(
            Options.SourceShardMoveBudgetPerWindow,
            sourceShardMoveCounts[shardId]);
    }

    public RadarProcessingRebalanceBudget GetTargetShardReceiveBudget(int shardId)
    {
        EnsureShardId(shardId);

        return new RadarProcessingRebalanceBudget(
            Options.TargetShardReceiveBudgetPerWindow,
            targetShardReceiveCounts[shardId]);
    }

    private int CalculateRemainingCooldown(
        long lastSequence,
        int cooldownEvaluations)
    {
        if (lastSequence < 0 || cooldownEvaluations == 0)
        {
            return 0;
        }

        var elapsed = EvaluationSequence - lastSequence;
        return elapsed >= cooldownEvaluations
            ? 0
            : checked((int)(cooldownEvaluations - elapsed));
    }

    private void ResetBudgets()
    {
        budgetWindowStartSequence = EvaluationSequence;
        globalMoveCount = 0;
        Array.Clear(sourceShardMoveCounts);
        Array.Clear(targetShardReceiveCounts);
    }

    private void EnsureInputShape(RadarProcessingRebalanceMovePolicyInput input)
    {
        EnsurePartitionId(input.PartitionId);
        EnsureShardId(input.SourceShardId);
        EnsureShardId(input.TargetShardId);
    }

    private void EnsurePartitionId(int partitionId)
    {
        if ((uint)partitionId < (uint)PartitionCount)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(partitionId));
    }

    private void EnsureShardId(int shardId)
    {
        if ((uint)shardId < (uint)ShardCount)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(shardId));
    }

    private static long[] CreateUninitializedSequenceArray(int length)
    {
        var result = new long[length];
        Array.Fill(result, -1L);
        return result;
    }
}
