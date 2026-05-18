# Milestone 007: Rebalance Production Hardening Plan

Status: draft.

This plan implements the milestone 007 architecture defined in
`007-rebalance-production-hardening.md`.

The plan is intentionally scoped to production hardening of the synchronous
rebalance control plane over the closed milestone 006 baseline. It should not
introduce retained async worker queues, live ingestion, durable transport,
source-level migration, partition splitting, or complex radar algorithms.

## Goal

Milestone 007 hardens the existing synchronous rebalance controller so it can
run for longer production-shaped sessions while remaining bounded,
explainable, and measurable.

The target control-plane shape is:

```text
RadarEventBatch
  -> process one batch against one topology snapshot
  -> derive partitioned telemetry and pressure samples
  -> advance quarantine lifecycle before planning
  -> evaluate cautious rebalance under bounded policy state
  -> publish validated migration only between batches
  -> retain bounded telemetry counters and recent detail
  -> report validation profile and allocation contour explicitly
```

The milestone must preserve the milestone 004 stream contract, milestone 005
processing-core lifetime boundary, and milestone 006 topology/migration
semantics. A single `RadarEventBatch` must never be routed against mixed
topology versions, and leased payload storage must never be retained beyond the
synchronous callback.

## Starting Point

Milestone 006 is complete and provides:

```text
RadarProcessingTopologyVersion
RadarProcessingTopologySnapshot
RadarProcessingTopologyManager
RadarProcessingBatchRouter
RadarProcessingBatchRoute
RadarProcessingTelemetry
RadarProcessingPressureSample
RadarProcessingPressureWindow
RadarProcessingRebalanceOptions
RadarProcessingRebalancePolicyState
RadarProcessingRebalanceDecision
RadarProcessingDirectHotReliefPlanner
RadarProcessingHotPartitionState
RadarProcessingHotPartitionClassifier
RadarProcessingColdEvacuationPlanner
RadarProcessingMigrationCoordinator
RadarProcessingStateHandoffValidator
RadarProcessingRebalanceSession
RadarProcessingRebalanceSessionResult
RadarProcessingRebalanceValidator
RadarProcessingSyntheticRebalanceWorkload
RadarProcessingSyntheticRebalanceBenchmark
processing benchmark rebalance-synthetic CLI command
processing benchmark rebalance-archive CLI command
```

Important existing constraints:

```text
RadarEventBatch is the processing input boundary.
RadarStreamEvent is a 64-byte unmanaged value type.
Leased batches are valid only during the synchronous publish callback.
SourceId -> PartitionId remains stable for a processing session.
PartitionId -> ShardId may change only through topology publication.
Rebalance is pressure relief, not mechanical equalization.
Direct hot relief is attempted before cold evacuation.
Cold evacuation is a fallback from a hot shard, not general shuffling.
Skipped rebalance decisions are first-class telemetry.
PartitionedBarrier remains synchronous and is the correctness reference.
```

Known milestone 006 hardening inputs:

```text
quarantine lifecycle:
  quarantined hot partitions must decay, clear, or become retry-eligible

telemetry retention:
  skipped decisions must remain visible without unbounded detail growth

allocation:
  cache-wide real-data allocation is about 0.23 bytes/payload value versus
  0.03 in the milestone 005 processing-only synthetic baseline

real-data confidence:
  the accepted cache-wide capture is one local KTLX corpus; repeated and
  broader contours are still needed

benchmark interpretation:
  archive end-to-end timings are replay dominated and must be reported
  separately from processing callback timings
```

## Target Implementation Shape

Most milestone 007 domain contracts should live under
`RadarPulse.Domain.Processing`, because hardening is part of the processing
control plane and must be testable without archive infrastructure.

Candidate layering:

```text
src/Domain/Processing
  hardening options, validation profiles, lifecycle state, telemetry summaries,
  bounded retention, allocation-neutral counters, session result extensions

src/Infrastructure/Processing
  synthetic lifecycle workloads, retention stress workloads, benchmark harness
  extensions, allocation attribution helpers

src/Presentation
  CLI options and output for retention mode, validation profile, lifecycle
  counters, allocation contour, and performance comparison
```

The implementation should keep these responsibilities separate:

```text
Quarantine lifecycle:
  converts recent pressure and move evidence into effective hot-partition
  classification without retaining payloads

Telemetry retention:
  stores aggregate counters and bounded recent detail for decisions,
  lifecycle transitions, accepted moves, and validation failures

Validation profile:
  controls which read-side diagnostics are paid for on a session run

Allocation attribution:
  reports allocation by measured contour without confusing replay construction
  with processing callback and control-plane work

Benchmark comparison:
  compares milestone 007 results against the accepted milestone 006 baseline
  before closeout
```

The existing rebalance session should remain the composition boundary. The
router should not trigger policy, and planners should not mutate topology
directly.

## Implementation Slices

### 1. Hardening Options And Profiles

Introduce explicit options for milestone 007 hardening behavior.

Candidate types:

```text
RadarProcessingRebalanceHardeningOptions
RadarProcessingTelemetryRetentionOptions
RadarProcessingQuarantineLifecycleOptions
RadarProcessingValidationProfile
RadarProcessingDiagnosticRetentionMode
```

Required behavior:

```text
options have deterministic defaults
retention limits reject negative values
zero retained detail means counters-only retention
quarantine TTL is expressed in logical evaluations
sustained cooling count is expressed in pressure samples/evaluations
material pressure-change threshold is explicit
validation profile defaults to diagnostic for tests/benchmarks if needed
production-shaped default can be essential once verified
```

Expected tests:

```text
default hardening options are valid
invalid retention limits are rejected
invalid quarantine TTL/cooling thresholds are rejected
validation profile enum values are stable
retention mode and validation profile are independent
hardening options do not change existing rebalance options unless specified
```

Notes:

```text
Do not fold all hardening settings into RadarProcessingRebalanceOptions if that
would blur policy decisions with diagnostic and lifecycle retention settings.
The implementation can compose options, but the architecture should keep the
concerns visible.
```

### 2. Bounded Telemetry Contracts

Add compact domain contracts for aggregate telemetry and recent detail.

Candidate types:

```text
RadarProcessingRebalanceTelemetrySummary
RadarProcessingRebalanceTelemetryCounters
RadarProcessingRebalanceSkippedReasonCounter
RadarProcessingRebalanceRecentDecision
RadarProcessingRebalanceRecentAcceptedMove
RadarProcessingRebalanceRecentValidationFailure
RadarProcessingRebalanceRetentionStats
```

Required behavior:

```text
summary exposes total evaluations
summary exposes accepted move counts by move kind
summary exposes skipped decisions by skipped reason
summary exposes rejected candidates and no-action decisions
summary exposes failed migration and validation counts
summary exposes retained detail counts and dropped detail counts
summary stores stable enum/code values, not formatted strings
recent decision detail is compact and numeric
public summary/result objects are immutable or defensively copied
```

Expected tests:

```text
counters increment deterministically
skipped reasons aggregate by reason code
accepted move summaries aggregate by move kind
recent detail respects max retained decisions
dropped detail count increments when ring limits are exceeded
zero retention still preserves counters
public snapshots cannot mutate internal retained state
```

Implementation guidance:

```text
Use arrays indexed by stable enum ordinal only if the enum domain is controlled
and tests protect the mapping. Otherwise use small dictionaries with explicit
copy-on-publication. Avoid storing formatted CLI text in domain state.
```

### 3. Telemetry Recorder And Retention Windows

Implement the recorder that converts decisions, lifecycle transitions, and
validation outcomes into bounded telemetry.

Candidate types:

```text
RadarProcessingRebalanceTelemetryRecorder
RadarProcessingBoundedTelemetryWindow<T>
RadarProcessingTelemetryRetentionResult
```

Required behavior:

```text
recording an evaluation updates aggregate counters
recording a decision updates decision-kind counters
recording skipped reasons updates reason counters
recording an accepted move updates move-kind counters and optional recent move detail
recording validation failures updates counters and optional recent failure detail
bounded windows drop oldest detail when limits are reached
dropped counts are visible in summary
recorder reset is explicit and deterministic
recorder does not retain RadarEventBatch, route buffers, or payload spans
```

Expected tests:

```text
recorder keeps counters when recent detail retention is disabled
recorder keeps only the latest N decisions
recorder tracks dropped decision detail
recorder keeps only the latest N validation failures
accepted move recording does not require candidate payload references
summary snapshot is stable after more recorder mutations
```

Performance notes:

```text
The recorder is expected to run on every evaluation. No-action and skipped
decision paths should not allocate avoidable collections after warmup.
```

### 4. Quarantine Lifecycle State And Transition Contracts

