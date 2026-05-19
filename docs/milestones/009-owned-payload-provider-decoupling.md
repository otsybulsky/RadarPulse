# Milestone 009: Owned Payload Provider Decoupling Architecture

Status: draft.

RadarPulse milestone 009 starts from the closed milestone 008 retained async
shard transport and defines the architecture for the first explicit owned
payload boundary between replay providers and processing.

This document is intentionally not an implementation plan. It records the
payload ownership concept, provider/processing boundary, queue and backpressure
model, topology and rebalance ordering rules, validation posture, benchmark
scope, and expected result before any task breakdown is written.

Milestone 008 made processing execution worker-shaped while preserving the
borrowed `RadarEventBatch` lifetime rule. Milestone 009 should make replay and
processing separable across an explicit ownership boundary without weakening
the stream, processing, topology, validation, or telemetry contracts already
closed by milestones 004 through 008.

The core decision is:

```text
008 retained workers.
009 makes payload retainable.
```

The milestone should not hide payload lifetime behind async APIs. Work that
outlives a provider callback must own its input.

## Milestone Goal

Milestone 009 should let replay/provider code hand off batches to processing
without requiring processing to finish inside the provider callback, while
keeping retained payload lifetime explicit and measurable.

The output of the milestone is the architectural definition of:

```text
explicit owned RadarEventBatch retention boundary
provider-side owned snapshot or ownership-transfer protocol
bounded provider-to-processing queue
backpressure, cancellation, and fault propagation semantics
processing consumer over owned batches
deterministic topology and rebalance publication ordering
queued-processing telemetry and validation
same-run benchmark contours that expose copy, queue, and processing costs
```

The resulting design must preserve these closed contracts:

```text
RadarEventBatch remains the processing input.
Leased payload storage is valid only during the synchronous publish callback.
Owned RadarEventBatch values may be retained after callback return.
SourceId -> PartitionId remains stable.
PartitionId -> ShardId changes only through versioned topology publication.
Each processed batch uses one captured topology snapshot.
Accepted topology changes are published only after successful processing.
Skipped rebalance decisions remain explainable bounded telemetry.
The synchronous processing path remains available as a correctness reference.
```

The key milestone boundary is:

```text
borrowed batch:
  may be processed inside the provider callback only
  may be converted to an owned snapshot before the callback returns
  must not be queued for later processing as borrowed input

owned batch:
  may be queued after the provider callback returns
  may be processed by retained workers outside the callback
  carries the allocation and copy cost of retaining payload safely
```

## Expected Outcome

At the end of milestone 009, RadarPulse should have a clear architecture for
replay/provider and processing to operate as separate pipeline stages over an
owned-batch handoff.

The expected result is:

```text
provider callbacks can convert leased batches to owned batches deliberately
owned batches can be enqueued to a bounded provider-to-processing queue
provider replay can advance until queue backpressure says it must wait
processing can drain owned batches through the existing async shard transport
processing failures can fault the queued session and stop later intake
cancellation can stop provider intake and drain or cancel queued work explicitly
topology snapshots remain deterministic per processed batch
rebalance publication remains ordered after successful batch processing
telemetry reports owned-copy, enqueue, queue-wait, processing, and drain costs
benchmarks compare borrowed blocking async against owned queued async
```

The core idea is:

```text
008 made the worker group safe.
009 makes the input safe to hand off to that worker-shaped processing stage.
It should separate replay and processing by ownership, not by hope.
```

Milestone 009 is successful when provider decoupling can be reasoned about as a
bounded, owned-input pipeline over the existing processing core, not as a new
stream format or a hidden lifetime extension.

## Starting Position

Milestone 008 closed this reference path:

```text
provider callback publishes one borrowed RadarEventBatch
  -> processing captures one topology snapshot
  -> async dispatcher routes shard work against that snapshot
  -> retained workers process bounded shard work
  -> completion barrier finishes before callback return
  -> deterministic aggregation creates processing telemetry
  -> rebalance may publish topology N+1 only after successful processing
```

The important milestone 008 signals are:

```text
correctness:
  async execution preserved deterministic checksums, source snapshots,
  accepted moves, skipped decisions, validation status, and zero failed
  worker items across captured contours

payload lifetime:
  borrowed RadarEventBatch payload still completed before callback exit

bounded worker telemetry:
  worker counters remained complete and recent detail stayed capped

production-shaped callback cost:
  full-cache archive async callback latency was parity with synchronous
  execution, with about 0.90% additional callback allocation

default mode:
  synchronous execution remains the correctness oracle and default path
```

Milestone 008 intentionally deferred:

```text
owned RadarEventBatch snapshots or payload ownership transfer
provider-level async queues that return before processing completes
multi-batch pipeline scheduling with topology publication ordering
durable broker integration or live ingestion
physical worker-local state transfer
source-level migration
partition splitting or repartitioning
complex radar algorithms
```

Those deferrals are the input to milestone 009. The first implementation should
target provider decoupling through owned input before broadening scheduling,
broker, or live-ingestion behavior.

## Architectural Principles

Milestone 009 should follow these principles:

```text
make ownership explicit before returning from the provider callback
keep RadarEventBatch as the processing input contract
preserve the borrowed-batch path as the low-allocation reference contour
keep synchronous processing available as the correctness oracle
prefer bounded queues and visible backpressure over unbounded buffering
process queued owned batches in deterministic sequence first
publish rebalance topology changes only after successful ordered processing
separate enqueue success from processing completion in result contracts
measure owned-copy and queue costs separately from processing execution
avoid durable broker or live ingestion until the in-process boundary is proven
```

The most important separation is:

```text
replay/provider:
  reads input, decompresses, projects to RadarEventBatch, owns or borrows
  payload, enqueues owned input, and observes backpressure

processing:
  consumes owned RadarEventBatch values, captures topology, dispatches shard
  work, aggregates telemetry, validates output, and runs rebalance after
  successful processing

ownership:
  defines whether a batch may cross the provider callback boundary

backpressure:
  defines when provider replay must wait because processing has not caught up
```

Milestone 009 should not use async naming to weaken lifetime rules. If a batch
is not owned, it cannot outlive the callback that published it.

## Core Concepts

### Borrowed Batch

A borrowed batch is a `RadarEventBatch` with `Lifetime == Leased`.

It is valid only for the duration of the synchronous provider callback. It may
be processed by the synchronous or async processing path only when completion
is guaranteed before the callback returns.

Borrowed batch rules:

```text
may be read by processing inside callback scope
may be converted to an owned snapshot inside callback scope
must not be stored in provider queues
must not be captured by retained work that can outlive callback return
must not be accepted by fire-and-forget APIs
```

### Owned Batch

An owned batch is a `RadarEventBatch` with `Lifetime == Owned`.

It may be retained, queued, and processed after provider callback return. The
existing `RadarEventBatch.ToOwnedSnapshot()` method is the current concrete
ownership conversion surface and must preserve the batch metadata, event
sequence, payload bytes, and precomputed payload metrics.

Owned batch rules:

```text
may be queued after provider callback return
may be processed outside callback scope
may be retained by benchmark or diagnostics contracts when explicitly bounded
must preserve stream schema, dictionary version, source-universe version,
event order, payload offsets, payload bytes, and payload metrics
must make copy/allocation cost visible where conversion is required
```

### Ownership Transfer

Ownership transfer is the broader protocol behind `ToOwnedSnapshot()`. The
first milestone 009 implementation may use owned snapshots only. The
architecture should still use vocabulary that leaves room for a later
zero-copy or pooled ownership-transfer path if the producer can safely hand off
buffers instead of borrowing them.

Ownership transfer must prove:

```text
the producer will not mutate handed-off event or payload buffers
the consumer has exclusive or immutable read access until processing completes
the release/dispose boundary is explicit
metrics and validation see the same bytes as the borrowed reference path
```

Milestone 009 should not require a zero-copy transfer path to close. An owned
snapshot is acceptable if its cost is measured and the contracts leave room for
future transfer optimization.

### Provider Queue

The provider queue is a bounded in-process queue of owned batches and their
session metadata. It separates replay/projection progress from processing
completion.

The first target shape should be conservative:

```text
many owned batches may wait in a bounded queue
one queued batch is actively processed at a time
each active batch still uses the milestone 008 async shard worker transport
rebalance publication remains ordered after each active batch completes
```

This is provider decoupling, not full concurrent multi-batch processing. It
allows replay to advance while processing drains at its own pace, but it avoids
the harder topology-ordering problem of processing multiple batches
concurrently.

### Processing Consumer

The processing consumer owns the drain loop from the provider queue into the
existing processing core or async rebalance session.

The consumer should:

```text
dequeue owned batches in provider sequence order
capture one topology snapshot per processed batch
dispatch work through the existing async shard transport when selected
aggregate processing telemetry deterministically
run rebalance only after successful batch processing
publish topology changes only before the next batch starts processing
report processing completion separately from provider enqueue completion
```

The consumer should not perform replay, decompression, Archive Two projection,
or durable input management.

## Target Pipeline

Milestone 009 should define the first decoupled path as:

```text
archive replay/provider callback
  -> receives leased RadarEventBatch
  -> converts to owned RadarEventBatch before callback return
  -> enqueues owned batch into bounded provider queue
  -> returns after enqueue success or backpressure wait

processing consumer
  -> dequeues owned batch in sequence order
  -> captures current topology snapshot
  -> routes the batch against that snapshot
  -> dispatches shard work through retained async workers
  -> waits for batch completion outside provider callback
  -> aggregates deterministic processing telemetry
  -> validates according to configured profile
  -> runs rebalance after successful processing
  -> publishes topology N+1 only before the next processed batch
```

The reference borrowed path remains:

```text
archive replay/provider callback
  -> receives leased RadarEventBatch
  -> processing completes before callback return
  -> callback returns only after processing and optional rebalance finish
```

Both paths should be comparable. The owned queued path is valuable when replay
and processing need independent pacing. The borrowed blocking path remains the
lowest-lifetime-risk and low-copy baseline.

## Queue And Backpressure

Provider decoupling must be bounded.

The queue contract should define:

```text
capacity in batches
optional capacity in payload bytes or retained payload values
enqueue wait policy
enqueue timeout policy
cancellation behavior before enqueue
cancellation behavior after enqueue
fault behavior when processing has already failed
drain behavior on normal completion
dispose behavior with queued or active work
```

The default policy should prefer backpressure over dropping:

```text
if queue has capacity:
  enqueue owned batch and let provider continue

if queue is full:
  provider waits, times out, or observes cancellation according to explicit
  options

if processing faults:
  later enqueue attempts fail with the processing fault state

if cancellation is requested:
  provider stops accepting new work and the session drains or cancels queued
  work according to the configured shutdown mode
```

Unbounded queues are out of scope. Silent dropping is out of scope for the
first provider decoupling milestone because it would weaken deterministic
replay and validation.

## Ordering And Topology

The first milestone 009 processing consumer should process queued batches in
provider sequence order.

This keeps topology rules aligned with milestones 006 through 008:

```text
batch N captures topology version T
batch N processes completely against topology version T
rebalance may publish topology version T+1 after successful batch N
batch N+1 captures the latest topology after publication decisions for batch N
```

The provider may enqueue ahead, but processing publication remains ordered:

```text
provider sequence:
  enqueue batch 1, batch 2, batch 3

processing sequence:
  process batch 1
  publish any accepted move from batch 1
  process batch 2 against the latest topology
  publish any accepted move from batch 2
  process batch 3 against the latest topology
```

This avoids processing batch 2 against stale topology while batch 1 is still
deciding whether to publish a migration.

Full concurrent multi-batch processing may become possible later, but it
requires a separate scheduler and topology publication model. That later model
must explain how queued batch topology snapshots, completed results, accepted
moves, and migration validation are ordered when several batches finish out of
order.

Milestone 009 should not solve that broader scheduler problem.

## Result Boundaries

Provider enqueue and processing completion are different events.

The architecture should keep them separate:

```text
enqueue result:
  says whether an owned batch entered the provider queue, waited for
  backpressure, timed out, was canceled, or was rejected because the session
  faulted

processing result:
  says whether the batch processed successfully, failed validation, failed
  worker execution, failed migration, or was canceled during drain

session result:
  summarizes provider intake, queue behavior, processing completion, failures,
  cancellation, final topology version, and deterministic checksums
```

Archive replay results should not pretend processing has completed merely
because the provider successfully enqueued a retained batch.

Benchmark and CLI output should label this clearly:

```text
provider/replay time
owned snapshot time and allocation
enqueue wait time
queue drain time
processing worker time
rebalance/control-plane time
end-to-end session time
```

## Failure And Cancellation

Milestone 009 should make failure propagation explicit.

Required semantics:

```text
owned snapshot conversion failure:
  provider callback fails before enqueue and does not expose borrowed input

enqueue failure:
  provider reports rejected, timed out, canceled, or faulted intake

processing failure:
  session enters a faulted state, later enqueue attempts are rejected, and the
  final session result reports the failed batch sequence

worker failure:
  propagates through the existing async worker result contracts and faults the
  queued processing session unless a later policy explicitly handles it

rebalance failure:
  preserves existing no-partial-topology-publication semantics

cancellation:
  stops provider intake and then drains or cancels queued work according to an
  explicit shutdown mode
```

The first implementation should prefer deterministic faulting over partial
success policies. Recovery and retry can be considered after the queue and
ownership boundary are proven.

## Telemetry

Milestone 009 telemetry should extend the milestone 008 worker telemetry rather
than replacing it.

Provider decoupling should report:

```text
owned batches created
owned snapshot elapsed time
owned snapshot allocated bytes
owned snapshot payload bytes
enqueue attempts
enqueued batches
enqueue wait elapsed time
enqueue timeout/cancellation/fault counts
queue depth high-water mark
queued payload bytes high-water mark where available
dequeued batches
processing completion count
processing failure count
drain elapsed time
end-to-end provider-to-processing latency
```

