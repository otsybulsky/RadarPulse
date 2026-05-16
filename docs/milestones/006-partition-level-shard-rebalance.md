# Milestone 006: Partition-Level Shard Rebalance Architecture

Status: concept draft.

RadarPulse milestone 006 starts from the closed milestone 005 processing-core
baseline and defines the architecture for cautious partition-level shard
rebalance.

This document is intentionally not an implementation plan. It records the
rebalance concept, ownership model, migration safety boundaries, pressure
signals, anti-churn rules, and expected result before any task breakdown is
written.

## Milestone Goal

Milestone 006 should define how RadarPulse can move processing partitions
between shards when one or more shards are under sustained pressure.

The output of the milestone is the architectural definition of:

```text
versioned partition-to-shard topology
rebalance pressure model
cautious rebalance controller
direct hot-partition relief
cold-partition evacuation from hot shards
migration safety protocol
state handoff and validation boundary
anti-churn policy
rebalance telemetry and skipped-reason diagnostics
```

The resulting design must preserve the milestone 004 stream contract and the
milestone 005 processing-core ownership model:

```text
RadarEventBatch remains the processing input.
SourceId -> PartitionId remains stable.
PartitionId -> ShardId may change only through an explicit topology update.
One source is mutated by one owner at a time.
Leased batch payloads are not retained past the synchronous processing boundary.
```

Milestone 006 is not about live ingestion, durable transport, retained async
payload queues, complex radar algorithms, source-level migration, or changing
`RadarEventBatch`. It is about making partition ownership movable without
turning rebalance into a source of instability.

## Expected Outcome

At the end of milestone 006, RadarPulse should have a clear architecture for a
cautious partition-level shard rebalance controller.

The expected result is:

```text
an accepted versioned topology model for PartitionId -> ShardId ownership
a pressure model based on sustained shard load, not one-batch imbalance
a rebalance policy that moves work only when projected relief is credible
direct hot-partition movement when it is safe and useful
cold-partition evacuation when the hot partition cannot move safely
hysteresis, cooldown, residency, and move-budget rules that prevent churn
a migration lifecycle that preserves source-local state and ordering
validation hooks for state handoff and topology changes
telemetry that explains both executed and skipped rebalance decisions
```

The core idea is:

```text
006 does not rebalance because load is uneven.
006 rebalances only when a shard is under sustained pressure,
there is a credible relief move,
and the move is unlikely to create rebalance churn.
```

The controller should behave as a pressure-relief mechanism. It should reduce
load on shards that are persistently too hot when a safe move exists. It should
not chase every short spike, keep moving the same hot partition between shards,
or spend migration budget on cosmetic balance.

## Starting Position

Milestone 005 produced the static processing baseline:

```text
RadarEventBatch
  -> static SourceId -> PartitionId -> ShardId topology
  -> dense source-local state
  -> source-local handler slots
  -> sequential correctness reference
  -> synchronous PartitionedBarrier processing
  -> partition and shard telemetry
  -> processing-output validation
  -> processing-only benchmark contour
```

The current topology is immutable after construction. The existing
`PartitionedBarrier` path routes a batch against one static topology and
returns only after all shard loops have completed. This gives milestone 006 a
simple and safe first rebalance boundary:

```text
one batch is processed against one topology snapshot
topology changes are applied between batch/barrier boundaries
leased payload storage is never retained to complete a migration
```

That boundary should remain the first architectural baseline even if later
milestones add real worker execution or retained async transport.

## Architectural Principles

Milestone 006 should follow these principles:

```text
rebalance is pressure relief, not mechanical equalization
SourceId -> PartitionId remains stable
PartitionId -> ShardId is versioned and externally observable
one batch observes one topology snapshot
migration is explicit, bounded, and validated
state ownership moves only at partition boundaries
hot partitions are not allowed to ping-pong between shards
cold partitions may move to reduce pressure when hot partitions cannot
skipped rebalance decisions are first-class telemetry
```

