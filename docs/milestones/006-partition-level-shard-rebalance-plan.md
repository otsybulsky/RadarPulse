# Milestone 006: Partition-Level Shard Rebalance Plan

Status: planned.

This plan implements the milestone 006 architecture defined in
`006-partition-level-shard-rebalance.md`.

The plan is intentionally scoped to cautious, synchronous, partition-level
rebalance over the milestone 005 `PartitionedBarrier` baseline. It should not
introduce retained async queues, live ingestion, durable transport, source-level
migration, partition splitting, or complex radar algorithms.

## Goal

Milestone 006 implements a safe partition-level shard rebalance foundation:

```text
RadarEventBatch
  -> processing against one topology snapshot
  -> telemetry window updates shard and partition pressure
  -> cautious rebalance controller evaluates sustained pressure
  -> direct hot relief when safe
  -> cold evacuation when direct hot relief is unsafe
  -> migration coordinator publishes a new topology version
  -> validation proves ownership, state, and ordering survived
```

The milestone must preserve the milestone 004 stream contract and the
milestone 005 processing-core lifetime boundary. Rebalance happens between
batch/barrier processing calls. A single `RadarEventBatch` must never be routed
against mixed topology versions.

## Starting Point

Milestone 005 is complete and provides:

```text
RadarProcessingCore
RadarProcessingCoreOptions
RadarProcessingExecutionMode.Sequential
RadarProcessingExecutionMode.PartitionedBarrier
RadarProcessingTopology
RadarProcessingPartitionAssignment
RadarProcessingBatchRouter
RadarProcessingBatchRoute
RadarProcessingTelemetry
RadarProcessingPartitionTelemetry
RadarProcessingShardTelemetry
RadarSourceProcessingStateStore
RadarSourceProcessingSnapshot
RadarSourceProcessingHandlerSnapshot
RadarProcessingOutputValidator
RadarProcessingSyntheticBenchmark
processing benchmark synthetic CLI command
```

Important existing constraints:

```text
RadarEventBatch is the processing input boundary.
RadarStreamEvent is a 64-byte unmanaged value type.
RadarEventBatch payload references are batch-local.
Leased batches are valid only during the synchronous publish callback.
PartitionedBarrier is synchronous and currently proves routing/barrier shape,
not real multi-core worker scaling.
SourceId -> PartitionId -> ShardId ownership is static in milestone 005.
Dense source-local state is indexed by SourceId.
```

Milestone 006 changes the ownership model from static to versioned:

```text
stable:
  SourceId -> PartitionId

movable:
  PartitionId -> ShardId
```

The first implementation should keep partition source ranges stable. Splitting
or merging partitions is deliberately out of scope.

## Target Implementation Shape

Most milestone 006 types should live under `RadarPulse.Domain.Processing`,
because rebalance is part of the processing-core ownership model and must be
testable without archive infrastructure.

Candidate layering:

```text
src/Domain/Processing
  topology snapshots, rebalance options, pressure model, move plans,
  migration lifecycle, validation results, decision telemetry

src/Infrastructure/Processing
  synthetic rebalance workloads and benchmark harnesses

src/Presentation
  optional synthetic rebalance smoke or benchmark command
```

The implementation should keep these responsibilities separate:

```text
Topology snapshot:
  immutable partition-to-shard ownership for one version

Topology manager:
  publishes validated topology snapshots

Pressure tracker:
  converts processing telemetry into windowed shard and partition pressure

Rebalance controller:
  proposes or rejects moves from topology and pressure data

Migration coordinator:
  applies an accepted move between processing barriers

Validation:
  proves topology, state, route, and migration invariants
```

The controller should not directly mutate source state, and the router should
not silently trigger rebalance.

## Implementation Slices

### 1. Versioned Topology Contracts

Introduce explicit versioned topology contracts while preserving the existing
contiguous `SourceId -> PartitionId` mapping.

