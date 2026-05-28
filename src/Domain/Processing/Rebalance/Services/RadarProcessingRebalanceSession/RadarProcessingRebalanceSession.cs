namespace RadarPulse.Domain.Processing;


/// <summary>
/// Stateful processing session that evaluates pressure and publishes rebalance moves.
/// </summary>
/// <remarks>
/// The session wraps a partitioned processing core, records pressure samples,
/// runs direct hot-relief before cold evacuation, validates state handoff before
/// and after publication, and retains diagnostic telemetry according to hardening
/// options. It is synchronous; async shard transport uses a separate async session.
/// </remarks>
public sealed partial class RadarProcessingRebalanceSession
{
    private readonly RadarProcessingCore core;
    private readonly RadarProcessingPressureOptions pressureOptions;
    private readonly RadarProcessingPressureWindow pressureWindow;
    private readonly RadarProcessingRebalancePolicyState policyState;
    private readonly RadarProcessingHotPartitionClassifier hotPartitionClassifier;
    private readonly RadarProcessingQuarantineLifecycleTracker quarantineLifecycleTracker;
    private readonly RadarProcessingRebalanceTelemetryRecorder telemetryRecorder;
    private readonly RadarProcessingRebalanceHardeningOptions hardeningOptions;
    private readonly RadarProcessingPressureSkewTransformer? pressureSkewTransformer;
    private readonly RadarProcessingDirectHotReliefPlanner directHotReliefPlanner;
    private readonly RadarProcessingColdEvacuationPlanner coldEvacuationPlanner;
    private readonly RadarProcessingMigrationCoordinator migrationCoordinator;
    private long nextDecisionId = 1;

    /// <summary>
    /// Creates a rebalance session around a compatible processing core.
    /// </summary>
    public RadarProcessingRebalanceSession(
        RadarProcessingCore core,
        RadarProcessingPressureOptions? pressureOptions = null,
        RadarProcessingPressureWindow? pressureWindow = null,
        RadarProcessingRebalancePolicyState? policyState = null,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier = null,
        RadarProcessingDirectHotReliefPlanner? directHotReliefPlanner = null,
        RadarProcessingColdEvacuationPlanner? coldEvacuationPlanner = null,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker = null,
        RadarProcessingRebalanceTelemetryRecorder? telemetryRecorder = null,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingPressureSkewOptions? pressureSkewOptions = null)
    {
        ArgumentNullException.ThrowIfNull(core);

        if (core.Options.ExecutionMode is not RadarProcessingExecutionMode.PartitionedBarrier and
            not RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new ArgumentException(
                "Rebalance sessions require partitioned barrier or async shard transport processing.",
                nameof(core));
        }

        this.core = core;
        this.hardeningOptions = hardeningOptions ?? RadarProcessingRebalanceHardeningOptions.Default;
        pressureSkewTransformer = pressureSkewOptions?.IsEnabled == true
            ? new RadarProcessingPressureSkewTransformer(pressureSkewOptions)
            : null;
        this.pressureOptions = pressureOptions ?? RadarProcessingPressureOptions.Default;
        this.pressureWindow = pressureWindow ?? new RadarProcessingPressureWindow();
        this.policyState = policyState ?? new RadarProcessingRebalancePolicyState(
            core.Options.PartitionCount,
            core.Options.ShardCount);
        this.hotPartitionClassifier = hotPartitionClassifier ??
                                      new RadarProcessingHotPartitionClassifier(core.Options.PartitionCount);
        this.quarantineLifecycleTracker = quarantineLifecycleTracker ??
                                          new RadarProcessingQuarantineLifecycleTracker(
                                              core.Options.PartitionCount,
                                              this.hardeningOptions.QuarantineLifecycle);
        this.telemetryRecorder = telemetryRecorder ??
                                 new RadarProcessingRebalanceTelemetryRecorder(this.hardeningOptions.TelemetryRetention);
        this.directHotReliefPlanner = directHotReliefPlanner ?? new RadarProcessingDirectHotReliefPlanner();
        this.coldEvacuationPlanner = coldEvacuationPlanner ?? new RadarProcessingColdEvacuationPlanner();
        migrationCoordinator = new RadarProcessingMigrationCoordinator(core.TopologyManager);

        EnsureCompatibleShape(this.policyState, this.hotPartitionClassifier, this.quarantineLifecycleTracker);
    }

    /// <summary>
    /// Processing core owned by the session.
    /// </summary>
    public RadarProcessingCore Core => core;

    /// <summary>
    /// Current topology snapshot of the processing core.
    /// </summary>
    public RadarProcessingTopology CurrentTopology => core.Topology;

    /// <summary>
    /// Rolling pressure window used by rebalance planners.
    /// </summary>
    public RadarProcessingPressureWindow PressureWindow => pressureWindow;

    /// <summary>
    /// Stateful policy budget, cooldown, and residency evaluator.
    /// </summary>
    public RadarProcessingRebalancePolicyState PolicyState => policyState;

    /// <summary>
    /// Hot partition classifier used to block intrinsic-hot direct moves.
    /// </summary>
    public RadarProcessingHotPartitionClassifier HotPartitionClassifier => hotPartitionClassifier;

    /// <summary>
    /// Quarantine lifecycle tracker used to block or retry problematic partitions.
    /// </summary>
    public RadarProcessingQuarantineLifecycleTracker QuarantineLifecycleTracker => quarantineLifecycleTracker;

    /// <summary>
    /// Telemetry recorder for decisions, moves, validation failures, and quarantine transitions.
    /// </summary>
    public RadarProcessingRebalanceTelemetryRecorder TelemetryRecorder => telemetryRecorder;

    /// <summary>
    /// Hardening options applied by the session.
    /// </summary>
    public RadarProcessingRebalanceHardeningOptions HardeningOptions => hardeningOptions;

    /// <summary>
    /// Validation profile selected from hardening options.
    /// </summary>
    public RadarProcessingValidationProfile ValidationProfile => hardeningOptions.ValidationProfile;
}