Worker telemetry should remain bounded and comparable to milestone 008:

```text
dispatched batches
submitted work items
completed/succeeded/failed work items
dispatch elapsed time
queue wait elapsed time
execution elapsed time
aggregation elapsed time
barrier wait elapsed time
bounded recent failure/detail samples
```

Telemetry detail must stay bounded. Counters can be complete for the run, but
recent per-batch details should have an explicit retention cap.

## Validation

Validation should prove that decoupling did not change processing semantics.

Required validation contours:

```text
borrowed blocking sync versus owned queued sync
borrowed blocking async versus owned queued async
single-file archive replay deterministic parity
full-cache archive replay deterministic parity
owned snapshot metric parity with borrowed reference batch
queue ordering parity
topology version monotonicity
rebalance accepted/skipped decision parity where applicable
worker failure propagation
processing fault rejects later enqueue
cancellation before enqueue
cancellation while queued
cancellation while active batch is processing
bounded telemetry retention
```

Validation should compare:

```text
payload value counts
raw-value checksums
processing checksums
source snapshots where applicable
accepted move counts
skipped decision counts
failed migration counts
final topology version
worker failure counts
session status
```

The synchronous borrowed path remains the reference oracle. The owned queued
path may have different timing and allocation, but not different deterministic
processing output for the same input and policy configuration.

## Benchmark Scope

Milestone 009 benchmarks should answer a narrow question:

```text
What does safe provider decoupling cost, and where is that cost paid?
```

Required same-run comparisons:

```text
borrowed blocking synchronous processing
borrowed blocking async shard transport
owned queued synchronous processing
owned queued async shard transport
```

Archive contours should include:

```text
single-file Archive Two smoke
full local KTLX cache contour matching milestone 008 where practical
sampling-only and rebalance-session modes where practical
queue capacity 1 as a conservative backpressure contour
a larger bounded capacity contour to show provider/processing overlap
```

Benchmark output should separate:

```text
provider end-to-end time
archive replay/decompression/projection time where available
owned snapshot time
owned snapshot allocation
enqueue wait time
queue drain time
processing callback or consumer time
worker dispatch and barrier time
rebalance/control-plane time
total allocated bytes
allocated bytes per payload value
deterministic checksums
validation status
```

The milestone does not need to prove an immediate throughput win. It needs to
prove a safe owned-input boundary, bounded queue behavior, deterministic output
parity, and an honest cost model.

## In Scope

Milestone 009 should include:

```text
owned payload architecture and vocabulary
explicit retained-batch conversion guardrails
bounded in-process provider-to-processing queue
provider enqueue and processing completion result separation
processing consumer over owned RadarEventBatch values
integration with synchronous and async processing modes
rebalance integration after queued processing completion
backpressure, cancellation, fault, drain, and dispose semantics
bounded provider queue telemetry
validation against borrowed blocking reference paths
same-run benchmark contours that expose copy, queue, and processing costs
CLI options only as needed to run and report the benchmark contours
decision trace, closeout, and handoff update
```

## Out Of Scope

Milestone 009 should not implement:

```text
durable broker integration
live ingestion
cross-process workers
physical worker-local state transfer
source-level migration
partition splitting or repartitioning
concurrent multi-batch processing with out-of-order completion
topology publication from worker threads
dropping queued batches as a normal policy
unbounded queues
making owned queued processing the default execution mode
complex radar algorithms
```

The first provider decoupling milestone should stay in-process and
deterministic. Durable or live integration should build on the owned boundary
after its cost and behavior are known.

## Completion Criteria

Milestone 009 is complete when:

```text
the owned payload boundary is documented and implemented
borrowed batches cannot be queued past callback lifetime
owned batches can be enqueued and processed after callback return
the provider queue is bounded and reports backpressure
processing drains queued batches in deterministic provider sequence order
rebalance publication remains ordered and batch-boundary safe
failure and cancellation semantics are tested
queued processing validates against borrowed blocking reference paths
benchmarks report owned-copy, queue, processing, worker, and rebalance costs
the final performance assessment states whether the measured cost is acceptable
decision trace and closeout are written
handoff identifies the next milestone input
```

## Likely Next Milestone Input

If milestone 009 closes successfully, the next milestone can choose between:

```text
production configuration and tuning for owned queued processing
multi-batch concurrent processing with explicit topology publication ordering
durable broker or live-ingestion adapter over the owned-batch boundary
pooled or zero-copy ownership transfer optimization
worker-local state residency and physical shard ownership
```

The correct next step should be chosen from measured 009 results. If owned
snapshot allocation dominates, optimize ownership transfer before broader
scheduling. If queue behavior is stable and copy cost is acceptable, live or
durable provider integration becomes realistic.