Extend hot-partition state with explicit lifecycle evidence.

Candidate types:

```text
RadarProcessingQuarantineLifecycleState
RadarProcessingQuarantineTransition
RadarProcessingQuarantineTransitionReason
RadarProcessingQuarantineEffectiveClassification
RadarProcessingQuarantineEvidence
```

Required behavior:

```text
state records quarantine start sequence
state records latest evidence sequence
state records baseline partition pressure when quarantine began
state records latest partition pressure
state records sustained cooled-sample count
state records retry eligibility
state records transition reason codes
state can clear, downgrade, or re-enter quarantine deterministically
state is compact and does not retain pressure sample objects unnecessarily
```

Expected tests:

```text
new partition starts unclassified/movable according to existing classifier behavior
quarantine records start sequence and baseline pressure
transition snapshots are immutable
cooling evidence increments cooled-sample count
hot evidence resets cooled-sample count when appropriate
state rejects out-of-order logical sequence updates if required
state does not require topology mutation to change effective classification
```

Compatibility note:

```text
If existing RadarProcessingHotPartitionState can carry the required lifecycle
state without becoming confusing, prefer extending it over adding a parallel
state store. The observable behavior is more important than the exact type
names.
```

### 5. Quarantine Lifecycle Evaluator

Implement lifecycle advancement before rebalance planning.

Candidate type:

```text
RadarProcessingQuarantineLifecycleEvaluator
```

Required behavior:

```text
advance lifecycle from latest pressure window/sample
clear quarantine after configured sustained cooled samples
mark partition retry-eligible after configured TTL
mark partition retry-eligible after material pressure change
preserve quarantine when hot evidence remains current
record transition telemetry for clear, retry, re-entry, and downgrade paths
do not mutate topology or policy budgets
```

Expected tests:

```text
TTL expiry makes a quarantined partition retry-eligible
sustained cooling clears quarantine
material pressure drop makes quarantine retry-eligible
material pressure increase can also make old evidence stale if configured
insufficient cooling keeps quarantine active
repeated hot samples keep quarantine active
transition telemetry reports reason, sequence, partition, and pressure
evaluation before planning lets planners reconsider retry-eligible partitions
```

Ordering requirement:

```text
sample pressure
advance lifecycle
then run direct hot relief and cold evacuation planners
```

This ordering is required so stale quarantine evidence does not block a safe
move for one extra evaluation.

### 6. Planner Integration For Effective Classification

Update direct hot relief and cold evacuation planning to consume effective
classification from the lifecycle-aware state.

Required behavior:

```text
direct hot relief skips actively quarantined partitions
direct hot relief may reconsider retry-eligible partitions
intrinsic-hot classification still blocks unsafe direct movement
cold evacuation remains available when direct movement is unsafe
retry failure can re-enter quarantine with fresh evidence
successful observed relief can clear or downgrade previous quarantine evidence
skipped reasons remain explicit
```

Expected tests:

```text
active quarantine blocks direct hot relief
TTL retry eligibility allows planner to reconsider a partition
cooled partition clears quarantine and no longer reports stale skipped reason
retry that again finds no safe target re-enters quarantine
successful move with adequate relief clears ineffective-move evidence
cold evacuation still runs only as fallback from a hot source shard
skipped reason distinguishes active quarantine from intrinsic-hot and no-safe-target
```

Guardrail:

```text
Do not let lifecycle retry make rebalance more aggressive globally. Retry only
removes stale classification evidence; the normal pressure, headroom, benefit,
cooldown, residency, and budget gates still apply.
```

### 7. Session Integration And Result Surfaces

Integrate hardening into `RadarProcessingRebalanceSession`.

Candidate changes:

```text
RadarProcessingRebalanceSessionOptions or existing session constructor options
RadarProcessingRebalanceSessionResult.TelemetrySummary
RadarProcessingRebalanceSessionResult.RetentionStats
RadarProcessingRebalanceSessionResult.ValidationProfile
RadarProcessingRebalanceSessionResult.QuarantineTransitions
```

Required behavior:

```text
session owns hardening state across processed batches
session advances lifecycle before planning
session records every evaluation in telemetry summary
session records accepted moves, skipped decisions, lifecycle transitions, and validation failures
session exposes bounded summary without exposing mutable recorder internals
session can run counters-only, recent-detail, or diagnostic retention mode
session still processes one batch against one topology snapshot
session still publishes accepted topology changes only between batches
```

