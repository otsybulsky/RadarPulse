# Milestone 008: Retained Async Shard Transport Architecture

Status: draft.

RadarPulse milestone 008 starts from the closed milestone 007 synchronous
rebalance control plane and defines the architecture for the first retained
async shard worker transport.

This document is intentionally not an implementation plan. It records the
transport concept, payload lifetime boundary, worker lifecycle, topology and
rebalance coordination, state ownership model, telemetry, validation, benchmark
scope, and expected result before any task breakdown is written.

Milestone 008 should introduce retained workers and async shard scheduling
without introducing retained `RadarEventBatch` payload storage. The synchronous
`PartitionedBarrier` path remains the reference correctness boundary and the
comparison oracle for the new transport.

## Milestone Goal

Milestone 008 should make RadarPulse processing execution worker-shaped while
preserving the batch-safe correctness model proven by milestones 005, 006, and
007.

The output of the milestone is the architectural definition of:

```text
retained shard workers
bounded in-process shard work queues
batch-scoped async dispatch and completion barrier
worker lifecycle start/drain/stop/dispose semantics
exception, cancellation, and failed-work propagation
worker timing and queue telemetry
async execution validation against the synchronous reference path
rebalance integration over the async execution boundary
benchmark contours that separate replay, dispatch, execution, and control-plane cost
```

The resulting design must preserve these closed contracts:

```text
RadarEventBatch remains the processing input.
Leased payload storage is valid only during the synchronous publish callback.
SourceId -> PartitionId remains stable.
PartitionId -> ShardId changes only through versioned topology publication.
One batch is processed against one topology snapshot.
Accepted topology changes are published only between batch boundaries.
Skipped rebalance decisions remain explainable bounded telemetry.
The synchronous processing path remains available as a correctness reference.
```

The key milestone decision is:

```text
008 introduces retained workers.
008 does not introduce retained leased payloads.
```

Any work item that references a leased `RadarEventBatch` must complete before
the publish callback returns. A future milestone may define an owned snapshot
or retained payload protocol, but that protocol should not be smuggled into
the first async transport milestone.

Implementation target:

```text
first implementation:
  conservative one-in-flight borrowed batch per worker group

architecture posture:
  keep API terms clear enough that a future owned snapshot or wider scheduler
  can be added without redefining the worker runtime

hard boundary:
  retained workers are allowed
  retained borrowed RadarEventBatch payload is not allowed
```

This means milestone 008 should start with a batch-scoped dispatch barrier
rather than a generic durable scheduler. The broader scheduler problem becomes
tractable later only if the first borrowed-batch runtime proves its lifetime,
topology, failure, and validation rules.

## Expected Outcome

At the end of milestone 008, RadarPulse should have a clear architecture for
running processing work through retained shard workers while keeping the same
observable behavior as the synchronous processing boundary.

The expected result is:

```text
shard workers can be started once and reused across many batch evaluations
batch work can be dispatched asynchronously to shard-owned queues
the caller can wait for a deterministic completion barrier before callback exit
worker failures and cancellation propagate to the batch result
topology snapshots are captured before dispatch and remain stable during the batch
rebalance planning still happens only after batch work completes
accepted migrations still publish topology N+1 only between batches
worker telemetry exposes queue wait, execution time, barrier wait, and failures
validation can compare async output against the synchronous reference path
benchmarks can say whether async transport improves, preserves, or regresses the 005-007 contours
```

The core idea is:

```text
007 made synchronous rebalance production-shaped.
008 makes processing execution worker-shaped.
It should not broaden payload lifetime or topology semantics while doing so.
```

Milestone 008 is successful when the new runtime shape can be reasoned about
as an execution transport over the existing processing and rebalance contracts,
not as a new data model.

## Starting Position

Milestone 007 closed this reference path:

```text
leased RadarEventBatch callback
  -> synchronous processing against one topology snapshot
  -> bounded rebalance telemetry counters plus capped detail
  -> quarantine lifecycle before planning
  -> cautious direct hot relief or cold evacuation
  -> validation profile selected explicitly
  -> accepted topology change only between batches
  -> archive callback throughput and allocation reported separately from replay
```

The milestone 007 closeout accepted the following important signals:

```text
correctness:
  synthetic and archive rows validated successfully with zero failed migrations

bounded diagnostics:
  rebalance telemetry retains aggregate counters and bounded recent detail

production-shaped callback cost:
  cache-wide no-skew rebalance callback throughput remained above the accepted
  milestone 005 and 006 baselines

explicit validation:
  validation profiles make diagnostic cost visible

explicit allocation:
  archive benchmark output separates processing callback allocation from
  replay and batch construction allocation
```

Milestone 007 intentionally deferred:

```text
retained async worker queues
physical worker-local state transfer
multi-core shard execution runtime and scheduling policy
source-level migration
partition splitting or repartitioning
durable production configuration
live ingestion and durable broker integration
owned retained payload snapshots
```

Those deferrals are the input to milestone 008.

## Architectural Principles

Milestone 008 should follow these principles:

```text
add async execution without changing the stream contract
keep the synchronous path as the correctness oracle
capture topology once before dispatch
complete all leased-payload work before callback exit
make worker lifecycle explicit and disposable
prefer bounded queues and deterministic failure propagation
keep rebalance publication after worker completion
measure scheduler cost separately from processing cost where practical
reuse milestone 007 telemetry and validation surfaces instead of replacing them
avoid broad policy tuning while transport correctness is being proven
```

The most important separation is:

```text
data lifetime:
  leased RadarEventBatch payload exists only inside the synchronous callback

execution lifetime:
  worker threads and queues may live across callbacks

work item lifetime:
  work items that reference leased payload must be submitted, executed, and
  completed inside the callback boundary

control-plane lifetime:
  topology, pressure windows, rebalance policy state, telemetry counters, and
  validation profiles survive across callbacks
```

Milestone 008 should not blur these lifetimes. Retained workers are allowed;
retained borrowed payload is not.

## Production Value

Milestone 008 should produce practical value even before live ingestion or
durable transport exists.

The main benefits are:

```text
runtime shape:
  the processing core gains real worker lifecycle, queue, dispatch, and drain
  semantics instead of only a synchronous barrier loop

multi-core readiness:
  shard work can be scheduled through retained workers, making actual
  execution parallelism measurable rather than implied by partition topology

rebalance realism:
  topology snapshotting, worker completion, and migration publication are
  proven under async scheduling rather than only direct synchronous calls

payload safety:
  the milestone makes the retained-worker versus retained-payload boundary
  explicit before live ingestion or broker integration depends on it

observability:
  queue wait, worker busy time, dispatch latency, completion latency, and
  failed work become first-class telemetry

future scheduler foundation:
  later milestones can add timer-driven scheduling, owned snapshots,
  backpressure, worker-local state transfer, or durable input without
  redesigning the local worker runtime from scratch

performance truth:
  benchmarks can show whether the scheduler helps, where it costs, and whether
  the bottleneck has moved to replay, routing, queues, validation, or state access
```

This is a runtime-enabling milestone. Throughput improvement is valuable, but
the primary win is establishing an execution substrate that preserves the
already-proven invariants.

## Async Transport Definition

For this milestone, "retained async shard transport" means:

```text
workers are created once and reused
each worker owns a deterministic execution lane
work is submitted to bounded in-process queues
work items identify batch scope, topology version, shard, and partition range
workers process submitted work asynchronously from the caller thread
the caller waits for all submitted work before leaving the batch callback
completion produces deterministic processing telemetry and result surfaces
```

It does not mean:

```text
work can outlive the leased batch callback
payload spans can be retained by queues after callback exit
topology can change while submitted batch work is in flight
rebalance can publish during worker execution
worker state can migrate physically without an explicit protocol
durable queues or broker offsets are introduced
live ingestion owns worker scheduling
```

This distinction keeps milestone 008 small enough to validate. It introduces
the runtime shape without taking on the data ownership problem that belongs to
owned snapshots or durable ingestion.

## Worker Boundary

The worker boundary should be explicit and testable without archive
infrastructure.

Conceptually, the transport should provide:

```text
worker group:
  owns worker lifecycle and worker count

worker mailbox:
  receives shard-scoped work items through a bounded queue or equivalent
  in-process scheduling primitive

work item:
  describes batch sequence, topology version, shard id, assigned partition ids,
  and the processing input view needed for that batch

completion:
  records success, cancellation, exception, processing metrics, and worker
  timing for the submitted work

barrier:
  waits until every work item for one batch scope is complete or failed
```

The public processing core should not expose raw worker queues as mutable
state. Callers should submit through a transport boundary that can enforce
batch scope, topology version, queue limits, and disposal checks.

