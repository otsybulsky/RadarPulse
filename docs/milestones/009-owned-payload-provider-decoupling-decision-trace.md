# Milestone 009: Decision Trace

Status: complete.

Closeout: `009-owned-payload-provider-decoupling-closeout.md`.

Performance gate:
`009-owned-payload-provider-decoupling-performance-gate.md`.

## 1. What Was Implemented

Milestone 009 added the first explicit owned-payload provider decoupling layer
on top of the milestone 008 retained async worker substrate:

- Owned snapshot guardrails around `RadarEventBatch.ToOwnedSnapshot()` and the
  leased batch builder callback boundary.
- Provider queue contracts for queue options, full/shutdown modes, provider
  sequence ids, enqueue results, dequeue results, queued batch processing
  results, queued session results, and provider queue telemetry summaries.
- `RadarProcessingOwnedBatchQueue`, a bounded in-process queue that accepts
  only owned `RadarEventBatch` values and reports deterministic enqueue,
  dequeue, close, fault, cancellation, timeout, and dispose behavior.
- Queued processing and queued rebalance sessions that drain owned batches in
  provider sequence order.
- Archive queueing publisher integration that converts leased archive replay
  batches to owned snapshots before callback return and enqueues only owned
  payloads.
- Bounded provider queue telemetry for owned snapshot copy cost, enqueue wait,
  provider-to-processing latency, queue depth high-water mark, queued payload
  bytes high-water mark, drain time, completion counters, and bounded recent
  details.
- Queued provider validation profiles that prove ownership, accepted and
  processed sequence monotonicity, completion accounting, topology version
  monotonicity, worker failure propagation, and benchmark parity against a
  borrowed blocking reference.
- Archive rebalance benchmark provider modes:
  `blocking-borrowed` and `queued-owned`.
- CLI provider controls for `processing benchmark rebalance-archive`:
  `--provider blocking-borrowed|queued-owned`, `--queue-capacity`,
  `--queue-timeout-ms`, and `--queue-telemetry none|summary|recent`.
- Release performance gate comparing blocking-borrowed sync,
  blocking-borrowed async, queued-owned sync, and queued-owned async on a
  single KTLX file and the local KTLX cache.

The borrowed `PartitionedBarrier` archive path remains the default correctness
oracle. Queued-owned is explicit, measured, and validated; it is not the hidden
default.

## 2. Decision Matrix

### Make Ownership Explicit At The Provider Boundary

Decision: convert a leased `RadarEventBatch` to an owned snapshot before it can
enter a provider-to-processing queue.

Why chosen: milestone 004 established that leased batches are valid only
during the synchronous provider callback. Milestone 008 allowed retained
workers, but still waited for borrowed work to complete before callback return.
Milestone 009 needed the next boundary: providers may enqueue retained payload
only after ownership is explicit.

Alternatives: let queued consumers retain leased payload references, copy
payloads implicitly inside lower-level processing, or change the archive
publisher callback to an async interface.

Rejected because: retaining leased references would violate the stream
contract, implicit copying would hide the largest new cost, and an async
callback still needs an ownership and backpressure contract before work can
outlive the callback.

Trade-offs/debt: copying owned snapshots is expensive. The performance gate
measured about `9.95 GB` of owned snapshot allocation on the local KTLX
full-cache contour.

Review explanation: "Borrowed input can be processed during a callback; queued
input must be owned before it leaves that callback."

### Start With A Bounded In-Process Queue

Decision: implement the first provider queue as a bounded in-process
`RadarProcessingOwnedBatchQueue`.

Why chosen: the milestone needed to prove ownership, backpressure, ordering,
completion accounting, and telemetry without introducing cross-process
transport or durability semantics.

Alternatives: use an unbounded queue, adopt a durable broker, or build live
ingestion directly.

Rejected because: unbounded queues can retain unlimited payloads, durable
brokers add persistence and operational semantics outside the milestone, and
live ingestion would mix provider decoupling with real-time scheduling.

Trade-offs/debt: the queue is process-local and not a production ingestion
broker. It is the first correctness and measurement substrate for later
pipeline work.

Review explanation: "The first queue proves the ownership contract, not a
distributed ingestion architecture."

### Accept Only Owned Batches In The Queue

Decision: reject any non-owned `RadarEventBatch` before enqueue.

Why chosen: the queue is a retention boundary. Accepting leased batches would
make the queue unsafe by construction.

Alternatives: let callers mark a leased batch as retained, clone lazily on
dequeue, or trust provider adapters to avoid mistakes.

Rejected because: a retained marker would need a separate lease protocol,
lazy clone on dequeue would still retain invalid leased memory, and trusting
callers would make the invariant unenforceable.