Expected tests:

```text
session result reports telemetry summary after no-action evaluation
session result reports lifecycle transition after quarantine clears
session honors zero retained decision detail
session honors bounded retained decision detail
session records dropped-detail counts under stress
session validation profile is visible in result
session does not retain leased batch payload after callback returns
owned and leased-equivalent inputs still produce matching processing metrics
```

Compatibility note:

```text
Avoid breaking existing callers that use milestone 006 defaults. New options
should have default behavior that preserves current correctness and cautious
policy semantics.
```

### 8. Validation Profiles

Make validation cost explicit through profiles.

Candidate enum:

```text
RadarProcessingValidationProfile
  Off
  Essential
  Diagnostic
  Benchmark
```

Profile behavior:

```text
Off:
  no read-side rebalance validation beyond construction-time guardrails

Essential:
  topology version sequence, migration publication, failed migration, and state
  handoff result checks

Diagnostic:
  essential checks plus route, telemetry, pressure sample, decision, migration,
  and handoff diagnostics

Benchmark:
  diagnostic checks plus deterministic checksum/summary fields needed for
  run-to-run comparison
```

Required behavior:

```text
session uses selected validation profile
benchmark harness can force Benchmark profile
tests can force Diagnostic or Benchmark profile
validation failure counters record failures by code
recent validation failures obey retention limits
validation output does not allocate unnecessary detail when profile is Off or Essential
```

Expected tests:

```text
Off profile skips read-side validator
Essential profile records migration/handoff failures
Diagnostic profile preserves existing milestone 006 validation behavior
Benchmark profile includes deterministic comparison fields
validation profile is included in benchmark result
validation failure retention is bounded
```

Guardrail:

```text
Do not remove the validator introduced in milestone 006. The milestone 007
change is to make its cost profile explicit and selectable.
```

### 9. Allocation Attribution Baseline

Add measurement support that can attribute allocation to the relevant contour.

Candidate changes:

```text
RadarProcessingRebalanceAllocationSummary
RadarProcessingBenchmarkAllocationSnapshot
synthetic benchmark result allocation fields
archive rebalance benchmark callback allocation fields
```

Required behavior:

```text
synthetic benchmark reports processing/control-plane allocation per payload value
archive benchmark reports callback allocation separately from archive end-to-end allocation
benchmark result states validation profile and retention mode
benchmark result states whether CLI formatting is outside measured callback timing
same-run static/sampling/rebalance modes report comparable allocation fields
```

Expected tests:

```text
benchmark result includes allocation summary
same-run modes all populate allocation fields
validation profile is reported in allocation-bearing results
retention mode is reported in allocation-bearing results
callback allocation field is not confused with end-to-end replay allocation
```

Implementation notes:

```text
Use the benchmark harness measurement approach already established in earlier
milestones. If exact subsystem attribution is not technically reliable inside
one process, report contour-level attribution explicitly and document the
remaining blind spot.
```

### 10. Allocation Reduction Pass

Reduce avoidable allocation discovered by the baseline.

Initial target areas:

```text
no-action decision path
skipped-reason aggregation
telemetry summary snapshots
bounded recent-detail windows
validation failure reporting
accepted move summaries
CLI row formatting outside measured callback timing
```

Required behavior:

```text
no-action and skipped-only evaluations avoid collection churn after warmup
retention windows reuse storage where safe
public snapshots remain immutable or defensively copied
summary counters avoid string allocation
validation detail is generated only for selected profiles
CLI formatting is not part of processing callback measurement
```

Expected tests:

```text
counters-only long run does not grow retained detail
recent-detail long run retains no more than configured limit
snapshot mutation cannot affect recorder state
validation Off or Essential produces less detail than Diagnostic
benchmark allocation fields remain stable enough for regression comparison
```

Benchmark requirement:

```text
Capture before/after allocation for synthetic long-run retention stress and
cache-wide real-data callback contours. If allocation does not improve, the
closeout must explain which sources remain necessary or outside rebalance.
```

### 11. Synthetic Quarantine Lifecycle Workloads

Extend the synthetic workload catalog to prove lifecycle behavior.

Candidate workload kinds:

```text
QuarantineTtlRetry
QuarantineSustainedCoolingClear
QuarantinePressureChangeRetry
QuarantineRetryReentry
QuarantineSuccessfulReliefClear
```

Required behavior:

```text
TTL workload quarantines a partition and later makes it retry-eligible
cooling workload clears quarantine after sustained normal/cool samples
pressure-change workload makes old quarantine evidence stale
retry-reentry workload retries then re-enters quarantine after repeated failure
successful-relief workload clears ineffective-move evidence after observed relief
```

Expected tests:

```text
each workload produces deterministic lifecycle counters
each workload validates successfully
each workload has stable checksum/summary
TTL retry does not bypass target headroom gates
cooling clear removes stale quarantine skipped reason
retry re-entry increments transition counters
```

Design note:

```text
Keep these workloads small and behavioral like milestone 006 synthetic
rebalance workloads. They prove lifecycle decisions, not raw throughput.
```

### 12. Synthetic Retention Stress Workloads

Add long-running synthetic workloads that stress telemetry retention.

Candidate workload kinds:

```text
LongNoHotShard
LongCooldownRejection
LongUnsafeTargetRejection
LongMixedSkippedReasons
LongValidationFailureDiagnostics if a safe synthetic failure harness exists
```

Required behavior:

```text
workloads run many evaluations
counters equal total expected evaluations
retained detail remains bounded
dropped-detail counters increase when configured limits are exceeded
no retained payload or route buffers survive between evaluations
```

Expected tests:

```text
long no-hot-shard run increments no-hot-shard counter
long cooldown run increments cooldown skipped reason counter
mixed skipped run aggregates all expected reasons
retained decision count never exceeds configured limit
dropped decision count equals expected overflow
zero retention mode keeps counters and drops all detail
```

Performance note:

```text
These workloads are the main guard against accidental unbounded telemetry
growth.
```

### 13. Benchmark Harness Extensions

Extend synthetic and archive benchmark harnesses for hardening data.

Required synthetic benchmark behavior:

```text
supports existing milestone 006 modes
supports lifecycle workload catalog
supports retention stress workloads
accepts retention mode and retention limits
accepts validation profile
reports lifecycle counters
reports retained and dropped detail counts
reports allocation contour
preserves same-run static/sampling/rebalance comparison
```

Required archive benchmark behavior:

```text
preserves --file and --cache inputs
preserves static, sampling, rebalance, and all modes
accepts validation profile
accepts retention mode and retention limits
reports callback timing separately from end-to-end archive timing
reports lifecycle and retention counters
reports skipped reasons and accepted moves
reports allocation contour for callback and end-to-end where available
```

Expected tests:

```text
synthetic benchmark result includes lifecycle counters
synthetic benchmark result includes retention stats
archive benchmark result includes validation profile
archive benchmark result includes retention mode
all mode still reports comparable static/sampling/rebalance rows
legacy benchmark options continue to work with defaults
```

Compatibility requirement:

```text
Existing benchmark commands should keep working with default options. New
hardening options should be additive.
```

### 14. CLI Updates

Expose hardening options and diagnostics through existing processing benchmark
commands.

Candidate CLI additions:

```text
--validation-profile off|essential|diagnostic|benchmark
--retention-mode counters|recent|diagnostic
--max-retained-decisions n
--max-retained-transitions n
--max-retained-validation-failures n
--quarantine-ttl-evaluations n
--quarantine-cooling-samples n
--quarantine-pressure-change-threshold value
```

Required output additions:

```text
validation profile
retention mode
quarantine lifecycle options
quarantine entries/clears/retries/reentries
retained decision counts
dropped decision detail counts
retained transition counts
dropped transition detail counts
skipped-reason counters
callback allocation contour
end-to-end archive allocation contour when available
```

Expected tests:

```text
CLI accepts validation profile option
CLI rejects invalid validation profile
CLI accepts retention mode option
CLI rejects invalid retention limits
CLI prints retention and lifecycle fields for synthetic benchmark
CLI prints retention and lifecycle fields for archive benchmark
CLI keeps detailed samples capped
```

Output discipline:

```text
Do not print unbounded decision history. CLI output can show aggregate counters
and capped recent samples, but large runs must remain readable.
```

### 15. Policy Defaults And Tuning Audit

Audit default policy and lifecycle settings against the new workloads and
real-data contours.

Required behavior:

```text
defaults remain conservative
quarantine TTL is long enough to avoid immediate retry storms
cooling clear requires sustained evidence, not one cool sample
pressure-change retry requires a meaningful delta
retention defaults are bounded
validation defaults are explicit for benchmark and production-shaped runs
```

