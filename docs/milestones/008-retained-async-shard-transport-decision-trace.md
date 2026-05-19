# Milestone 008: Decision Trace

Status: implementation complete; closeout pending.

Closeout: `008-retained-async-shard-transport-closeout.md` (pending).

## 1. What Was Implemented

Milestone 008 added the first retained async shard transport over the
milestone 007 hardened synchronous processing and rebalance baseline:

- Execution mode and async execution option contracts:
  `RadarProcessingExecutionMode`, `RadarProcessingAsyncExecutionOptions`,
  worker affinity, and timeout policy.
- Worker lifecycle and health contracts for retained worker groups, workers,
  lifecycle transitions, health, failures, cancellation, and status reporting.
- Batch scope, work item, completion, and aggregation contracts for
  shard-scoped async processing.
- Bounded worker mailbox implementation with deterministic enqueue/dequeue
  semantics and lifecycle-aware completion behavior.
- Retained in-process worker group runtime with explicit start, dispatch,
  drain, stop, dispose, fault, cancellation, and timeout paths.
- Borrowed-batch guardrails that reject async processing unless the caller uses
  `RadarProcessingAsyncCoreSession` or `RadarProcessingAsyncRebalanceSession`.
- Async batch dispatcher that captures one topology snapshot, routes work
  against that snapshot, dispatches shard work, waits for a completion barrier,
  and aggregates telemetry deterministically.
- Async completion validation and benchmark comparison helpers that compare
  async processing results against synchronous reference results.
- Worker telemetry contracts and recorder for dispatch, queue wait, execution,
  aggregation, barrier wait, retained recent worker batches, and worker
  failures.
- Async processing core session and async rebalance session integration.
- Async-aware processing-only synthetic benchmark, synthetic rebalance
  benchmark, and archive rebalance benchmark surfaces.
- CLI execution controls for `processing benchmark synthetic`,
  `processing benchmark rebalance-synthetic`, and
  `processing benchmark rebalance-archive`, including worker count, queue
  capacity, and worker telemetry output.
- Full local archive cache performance guardrail for sync versus async archive
  rebalance and sampling contours.

The synchronous `PartitionedBarrier` path remains the correctness oracle. Async
execution is selectable, measured, and validated; it is not the hidden default.

## 2. Decision Matrix

### Retained Workers Before Owned Payload Snapshots

Decision: implement retained workers and bounded worker queues before adding an
owned `RadarEventBatch` snapshot or retained payload protocol.

Why chosen: milestone 008 needed to prove worker lifecycle, queueing,
dispatch, deterministic aggregation, failure propagation, and rebalance
coordination without changing the milestone 004 stream contract. Keeping
borrowed payload lifetime unchanged made the async runtime testable without
also introducing a new ownership model.

Alternatives: introduce owned batch snapshots first, implement a provider-owned
async queue, or move directly to live ingestion/broker-style input.

Rejected because: owned snapshots would broaden allocation and lifetime
semantics before worker correctness was proven; a provider-owned async queue
would require backpressure and durability decisions; live ingestion would mix
transport correctness with operational scheduling.

Trade-offs/debt: callbacks still block until the worker completion barrier
finishes. A future milestone must add an explicit owned payload or ownership
transfer protocol before callbacks can return immediately after enqueue.

Review explanation: "Workers can be retained now; borrowed payload cannot."

### Callback Blocks On The Completion Barrier

Decision: async archive and replay callbacks submit work to retained workers
but synchronously wait for all work to finish before returning.

Why chosen: `IArchiveRadarEventBatchPublisher.Publish` receives a leased
`RadarEventBatch`. The payload is valid only for that synchronous call. Waiting
inside the callback preserves the existing lifetime rule while still allowing
the execution work to run through retained workers.

Alternatives: let workers continue after callback return, copy payload data
implicitly, or change the publisher interface to async.

Rejected because: continuing after callback return would read invalid borrowed
payload; implicit copying would hide a large memory contract change; an async
publisher interface would still need an owned payload or backpressure contract
for real decoupling.

Trade-offs/debt: the provider/replay call stack is still blocked by processing.
This milestone separates the worker runtime from replay, but not the borrowed
payload lifetime from the callback.

