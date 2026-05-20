# Milestone 011: Queued-Owned Default Readiness Architecture

Status: draft.

RadarPulse milestone 011 starts from the closed milestone 010 optimized
owned-provider overlap contour and defines the architecture for deciding
whether `queued-owned + pooled-copy + producer-consumer` is credible as a
future default provider candidate.

This document is intentionally not an implementation plan. It records the
default-readiness concept, evidence model, retained-memory pressure boundary,
configuration posture, validation posture, benchmark scope, and expected result
before any task breakdown is written.

Milestone 010 proved that the owned-provider path can be correct, far cheaper
than snapshot copy, resource-clean, and useful under cache-level
producer/consumer overlap. It did not make `queued-owned` the default. The
natural contour still reported queue depth `1`, `HasQueuedAheadOverlap = no`,
and incomplete visibility into retained resources held by the active consumer
after dequeue.

The core decision is:

```text
010 proved the optimized queued-owned contour is worth keeping.
011 proves whether it is ready to become a default candidate.
```

Milestone 011 should not change the provider default by accident. It should
produce the telemetry, repeated Release evidence, and configuration guardrails
needed for a later explicit default decision.

## Milestone Goal

Milestone 011 should turn the optimized queued-owned path from a successful
opt-in benchmark contour into a production-readable default candidate, while
keeping `blocking-borrowed` as the default and same-run oracle.

The output of the milestone is the architectural definition of:

```text
default-candidate provider contour and configuration envelope
in-flight retained-resource pressure telemetry after dequeue
combined pending-plus-active retained memory accounting
repeatable natural Release gate matrix across data shapes and run counts
clear separation between natural overlap evidence and controlled queue-ahead
  proof tooling
same-run borrowed-reference validation for every readiness gate
operator-facing telemetry that explains speed, allocation, memory, and cleanup
default-readiness acceptance criteria
fallback and failure behavior that does not hide unsafe retention
```

The resulting design must preserve these closed contracts:

```text
RadarEventBatch remains the processing input.
Leased payload storage is valid only during the synchronous publish callback.
Only owned or retained-owned input may enter the provider queue.
Retained resources release only after final use.
Provider enqueue success remains distinct from processing completion.
Queued batches drain in provider sequence order.
One rebalance-enabled batch is processed and committed at a time.
Queued batches capture topology at processing time, not enqueue time.
Accepted topology changes publish only after successful processing.
Failed processing prevents later success claims.
Blocking-borrowed remains the default provider mode during milestone 011.
Blocking-borrowed remains the same-run correctness oracle.
Queued-owned remains explicit and observable.
```

The key milestone boundary is:

```text
safe in 011:
  strengthen telemetry and evidence for the existing optimized contour
  define a default-candidate configuration profile
  repeat natural gates enough to understand variance and memory pressure
  keep controlled consumer-delay proof separate from production evidence

not safe in 011 unless explicitly reprioritized:
  changing the default provider mode
  treating synthetic consumer delay as production throughput evidence
  silently falling back from unsafe retention to borrowed processing
  broadening into durable queues, live ingestion, or concurrent rebalance
```

## Expected Outcome

At the end of milestone 011, RadarPulse should have a clear answer to this
question:

```text
Is queued-owned + pooled-copy + producer-consumer ready to be proposed as the
next default provider mode, and under what measured limits?
```

The expected result is:

```text
retained memory pressure is visible for both queued pending batches and the
  active consumer-owned batch
high-water telemetry explains the true memory cost of overlap
natural Release gates are repeatable and compare same-run borrowed and
  optimized queued-owned contours
validation proves checksum, topology, rebalance, and cleanup parity
resource release failures remain visible and fail readiness
configuration exposes a named default-candidate contour without making it
  the default
gate output distinguishes throughput improvement from run-to-run noise
controlled queue-ahead proof remains available but cannot justify a default
```

The core idea is:

```text
010 made optimized queued-owned plausible.
011 should make the default decision evidence-based.
```

Milestone 011 is successful when the project can either recommend or reject a
future default switch using concrete correctness, memory, cleanup, throughput,
and configuration evidence rather than a single favorable benchmark row.

## Starting Position

Milestone 010 closed this optimized opt-in path:

```text
archive cache producer
  -> builds callback-scoped RadarEventBatch input
  -> retains owned payload through pooled-copy
  -> enqueues retained-owned input into a bounded provider queue
  -> continues across selected cache files while bounded backpressure permits

processing consumer
  -> dequeues retained-owned input in provider sequence order
  -> captures latest topology immediately before processing
  -> processes one rebalance-enabled batch at a time
  -> validates output and runs rebalance control-plane work
  -> releases retained resources after final use
```

The important milestone 010 signals are:

```text
correctness:
  borrowed-reference parity holds across optimized queued-owned contours

allocation:
  pooled-copy sharply reduces retained allocation versus snapshot-copy

resource lifecycle:
  retained batches release successfully with 0 failed releases in the captured
  gates

overlap:
  cache-level producer/consumer overlap improves wall-clock time on the
  repeated KTLX contour

natural queue-ahead:
  natural queue depth high-water mark remains 1 and HasQueuedAheadOverlap
  remains no

controlled queue-ahead:
  benchmark-only consumer delay proves bounded queue-ahead mechanics and
  retained-byte backpressure

default posture:
  blocking-borrowed remains the default provider mode and same-run oracle

telemetry gap:
  retained-byte high-water telemetry reports queued pending bytes, but not
  retained resources held by the active consumer after dequeue
```

Milestone 010 intentionally deferred:

```text
queued-owned as the default provider mode
natural full-cache queued-ahead proof without synthetic consumer delay
in-flight retained-resource high-water telemetry after dequeue
builder-transfer retained payload execution
multiple active rebalance-enabled processing batches
ordered concurrent rebalance commit barrier
durable queue or broker integration
live ingestion
cross-process provider or worker transport
source-level migration and partition splitting
physical worker-local state transfer
complex radar algorithms
```

Those deferrals are the input to milestone 011. The immediate target is not a
larger runtime. It is evidence quality for the optimized runtime already
implemented.

## Architectural Principles

Milestone 011 should follow these principles:

```text
evidence before defaults
same-run borrowed reference before interpreting queued-owned results
memory pressure must include both pending queue bytes and active consumer bytes
retained resources must release exactly once on success, failure, and cancel
natural gates decide production readiness; controlled delay proves mechanics
configuration should make the candidate contour easy to select and hard to
  misread as the current default
fallback behavior must be explicit and visible
variance matters; one fast run is not enough for default readiness
telemetry should explain cost movement without retaining payload data
builder-transfer remains unsupported until ownership transfer is proven
durable ingestion and concurrent rebalance remain separate milestones
```

The milestone should separate these concerns:

```text
candidate contour:
  the exact queued-owned configuration being evaluated for future default use

memory pressure:
  retained bytes still pending in the queue plus retained bytes currently owned
  by the active consumer

readiness evidence:
  repeated natural benchmark rows, validation parity, release health, and
  resource-pressure limits

configuration:
  how users select the candidate contour without changing global defaults

decision:
  whether the evidence supports a later default switch, continued opt-in use,
  or further optimization
```

## Core Concepts

### Default-Candidate Contour

The default-candidate contour is the exact optimized provider shape evaluated
for possible future default use.

The initial candidate should be:

```text
provider mode:
  queued-owned

retained payload strategy:
  pooled-copy

provider overlap:
  producer-consumer

processing:
  ordered single-consumer rebalance-enabled processing

oracle:
  same-run blocking-borrowed comparison
```

The candidate contour is not the default during milestone 011. It is a named
profile that can be selected intentionally by tests, benchmarks, and CLI
commands.