Expected tests:

```text
default lifecycle options avoid retry storm workload churn
default cooling threshold ignores one-sample noise
default pressure-change threshold avoids cosmetic retry
default retention mode does not grow detail unbounded
default benchmark profile keeps validation visible
```

Review requirement:

```text
Every default that changes milestone 006 behavior must be justified by a
benchmark or lifecycle test. If no evidence exists, keep the milestone 006
default behavior.
```

Audit artifact:

```text
docs/milestones/007-rebalance-production-hardening-policy-default-audit.md
```

Audit decision after slice 16:

```text
current defaults are accepted without code changes
hardening defaults remain diagnostic, bounded, and conservative
pressure skew remains opt-in and must not be part of baseline real-data runs
release comparisons should keep passing explicit topology, parallelism,
retention, validation, and skew settings
```

### 16. Documentation, Decision Trace, And Handoff

Close the milestone with documentation once implementation and benchmarks are
complete.

Required documentation:

```text
docs/milestones/007-rebalance-production-hardening-decision-trace.md
docs/milestones/007-rebalance-production-hardening-closeout.md
docs/handoff.md update
```

Decision trace status after slice 18:

```text
docs/milestones/007-rebalance-production-hardening-decision-trace.md is written
closeout remains pending until final comprehensive performance comparison is
captured and interpreted
```

Decision trace should record:

```text
why hardening preceded async worker transport
quarantine lifecycle model
telemetry retention model
validation profile model
allocation attribution and reduction decisions
benchmark interpretation decisions
remaining risks and deferred work
```

Closeout should record:

```text
implemented slices
verification commands and results
synthetic lifecycle benchmark results
retention stress benchmark results
single-file real-data results
cache-wide real-data results
allocation comparison
performance comparison against milestone 006 and 005 baselines
known remaining debt
next milestone recommendation
```

Handoff should record:

```text
current milestone state
new important files
latest verified commands
benchmark baselines
next milestone input
preserved invariants for future async worker transport
```

### 17. Final Comprehensive Performance Comparison And Regression Gate

End milestone 007 with a comprehensive performance comparison before closeout.
This is an implementation slice, not an optional reporting task.

Required comparison set:

```text
milestone 005 processing-only baseline:
  partitioned 24/24 none payload values/s
  partitioned 24/24 counter-checksum payload values/s
  allocation per payload value or stream event where applicable

milestone 006 accepted baseline:
  Release synthetic rebalance same-run static/sampling/rebalance ratios
  single-file real-data static/sampling/rebalance callback payload values/s
  comparable parallel real-data archive stream versus rebalance-archive result
  cache-wide real-data static/sampling/rebalance callback payload values/s
  cache-wide real-data allocation per payload value

milestone 007 results:
  same synthetic workloads under default hardening options
  quarantine lifecycle workloads
  retention stress workloads
  single-file real-data hardening run
  comparable parallel real-data hardening run
  cache-wide real-data hardening run
```

Required metrics:

```text
payload values/s
stream events/s where meaningful
processing callback elapsed time
archive end-to-end elapsed time for archive inputs
allocation per payload value
allocation per stream event where meaningful
accepted moves
skipped decisions by reason
quarantine lifecycle counters
retained detail counts
dropped detail counts
validation profile
retention mode
failed migrations
validation status
deterministic checksum or comparable validation marker
```

Interpretation rules:

```text
same-run static comparison is the primary synthetic overhead signal
archive callback timing is the primary processing/rebalance signal
archive end-to-end timing is replay dominated and must be labeled that way
tiny lifecycle workloads must not be compared directly to large throughput workloads
allocation regressions must be attributed to replay, processing, control plane,
validation, telemetry, or CLI formatting where practical
```

Regression policy:

```text
do not close milestone 007 with an unexplained callback throughput regression
do not close milestone 007 with unbounded telemetry growth
do not close milestone 007 with higher allocation unless the source is
measured and justified
do not tune policy more aggressively just to improve benchmark appearance
do not compare archive end-to-end throughput against processing-only baselines
without the callback timing column
```

Expected final artifact:

```text
a closeout performance table that shows milestone 005, milestone 006, and
milestone 007 side by side for the comparable contours, with explicit notes for
non-comparable contours
```

This gate exists to make sure production hardening does not quietly trade away
the throughput and allocation shape established by milestones 005 and 006.

## Milestone 007 Completion Criteria

