namespace RadarPulse.Domain.Processing;

/// <summary>
/// Stateful rebalance policy evaluator for budgets, cooldowns, and residency.
/// </summary>
/// <remarks>
/// The state advances once per rebalance evaluation. Accepted moves consume
/// global, source-shard, and target-shard budgets and reset residency/cooldown
/// sequences for the moved partition and participating shards.
/// </remarks>
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

    /// <summary>
    /// Creates policy state for a fixed processing topology shape.
    /// </summary>
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

    /// <summary>
    /// Policy options used by this state.
    /// </summary>
    public RadarProcessingRebalanceOptions Options { get; }

    /// <summary>
    /// Number of partitions tracked by the policy state.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Number of shards tracked by the policy state.
    /// </summary>
    public int ShardCount { get; }

    /// <summary>
    /// Monotonic rebalance evaluation sequence.
    /// </summary>
    public long EvaluationSequence { get; private set; }

    /// <summary>
    /// Current global move budget for the active budget window.
    /// </summary>
    public RadarProcessingRebalanceBudget GlobalMoveBudget =>
        new(Options.GlobalMoveBudgetPerWindow, globalMoveCount);

    /// <summary>
    /// Advances the evaluation sequence and resets budgets when a window expires.
    /// </summary>
    public void AdvanceEvaluation()
    {
        EvaluationSequence = checked(EvaluationSequence + 1);
        if (EvaluationSequence - budgetWindowStartSequence >= Options.BudgetWindowEvaluationCount)
        {
            ResetBudgets();
        }
    }

    /// <summary>
    /// Evaluates whether a candidate move satisfies residency, cooldown, budget, and benefit rules.
    /// </summary>
    public RadarProcessingRebalancePolicyResult EvaluateMove(
        RadarProcessingRebalanceMovePolicyInput input)
    {
        EnsureInputShape(input);

        List<RadarProcessingRebalancePolicyRejection>? rejections = null;

        if (!GetPartitionResidency(input.PartitionId).IsSatisfied)
        {
            AddRejection(
                ref rejections,
                RadarProcessingRebalancePolicyRejection.PartitionBelowMinimumResidency);
        }

        if (GetPartitionCooldown(input.PartitionId).IsActive)
        {
            AddRejection(
                ref rejections,
                RadarProcessingRebalancePolicyRejection.PartitionInCooldown);
        }

        if (GetSourceShardCooldown(input.SourceShardId).IsActive)
        {
            AddRejection(
                ref rejections,
                RadarProcessingRebalancePolicyRejection.SourceShardInCooldown);
        }

        if (GetTargetShardCooldown(input.TargetShardId).IsActive)
        {
            AddRejection(
                ref rejections,
                RadarProcessingRebalancePolicyRejection.TargetShardInCooldown);
        }

        if (GlobalMoveBudget.IsExhausted)
        {
            AddRejection(
                ref rejections,
                RadarProcessingRebalancePolicyRejection.GlobalMoveBudgetExhausted);
        }

        if (GetSourceShardMoveBudget(input.SourceShardId).IsExhausted)
        {
            AddRejection(
                ref rejections,
                RadarProcessingRebalancePolicyRejection.SourceShardMoveBudgetExhausted);
        }

        if (GetTargetShardReceiveBudget(input.TargetShardId).IsExhausted)
        {
            AddRejection(
                ref rejections,
                RadarProcessingRebalancePolicyRejection.TargetShardReceiveBudgetExhausted);
        }

        if (input.ProjectedBenefit < Options.MinimumProjectedBenefit)
        {
            AddRejection(
                ref rejections,
                RadarProcessingRebalancePolicyRejection.InsufficientProjectedBenefit);
        }

        if (input.TargetProjectedPressure.Value > Options.TargetHeadroomThreshold)
        {
            AddRejection(
                ref rejections,
                RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded);
        }

        return rejections is null
            ? RadarProcessingRebalancePolicyResult.Allowed(input)
            : RadarProcessingRebalancePolicyResult.Rejected(input, rejections);
    }

    /// <summary>
    /// Records an accepted move after re-evaluating policy eligibility.
    /// </summary>
    /// <returns>
    /// Allowed result when counters were updated; rejected result when the input no
    /// longer satisfies policy.
    /// </returns>
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

    /// <summary>
    /// Returns current owner residency for a partition.
    /// </summary>
    public RadarProcessingPartitionResidency GetPartitionResidency(int partitionId)
    {
        EnsurePartitionId(partitionId);

        return new RadarProcessingPartitionResidency(
            partitionId,
            EvaluationSequence - partitionResidencyStartSequences[partitionId],
            Options.MinimumPartitionResidencyEvaluations);
    }

    /// <summary>
    /// Returns remaining partition cooldown after its most recent move.
    /// </summary>
    public RadarProcessingPartitionCooldown GetPartitionCooldown(int partitionId)
    {
        EnsurePartitionId(partitionId);

        return new RadarProcessingPartitionCooldown(
            partitionId,
            CalculateRemainingCooldown(
                partitionLastMoveSequences[partitionId],
                Options.PartitionMoveCooldownEvaluations));
    }

    /// <summary>
    /// Returns remaining cooldown for moving work away from a source shard.
    /// </summary>
    public RadarProcessingShardCooldown GetSourceShardCooldown(int shardId)
    {
        EnsureShardId(shardId);

        return new RadarProcessingShardCooldown(
            shardId,
            CalculateRemainingCooldown(
                sourceShardLastMoveSequences[shardId],
                Options.SourceShardMoveCooldownEvaluations));
    }

    /// <summary>
    /// Returns remaining cooldown for receiving work into a target shard.
    /// </summary>
    public RadarProcessingShardCooldown GetTargetShardCooldown(int shardId)
    {
        EnsureShardId(shardId);

        return new RadarProcessingShardCooldown(
            shardId,
            CalculateRemainingCooldown(
                targetShardLastReceiveSequences[shardId],
                Options.TargetShardReceiveCooldownEvaluations));
    }

    /// <summary>
    /// Returns the active budget for moves away from a source shard.
    /// </summary>
    public RadarProcessingRebalanceBudget GetSourceShardMoveBudget(int shardId)
    {
        EnsureShardId(shardId);

        return new RadarProcessingRebalanceBudget(
            Options.SourceShardMoveBudgetPerWindow,
            sourceShardMoveCounts[shardId]);
    }

    /// <summary>
    /// Returns the active budget for moves into a target shard.
    /// </summary>
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

    private static void AddRejection(
        ref List<RadarProcessingRebalancePolicyRejection>? rejections,
        RadarProcessingRebalancePolicyRejection rejection)
    {
        rejections ??= new List<RadarProcessingRebalancePolicyRejection>();
        rejections.Add(rejection);
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
