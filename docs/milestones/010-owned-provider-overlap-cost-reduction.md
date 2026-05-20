# Milestone 010: Owned Provider Overlap And Cost Reduction Architecture

Status: complete.

RadarPulse milestone 010 starts from the closed milestone 009 owned-payload
provider decoupling substrate and defines the architecture for making
`queued-owned` useful under real replay/processing overlap without hiding the
cost or weakening correctness.

This document is intentionally not an implementation plan. It records the
optimization concept, retained-payload cost boundary, producer/consumer
overlap model, topology and rebalance ordering rules, resource lifecycle,
telemetry, validation posture, benchmark scope, and expected result before any
task breakdown is written.

Milestone 009 made the owned provider boundary correct and measurable:
borrowed archive callbacks remain blocking and safe by default, while
`queued-owned` can convert leased `RadarEventBatch` values into owned snapshots
and enqueue them into a bounded provider-to-processing queue. The performance
gate accepted that shape as a validation and measurement substrate, but not as
the default path because owned snapshot allocation was the dominant cost and
the benchmark did not overlap replay with processing.

The core decision is:

```text
009 made provider handoff safe through owned input.
010 makes the owned handoff cheaper and proves real overlap.
```

The milestone should not broaden into live ingestion, durable brokers, or
distributed processing. It should make the in-process owned boundary cheap
enough and observable enough that later production ingestion work has a stable
runtime shape to build on.

## Milestone Goal

Milestone 010 should reduce owned payload retention cost and add a true
producer/consumer archive benchmark contour where replay can run ahead of
processing within bounded limits.

The output of the milestone is the architectural definition of:

```text
lower-allocation owned RadarEventBatch retention strategy
explicit resource lifecycle for retained payload buffers
bounded producer/consumer overlap across archive files or batches
queue depth and retained-byte limits that create visible backpressure
single-consumer rebalance-safe processing as the default overlap shape
topology pinning rules for queued and processing-active batches
ordered completion and rebalance publication boundaries
borrowed-reference parity validation for overlapped queued-owned runs
telemetry that separates replay, ownership, queue, processing, and overlap cost
benchmark contours that show whether queue capacity produces useful overlap
```

The resulting design must preserve these closed contracts:

```text
RadarEventBatch remains the processing input.
Leased payload storage is valid only during the synchronous publish callback.
Only owned RadarEventBatch values may enter the provider queue.
Provider enqueue success remains distinct from processing completion.
Queued batches remain ordered by provider sequence id.
SourceId -> PartitionId remains stable.
PartitionId -> ShardId changes only through versioned topology publication.
Each processed batch uses one captured topology snapshot.
Accepted topology changes are published only after successful processing.
The borrowed blocking path remains available as the correctness oracle.
Async worker telemetry remains visible under queued-owned input.
Provider queue telemetry remains bounded.
```

The key milestone boundary is:

```text
safe in 010:
  provider can retain owned input and continue replay while processing drains
  processing can consume owned input after the provider callback returns
  resource reuse can reduce allocation after the batch is fully complete

not safe in 010 unless explicitly proven:
  borrowed input crossing the callback boundary
  unbounded queued payload growth
  hidden dropping of provider batches
  topology publication while earlier/later processing order is ambiguous
  queued-owned becoming the default before cost and overlap improve
```

## Expected Outcome

At the end of milestone 010, RadarPulse should have a clear architecture for
running archive replay and processing as overlapping in-process stages while
keeping payload lifetime, memory pressure, topology visibility, and validation
deterministic.

The expected result is:

```text
owned payload retention allocates materially less than milestone 009 snapshots
resource ownership is explicit from provider conversion through consumer release
provider replay can enqueue multiple owned batches before processing drains them
queue capacity can produce measurable overlap instead of only bounded behavior
provider backpressure is visible when processing or retained memory falls behind
processing drains queued batches in provider order by default
rebalance publication remains ordered between completed processed batches
borrowed blocking output remains the same-run validation reference
telemetry reports owned allocation/time, pool or transfer behavior, queue depth,
  retained bytes, provider blocked time, consumer idle time, overlap time, and
  processing/rebalance results separately
benchmarks can explain whether queued-owned is closer to becoming a default
```