Review explanation: "The callback can call async workers, but it cannot release
the borrowed batch until those workers are done."

### One In-Flight Borrowed Batch Per Worker Group

Decision: make one in-flight borrowed batch per worker group the first
supported async shape.

Why chosen: one in-flight batch keeps ownership, topology snapshotting, worker
completion, aggregation, and rebalance publication deterministic. It also
matches the existing batch boundary: topology version `N` is processed, then
optional topology version `N+1` is published only after completion.

Alternatives: allow multiple in-flight batches, maintain per-topology batch
pipelines, or run rebalance concurrently with later batch processing.

Rejected because: multiple in-flight borrowed batches require more complex
payload lifetime tracking, topology version pinning, ordered publication, and
pressure/rebalance semantics. Those are scheduler problems, not first-runtime
problems.

Trade-offs/debt: this shape does not exploit pipeline parallelism between
batches. It proves the local worker substrate first.

Review explanation: "One borrowed batch crosses the worker group at a time, and
it leaves before the callback exits."

### Workers Are Independent From Replay

Decision: keep worker lifecycle and dispatch in processing infrastructure, not
inside archive replay or synthetic workload providers.

Why chosen: replay publishes batches; workers execute processing work. The
runtime should be usable by synthetic workloads, archive replay, and future
live ingestion without making workers depend on an archive-specific publisher.

Alternatives: make the archive session own worker groups, let each provider
implement its own worker scheduling, or let rebalance own worker lifecycle.

Rejected because: archive-owned workers would not help synthetic or live input,
provider-specific scheduling would duplicate behavior, and rebalance should
consume completed processing telemetry rather than schedule shard work itself.

Trade-offs/debt: the current archive benchmark still constructs processors
inside replay callbacks. The worker group itself is retained across benchmark
iterations and is not coupled to archive decompression.

Review explanation: "Replay decides when a batch exists; processing decides how
that batch is executed."

### Synchronous Processing Remains The Oracle

Decision: keep synchronous partitioned processing as the reference correctness
path and validate async behavior against it where appropriate.

Why chosen: milestones 005-007 already proved processing, topology, migration,
rebalance, validation, and telemetry semantics at the synchronous batch
boundary. Async transport should not redefine those semantics.

Alternatives: make async the new default, validate async only through internal
worker invariants, or remove sync/async comparison from benchmarks.

Rejected because: default async would hide risk before closeout evidence,
worker-local invariants do not prove end-state parity, and benchmark comparison
is the clearest regression signal.

Trade-offs/debt: comparison validation costs extra work and is mainly a
benchmark/diagnostic profile tool. Production can choose lighter validation
profiles later.

Review explanation: "Async is an execution transport, not a new result model."

### Deterministic Topology Snapshot And Aggregation

Decision: async dispatch captures one topology snapshot before routing a batch
and aggregates worker completions in deterministic topology/work-item order.

Why chosen: each batch must be processed against exactly one topology version,
and async scheduling order must not affect metrics, validation, or rebalance
input.

Alternatives: let workers read the current topology dynamically, aggregate in
completion order, or let workers publish topology changes.

Rejected because: dynamic topology reads would mix versions inside one batch,
completion-order aggregation would make results scheduler-dependent, and worker
topology publication would bypass the rebalance control plane.

Trade-offs/debt: deterministic aggregation can add a small aggregation step
after worker completion. The benchmark reports this cost explicitly.

Review explanation: "Workers may finish in any order; the result is assembled
in topology order."

### Bounded Mailboxes And One-Batch Backpressure

Decision: use bounded worker mailboxes with explicit queue capacity and
lifecycle-aware enqueue/dequeue results.

Why chosen: retained workers need bounded resources and visible backpressure.
Queue capacity is part of the benchmark surface because it changes latency,
memory, and failure behavior.

Alternatives: unbounded queues, a global work queue, or immediate thread-pool
tasks without worker mailboxes.

Rejected because: unbounded queues can retain unlimited work, a global queue
weakens worker ownership diagnostics, and ad hoc thread-pool tasks do not
exercise retained worker lifecycle or queue telemetry.