Trade-offs/debt: providers that want decoupling must pay the explicit owned
snapshot cost before enqueue.

Review explanation: "The queue is the guardrail: if it stores a batch, that
batch must already be owned."

### Separate Enqueue Success From Processing Completion

Decision: model enqueue outcomes and processing outcomes as separate result
families and telemetry counters.

Why chosen: provider backpressure and processing success are different
contracts. A batch can be accepted by the queue and later fail validation,
processing, or migration.

Alternatives: make enqueue return final processing status, or treat accepted
enqueue as successful processing.

Rejected because: waiting for final processing would collapse decoupling back
into callback blocking, while equating enqueue acceptance with processing
success would hide failures after retention.

Trade-offs/debt: callers now need to interpret two stages: provider handoff and
consumer drain. This is intentional and is reflected in benchmark and CLI
output.

Review explanation: "Accepted means the queue owns it; completed means
processing survived it."

### Preserve Provider Sequence Order

Decision: assign monotonic provider sequence ids and drain queued batches in
that order with one active processing batch at a time.

Why chosen: archive replay emits ordered batch boundaries. Rebalance topology
publication is only safe between completed batches. Sequential drain preserves
the existing batch and topology semantics while decoupling payload ownership.

Alternatives: process multiple queued batches concurrently, reorder by worker
availability, or let topology updates race with later queued batches.

Rejected because: concurrent multi-batch processing requires topology version
pinning, ordered publication, and multi-batch backpressure policy. Those are
pipeline scheduler decisions beyond this first provider queue.

Trade-offs/debt: the current queue validates decoupling but does not yet
exploit multi-batch overlap.

Review explanation: "Provider order is the replay order; topology changes are
published only between processed batches."

### Reuse Existing Processing And Rebalance Sessions

Decision: queued consumers call the existing processing core, async processing
session, rebalance session, and async rebalance session instead of creating a
new processing model.

Why chosen: milestones 005 through 008 already proved processing,
telemetry, migration, topology, validation, and retained async worker
semantics at the batch boundary. Provider decoupling should change payload
ownership and scheduling, not processing results.

Alternatives: build a queue-specific processing core, let the provider queue
own rebalance policy, or move processing into archive infrastructure.

Rejected because: queue-specific processing would duplicate correctness logic,
queue-owned rebalance would bypass the control plane, and archive-owned
processing would couple replay to processing internals.

Trade-offs/debt: queued-owned inherits the existing one-batch processing model.
Future producer/consumer overlap should compose with these sessions rather
than replace them.

Review explanation: "The queue changes when processing receives owned input;
it does not change what processing means."

### Keep Blocking Borrowed As The Default Benchmark And CLI Mode

Decision: add `queued-owned` as an explicit provider mode while keeping
`blocking-borrowed` as the default.

Why chosen: the borrowed path is the established correctness and performance
baseline. Queued-owned adds allocation and copy costs that should be selected
and measured intentionally.

Alternatives: make queued-owned the new default, remove the borrowed path, or
hide provider mode behind execution mode.

Rejected because: queued-owned is not yet performance-ready as a default,
borrowed remains the lowest-overhead reference, and provider ownership is
orthogonal to sync versus async execution.

Trade-offs/debt: users must choose `--provider queued-owned` for the new
contour. This is appropriate until closeout evidence justifies a default
change.

Review explanation: "Provider mode is about payload lifetime; execution mode
is about processing transport."

### Attribute Copy, Queue, Worker, And Rebalance Costs Separately

Decision: expose owned snapshot allocation/time, enqueue wait, queue drain,
queue high-water marks, worker telemetry, processing callback timing, replay
timing, and rebalance counters as separate benchmark/CLI fields.

Why chosen: provider decoupling moves cost across boundaries. The milestone
needed to show where the cost moved rather than hiding it inside aggregate
elapsed time or allocation.

Alternatives: report only end-to-end time, add one generic queued-overhead
field, or rely only on test assertions.

Rejected because: end-to-end time is dominated by archive replay, a single
overhead field would not distinguish copy from queue wait or processing
drain, and tests cannot explain production-shaped cost.

Trade-offs/debt: CLI output is more verbose for queued-owned runs. The
`--queue-telemetry` option can suppress or expand queue telemetry.

Review explanation: "Provider decoupling is acceptable only if copy,
backpressure, drain, worker, and rebalance costs are visible."

### Keep Provider Queue Telemetry Bounded

Decision: retain complete counters but keep recent provider queue details in a
bounded window.