The core idea is:

```text
009 proved that owned queueing is correct.
010 should prove that owned queueing can be cheap enough and overlapped enough
to be worth keeping on the path toward production ingestion.
```

Milestone 010 is successful when queued-owned can be reasoned about as a
bounded, resource-owned, overlapped pipeline over the existing processing core,
not merely as a copied snapshot queue that drains after replay is already done.

## Starting Position

Milestone 009 closed this reference queued path:

```text
archive provider publishes one leased RadarEventBatch
  -> queueing publisher converts it to an owned snapshot before callback return
  -> owned batch is enqueued into a bounded provider queue
  -> queued consumer drains owned batches in provider sequence order
  -> processing uses the existing synchronous or async processing execution
  -> rebalance may publish topology N+1 only after successful processing
  -> queued output is validated against borrowed blocking reference output
```

The important milestone 009 signals are:

```text
correctness:
  queued-owned preserved deterministic parity against the borrowed reference
  across provider and execution modes

ownership:
  only owned RadarEventBatch values may enter the provider queue

cost visibility:
  owned snapshot allocation and elapsed time are reported separately from
  enqueue, drain, worker, processing, and rebalance cost

measured cost:
  full-cache queued-owned added about 9.95 GB of owned snapshot allocation and
  about 0.53-0.58 seconds of explicit owned snapshot time on the KTLX contour

overlap gap:
  queue capacity 8 did not improve throughput because the benchmark drained
  after each file publish; the queue high-water mark remained 1

default mode:
  blocking-borrowed remains the default provider mode and correctness oracle
```

Milestone 009 intentionally deferred:

```text
queued-owned as the default provider mode
true producer/consumer overlap between archive replay and processing
multiple active queued processing batches
topology version pinning for concurrent queued batches
buffer pooling, move/transfer semantics, or lower-allocation owned payloads
durable broker integration or live ingestion
cross-process or distributed workers
physical worker-local state transfer
source-level migration and partition splitting
complex radar algorithms
```

Those deferrals are the input to milestone 010. The first implementation
should target memory cost and in-process overlap before broadening into durable
or live provider behavior.

## Architectural Principles

Milestone 010 should follow these principles:

```text
optimize only after preserving the explicit ownership boundary
keep borrowed blocking as the low-allocation correctness reference
keep queued-owned opt-in until cost and overlap are both measured better
prefer transfer or pooling where lifetime can be proven
never reuse retained buffers until all processing and validation have finished
bound retained payloads by item count and, where practical, retained bytes
make provider backpressure visible instead of silently dropping or expanding
drain queued batches in provider order for the default rebalance-enabled shape
publish topology only at deterministic processing boundaries
measure allocation movement instead of hiding it inside replay or processing
keep validation strong enough to catch ordering, topology, and retention bugs
```

The milestone should separate these concerns:

```text
ownership cost:
  how a leased provider batch becomes retainable without unsafe aliasing

overlap:
  whether provider replay can run ahead while processing drains previous input

topology:
  which topology version a processing-active batch observes and when accepted
  migrations become visible

resource lifecycle:
  who owns retained buffers after enqueue and who releases or recycles them

benchmark interpretation:
  whether the new shape reduces allocation, creates overlap, or only moves cost
```

## Core Concepts

### Retained Payload Strategy

The retained payload strategy is the mechanism that turns callback-scoped input
into input that may safely outlive the callback.

Milestone 009 used owned snapshots as the first correct strategy. Milestone 010
should evaluate lower-allocation strategies without changing the processing
contract.

Allowed strategy families:

```text
snapshot copy:
  allocate new owned event and payload storage and copy leased data

pooled copy:
  copy leased data into rented buffers whose lifetime is owned by the queued
  pipeline and returned after processing completion

builder transfer:
  move builder-owned event and payload buffers into the final RadarEventBatch
  when the provider can prove those buffers will not be reused

selective retained representation:
  retain only the data needed by processing and validation while preserving the
  same observable RadarEventBatch contract
```

Any strategy must preserve:

```text
stream schema
dictionary and source-universe versions
event ordering
payload bytes and payload metrics
source identity and source-local order
owned lifetime marker
stable contents after provider builder reuse
validation checksum parity with the borrowed reference
```

The milestone may implement one strategy first, but the architecture should
leave room to compare strategies under the same benchmark surface.

### Resource-Owned Batch

A resource-owned batch is an owned `RadarEventBatch` plus any resource handle
needed to release or recycle retained storage.

The processing API should still see `RadarEventBatch` as input. The queued
pipeline may carry resource ownership alongside the batch so pooled or
transferred buffers are released exactly once.

Resource ownership rules:

```text
provider owns leased input before enqueue
provider transfers owned retained input to the queue on accepted enqueue
queue owns accepted input until dequeue
consumer owns dequeued input until processing, validation, and telemetry finish
consumer releases or recycles retained resources after final use
fault and cancellation paths release accepted but unprocessed resources
recent telemetry must not retain full batch payloads
```

This concept should not allow borrowed payloads to enter the queue. It only
describes how owned storage is cleaned up after queue acceptance.

### Producer/Consumer Overlap

Producer/consumer overlap means archive replay and processing are active during
the same benchmark interval.

The milestone 009 queue validated bounded handoff but did not create useful
overlap because archive replay drained after each file publish. Milestone 010
should introduce a contour where the producer can continue publishing later
files or batches while the consumer processes earlier owned input.

Minimum overlap shape:

```text
producer:
  reads archive input
  builds or converts RadarEventBatch values
  retains owned payload
  enqueues by provider sequence id
  waits only when queue capacity or retained-byte limits are reached

consumer:
  dequeues owned batches in provider sequence order
  captures topology for the batch
  processes through synchronous or async processing execution
  runs validation and rebalance publication
  releases retained resources
```

The first rebalance-enabled overlap shape should remain single-consumer and
ordered. That gives real replay/processing overlap without introducing
concurrent topology publication ambiguity.

### Pipeline Window

The pipeline window is the bounded amount of provider work allowed to get ahead
of completed processing.

Item capacity alone is useful but incomplete. A large radar batch can carry
much more retained payload than a small one, so the architecture should also
track retained bytes where practical.

Pipeline window controls:

```text
max queued item count
optional max retained payload bytes
enqueue wait timeout
shutdown behavior: drain accepted work or cancel pending work
full behavior: wait, fail enqueue, or stop provider intake
```

The default posture should be conservative:

```text
bounded queue
no silent dropping
no unbounded memory growth
provider wait time is measured
retained payload high-water marks are reported
```

### Queue Capacity Versus Useful Overlap

Queue capacity is useful only when it allows producer and consumer work to
overlap.

The benchmark must distinguish:

```text
bounded behavior:
  queue accepts work and preserves order, but high-water mark stays 1

useful overlap:
  producer publishes later input while consumer processes earlier input, with
  queue depth or active-time telemetry proving that overlap occurred
```

Milestone 010 should avoid interpreting larger queue capacity as an improvement
unless telemetry shows actual overlap or reduced provider idle time.

### Topology Pin

A topology pin is the topology snapshot captured for one processing-active
batch.

Queued payloads that are waiting in the provider queue should not need a
topology pin yet. They are retained input, not processed input. The default
rebalance-enabled consumer captures topology only when a batch is dequeued for
processing.

Default topology rules:

```text
queued state:
  batch has provider sequence id and owned payload
  batch has no processing topology pin

processing state:
  consumer captures the latest published topology snapshot before processing
  one processed batch uses one captured topology snapshot

commit state:
  processing result, validation, and rebalance decision are committed in
  provider sequence order
  accepted topology changes publish only after successful processing
  next dequeued batch observes the latest published topology
```

This keeps the milestone 006-009 invariant intact:

```text
accepted topology changes become visible between processed batches.
```

### Concurrent Processing Boundary

Multiple queued batches are in scope for overlap. Multiple concurrently
processing rebalance-enabled batches are not the default milestone 010 shape.

Concurrent processing creates a topology problem:

```text
batch N starts on topology T
batch N+1 starts before N commits
batch N accepts a migration and publishes topology T+1
batch N+1 has already processed against T
```

That behavior is not equivalent to the closed sequential rebalance semantics.
Therefore milestone 010 should use this boundary:

```text
default:
  many queued owned batches may wait
  one rebalance-enabled batch processes and commits at a time

optional future or stretch contour:
  concurrent processing may exist only with an ordered commit barrier and an
  explicit policy for stale topology pins, rebalance deferral, or recomputation
```

If the implementation experiments with concurrent processing, benchmark output
must identify whether rebalance publication is disabled, deferred, or ordered
through a proven commit barrier. It must not silently change topology semantics.

### Ordered Commit Barrier

The ordered commit barrier is the rule that observable processing completion
is published in provider sequence order.

For the default single-consumer shape, the barrier is naturally satisfied by
FIFO dequeue and sequential commit.

For any future concurrent contour, the barrier would need to hold completed
results until all earlier provider sequence ids have committed.

Barrier responsibilities:

```text
preserve provider sequence completion order
publish validation results in sequence order
publish accepted topology moves only at ordered commit points
release retained resources after their result can no longer be inspected
fault the queued session deterministically when processing or migration fails
```

The milestone should define this concept even if the first implementation uses
the simpler single-consumer form.

## Target Pipeline

The target milestone 010 pipeline is:

```text
archive replay producer
  -> reads archive file or file segment
  -> constructs leased or transfer-ready RadarEventBatch
  -> retains owned payload through selected strategy
  -> enqueues owned batch with provider sequence id
  -> continues until input completes or backpressure blocks

provider queue
  -> stores owned batch plus resource ownership handle
  -> enforces item and retained-byte limits
  -> records enqueue wait and high-water marks
  -> closes, drains, faults, or cancels deterministically

processing consumer
  -> dequeues in provider sequence order
  -> captures latest topology snapshot
  -> processes through sync or async processing execution
  -> validates against the configured profile
  -> runs rebalance and publishes accepted moves between batches
  -> releases retained resources

benchmark and validation
  -> compares queued-owned overlap against borrowed blocking reference
  -> reports replay, retention, queue, overlap, processing, worker, validation,
     rebalance, and release costs separately
```

The minimum useful overlap contour should operate across multiple archive files
or enough batches to let the queue fill beyond depth 1 when processing is the
slower stage.

## Queue And Backpressure

Milestone 010 should reuse the milestone 009 bounded queue posture and extend
it where needed for retained-byte visibility.

Backpressure semantics:

```text
accepted:
  owned input is now queue-owned and will be processed, drained, or released

full:
  queue capacity or retained-byte budget is exhausted before acceptance

timed out:
  provider waited for capacity and the configured timeout elapsed

canceled:
  caller cancellation occurred before acceptance or while waiting

closed:
  intake is closed and no later provider input should be accepted

faulted:
  processing, validation, migration, or resource lifecycle failure made later
  intake unsafe
```

Provider backpressure should be measured as a first-class signal:

```text
enqueue wait elapsed
provider blocked elapsed
provider active elapsed
queue depth high-water mark
retained payload bytes high-water mark
consumer idle elapsed
producer completed before consumer completed
```

The milestone should not treat backpressure as a failure when it is expected
and bounded. It is a signal that the pipeline window is doing its job.

## Ordering And Topology

Milestone 010 must preserve the ordering model that made milestone 006 through
009 rebalance deterministic.

Default ordering rules:

```text
provider sequence id is monotonic for accepted batches
queue exposes accepted batches in provider sequence order
consumer processes one rebalance-enabled batch at a time
topology is captured immediately before processing starts
processing result carries captured topology version
rebalance evaluates only after successful processing and validation
accepted moves publish only before the next batch starts processing
failed processing prevents rebalance publication for that batch
faulted queue stops later intake and releases pending owned resources
```

The important distinction is:

```text
queued ahead:
  safe, because queued batches have no topology snapshot yet

processing ahead:
  unsafe for rebalance semantics unless topology pinning and ordered commit
  behavior are explicitly defined and validated
```