The most important separation is:

```text
pressure detection:
  identify sustained overload from telemetry windows

rebalance planning:
  choose a bounded move only if projected relief is meaningful

migration:
  change ownership safely without losing state or reordering source events
```

This separation keeps load policy, ownership metadata, and processing mutation
from collapsing into one hard-to-test path.

## Versioned Topology Model

Milestone 006 should introduce a versioned topology view for partition
ownership.

Conceptually:

```text
TopologyVersion 17:
  Partition 0 -> Shard 0
  Partition 1 -> Shard 0
  Partition 2 -> Shard 1

TopologyVersion 18:
  Partition 0 -> Shard 0
  Partition 1 -> Shard 2
  Partition 2 -> Shard 1
```

The stable mapping remains:

```text
SourceId -> PartitionId
```

The movable mapping is:

```text
PartitionId -> ShardId
```

A topology snapshot should be immutable once published. A processing call
captures one topology snapshot and uses it for the entire batch. New snapshots
become visible only at explicit boundaries.

Required topology properties:

```text
version is monotonic
partition count is stable for a processing session
shard count is stable for a processing session
every partition has exactly one owner shard in a topology snapshot
source ranges covered by partitions do not change during rebalance
topology assignment is externally observable for diagnostics
```

Milestone 006 should not require repartitioning the source universe. Splitting
or merging partitions can be a later milestone if one partition is intrinsically
too hot to be relieved by movement.

## Pressure Model

The rebalance controller should not use a single event count from one batch as
the decision signal. It should classify pressure over a recent window.

Candidate pressure inputs:

```text
per-shard event count
per-shard payload value count
per-shard route metrics
per-shard processing time when available
barrier elapsed contribution when available
future worker wait time or queue depth if async modes exist
```

The first pressure score can be deterministic and simple:

```text
ShardPressure =
  weighted event pressure
  + weighted payload pressure
  + weighted processing-time pressure
```

The architectural requirement is that the score is windowed. Good options are:

```text
rolling window over the last N completed batches
exponential moving average over shard pressure samples
minimum sample count before rebalance is allowed
```

The controller should classify shards by pressure band:

```text
Cold:
  enough headroom to receive work

Normal:
  no rebalance action needed

Warm:
  elevated load, but not a migration trigger

Hot:
  sustained pressure above the rebalance threshold

SuperHot:
  sustained pressure high enough to justify stronger relief attempts
```

The exact numeric thresholds belong in the implementation plan. The architecture
should require hysteresis:

```text
enter hot state at a higher threshold
exit hot state at a lower threshold
```

This prevents a shard from repeatedly crossing one threshold and causing a
rebalance storm.

## Rebalance Controller

The controller should be a policy component over telemetry and topology. It
should not mutate processing state directly.

Conceptual stages:

```text
collect telemetry samples
update pressure windows
classify shard pressure
identify hot or superhot shards
generate candidate moves
project post-move pressure
apply anti-churn gates
select at most a bounded number of moves
hand the accepted plan to the migration coordinator
emit decision telemetry
```

The controller should produce a plan, not perform the migration itself.

Candidate plan shape:

```text
rebalance decision id
current topology version
move kind
source shard id
target shard id
partition id
pressure before
projected pressure after
expected relief
policy gates passed
policy gates rejected
```

Only accepted plans should reach the migration coordinator. Rejected candidates
should still be visible as skipped decisions when they explain system behavior.

## Move Kinds

Milestone 006 should support two primary move concepts and one future concept.

### Direct Hot Relief

Direct hot relief moves a high-pressure partition away from a hot shard.

Conceptually:

```text
Shard A is hot.
Partition P contributes a meaningful share of Shard A pressure.
Shard B has enough headroom.
Moving P from A to B lowers the maximum projected shard pressure.
```

This is the preferred move when it is safe. It directly addresses the source of
pressure.

Required gates:

```text
source shard is hot or superhot
candidate partition is not in cooldown
candidate partition has satisfied minimum residency
target shard remains below the configured warm or hot threshold after the move
projected max shard pressure improves by at least the minimum benefit threshold
global and per-shard move budgets are not exhausted
```

Direct movement is invalid if it merely transfers the hot condition to another
shard.

### Cold Evacuation

Cold evacuation moves low-pressure partitions away from a hot shard when the
dominant hot partition cannot be moved safely.

Conceptually:

```text
Shard A is superhot.
Partition P is the dominant hot partition.
Moving P would make every target shard hot.
Partition C is cold but still contributes nonzero pressure on Shard A.
Moving C from A to a cold target lowers Shard A pressure without moving P.
```

This is a relief move, not a balancing move. It is useful when a shard is hot
because it owns one unavoidable hot partition plus other smaller partitions.
Removing the smaller partitions may reduce total pressure enough to keep the
shard operational.

Required gates:

```text
source shard is hot or superhot
direct hot relief is unavailable or unsafe
cold candidate has low recent pressure
target shard has clear headroom
target shard does not become warm or hot after the move
projected relief is meaningful
cold-move budget is not exhausted
candidate partition is not in cooldown
```

Cold evacuation is invalid when it produces only cosmetic balance:

```text
hot shard pressure does not drop meaningfully
target shard becomes pressured
the move consumes budget without improving the maximum pressure
```

### Room-Making

Room-making moves cold partitions between non-hot shards to create future
headroom for a later direct hot move.

This is more complex because it creates multi-step plans. It can be described
as a future-compatible concept, but it should not be the default milestone 006
baseline unless the implementation plan deliberately includes it.

Milestone 006 should prefer:

```text
direct hot relief first
cold evacuation from the hot shard second
room-making later or only as an explicitly bounded optional slice
```

## Anti-Churn Policy

The controller must avoid rebalance storms.

Storm examples:

```text
one hot partition moves from shard to shard without reducing pressure
two shards trade cold partitions because their relative load changes slightly
short-lived spikes trigger topology updates every evaluation window
the same source range repeatedly changes ownership before state has stabilized
```

Milestone 006 should define anti-churn rules as correctness-adjacent policy, not
as cosmetic tuning.

Required gates:

```text
sustained pressure window before any move is allowed
hysteresis between hot entry and hot exit thresholds
minimum partition residency before a partition may move again
partition cooldown after any move
source shard cooldown after initiating a move
target shard cooldown after receiving a moved partition
global move budget per evaluation window
per-shard move budget per evaluation window
minimum projected benefit threshold
target headroom requirement
```

The controller should also support backoff:

```text
if a move fails validation, increase cooldown
if a move produces insufficient measured relief, reduce willingness to move the
same partition again
if a partition remains dominant after movement, classify it as intrinsic hot
```

These rules are what make the mechanism cautious rather than mechanical.

## Hot Partition Handling

Some partitions may be intrinsically hot. If one partition is the dominant load
source, moving it may not solve overload; it may only move overload elsewhere.

Milestone 006 should classify hot partitions:

```text
MovableHot:
  high load, but a target shard can absorb it safely

IntrinsicHot:
  high load that would make any target shard hot

QuarantinedHot:
  recently moved or repeatedly ineffective to move
```

The controller should not repeatedly move an intrinsic hot partition. If no
safe direct target exists, cold evacuation is the safer relief strategy.

Partition splitting is intentionally out of scope for milestone 006. The
architecture should leave a clear diagnostic trail when splitting would be the
real solution:

```text
skipped: partition is intrinsic hot
skipped: no target shard can absorb candidate partition
suggested future action: split or subdivide partition
```

## Migration Lifecycle

Migration should be explicit and observable.

Candidate partition migration states:

```text
Active:
  partition is normally routed to its owner shard

PendingMigration:
  rebalance plan accepted, not yet applied

Draining:
  current barrier work for the old topology is completing

Migrating:
  ownership metadata and state handoff are being updated

ActiveOnNewShard:
  new topology version is published and routing uses the new owner

Rejected:
  plan failed a policy or validation gate before migration

Failed:
  migration started but did not complete successfully

RolledBack:
  previous topology remains active after a migration failure
```