Milestone 007 completion criteria are satisfied when:

```text
[ ] hardening options and profiles are implemented and tested
[ ] bounded telemetry contracts are implemented and tested
[ ] telemetry recorder retains counters and bounded recent detail
[ ] quarantine lifecycle state and transitions are implemented and tested
[ ] lifecycle evaluator advances quarantine before planning
[ ] direct hot relief and cold evacuation honor effective classification
[ ] rebalance session exposes hardening telemetry and retention stats
[ ] validation profiles are implemented and tested
[ ] allocation attribution is reported by benchmark contours
[ ] avoidable control-plane allocation is reduced or explicitly justified
[ ] lifecycle synthetic workloads are implemented and tested
[ ] retention stress workloads are implemented and tested
[ ] synthetic and archive benchmark harnesses expose hardening fields
[ ] CLI exposes validation, retention, and quarantine options
[ ] policy defaults are audited against workloads and real-data contours
[ ] decision trace is written
[ ] closeout is written
[ ] handoff is updated
[ ] final comprehensive performance comparison is captured and interpreted
```

## Non-Goals

Milestone 007 does not implement:

```text
retained async worker queues
physical worker-local state transfer
multi-core shard execution runtime
timer-owned background rebalance thread
live ingestion
durable broker integration
partition splitting
source-level migration
source-universe repartitioning
complex radar algorithms
visualization
long-term storage format changes
```

Milestone 007 should also avoid broad policy aggression. If repeated real-data
runs justify small threshold or budget changes, those changes must be
documented and compared against the milestone 006 baseline.

## Risks And Watchpoints

### Telemetry Becomes A Hidden Log Store

Risk:

```text
keeping skipped decisions visible can accidentally become retaining every
decision forever
```

Mitigation:

```text
use aggregate counters plus bounded recent windows
track dropped-detail counts
stress-test long runs with retention limits
```

### Quarantine Retry Storms

Risk:

```text
automatic TTL retry can repeatedly reconsider the same unsafe hot partition
```

Mitigation:

```text
keep normal headroom, benefit, cooldown, residency, and budget gates active
support re-entry with fresh evidence
use synthetic retry-storm workloads
```

### Quarantine Never Clears

Risk:

```text
hot partitions can remain blocked after pressure cools or changes shape
```

Mitigation:

```text
advance lifecycle before planning
support sustained cooling clear
support material pressure-change retry
record lifecycle transition telemetry
```

### Validation Cost Hides In The Hot Path

Risk:

```text
diagnostic validation can distort production-shaped benchmark results
```

Mitigation:

```text
make validation profile explicit
report validation profile in every benchmark row
compare essential and benchmark profiles when needed
```

### Allocation Reduction Weakens Result Safety

Risk:

```text
pooling or reuse can expose mutable internal buffers through public results
```

Mitigation:

```text
reuse only private session scratch storage
publish immutable or defensively copied snapshots
test snapshot stability after later mutations
```

### Benchmark Misinterpretation

Risk:

```text
archive end-to-end numbers, tiny synthetic lifecycle workloads, and large
processing-only baselines can be compared incorrectly
```

Mitigation:

```text
keep callback timing separate from archive end-to-end timing
use same-run static ratios for tiny synthetic workloads
label non-comparable contours explicitly
finish with the comprehensive performance comparison gate
```

## Final Performance Comparison Requirement

The milestone must not close until the final comparison is captured and
interpreted.

Required final table:

```text
milestone 005:
  processing-only synthetic partitioned baselines

milestone 006:
  accepted synthetic rebalance baseline
  accepted single-file real-data baseline
  accepted comparable parallel real-data baseline
  accepted cache-wide real-data baseline

milestone 007:
  hardened synthetic rebalance baseline
  quarantine lifecycle workloads
  retention stress workloads
  hardened single-file real-data contour
  hardened comparable parallel real-data contour
  hardened cache-wide real-data contour
```

Required conclusion:

```text
state whether milestone 007 preserved callback throughput
state whether allocation improved, stayed flat, or regressed
state whether any regression is replay, processing, control-plane, validation,
telemetry, or CLI-formatting related
state whether telemetry retention stayed bounded
state whether quarantine lifecycle behaved deterministically
state whether policy remained conservative pressure relief
```

If the comparison shows a meaningful throughput or allocation regression, the
closeout must either fix it or record a specific measured reason before the
milestone is considered complete.