Candidate types:

```text
RadarProcessingTopologyVersion
RadarProcessingTopologySnapshot
RadarProcessingPartitionOwner
RadarProcessingTopologyChange
RadarProcessingTopologyValidationResult
```

Required behavior:

```text
topology version starts from a deterministic initial value
topology version increases monotonically after accepted ownership changes
partition count and shard count remain stable for a processing session
SourceId -> PartitionId mapping remains stable
PartitionId -> ShardId ownership is stored in the topology snapshot
every partition has exactly one shard owner
every shard id is in range
topology snapshots are immutable after publication
```

The existing `RadarProcessingTopology` can either become the versioned snapshot
itself or delegate to a new immutable snapshot object. The implementation
should avoid duplicating source-range partition logic.

Expected tests:

```text
initial topology matches the milestone 005 static topology
topology version is exposed and stable
topology snapshots reject duplicate or missing partition owners
topology snapshots reject out-of-range shard ids
SourceId -> PartitionId mapping remains unchanged after a partition move
PartitionId -> ShardId changes only for the moved partition
old snapshots are unchanged after a new snapshot is published
```

### 2. Topology Manager And Publication Boundary

Add a small publication boundary for topology snapshots.

Candidate types:

```text
RadarProcessingTopologyManager
RadarProcessingTopologyMoveRequest
RadarProcessingTopologyMoveResult
```

Required behavior:

```text
expose the current immutable topology snapshot
accept a validated partition move request
publish a new topology snapshot with version + 1
reject no-op moves
reject moves from a shard that does not currently own the partition
reject moves to the same shard
reject moves against stale topology versions
return both previous and current versions for diagnostics
```

The manager is not a load policy. It only enforces topology consistency.

Expected tests:

```text
valid move publishes version N+1
stale move request is rejected
no-op move is rejected
wrong source owner is rejected
out-of-range partition or shard ids are rejected
previous topology remains inspectable after publication
```

### 3. Route Topology Version Integration

Make routing topology-version aware.

Candidate changes:

```text
RadarProcessingBatchRoute includes TopologyVersion
RadarProcessingTelemetry includes TopologyVersion
RadarProcessingPartitionTelemetry can expose owner version if useful
RadarProcessingCore captures one topology snapshot at Process start
```

Required behavior:

```text
one route is built against one topology snapshot
route and telemetry identify the topology version used
topology changes after route construction do not affect the active route
processing result can explain the topology version used for the batch
```

For milestone 006, topology updates should happen between calls to
`RadarProcessingCore.Process` or an equivalent rebalance-aware processing
operation.

Expected tests:

```text
route exposes the captured topology version
changing the manager after route creation does not change the route
telemetry topology version matches the route topology version
processing a batch cannot observe mixed topology versions
```

### 4. Pressure Sample Model

Introduce deterministic pressure samples built from routed batch telemetry.

Candidate types:

```text
RadarProcessingPressureSample
RadarProcessingShardPressureSample
RadarProcessingPartitionPressureSample
RadarProcessingPressureScore
RadarProcessingPressureBand
```

Required behavior:

```text
samples are derived from one processed batch or route telemetry snapshot
samples include topology version
shard pressure can include event count, payload value count, and optional time
partition pressure can include event count and payload value count
pressure score calculation is deterministic
pressure bands classify cold, normal, warm, hot, and superhot
empty samples are valid and produce no pressure
```

Milestone 006 can use simple default weights:

```text
event count weight
payload value count weight
processing time weight when available
```

The exact numeric defaults can be conservative. The important point is that
policy uses sustained windowed pressure, not one instantaneous event count.

Expected tests:

```text
empty telemetry produces zero pressure
shard pressure score increases with event count
shard pressure score increases with payload value count
partition pressure totals can be reconciled with shard pressure
pressure band classification respects configured thresholds
sample topology version matches the source telemetry
```

### 5. Pressure Window Tracker