The candidate must remain stable enough that repeated gates compare the same
shape:

```text
provider mode is explicit
retention strategy is explicit
overlap mode is explicit
queue capacity is explicit or profile-owned
retained-byte budget is explicit or profile-owned
telemetry level is explicit enough to interpret the gate
consumer-delay proof tooling is disabled
```

Any later change to the candidate profile should be recorded as a gate input
change, not mixed into the same evidence series.

### Readiness Evidence Package

The readiness evidence package is the collection of results required before a
default switch can be recommended.

It should include:

```text
correctness:
  same-run borrowed-reference parity for payload values, checksums, accepted
  moves, skipped decisions, failed migrations, topology versions, and validation
  status

resource lifecycle:
  retained batch release counts, already-released counts, not-required counts,
  failed releases, and cleanup behavior under fault and cancellation

memory pressure:
  pending retained bytes, in-flight retained bytes, combined retained bytes,
  high-water marks, retained-byte budget, and provider blocked time

performance:
  end-to-end elapsed, callback elapsed, replay/build elapsed, retention elapsed,
  queue wait, consumer idle, worker wait, and overlap attribution

allocation:
  retained allocation, measured total allocation, unattributed allocation, and
  comparison against snapshot-copy where useful

variance:
  repeated Release rows that make run-to-run spread visible

configuration:
  exact candidate profile, queue capacity, retained-byte budget, execution mode,
  worker counts, data selection, and validation profile
```

The package should support three possible conclusions:

```text
ready to propose default switch in a later milestone
keep optimized queued-owned as opt-in and gather more evidence
reject the current candidate and optimize or narrow the contour
```

### In-Flight Retained Resource Pressure

In-flight retained resource pressure is memory retained by a batch after it has
left the pending queue but before the consumer releases it.

Milestone 010 queue telemetry reports pending queued retained-byte high-water
marks. That does not describe the full overlap memory footprint because the
active consumer may hold a retained batch while the producer is retaining or
queueing later batches.

Milestone 011 should model retained resources in these states:

```text
pending:
  accepted into the provider queue and not yet dequeued

active:
  dequeued by the consumer and not yet released

releasing:
  consumer has finished final use and is releasing or recycling resources

released:
  resource release completed or was not required

faulted:
  resource release failed or the retained batch could not be cleaned up safely
```

The important accounting movement is:

```text
enqueue accepted:
  pending bytes increase

dequeue accepted:
  pending bytes decrease
  active bytes increase

final release:
  active bytes decrease
  release counters update
```

Telemetry should expose:

```text
pending retained bytes current and high-water
active retained bytes current and high-water
combined retained bytes current and high-water
active retained batch count current and high-water
combined retained batch count current and high-water
retained-byte budget and whether the high-water approached it
release failures and unreleased resources
```

For the current single-consumer contour, active retained batch count should
normally be at most one. The telemetry model should still use explicit active
state so future concurrent contours do not need to redefine the metric.

Telemetry must not retain payload arrays, full event lists, or batch contents
for diagnostic detail. It should retain counts, bytes, timings, sequence ids,
and bounded recent summaries only.

### Natural Versus Controlled Overlap

Natural overlap is produced by real replay and processing timing without
synthetic consumer delay.

Controlled overlap is produced by benchmark-only delay or pressure tooling that
intentionally slows a stage to prove bounded queue mechanics.

Milestone 011 should keep both concepts, but only natural overlap can support a
default-readiness decision.

Natural gate interpretation:

```text
valid for default-readiness evidence
uses real replay and processing timings
consumer-delay proof tooling disabled
reports whether overlap is wall-clock useful
reports queue depth and active retained pressure honestly
```

Controlled proof interpretation:

```text
valid for mechanical queue-ahead and backpressure proof
not valid as production throughput evidence
must be labeled as controlled in CLI, benchmark, and docs
must not be mixed into natural gate aggregates
```

The milestone should avoid this mistake:

```text
controlled queue depth 8 proves the queue can fill under pressure.
It does not prove the natural production contour needs or benefits from depth 8.
```

### Production Configuration Envelope

The production configuration envelope defines the allowed option combinations
for the candidate contour.

It should make common choices explicit:

```text
default provider:
  blocking-borrowed

candidate provider profile:
  queued-owned + pooled-copy + producer-consumer

compatibility retained strategy:
  snapshot-copy

unsupported retained strategy:
  builder-transfer

controlled proof option:
  consumer delay, disabled by default and rejected outside producer-consumer
  queued-owned benchmark contours
```

The envelope should reject ambiguous or unsafe combinations early:

```text
provider-overlap with blocking-borrowed
builder-transfer execution without proven ownership transfer
consumer-delay outside controlled benchmark usage
negative or impossible retained-byte budgets
unbounded queue capacity for readiness gates
telemetry levels that hide required readiness fields
```

The candidate profile may provide convenience, but the expanded option values
must remain visible in benchmark output so a gate can be reproduced.

### Default Switch Gate

The default switch gate is the evidence threshold required before a later
milestone may change the provider default.

Milestone 011 defines and tests the gate. It does not need to pass the gate,
and it should not change defaults as a side effect of defining it.

Candidate gate dimensions:

```text
correctness:
  deterministic same-run parity against blocking-borrowed across selected data
  shapes and validation profiles

lifecycle:
  all accepted retained resources are released, failed releases stay at zero,
  and cancellation/fault paths do not leak retained resources

memory:
  combined retained-byte high-water remains bounded and explainable under the
  configured retained-byte budget

performance:
  repeated natural Release runs are faster than, or acceptably close to,
  blocking-borrowed after variance is considered

allocation:
  retained allocation remains materially lower than snapshot-copy and total
  allocation movement is explained

operator surface:
  CLI and result output make provider mode, strategy, overlap mode, queue
  capacity, retained-byte budget, and telemetry posture explicit

fallback:
  unsupported or failed retention does not silently produce a successful
  queued-owned result
```

A later default switch should require an explicit decision trace that names the
gate result and the chosen rollout posture.

### Fallback And Failure Policy

Fallback behavior must not hide correctness or lifetime bugs.

Readiness gates should prefer fail-closed behavior:

```text
unsupported retention strategy:
  report unsupported and fail the candidate run

retention failure:
  stop intake, release accepted resources, and fail the candidate run

processing failure:
  fault the session, prevent later success claims, and release retained
  resources that were accepted

release failure:
  report failed release, fail readiness, and keep the failure visible in
  telemetry

cancellation:
  stop intake deterministically and release accepted retained resources
```

An explicit fallback-to-borrowed configuration may be useful in a later
production rollout, but readiness benchmarks must not silently convert a failed
candidate into a successful borrowed run.

### Operator Telemetry Contract

Operator telemetry is the stable output needed to understand a candidate run.

Milestone 011 should keep telemetry bounded while making these fields visible:

```text
provider mode
retained payload strategy
provider overlap mode
queue capacity
retained-byte budget
pending retained-byte high-water
active retained-byte high-water
combined retained-byte high-water
pending and active retained batch high-water marks
provider blocked elapsed
consumer idle elapsed
overlap active/shared elapsed
retention elapsed and retained allocation
release counts and failed releases
validation status and deterministic output summary
same-run borrowed comparison where available
```

Verbose or recent telemetry may include bounded recent sequence summaries, but
it should not make long archive runs grow diagnostics without limit.

## Target Pipeline

The milestone 011 target pipeline is the milestone 010 optimized pipeline with
stronger readiness instrumentation:

```text
archive replay producer
  -> reads archive file or selected cache stream
  -> constructs callback-scoped RadarEventBatch input
  -> retains owned payload through pooled-copy
  -> records retained payload bytes and retention cost
  -> enqueues retained-owned batch with provider sequence id
  -> increases pending retained-resource accounting
  -> continues until input completes or bounded backpressure blocks

provider queue
  -> stores retained-owned batch plus release handle
  -> enforces item and retained-byte limits
  -> records pending retained bytes and queue depth
  -> moves accepted work to consumer in provider sequence order

processing consumer
  -> dequeues the next retained-owned batch
  -> moves retained bytes from pending to active accounting
  -> captures latest topology snapshot
  -> processes through sync or async execution
  -> validates output and runs rebalance after successful processing
  -> releases retained resources after final telemetry and validation use
  -> decreases active retained-resource accounting

benchmark and validation
  -> runs same-run borrowed reference and candidate contour
  -> reports deterministic parity, timing, allocation, overlap, memory, and
     cleanup evidence
```

The target pipeline should still process one rebalance-enabled batch at a time.
Milestone 011 is not the ordered concurrent processing milestone.

## Resource Pressure Model

Resource pressure should be reported as a pipeline-level concept rather than a
queue-only concept.

Required state transitions:

```text
retained:
  payload is owned by a retained batch resource, but not accepted into the
  queue yet

queued:
  accepted by the provider queue and counted as pending retained pressure

active:
  dequeued and counted as active consumer retained pressure

released:
  final use is complete and the retained resource has been released or marked
  release-not-required

failed:
  retention, processing, validation, migration, or release failed
```

Readiness telemetry should be able to answer:

```text
How many retained bytes were pending in the queue at peak?
How many retained bytes were active in the consumer at peak?
How many retained bytes were held by the whole pipeline at peak?
Did retained pressure approach or hit the configured budget?
Was provider blocked by retained-byte pressure or item capacity?
Were all retained resources released exactly once?
```

The combined retained-byte high-water is the important default-readiness
metric:

```text
combined retained bytes = pending retained bytes + active retained bytes
```

This metric should be interpreted with the retained-byte budget and queue
capacity. A low pending high-water is not enough to prove low memory pressure
if the active consumer holds a large retained batch for most of the overlap
window.

## Configuration And Defaults

Milestone 011 should keep current defaults conservative:

```text
default provider mode:
  blocking-borrowed

default retained strategy when queued-owned is selected without override:
  existing compatibility behavior unless the implementation plan explicitly
  changes this for opt-in candidate commands

default overlap mode:
  no provider overlap unless selected or implied by an explicit candidate
  profile
```

The candidate contour should be easy to select explicitly. Possible surfaces
include a named benchmark profile or clearly expanded CLI flags.

Every result should print the expanded effective configuration:

```text
provider mode
retained payload strategy
provider overlap mode
execution mode
worker count
queue capacity
retained-byte budget
telemetry level
consumer-delay setting
validation profile
input selection
```

The implementation plan can decide exact option names. The architecture only
requires that the candidate is reproducible and not confused with the default.

## Validation

Validation remains borrowed-reference driven.

Every default-readiness gate should compare the candidate against a same-run
`blocking-borrowed` contour when practical.

Validation must preserve:

```text
published file count
payload value count
raw or validation checksum
accepted move count
skipped decision count
failed migration count
topology publication ordering
provider sequence ordering
retained resource release counts
validation status and errors
```

The validation output should distinguish:

```text
candidate failed correctness
candidate failed cleanup
candidate failed memory-pressure gate
candidate passed correctness but regressed performance
candidate passed but evidence is too noisy for default readiness
```

Milestone 011 should avoid treating performance success as correctness success.
Correctness, cleanup, and memory bounds are separate readiness dimensions.

## Benchmark Scope

Milestone 011 benchmark scope should focus on natural Release evidence.

Required benchmark posture:

```text
Release build
same-run borrowed async reference where practical
queued-owned pooled-copy producer-consumer candidate contour
consumer-delay disabled for readiness gates
retained-byte and active-resource telemetry enabled
deterministic output comparison captured
repeated runs captured clearly enough to show variance
```