### Worker Lifecycle

The lifecycle should define:

```text
start:
  create worker resources and make mailboxes available

dispatch:
  submit work for one captured topology snapshot

drain batch:
  wait for all work in the current batch scope

stop accepting:
  reject new work while allowing already accepted work to finish if configured

cancel:
  request cancellation of pending or running work

dispose:
  release worker resources and make later dispatch invalid
```

Lifecycle state should be deterministic and visible in tests. Dispatch after
dispose, dispatch before start, and duplicate completion must be invalid.

### Queue Bounds

Queues should be bounded by design. A bounded queue can be implemented through
a fixed mailbox capacity, a single in-flight batch rule, or another explicit
backpressure mechanism.

The architecture requirement is:

```text
worker queues must not become unbounded retention stores for batch payloads
```

For the first milestone, it is acceptable to require at most one in-flight
leased batch per worker group. That rule is simple, deterministic, and matches
the callback lifetime constraint.

## Payload Lifetime Boundary

Payload lifetime is the highest-risk design boundary in milestone 008.

Allowed to retain across callbacks:

```text
worker threads and scheduler state
mailbox objects
topology manager
rebalance policy state
pressure windows
quarantine lifecycle state
bounded telemetry counters and recent detail
worker timing aggregates
dense source-local processing state owned by the processing core
```

Allowed only inside the callback and current batch barrier:

```text
RadarEventBatch references
payload spans derived from RadarEventBatch
route buffers that point into batch data
work items containing borrowed batch input
per-batch worker completions that depend on borrowed payload
```

Not allowed in milestone 008:

```text
retaining leased RadarEventBatch payload after callback exit
retaining caller-owned mutable arrays without explicit ownership transfer
background workers reading payload after the caller has released the batch
async fire-and-forget processing over leased archive replay batches
```

If future work needs processing to outlive the callback, it should define one
of these explicitly:

```text
owned RadarEventBatch snapshot
payload copy protocol
reference-counted retained payload lease
durable event storage and replay cursor
```

Those are not part of milestone 008.

## Topology And Rebalance Coordination

Milestone 008 must preserve the topology semantics established in milestones
006 and 007.

The async processing flow should be:

```text
capture topology snapshot
build route for one RadarEventBatch against that snapshot
dispatch shard work according to the captured snapshot
wait for all shard work to complete
merge or aggregate processing telemetry
derive pressure sample and advance rebalance control plane
publish accepted topology migration only after worker completion
let the next batch capture the latest topology version
```

The forbidden flow is:

```text
capture topology snapshot
dispatch shard work
publish topology change while workers still process that batch
let part of the batch observe topology N and another part observe topology N+1
```

The invariant remains:

```text
no batch observes mixed topology
```

Async dispatch may change which thread processes shard work. It must not
change ownership semantics or publication timing.

### Rebalance Session Integration

The milestone should define an async-capable processing session boundary that
can compose with the existing rebalance session.

The expected composition is:

```text
async processing transport
  -> partitioned telemetry with topology version
  -> pressure sample/window
  -> quarantine lifecycle
  -> direct hot relief or cold evacuation
  -> migration validation
  -> topology publication between batches
  -> bounded telemetry summary
```

The rebalance controller should not need to know whether the batch was
processed through the synchronous barrier or async worker transport, except
through explicit execution telemetry.

## State Ownership

Milestone 005 established dense source-local state. Milestone 006 and 007
proved partition ownership movement and state handoff at topology boundaries.
Milestone 008 should preserve that model before adding physical worker-local
state transfer.

The first async transport should target this ownership model:

```text
source-local state remains indexed by SourceId
partition ownership defines which shard may update which source range
worker executes only shard-owned partition work for the captured topology
state handoff validation remains a topology-boundary concern
```

The milestone should not require:

```text
physical memory movement between worker-local stores
source-level migration
partition splitting
NUMA-local state placement
durable state checkpoints
```

Physical worker-local state transfer may become useful once retained workers
are proven. It is better treated as a later milestone because it has a
different risk profile: ownership transfer, memory locality, handoff latency,
and state snapshot lifetime.

## Scheduling Model

The first scheduling model should be deliberately conservative.

Recommended baseline:

```text
one worker lane per shard or configured worker slot
one submitted batch scope at a time
bounded work item count per batch
caller-owned wait for completion before callback exit
deterministic aggregation order independent of worker completion order
```