Add a rolling or EMA pressure tracker used by the controller.

Candidate types:

```text
RadarProcessingPressureWindow
RadarProcessingPressureWindowOptions
RadarProcessingShardPressureState
RadarProcessingPartitionPressureState
```

Required behavior:

```text
track the last N samples or deterministic EMA state
require a minimum sample count before rebalance is allowed
maintain shard pressure bands with hysteresis
maintain recent partition pressure estimates
expose hot and superhot shards
expose cold target shard candidates
expose partition pressure by current owner shard
```

Hysteresis should be explicit:

```text
hot enter threshold
hot exit threshold
superhot enter threshold
superhot exit threshold
```

Expected tests:

```text
single spike does not enter hot state before minimum sample count
sustained pressure enters hot state
pressure below exit threshold leaves hot state
pressure between enter and exit thresholds preserves current band
partition pressure window follows the latest owner topology
cold target selection ignores hot or warm shards when configured
```

### 6. Rebalance Options And Anti-Churn State

Introduce policy options and state that prevent migration storms.

Candidate types:

```text
RadarProcessingRebalanceOptions
RadarProcessingRebalanceBudget
RadarProcessingPartitionResidency
RadarProcessingPartitionCooldown
RadarProcessingShardCooldown
RadarProcessingRebalancePolicyState
```

Required behavior:

```text
global move budget per evaluation window
per-source-shard move budget
per-target-shard receive budget
minimum partition residency before another move
partition cooldown after any move
source shard cooldown after initiating a move
target shard cooldown after receiving a move
minimum projected benefit threshold
target headroom threshold
cold evacuation budget separate from direct hot relief if useful
```

The policy state should be deterministic and testable without timers. Prefer a
logical evaluation sequence or batch sequence over wall-clock time for the first
implementation.

Expected tests:

```text
partition cannot move before minimum residency
partition cannot move during cooldown
source shard budget blocks repeated moves
target shard budget blocks repeated receives
global move budget caps accepted moves
policy state advances by evaluation sequence
failed migration can increase cooldown or block repeated attempts
```

### 7. Rebalance Decision And Skipped Reasons

Define the controller output before implementing move selection.

Candidate types:

```text
RadarProcessingRebalanceDecision
RadarProcessingRebalanceDecisionKind
RadarProcessingRebalanceMoveKind
RadarProcessingRebalanceSkippedReason
RadarProcessingRebalanceCandidate
RadarProcessingProjectedPressure
```

Decision kinds:

```text
NoAction:
  no pressure or no valid candidate

AcceptedMove:
  one partition move is ready for migration

RejectedCandidate:
  evaluated candidate failed one or more policy gates
```

Move kinds:

```text
DirectHotRelief
ColdEvacuation
RoomMakingReserved
```

Required skipped reasons:

```text
NoSustainedPressure
NoHotShard
NoColdTargetShard
DirectHotPartitionHasNoSafeTarget
InsufficientProjectedBenefit
TargetWouldBecomeWarm
TargetWouldBecomeHot
CandidatePartitionInCooldown
CandidatePartitionBelowMinimumResidency
SourceShardMoveBudgetExhausted
TargetShardMoveBudgetExhausted
GlobalMoveBudgetExhausted
PartitionClassifiedIntrinsicHot
ColdEvacuationInsufficientBenefit
MigrationValidationFailed
```

Expected tests:

```text
no pressure returns NoAction with NoSustainedPressure
hot shard with no target records skipped target reason
candidate failing multiple gates records all relevant reasons
accepted decision carries move kind, partition id, source shard, target shard
decision is deterministic for the same pressure and topology inputs
```

### 8. Direct Hot Relief Planner

Implement direct hot relief candidate selection.

Required behavior:

```text
identify hot or superhot source shards from the pressure window
find high-pressure partitions currently owned by the hot shard
sort candidates deterministically by projected relief and partition id
find cold target shards with enough headroom
project post-move source and target pressure
reject moves that make the target warm or hot
reject moves below minimum benefit threshold
apply anti-churn gates before accepting
select at most the configured move budget
```

Direct hot relief should be attempted before cold evacuation.

Expected tests:

```text
sustained hot shard produces a direct relief candidate
largest useful partition is selected deterministically
candidate is rejected when every target would become hot
candidate is rejected when projected relief is too small
candidate is rejected during cooldown
accepted direct move lowers projected max pressure
```

### 9. Intrinsic Hot Partition Classification

Classify partitions that should not be repeatedly moved.

Candidate types:

```text
RadarProcessingHotPartitionClassification
RadarProcessingHotPartitionState
```

Required behavior:

```text
classify a partition as movable hot when at least one target can absorb it
classify a partition as intrinsic hot when no target can absorb it safely
classify a partition as quarantined when recent movement was ineffective
record classification in decision telemetry
avoid repeated movement of intrinsic or quarantined hot partitions
```

Milestone 006 does not split intrinsic hot partitions. It should expose the
condition as telemetry and allow cold evacuation to reduce adjacent load.

Expected tests:

```text
dominant partition with no safe target becomes intrinsic hot
intrinsic hot partition is not selected for direct movement
recently moved ineffective partition can become quarantined
classification includes a skipped reason for diagnostics
classification does not prevent cold evacuation of other partitions
```

### 10. Cold Evacuation Planner

Implement cold evacuation from hot shards when direct hot relief is unavailable
or unsafe.

Required behavior:

```text
run only for hot or superhot source shards
run after direct hot relief fails or is rejected
select low-pressure partitions currently owned by the hot shard
reject zero-benefit cosmetic moves
find cold target shards with clear headroom
reject moves that make the target warm or hot
apply cold-move budget and anti-churn gates
select the smallest useful cold move or best projected relief deterministically
```

Cold evacuation is a pressure-relief fallback. It should not become general
load shuffling.

Expected tests:

```text
direct hot relief unsafe allows cold evacuation
cold partition can be moved off the hot shard
target shard remains below configured headroom threshold
cold evacuation is rejected when relief is too small
cold evacuation is rejected when target becomes warm or hot
cold move budget caps repeated cold evacuation
```

### 11. Migration Lifecycle And Coordinator

Implement the synchronous migration coordinator.

Candidate types:

```text
RadarProcessingPartitionMigrationState
RadarProcessingPartitionMigration
RadarProcessingMigrationCoordinator
RadarProcessingMigrationResult
RadarProcessingMigrationValidationResult
```

Required behavior:

```text
accept only an accepted rebalance decision
verify the decision references the current topology version
transition through explicit migration states
validate old shard ownership before move
publish new topology through the topology manager
record previous and new topology versions
roll back or keep previous topology if validation fails before publication
surface failure without partial ownership changes
```

For milestone 006, migration should occur only after the current
`PartitionedBarrier` work is complete.

Expected tests:

```text
accepted decision migrates partition and publishes topology N+1
stale decision is rejected
wrong old owner is rejected
failed validation does not publish a new topology
migration result records lifecycle state and versions
rollback path preserves previous topology
```

### 12. State Handoff Validation

Add validation around partition-owned source state before and after migration.

Candidate types:

```text
RadarProcessingPartitionStateSnapshot
RadarProcessingPartitionStateChecksum
RadarProcessingStateHandoffValidator
RadarProcessingStateHandoffValidationError
```

Required behavior:

```text
capture source-state summary for a partition source range
include active source count
include processed event count
include processed payload value count
include raw value checksum
include processing checksum
include last timestamp checksum or equivalent order-sensitive summary
include handler snapshot checksum when handlers are configured
compare before and after migration summaries
validate no source state is lost
validate no source state is duplicated
```