For the first barrier-based architecture, migration should happen between
processing calls:

```text
process batch using topology version N
complete all work for topology version N
evaluate rebalance
if a plan is accepted, migrate partition ownership
publish topology version N+1
process next batch using topology version N+1
```

This rule preserves the leased batch lifetime boundary. No migration should
depend on retaining event payload references after the processing call returns.

## State Handoff Boundary

Milestone 005 stores source state in dense arrays indexed by `SourceId`. That
does not require immediate physical copying to model rebalance correctly.

Milestone 006 should define ownership handoff independently from storage
mechanics:

```text
partition owns a stable SourceId range
partition movement changes the owner shard for that range
state for all sources in that range remains intact
future shard-local stores may physically move state during handoff
current dense global stores may update ownership metadata first
```

The handoff contract should validate:

```text
partition id is valid in the current topology
source range belongs to the partition being moved
old shard owns the partition before migration
new shard owns the partition after migration
active source count is preserved
processed event count is preserved
processed payload value count is preserved
raw value checksum is preserved
source-local last timestamp values are preserved
handler slot values are preserved
processing checksum is preserved
```

The validation surface should make state loss or duplicate ownership visible
before richer worker execution is introduced.

## Routing Boundary

Routing must be topology-version aware.

Required routing properties:

```text
a route is built against one topology snapshot
route telemetry records the topology version
route validation can explain which shard owned each partition
topology changes do not affect a route already being processed
new routes use the latest published topology snapshot
```

This avoids a class of bugs where half of a batch is routed by an old topology
and the other half by a new topology.

If later async queues are introduced, work items must carry the topology version
or a lease over the topology snapshot they were routed against.

## Ordering Semantics

Milestone 006 must preserve the milestone 005 ordering rule:

```text
events for the same SourceId are applied in canonical batch order
```

Partition movement is valid only between safe processing boundaries. It must
not create a state where old events for a source are still being applied on the
previous shard while newer events are applied on the new shard.

The first barrier-based rule is:

```text
no partition ownership change while a batch using the old topology is active
```

Future retained async modes will need a stronger drain or epoch protocol:

```text
mark partition migrating
stop accepting new work for the old owner
drain old topology epoch
move ownership
replay or release buffered work in source order
publish new topology epoch
```

That future protocol should build on the versioned topology model defined here.

## Telemetry And Diagnostics

Rebalance telemetry should explain both action and restraint.

Required decision telemetry:

```text
decision id
decision timestamp or sequence
current topology version
result topology version if changed
move kind
source shard id
target shard id
partition id
pressure window size
source shard pressure before
target shard pressure before
projected source shard pressure after
projected target shard pressure after
expected relief
actual measured relief when available
policy gates passed
policy gates rejected
```

Required rebalance counters:

```text
topology version
rebalance evaluation count
accepted move count
rejected candidate count
skipped evaluation count
direct hot relief count
cold evacuation count
failed migration count
rollback count
partition cooldown count
intrinsic hot partition count
```

Skipped reasons should be explicit:

```text
skipped: no sustained pressure
skipped: no hot shard
skipped: no cold target shard
skipped: direct hot partition has no safe target
skipped: insufficient projected benefit
skipped: target would become warm
skipped: target would become hot
skipped: candidate partition in cooldown
skipped: candidate partition below minimum residency
skipped: source shard move budget exhausted
skipped: target shard move budget exhausted
skipped: global move budget exhausted
skipped: partition classified intrinsic hot
skipped: cold evacuation insufficient benefit
skipped: migration validation failed
```

Skipped decisions are important because the correct behavior may be to avoid a
move. Operators and tests should be able to distinguish "controller is idle
because nothing is wrong" from "controller detected pressure but rejected every
unsafe option."