The scheduler should support future extension points:

```text
different worker counts than shard counts
work stealing or shard multiplexing
timer-driven evaluation
backpressure from live ingestion
owned retained batch snapshots
```

Those extension points should not weaken the first milestone's deterministic
batch boundary.

### Ordering

Worker completion order must not become result order if that would change
observable behavior.

The transport should aggregate results by stable topology order:

```text
shard id
partition id
source range
event position where relevant
```

This matches the earlier archive replay lesson: parallel work is allowed only
behind an ordered merge or explicit ordering contract.

## Performance Guardrails

Milestone 008 must not assume async transport is automatically faster than the
synchronous barrier. The milestone is allowed to discover overhead. It is not
allowed to hide that overhead or mislabel it as replay cost.

The main performance risks are:

```text
per-batch task creation and scheduler churn
too many fine-grained work items
heavy barrier synchronization
shared counter contention between workers
route or payload copying in the baseline async path
queue allocations on every batch
ThreadPool jitter from ad hoc Task.Run dispatch
tiny synthetic workloads being mistaken for production-shaped throughput
```

The architecture guardrails are:

```text
use retained workers, not per-batch Task.Run
prefer one work item per shard or coarse partition group
keep queues bounded and reusable where practical
do not copy RadarEventBatch payload for the baseline borrowed-batch path
keep per-worker metrics local and aggregate after completion
avoid shared hot counters on the worker execution path
measure dispatch, queue wait, execution, aggregation, and barrier time separately
keep the synchronous path available when async is not the fastest mode
```

The benchmark guardrails are:

```text
same-run synchronous versus async comparison is required
archive callback timing must stay separate from archive end-to-end timing
tiny synthetic contours must be labeled as behavioral when they are too small
callback allocation must be reported separately from replay allocation
async closeout must state whether throughput improved, stayed flat, or regressed
any regression must be attributed to dispatch, queueing, barrier, aggregation,
validation, replay, or unknown measured debt
```

This protects the milestone from a common mistake: building an async-shaped
runtime and then treating scheduling overhead as invisible. If the synchronous
path remains faster for some contours, that is useful information and the
execution mode should remain selectable.

## Failure And Cancellation

Async transport makes failure semantics more important than the synchronous
barrier path.

The architecture should define:

```text
work item exception capture
first failure versus all failure collection
batch cancellation propagation
worker cancellation request behavior
queue close behavior
whether pending work is skipped or completed on cancellation
how failed batch results affect rebalance planning
how failed dispatch affects topology publication
```

Recommended baseline:

```text
if any worker fails the batch, rebalance planning is skipped for that batch
failed batch does not publish topology changes
all worker failures are reported through bounded diagnostics
worker group remains reusable only if the failure policy says it is healthy
```

The control plane should not publish a migration from partial or failed
processing telemetry.

Cancellation should be deterministic in tests:

```text
canceled before dispatch
canceled while queued
canceled while running
canceled during barrier wait
```

Each case should have an explicit result shape.

## Slow And Stalled Workers

Milestone 008 intentionally waits inside the provider callback for borrowed
batch work to complete. That wait is a backpressure boundary, not an accident.

If one worker is slow, the expected behavior is:

```text
provider callback is still active
completion barrier waits
replay/provider does not advance to the next borrowed batch
worker and barrier telemetry show where time was spent
borrowed payload remains valid because callback has not returned
```

This can reduce throughput, but it preserves the payload lifetime contract and
prevents unbounded borrowed-batch accumulation.

If one worker fails, the expected behavior is:

```text
batch result is failed
rebalance planning is skipped for that batch
topology migration is not published
failure is recorded through bounded worker diagnostics
worker group health is evaluated before accepting later batches
```

If one worker appears stalled or hung, timeout is useful as detection, not as a
safe payload-release mechanism:

```text
timeout may mark the batch and worker group unhealthy
timeout may request cooperative cancellation
timeout may fail the batch and suppress rebalance publication
timeout must not allow callback return while a worker may still read borrowed payload
```

The milestone should not implement this unsafe behavior:

```text
worker is still running over borrowed RadarEventBatch
timeout fires
callback returns anyway
replay releases or reuses payload storage
worker continues reading invalid borrowed data
```

Recommended first-milestone worker assumptions:

```text
workers perform bounded CPU work only
workers do not perform IO
workers do not call external services
work item size is coarse but finite
cooperative cancellation is checked at safe processing points
batch timeout is a health signal and benchmark diagnostic
non-cooperative hang makes the worker group unhealthy
safe recovery may require disposing the worker group or failing fast
```

This keeps the model honest:

```text
borrowed batch model:
  provider waits for workers

owned snapshot model:
  provider may hand off owned data and continue later

durable broker model:
  provider commits or advances according to a separate durable processing policy
```

Milestone 008 implements the borrowed batch model. Owned snapshots and durable
provider decoupling are future milestones.

## Telemetry And Observability

Milestone 007 introduced bounded rebalance telemetry. Milestone 008 should add
bounded worker transport telemetry without creating an unbounded execution log.

Required worker telemetry:

```text
batch sequence
topology version
worker id
shard id
submitted work item count
completed work item count
failed work item count
canceled work item count
queue depth or pending count
dispatch latency
queue wait time
worker execution time
completion barrier wait time
aggregation time where measurable
```

Aggregate counters should be retained for the session, while recent per-batch
detail should obey bounded retention rules.

Useful summary layers:

```text
hot counters:
  total dispatched batches, completed batches, failed batches, canceled
  batches, work items, worker failures

recent detail:
  capped recent worker batch summaries and failure samples

benchmark detail:
  explicit per-mode timing and allocation contours used for closeout
```

Worker telemetry should use numeric ids and stable enum/code values. CLI
formatting should remain a presentation concern.

## Validation Profiles

Milestone 008 should reuse milestone 007 validation profile discipline.

Recommended profile behavior:

```text
off:
  construction guardrails and worker lifecycle checks only

essential:
  validate batch completion, failed work propagation, topology version
  consistency, and no migration on failed processing

diagnostic:
  essential checks plus route, telemetry, worker assignment, aggregation, and
  state ownership diagnostics

benchmark:
  diagnostic checks plus deterministic checksums and sync-versus-async
  comparison markers
```

The important new validation question is:

```text
does async transport produce the same deterministic processing result as the
synchronous reference path for the same batch, topology, handlers, and state?
```

Validation should cover:

```text
every source is processed exactly once
no source outside an owned partition is updated by a worker
all work items use the captured topology version
aggregation is deterministic independent of completion order
failed or canceled batches do not run rebalance publication
leased payload is not retained after completion
```

The milestone should avoid making the async path correct only under benchmark
validation. Essential guardrails must protect the runtime boundary even when
diagnostic comparison is disabled.

## Benchmark Boundary

Milestone 008 benchmarks should measure async transport effects without
confusing replay construction with processing execution.

The benchmark matrix should include:

```text
processing-only synthetic:
  sequential, synchronous partitioned, and async worker transport

synthetic rebalance:
  static, sampling, and rebalance modes through async transport

single-file archive:
  callback timing separated from end-to-end archive replay

cache-wide archive:
  callback timing, end-to-end timing, allocation, and worker telemetry

stress contours:
  worker queue pressure, failure/cancellation smoke, skewed active rebalance
  where useful
```

Required reported metrics:

```text
payload values/s
stream events/s where meaningful
processing callback elapsed time
archive end-to-end elapsed time for archive inputs
dispatch time
queue wait time
worker execution time
completion barrier wait time
allocation per payload value
accepted moves
skipped decisions by reason
failed work items
failed migrations
validation profile
retention mode
deterministic checksum or validation marker
```

Interpretation rules:

```text
same-run synchronous comparison is the primary transport overhead signal
archive callback timing is the primary processing signal
archive end-to-end timing remains replay dominated
worker telemetry explains scheduler cost but does not replace correctness validation
throughput improvement is valuable only if deterministic results and lifetime rules hold
```

The closeout should compare milestone 008 against the accepted milestone 005,
006, and 007 baselines. It should state whether async transport improved,
preserved, or regressed callback throughput and allocation, and explain the
source of any regression.

## CLI And Diagnostics

The CLI should expose async transport as a benchmark and smoke surface, not as
the owner of domain scheduling policy.

Candidate CLI surface:

```text
processing benchmark synthetic --mode async-partitioned
processing benchmark rebalance-synthetic --execution async
processing benchmark rebalance-archive --execution async
```

Possible options:

```text
--execution synchronous|async
--workers n
--queue-capacity n
--worker-affinity none|shard
--validation-profile off|essential|diagnostic|benchmark
--retention-mode counters|recent|diagnostic
```

Required output additions:

```text
execution mode
worker count
queue capacity
dispatched work items
completed work items
failed work items
canceled work items
dispatch latency
queue wait time
worker execution time
completion barrier wait time
callback allocation contour
```

The CLI should not print unbounded worker event history. Detailed worker
samples should be capped in the same spirit as milestone 007 rebalance
telemetry.

## Architectural Boundaries

Milestone 008 should keep these boundaries explicit:

```text
stream boundary:
  RadarEventBatch remains unchanged

payload lifetime boundary:
  leased payload is not retained beyond callback completion

execution boundary:
  retained workers execute batch-scoped work and report completion

topology boundary:
  one captured topology snapshot per batch

rebalance boundary:
  rebalance evaluates and publishes only after async work completes

state boundary:
  dense source-local state remains the processing state model; physical
  worker-local state transfer is deferred

telemetry boundary:
  worker telemetry is bounded and numeric

validation boundary:
  async results are comparable to the synchronous reference path

benchmark boundary:
  replay, dispatch, execution, barrier, validation, and control-plane costs are
  labeled separately where practical
```

These boundaries are the reason milestone 008 should precede live ingestion.
Once local async execution is deterministic and observable, a later milestone
can connect a live or durable input source without also inventing the worker
runtime.

## Non-Goals For This Document

Milestone 008 does not define:

```text
retained RadarEventBatch payload snapshots
durable broker integration
live ingestion
timer-owned production scheduler
physical worker-local state transfer
source-level migration
partition splitting or repartitioning
complex radar algorithms
visualization
long-term storage format changes
distributed workers
cross-process transport
```

It also should not tune rebalance policy more aggressively merely because
execution became asynchronous. Policy changes should remain measured and
explicit.

## Risks And Watchpoints

### Retained Workers Accidentally Retain Payload

Risk:

```text
worker queues outlive the callback and keep references to leased payload data
```

Mitigation:

```text
allow only one in-flight leased batch scope
require completion before callback exit
make work item lifetime explicit
validate no pending work remains after the barrier
defer owned snapshots to a separate milestone
```

### Async Completion Changes Determinism

Risk:

```text
results become dependent on worker completion order
```

Mitigation:

```text
aggregate by stable topology order
compare async results with synchronous reference results
stress completion order in tests
```

### Rebalance Publishes During In-Flight Work

Risk:

```text
topology version N+1 is published while workers still process topology N
```

Mitigation:

```text
rebalance planning starts only after worker completion
migration coordinator validates current topology version before publication
failed or partial batches skip publication
```

### Queueing Cost Hides Throughput Regressions

Risk:

```text
async transport adds queue, synchronization, and barrier overhead without clear
benefit
```

Mitigation:

```text
measure dispatch, queue wait, execution, and barrier timing separately
compare same-run synchronous and async contours
preserve the synchronous path as an available execution mode
```

### Failure Semantics Become Ambiguous

Risk:

```text
worker exceptions or cancellation leave partial telemetry that rebalance treats
as valid
```

Mitigation:

```text
failed batch result shape is explicit
failed batches do not publish topology changes
worker failures are retained through bounded diagnostic telemetry
```

### Worker-Local State Is Added Too Early

Risk:

```text
physical state transfer becomes entangled with first transport correctness
```

Mitigation:

```text
keep dense source-local state model for milestone 008
validate ownership by topology and partition range
defer physical worker-local transfer until the transport is proven
```

## Baseline Architectural Position

The baseline position for milestone 008 is:

```text
RadarPulse already has a bounded, validated synchronous rebalance controller.
The next architectural risk is whether processing can run through retained
workers without weakening batch lifetime, topology, validation, or telemetry
contracts.
```

Milestone 008 is successful when the async worker path can be reasoned about as:

```text
batch-safe:
  borrowed payload work completes before callback exit

deterministic:
  one topology snapshot per batch and stable aggregation order

observable:
  worker scheduling, queueing, execution, barrier, failure, and cancellation
  are visible through bounded telemetry

comparable:
  async results can be validated against the synchronous reference path

rebalance-compatible:
  topology migration still publishes only between completed batches

measured:
  benchmark output explains whether scheduler cost is worth the runtime shape
```

The next milestone after 008 can then make an informed choice: introduce owned
retained payload snapshots for work that outlives callbacks, add physical
worker-local state transfer, connect live ingestion, or address partition
splitting for intrinsically hot partitions.