Because milestone 005 stores dense state globally, the first implementation may
validate ownership metadata movement without physically copying state arrays.
The validation contract should still be compatible with future shard-local
state stores.

Expected tests:

```text
state handoff validation passes when only owner shard changes
state handoff validation detects active source count mismatch
state handoff validation detects processed event count mismatch
state handoff validation detects raw checksum mismatch
handler slot checksum participates when handlers exist
empty partition state handoff is valid
```

### 13. Rebalance-Aware Processing Loop

Integrate rebalance evaluation into a synchronous processing flow.

Candidate shapes:

```text
RadarProcessingCore.Process(batch) remains unchanged and exposes telemetry.

RadarProcessingRebalanceSession wraps:
  core processing
  pressure tracking
  controller evaluation
  migration coordination
```

or:

```text
RadarProcessingCoreOptions carries optional rebalance components,
and Process returns optional rebalance decision telemetry.
```

The wrapper/session shape is preferable for milestone 006 if it keeps the core
processing boundary simpler and avoids turning every processing call into a
policy operation.

Required behavior:

```text
process one batch against the current topology snapshot
complete processing before evaluating migration
feed partitioned telemetry into pressure tracker
evaluate rebalance after processing
apply at most bounded accepted moves
next batch uses the latest topology snapshot
sequential mode either disables rebalance or reports a clear unsupported result
```

Expected tests:

```text
first batch uses initial topology
accepted rebalance after first batch affects second batch route
no batch uses mixed topology versions
sequential mode does not attempt partition rebalance
leased-equivalent batch lifetime guardrails remain valid
processing metrics remain stable across rebalance
```

### 14. Rebalance Validation Helpers

Add read-side validation for topology, routing, migration, and pressure
decisions.

Candidate types:

```text
RadarProcessingRebalanceValidator
RadarProcessingRebalanceValidationResult
RadarProcessingRebalanceValidationError
```

Required validation:

```text
topology versions are monotonic
each partition has exactly one owner
SourceId -> PartitionId mapping is unchanged
accepted moves change only PartitionId -> ShardId
route topology version matches telemetry topology version
route ownership matches the topology snapshot used
state handoff summary is preserved
source-local order validation still passes after rebalance
```

Expected tests:

```text
valid topology sequence passes
non-monotonic topology version is rejected
mixed route and telemetry topology versions are rejected
partition owner mismatch is rejected
invalid state handoff is reported with diagnostics
```

### 15. Synthetic Rebalance Workloads

Extend the synthetic processing harness or add a rebalance-specific harness.

Candidate workload shapes:

```text
Balanced:
  no shard should become hot

SustainedHotShard:
  one shard owns enough hot partitions to trigger direct relief

IntrinsicHotPartition:
  one partition is too hot for any target; cold evacuation is preferred

OscillatingSpike:
  short spikes should not trigger rebalance

CooldownStorm:
  repeated pressure attempts should be blocked by cooldown and budgets
```

Required behavior:

```text
workloads are deterministic
workloads use prebuilt RadarEventBatch values
workloads exclude archive replay construction
workloads can produce repeatable pressure windows
workloads can verify topology version changes over iterations
```

Expected tests:

```text
balanced workload produces no accepted moves
sustained hot workload produces direct hot relief
intrinsic hot workload rejects hot move and permits cold evacuation if useful
oscillating workload does not trigger churn
cooldown workload records skipped cooldown reasons
```

### 16. Rebalance Benchmarks

Add processing-only benchmark support for rebalance overhead and behavior.

Benchmark contours:

```text
static partitioned baseline with no rebalance
partitioned processing with pressure sampling only
partitioned processing with evaluations but no accepted moves
partitioned processing with bounded direct hot relief moves
partitioned processing with cold evacuation
oscillating workload with no accepted moves
```

Before milestone closeout, capture Release benchmark numbers and compare them
against the milestone 005 processing-only baseline. The comparison should use a
same-run static no-rebalance baseline when possible, then relate that result to
the latest recorded milestone 005 numbers:

```text
milestone 005 partitioned no-handler baseline
milestone 005 partitioned counter-checksum baseline
milestone 006 static no-rebalance same-run baseline
milestone 006 pressure-sampling overhead
milestone 006 evaluation-with-no-move overhead
milestone 006 accepted direct-hot-relief workload
milestone 006 accepted cold-evacuation workload
milestone 006 oscillating no-churn workload
```

Required benchmark output:

```text
execution mode
partition count
shard count
topology version count
workload kind
iteration count
warmup iteration count
processed batches/s
processed stream events/s
processed payload values/s
rebalance evaluations/s
accepted move count
skipped decision count
direct hot relief count
cold evacuation count
failed migration count
allocated bytes / stream event
allocated bytes / rebalance evaluation
pressure before and after accepted moves
validation checksum
throughput delta versus same-run static baseline
throughput delta versus milestone 005 recorded baseline
allocation delta versus same-run static baseline
allocation delta versus milestone 005 recorded baseline
```

Expected tests:

```text
warmup iterations are excluded from measured totals
static baseline performs zero rebalance evaluations if disabled
sampling-only mode performs evaluations with zero moves
direct relief benchmark records accepted direct moves
cold evacuation benchmark records accepted cold moves
benchmark totals are deterministic for the same workload
```

Closeout should include a compact benchmark table that makes the cost of
rebalance visible:

```text
workload
mode
handlers
topology versions
accepted moves
skipped decisions
payload values/s
stream events/s
allocated bytes / stream event
delta vs same-run static baseline
delta vs milestone 005 baseline
```

### 17. CLI Smoke Or Benchmark Command

Add a minimal manual command after the domain and benchmark harness are stable.

Candidate command:

```text
processing benchmark rebalance-synthetic
  [--workload balanced|hot-shard|intrinsic-hot|oscillating]
  [--sources n]
  [--batches n]
  [--events-per-batch n]
  [--payload-values n]
  [--partitions n]
  [--shards n]
  [--handlers none|counter-checksum]
  [--iterations n]
  [--warmup-iterations n]
```

Expected output:

```text
measured contour name
workload kind
execution mode
topology versions observed
accepted moves
skipped decisions by reason
direct hot relief count
cold evacuation count
failed migration count
throughput counters
allocation ratios
validation checksum
```

The command must keep replay construction out of the measured loop.

Expected tests:

```text
CLI command parses workload options
CLI command rejects incompatible sequential rebalance options
CLI command emits topology and rebalance counters
CLI command exits successfully for a small synthetic workload
```

### 18. Documentation, Decision Trace, And Handoff

Close the milestone with the same documentation pattern used by previous
milestones.

Expected documents:

```text
006-partition-level-shard-rebalance.md
006-partition-level-shard-rebalance-plan.md
006-partition-level-shard-rebalance-decision-trace.md
006-partition-level-shard-rebalance-closeout.md
docs/handoff.md update
```

The decision trace should record:

```text
why rebalance remained synchronous for milestone 006
why SourceId -> PartitionId stays stable
why only PartitionId -> ShardId moves
why direct hot relief is attempted before cold evacuation
why anti-churn rules are mandatory
why intrinsic hot partitions are not repeatedly moved
why partition splitting is deferred
```

The closeout should state:

```text
implemented versioned topology
implemented pressure windows
implemented direct hot relief
implemented cold evacuation
implemented anti-churn policy
implemented migration lifecycle
implemented state handoff validation
implemented rebalance telemetry
verified topology and processing validation
captured synthetic rebalance benchmark numbers
compared Release benchmark results with milestone 005 processing baselines
reported rebalance overhead against a same-run static no-rebalance baseline
remaining risks and next milestone input
```

## Milestone 006 Completion Criteria

Milestone 006 is complete when:

```text
[ ] versioned PartitionId -> ShardId topology is implemented and tested
[ ] topology snapshots are immutable and monotonic
[ ] routing and telemetry record the topology version used
[ ] one batch is processed against one topology snapshot
[ ] pressure samples are derived from partitioned telemetry
[ ] pressure windowing and hysteresis are implemented and tested
[ ] anti-churn policy supports cooldown, residency, budgets, and benefit gates
[ ] direct hot relief is implemented and tested
[ ] intrinsic hot partition classification is implemented and tested
[ ] cold evacuation fallback is implemented and tested
[ ] migration coordinator publishes topology N+1 between barriers
[ ] state handoff validation preserves source and handler state summaries
[ ] rebalance validation catches topology, route, and handoff errors
[ ] synthetic workloads cover balanced, hot, intrinsic-hot, and oscillating cases
[ ] processing-only rebalance benchmark reports overhead and decisions
[ ] Release rebalance benchmark numbers are captured before closeout
[ ] Release benchmark results are compared with milestone 005 processing baselines
[ ] Release benchmark results include same-run static no-rebalance comparison
[ ] CLI smoke or benchmark command can manually exercise rebalance
[ ] decision trace is written
[ ] closeout is written
[ ] handoff identifies the next milestone input
```

## Non-Goals

Milestone 006 does not implement:

```text
retained async processing queues
real production worker scheduler
live ingestion
durable broker integration
shared-memory transport
source-level migration
partition splitting or repartitioning
complex radar algorithms
visualization
changes to RadarEventBatch
changes to RadarStreamEvent
changes to stream dictionary or source-universe registration
file-backed end-to-end replay plus processing benchmark as the primary measure
```

The milestone may leave explicit seams for later async/worker execution, but
the rebalance correctness baseline stays synchronous.

## Risks And Watchpoints

### Rebalance Storms

Risk:

```text
the controller reacts to noise and moves partitions repeatedly
```

Mitigation:

```text
use sustained pressure windows
use hysteresis
enforce cooldown and minimum residency
cap move budgets
require projected benefit
test oscillating and cooldown workloads
```

### Hot Partition Ping-Pong

Risk:

```text
one intrinsic hot partition moves between shards and only transfers overload
```

Mitigation:

```text
project target pressure before moving
classify intrinsic hot partitions
quarantine ineffective moves
prefer cold evacuation when direct movement is unsafe
record skipped reasons
```

### Mixed Topology Routing

Risk:

```text
one batch is partially routed by old ownership and partially by new ownership
```

Mitigation:

```text
capture one immutable topology snapshot at route construction
record topology version on routes and telemetry
apply migration only between processing calls
add validation for route and telemetry version parity
```

### State Handoff Bugs

Risk:

```text
partition ownership changes but source-local state is lost, duplicated, or
misattributed
```

Mitigation:

```text
validate source range ownership
capture before/after partition state summaries
include handler slots in handoff checksums
reject or roll back failed handoff validation
```

### Hidden Async Lifetime Leak

Risk:

```text
rebalance work accidentally retains leased batch payload references
```

Mitigation:

```text
keep rebalance synchronous between barriers
make pressure samples copy only numeric telemetry
do not store RadarStreamEvent payload references in rebalance state
preserve leased batch guardrail tests
```

### Over-Complicated Policy

Risk:

```text
the first rebalance policy becomes too broad to test deterministically
```

Mitigation:

```text
start with direct hot relief
add cold evacuation as the only fallback
leave room-making and partition splitting out of the baseline
prefer logical evaluation counters over wall-clock policy state
```

### Benchmark Misinterpretation

Risk:

```text
rebalance benchmark numbers are read as production autoscaling or worker
scaling results
```

Mitigation:

```text
label benchmark contour explicitly
keep replay construction out of measured loops
report accepted and skipped decisions
state that PartitionedBarrier remains synchronous
compare overhead against static processing baseline
```