This milestone should make that distinction visible in names, telemetry, and
documentation.

## Resource Lifecycle

Lower allocation usually requires reuse, transfer, or pooling. Those strategies
make lifecycle correctness as important as processing correctness.

Required lifecycle states:

```text
provider-owned leased input
retention in progress
queue-owned retained input
consumer-owned retained input
validation/telemetry inspection
released or returned retained resources
faulted or canceled cleanup
```

Required lifecycle guarantees:

```text
no retained resource is returned before processing completes
no retained resource is returned before validation and checksum inspection
no recent telemetry detail retains full payload buffers
all accepted queued resources are released on drain, fault, cancellation, or
  dispose
release is idempotent where practical
release failures fault the queued session rather than becoming silent leaks
```

The milestone should include focused tests that mutate or reuse provider
buffers after enqueue to prove retained input remains stable.

## Result Boundaries

Milestone 010 should keep the result boundary separation from milestone 009:

```text
enqueue result:
  did provider input enter the queue?

processing result:
  did accepted queued input process successfully?

resource result:
  were retained resources released or recycled correctly?

session result:
  did producer, queue, consumer, validation, and shutdown reach a deterministic
  final state?
```

The overlap runner should not return success merely because the producer
finished. It succeeds only when all accepted work has a terminal processing and
resource lifecycle result.

## Failure And Cancellation

Failure handling should remain deterministic under overlap.

Failure rules:

```text
retention failure rejects or faults the affected provider input
enqueue failure does not imply processing failure for already accepted input
processing failure faults the queued session and stops later intake
validation failure faults the queued session and preserves diagnostics
migration failure prevents topology publication and faults or marks unhealthy
resource release failure is reported and counted
cancellation requests stop intake and either drain or cancel pending work based
  on explicit shutdown policy
dispose releases waiters and retained resources deterministically
```

The milestone should avoid ambiguous half-success states. If provider replay
completed but accepted processing did not, the session status must say so.

## Telemetry

Milestone 010 telemetry should extend milestone 009 surfaces instead of
replacing them.

Required telemetry groups:

```text
provider:
  files examined, files published, batches produced, provider active time,
  provider blocked time, provider completion status

retention:
  strategy name, retained batch count, retained event count, retained payload
  bytes, allocated bytes, allocation elapsed, transfer count, pool rent/return
  count, pool misses, release count, release failures

queue:
  accepted count, rejected count, full/timed-out/canceled/closed/faulted count,
  depth high-water mark, retained bytes high-water mark, enqueue wait elapsed,
  provider-to-processing latency

overlap:
  producer active interval, consumer active interval, overlap elapsed,
  consumer idle elapsed, producer blocked elapsed, producer completed before
  consumer completed, queue depth over time where bounded sampling is enabled

processing:
  sync/async mode, worker timing, processing elapsed, validation elapsed,
  checksum, payload value count, failed worker items

rebalance:
  accepted moves, skipped decisions, failed migrations, topology versions,
  validation status, state handoff checksum status

resource lifecycle:
  pending retained resources at completion, returned resources, leaked count if
  detectable, dispose cleanup count
```

Telemetry must remain bounded. Recent details may include provider sequence ids,
durations, statuses, sizes, topology versions, and checksums, but not full
payload buffers.

## Validation

Validation should keep the borrowed blocking path as the oracle.

Required validation contours:

```text
same input, blocking-borrowed reference
same input, queued-owned non-overlap or compatibility contour
same input, queued-owned overlap contour
checksum parity
payload value count parity
accepted move and skipped decision parity for rebalance-enabled contours
topology version and migration status sanity checks
provider sequence completeness: no duplicates, gaps, or reordered processing
all accepted resources released
bounded telemetry counters complete for the run
```

The validation posture should catch these bug classes:

```text
payload aliasing after provider buffer reuse
lost queued batch
out-of-order processing or commit
stale topology publication
hidden dropped input
unreleased retained resources
telemetry retaining payload memory
allocation cost moving to an unreported bucket
```

If an experimental concurrent processing contour disables or defers rebalance,
validation output must say that explicitly and should not be compared as a
rebalance-equivalent contour.