Why chosen: long archive runs need complete totals without retaining every
batch detail or payload reference. Bounded recent detail gives examples for
diagnostics while preserving memory safety.

Alternatives: retain all queue details, drop all detail, or make detail
retention unbounded only in diagnostic mode.

Rejected because: retaining all detail is unsafe for long runs, dropping all
detail makes queue problems opaque, and unbounded diagnostic mode still risks
memory growth.

Trade-offs/debt: recent details are examples, not a full event log. A future
trace artifact can provide exact histories when needed.

Review explanation: "Counters are complete; details are bounded."

### Validate Queued Output Against Borrowed References

Decision: add queued provider validation profiles and benchmark parity checks
against borrowed blocking references.

Why chosen: provider decoupling must preserve output semantics, ordering,
topology progression, worker failure accounting, and rebalance behavior.
Borrowed blocking is the clearest oracle.

Alternatives: validate only queue invariants, rely only on archive benchmark
checksums, or skip reference comparison for performance reasons.

Rejected because: queue invariants do not prove processing parity, checksums
alone do not explain sequence/completion failures, and benchmark-profile
validation can be explicit where cost is acceptable.

Trade-offs/debt: validation profiles add surface area, but they let production
use lighter checks while benchmarks use parity checks.

Review explanation: "Queued-owned is not a new semantic model; it must match
the borrowed model."

### Defer True Producer/Consumer Overlap

Decision: make the archive benchmark queued-owned path drain after file
publish, not a fully overlapped multi-file producer/consumer pipeline.

Why chosen: this shape proves owned snapshot conversion, queue acceptance,
processing drain, validation, and telemetry without simultaneously designing
multi-file scheduling and topology publication overlap.

Alternatives: add a background processing consumer immediately, allow archive
replay to run many files ahead, or process multiple queued batches in parallel.

Rejected because: those alternatives require a broader pipeline policy:
queue sizing, cancellation, failure shutdown, topology version ordering, and
memory pressure controls across many retained owned batches.

Trade-offs/debt: queue capacity larger than 1 does not improve the current
archive benchmark. The performance gate confirmed queue depth high-water mark
stayed at `1` for both capacity `1` and `8`.

Review explanation: "This milestone proves ownership handoff first; overlap is
the next scheduler problem."

### Treat The Performance Gate As Acceptance For A Substrate, Not A Default

Decision: accept queued-owned as a correctness-preserving measurement and
validation substrate, but do not make it the default path.

Why chosen: the Release performance gate preserved deterministic parity across
single-file and full-cache contours, but also made the owned-copy cost clear.
On the local KTLX full-cache contour, queued-owned added about `9.95 GB` of
owned snapshot allocation and about `529-576 ms` of owned snapshot time.

Alternatives: reject queued-owned until copy cost is lower, or make it the new
default because correctness parity passed.

Rejected because: rejecting it would discard a safe explicit substrate for
future decoupling work, while making it default would hide a large allocation
cost and the lack of producer/consumer overlap.

Trade-offs/debt: queued-owned remains opt-in. The next optimization target is
to reduce owned snapshot allocation and add an overlapped producer/consumer
contour that can justify larger queue capacities.

Review explanation: "Correct enough to keep and measure; not cheap enough to
make default."

## 3. Preserved Invariants

Milestone 009 preserved these invariants:

```text
RadarEventBatch remains the processing input boundary.
Leased payload may not outlive the provider callback.
Only owned batches may enter the provider queue.
Provider enqueue success is distinct from processing completion.
Queued batches drain in provider sequence order.
One active queued batch is processed at a time in this milestone.
One processed batch uses one topology snapshot.
Topology publication remains between completed batches.
Rebalance does not publish after failed processing or migration.
Synchronous borrowed processing remains available as the oracle.
Async worker telemetry remains visible under queued-owned input.
Provider queue telemetry is bounded; counters remain complete for the run.
Blocking-borrowed remains the default provider mode.
```

## 4. Remaining Work

Milestone 009 intentionally leaves these items for closeout or later
milestones:

- Write the final closeout.
- Reduce owned snapshot allocation, likely through pooling, transfer/move
  semantics, or more selective retained payload representation.
- Add a true producer/consumer archive contour that overlaps replay and
  processing across files or batches.
- Define multi-batch topology version pinning and publication ordering before
  allowing concurrent queued batch processing.
- Decide whether provider queue timeout and shutdown policy need production
  CLI/configuration surfaces beyond the benchmark controls.
- Keep evaluating queued-owned under larger and more varied archive corpora
  before considering a default provider mode change.
- Defer durable broker integration, live ingestion, cross-process workers, and
  source-level migration to later milestones.