## Validation Boundary

Milestone 006 should add deterministic validation hooks for rebalance.

Validation should prove:

```text
topology versions are monotonic
each partition has exactly one owner in each topology snapshot
SourceId -> PartitionId mapping is unchanged
accepted moves change only PartitionId -> ShardId ownership
no batch route uses mixed topology versions
state metrics are preserved across handoff
handler slot snapshots are preserved across handoff
source-local order validation still passes after migration
sequential reference results remain explainable
partitioned results remain explainable after topology changes
```

Validation should remain outside the hot path unless explicitly enabled. The
controller may use lightweight policy checks during normal operation, while
full state checksum validation can be a diagnostic or test-time operation.

## Benchmark Boundary

Milestone 006 benchmarks should measure rebalance overhead separately from
replay construction.

Good benchmark contours:

```text
static partitioned baseline with no rebalance
partitioned processing with telemetry sampling only
partitioned processing with evaluations but no accepted moves
partitioned processing with bounded accepted moves between batches
synthetic hot-shard workload with direct hot relief
synthetic intrinsic-hot workload with cold evacuation
synthetic oscillating workload that should not trigger churn
```

The benchmark should report:

```text
processing throughput
rebalance evaluation cost
accepted move count
skipped decision count
topology version count
state handoff validation cost when enabled
pressure before and after accepted moves
allocation cost per event and per rebalance evaluation
```

The benchmark must continue to exclude:

```text
archive file enumeration
BZip2 decompression
Archive Two scanning
identity normalization
dictionary registration
batch construction
CLI formatting inside measured loops
```

The performance question for milestone 006 is not whether rebalance makes a
synthetic workload faster in all cases. The first question is whether the
controller can detect sustained pressure, choose bounded useful moves, and keep
the steady-state overhead of evaluation and topology snapshots visible.

## Architectural Boundaries

Milestone 006 should keep these boundaries explicit:

```text
Input boundary:
  Rebalance consumes processing telemetry and topology state, not Archive Two
  records.

Topology boundary:
  SourceId -> PartitionId remains stable; only PartitionId -> ShardId changes.

Batch boundary:
  One batch uses one topology snapshot.

Lifetime boundary:
  Leased RadarEventBatch payload storage is not retained for migration.

State boundary:
  Source-local state remains dense and is handed off by partition ownership.

Policy boundary:
  The controller proposes moves; the migration coordinator applies validated
  ownership changes.

Anti-churn boundary:
  Sustained pressure, hysteresis, cooldown, residency, budget, and projected
  benefit gates are required before movement.

Telemetry boundary:
  Accepted and skipped rebalance decisions are both observable.
```

These boundaries allow RadarPulse to add load relief without making the
processing core nondeterministic or turning topology changes into hidden side
effects of routing.

## Non-Goals For This Document

This document does not define:

```text
implementation tasks
class or method names
exact numeric pressure thresholds
exact CLI commands
real worker-thread scheduling
retained async processing transport
live ingestion
durable broker integration
source-level migration
partition splitting or repartitioning
complex radar algorithms
visualization
changes to RadarEventBatch or RadarStreamEvent
```

Those belong in later design or plan documents once this architecture is
accepted.

## Baseline Architectural Position

RadarPulse should move from static partition ownership to cautious,
pressure-driven partition ownership:

```text
RadarEventBatch
  -> processing against topology snapshot N
  -> telemetry window updates pressure scores
  -> controller evaluates sustained hot shards
  -> direct hot relief if safe
  -> cold evacuation if hot partition movement is unsafe
  -> anti-churn gates reject unstable moves
  -> migration coordinator publishes topology snapshot N+1
  -> validation proves state and ordering survived the ownership change
```

Milestone 006 is successful when partition-level rebalance can be reasoned about
as a safe, bounded, observable ownership change. The controller should reduce
pressure when it has a credible move, decline movement when every option is
unsafe or low-value, and leave enough telemetry to prove why it acted or stayed
idle.