Trade-offs/debt: the current CLI exposes worker count and queue capacity, but
not every lower-level scheduler option. Timeout and affinity contracts exist
and can be surfaced later if needed.

Review explanation: "Bounded queues make overload a result, not an accident."

### Failure, Cancellation, Timeout, And Health Semantics

Decision: make worker failures, cancellation, timeouts, rejected dispatches,
and unhealthy worker groups explicit result/telemetry states.

Why chosen: async execution can fail before enqueue, during work, during drain,
or at aggregation. Those cases must be distinguishable so callers can suppress
rebalance publication after failed processing and diagnose worker health.

Alternatives: throw immediately for every failure, collapse failures into a
generic invalid result, or let rebalance decide how to interpret worker errors.

Rejected because: immediate exceptions lose partial worker telemetry, generic
invalid results hide failure origin, and rebalance should not publish topology
changes after failed processing.

Trade-offs/debt: timeout policy is diagnostic and health-oriented. It does not
grant permission to release borrowed payload before completion.

Review explanation: "Failed async work must be visible before rebalance can
consider moving topology."

### Rebalance Runs After Async Completion

Decision: `RadarProcessingAsyncRebalanceSession` awaits async processing,
validates the completed result, then invokes the existing rebalance session
completion path.

Why chosen: milestone 007 rebalance depends on complete processing telemetry
for one topology snapshot. Keeping rebalance after completion preserves
pressure samples, policy state, quarantine lifecycle, migration validation, and
topology publication rules.

Alternatives: let workers run rebalance decisions, run rebalance concurrently
with processing, or let rebalance consume partial worker telemetry.

Rejected because: worker-side rebalance would split the control plane,
concurrent rebalance would race with current batch telemetry, and partial
telemetry would weaken validation and policy decisions.

Trade-offs/debt: rebalance policy remains a serial control-plane step. Future
work can optimize policy cost, but not by publishing topology before batch
completion.

Review explanation: "Workers process; rebalance decides after workers are
done."

### Worker Telemetry Is Bounded And Comparable

Decision: add bounded worker telemetry alongside existing processing and
rebalance telemetry.

Why chosen: async transport must explain dispatch time, queue wait, execution
time, aggregation time, barrier wait, submitted/completed/failed work items,
and retained recent worker failures without retaining unbounded history.

Alternatives: rely on logs, print worker metrics only in CLI, or retain every
worker batch detail.

Rejected because: logs are not structured benchmark data, CLI-only metrics do
not help library callers/tests, and unbounded worker history is unsafe for
long-running sessions.

Trade-offs/debt: retained worker detail is capped. The counters are the
complete run summary; recent detail is diagnostic sample data.

Review explanation: "Processing tells us what happened; worker telemetry tells
us where async time went."

### Benchmark Surfaces Stay Explicit

Decision: expose async through processing-only synthetic, synthetic rebalance,
and archive rebalance benchmarks with explicit execution, worker, queue,
validation, retention, and allocation output.

Why chosen: async overhead depends heavily on contour. Processing-only
synthetic isolates core execution, synthetic rebalance isolates behavior, and
archive rebalance shows the real replay/callback split.

Alternatives: benchmark only synthetic, benchmark only archive, or hide worker
telemetry behind debug output.

Rejected because: synthetic-only misses replay interaction, archive-only makes
small behavioral regressions harder to isolate, and hidden telemetry makes
performance interpretation vague.

Trade-offs/debt: single-run numbers are guardrails, not final statistical
claims. Closeout should preserve the contour labels and avoid comparing
processing-only and archive end-to-end rates directly.

Review explanation: "Every benchmark row must say what it measured and which
execution mode paid the bill."

### Performance Guardrail Interpretation

Decision: accept async archive performance because full-cache callback latency
matched synchronous performance and correctness was identical, with a small
allocation cost.

Evidence: full local KTLX cache contour for `2026-05-04`,
`--max-files 220`, `--parallelism 24`, `--iterations 1`,
`--warmup-iterations 0`.

Rebalance-session result:

```text
sync:  callback 27,427.74 ms, callback allocation 260,599,080 bytes,
       checksum 7_480_064_646_096_449_000, accepted moves 2,
       skipped decisions 392, failed migrations 0.

async: callback 27,428.21 ms, callback allocation 262,952,952 bytes,
       checksum 7_480_064_646_096_449_000, accepted moves 2,
       skipped decisions 392, failed migrations 0,
       worker failed items 0.
```

Pressure-sampling result:

```text
sync:  callback 27,512.23 ms, callback allocation 258,245,568 bytes,
       checksum 2_540_507_904_059_963_540, evaluations 198.

async: callback 27,477.16 ms, callback allocation 260,567,328 bytes,
       checksum 2_540_507_904_059_963_540, evaluations 198,
       worker failed items 0.
```

Interpretation: async retained worker transport preserved correctness and did
not introduce meaningful callback latency regression at 4 workers and queue
capacity 1. The measurable cost was about 0.9% additional processing callback
allocation from dispatch, completion, and worker telemetry machinery. End-to-
end archive timing remained dominated by replay and batch construction.

Trade-offs/debt: async did not materially speed up this full-cache contour.
The milestone value is the retained worker substrate, scheduler observability,
and future decoupling path rather than a guaranteed throughput win.

Review explanation: "The async path is correct and observable; its current
cost is small and explicit."

### CLI Surface Is Deliberately Narrow

Decision: expose execution mode, worker count, queue capacity, validation, and
worker telemetry in CLI benchmark surfaces, but not every lower-level runtime
option.

Why chosen: these options are enough to compare sync and async execution
without turning the milestone CLI into a scheduler tuning surface.

Alternatives: expose all timeout/affinity/health options immediately, or keep
async callable only through tests and library APIs.

Rejected because: exposing all runtime knobs before operational requirements
would make the CLI noisy, while hiding async from CLI would block real-data
performance guardrails.

Trade-offs/debt: timeout and worker-affinity options can be promoted later if
they become part of production or benchmark interpretation.

Review explanation: "The CLI exposes the knobs needed to prove the milestone,
not every knob the runtime owns."

### Deferred Owned Payload And Async Provider Queue

Decision: defer owned payload snapshots, a provider-level async queue,
multi-batch pipeline scheduling, durable broker integration, and live ingestion
to later milestones.

Why chosen: those features require a broader ownership and backpressure model.
Milestone 008 intentionally proves retained worker execution under the
existing borrowed-batch contract first.

Alternatives: merge retained workers, owned snapshots, and provider queues into
one milestone.

Rejected because: combining all of them would make failures hard to attribute:
payload lifetime bugs, worker bugs, topology bugs, and scheduler bugs would all
arrive at once.

Trade-offs/debt: the current runtime is not yet a fully decoupled ingestion
pipeline. It is the worker substrate that such a pipeline can use once payload
ownership is explicit.

Review explanation: "First make workers safe; then make payloads retainable."

## 3. Preserved Invariants

Milestone 008 preserved these invariants:

```text
RadarEventBatch remains the processing input boundary.
Borrowed payload is not retained past callback exit.
One batch is processed against one captured topology snapshot.
Topology publication remains between completed batches.
Rebalance does not run after failed async processing.
Synchronous processing remains available and comparable.
Worker queues are bounded.
Worker failures are surfaced through result and telemetry contracts.
Telemetry detail is bounded; counters remain complete for the run.
Benchmark rows label execution mode, validation profile, worker count, queue
capacity, and allocation contour.
```

## 4. Remaining Work

The next architecture step is not more callback blocking. It is a new payload
ownership protocol:

```text
borrowed RadarEventBatch
  -> explicit owned snapshot or ownership transfer
  -> provider queue can return before processing completes
  -> retained workers process owned payload outside the callback
  -> backpressure and cancellation become provider-level contracts
```

Other deferred work:

- Worker-local state transfer and physical shard residency.
- Multi-batch pipeline scheduling with topology publication ordering.
- Production configuration for worker counts, queue capacity, timeouts, and
  health policy.
- Live ingestion and durable broker integration.
- Broader statistical performance runs with multiple iterations and medians.