## Benchmark Scope

Milestone 010 benchmarks should answer three questions:

```text
Did retained input allocation decrease compared with milestone 009 snapshots?
Did producer and consumer actually overlap?
Did correctness remain equivalent to blocking-borrowed for the same input?
```

Required benchmark contours:

```text
single-file compatibility:
  proves the optimized retained payload strategy still matches borrowed output

multi-file local cache overlap:
  lets producer run ahead and validates queue capacity, retained bytes, and
  consumer drain under sustained input

blocking-borrowed sync and async:
  remain the reference timing and correctness contours

queued-owned milestone 009 style:
  remains useful as a compatibility baseline when practical

queued-owned optimized overlap:
  measures lower allocation, queue depth, overlap elapsed, and total time
```

Recommended reported fields:

```text
provider mode
retention strategy
execution mode
queue capacity
retained-byte limit
examined/published/skipped files
payload value count
validation checksum
end-to-end elapsed
provider active and blocked elapsed
consumer active and idle elapsed
overlap elapsed
owned/retained allocation bytes and elapsed
queue depth and retained-byte high-water marks
enqueue wait and provider-to-processing latency
processing, worker, validation, and rebalance elapsed
accepted moves, skipped decisions, failed migrations
resource release count and pending retained resource count
```

The performance gate should not require `queued-owned` to beat
`blocking-borrowed` immediately. It should require the benchmark to explain
whether the remaining gap is ownership cost, replay cost, queue wait,
processing cost, or lack of overlap.

## In Scope

Milestone 010 includes:

```text
architecture and plan for owned provider overlap cost reduction
one lower-allocation retained payload strategy
resource lifecycle model for retained payload buffers
producer/consumer archive overlap runner or benchmark contour
bounded queue capacity and retained-byte telemetry
provider backpressure and overlap telemetry
ordered single-consumer rebalance-safe queued processing
topology pinning rules for queued versus processing-active batches
borrowed-reference validation for optimized queued-owned runs
Release performance gate comparing milestone 009 and milestone 010 contours
decision trace, closeout, and handoff update
```

## Out Of Scope

Milestone 010 does not include:

```text
making queued-owned the default provider mode before the gate supports it
durable queue or broker integration
live ingestion
cross-process or distributed workers
unbounded queues
silent provider batch dropping
retaining borrowed payloads after callback return
rebalance-enabled concurrent processing without ordered commit semantics
physical worker-local state transfer
source-level migration
partition splitting or repartitioning
complex radar algorithms
```

## Completion Criteria

Milestone 010 is complete when:

```text
lower-allocation retained payload strategy is implemented and tested
retained resource lifecycle is explicit and validated on success/fault/cancel
archive producer/consumer overlap contour is implemented
queue capacity can produce measurable overlap on a multi-file contour
benchmark output reports replay, retention, queue, overlap, processing,
  validation, worker, rebalance, and resource lifecycle cost separately
borrowed-reference parity is preserved for optimized queued-owned runs
topology capture and rebalance publication remain deterministic
all accepted queued resources reach terminal processing and release states
blocking-borrowed remains the default unless the performance gate justifies a
  separate explicit default-change decision
decision trace, closeout, and handoff are updated
```

The performance gate should explicitly answer:

```text
How much owned allocation was removed versus milestone 009?
How much producer/consumer overlap was achieved?
Did queue capacity improve throughput or only increase retained memory?
Did validation parity hold across provider and execution modes?
Is queued-owned still only a measurement mode, or is it ready for production
configuration work in a later milestone?
```

## Likely Next Milestone Input

If milestone 010 closes successfully, the next milestone can choose between:

```text
production configuration and tuning for queued-owned provider mode
larger corpus validation and default-mode readiness assessment
durable broker or live-ingestion adapter over the optimized owned boundary
ordered concurrent processing with explicit topology commit semantics
physical worker-local state transfer
```

The correct next step should be chosen from measured milestone 010 results. If
allocation remains high or overlap remains weak, the next milestone should keep
optimizing the owned provider boundary instead of broadening into live or
durable ingestion.