Candidate data shapes:

```text
current KTLX 2026-05-04 contour used by milestone 010
larger local cache contour where available
at least one different radar/date shape where local data exists
single-file compatibility smoke for output shape, not default readiness
controlled-delay contour only as a separate mechanics proof
```

Benchmark interpretation should report:

```text
best, worst, and representative elapsed times where multiple runs exist
candidate delta versus same-run borrowed async
candidate delta versus non-overlapped queued-owned pooled-copy where useful
retained allocation and total measured allocation movement
pending, active, and combined retained pressure
queue depth and HasQueuedAheadOverlap
provider blocked time and consumer idle time
worker queue wait and processing callback cost
release counts and failed releases
```

The milestone should not require natural queue depth greater than 1 as a
readiness condition by itself. Milestone 010 already showed useful overlap can
come from producer replay running while the consumer processes an active
retained batch. The readiness condition is that the overlap, memory cost, and
variance are understood.

## Failure And Cancellation

Failure and cancellation behavior is part of default readiness.

The candidate contour should prove:

```text
retention failure stops intake and releases accepted resources
processing failure faults the queued session and prevents later success claims
validation failure is reported as candidate failure, not hidden by fallback
rebalance migration failure preserves existing failed-migration semantics
release failure increments failed-release telemetry and fails readiness
cancellation closes intake deterministically and releases retained resources
```

Failure telemetry should include enough bounded detail to identify the provider
sequence and lifecycle phase without retaining payload data.

## In Scope

Milestone 011 includes:

```text
in-flight retained-resource high-water telemetry after dequeue
combined retained memory pressure accounting
default-candidate contour definition
candidate configuration/profile surface
repeatable natural Release gate matrix
same-run borrowed-reference validation for readiness gates
readiness acceptance criteria
bounded operator telemetry for candidate runs
failure, cancellation, and release-health readiness checks
decision trace that recommends default switch, continued opt-in, or more work
```

## Out Of Scope

Milestone 011 does not implement:

```text
changing queued-owned to the default provider mode
builder-transfer retained payload execution
durable broker integration
live ingestion
cross-process provider or worker transport
multiple active rebalance-enabled processing batches
ordered concurrent rebalance commit barrier
source-level migration or partition splitting
physical worker-local state transfer
complex radar algorithms
visualization or product-facing radar analysis features
```

Those remain future milestones unless explicitly reprioritized.

## Completion Criteria

Milestone 011 is complete when:

```text
the default-candidate contour is named and reproducible
in-flight retained-resource telemetry is implemented and visible
pending, active, and combined retained high-water fields are captured
resource lifecycle telemetry proves accepted retained resources release
same-run borrowed-reference validation is preserved for readiness gates
natural Release gates are repeated and interpreted with variance
controlled-delay proof remains clearly separated from readiness evidence
candidate configuration output is explicit and reproducible
failure and cancellation cleanup behavior is tested or otherwise gated
the decision trace records whether the evidence supports a future default
  proposal, continued opt-in status, or further optimization
blocking-borrowed remains the default at milestone closeout
```

The milestone should close with a decision, not with an automatic default
change.

## Likely Next Milestone Input

If milestone 011 supports a future default proposal, the next milestone can
focus on a controlled default rollout:

```text
provider default switch decision
compatibility and rollback controls
operator documentation and CLI default changes
broader regression gate around archive benchmarks and validation
```

If milestone 011 does not support a default proposal, the next milestone should
stay on the bottleneck shown by the gate:

```text
retained memory pressure reduction
overlap variance reduction
producer replay optimization
processing consumer latency reduction
allocation attribution
configuration narrowing
```

Deferred unless explicitly reprioritized:

```text
durable queues
live ingestion
cross-process workers
concurrent rebalance processing
builder-transfer
source-level migration
partition splitting
complex radar algorithms
```
