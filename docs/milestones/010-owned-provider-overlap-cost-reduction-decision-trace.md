# Milestone 010: Decision Trace

Status: complete.

Closeout: `010-owned-provider-overlap-cost-reduction-closeout.md` (pending).

Performance gate:
`010-owned-provider-overlap-cost-reduction-performance-gate.md`.

## 1. What Was Implemented

Milestone 010 made the milestone 009 `queued-owned` provider boundary cheaper,
resource-owned, and measurably overlapped while preserving the borrowed
blocking reference path:

- Retained payload strategy contracts for `snapshot-copy`, `pooled-copy`, and
  guarded unsupported `builder-transfer`.
- Explicit retained resource lifecycle contracts and release telemetry.
- `pooled-copy` retained payload implementation that keeps owned input valid
  through processing and releases retained resources after final use.
- Retained-byte-aware provider queue accounting and high-water telemetry.
- Producer/consumer archive overlap runner with producer, consumer, queue,
  retention, allocation, and overlap summaries.
- Ordered consumer integration that drains retained batches by provider
  sequence and preserves rebalance topology publication between completed
  batches.
- Optimized queued validation that keeps borrowed-reference parity as the
  correctness oracle.
- Archive benchmark and CLI controls for provider overlap, retention strategy,
  retained-byte budget, overlap telemetry, and controlled consumer delay.
- Cache-level producer pipeline that can publish a selected cache file set
  through one shared overlap runner.
- Controlled queue-ahead proof contour that slows the consumer after dequeue
  to prove bounded queued-ahead behavior without treating the delay as a
  production throughput result.
- Release performance gates for initial, repeated cache-level, and controlled
  queued-ahead contours.

The default provider mode remains `blocking-borrowed`. `queued-owned` is an
explicit validation, measurement, and optimized benchmark contour.

## 2. Decision Matrix

### Keep Blocking Borrowed As Default

Decision: keep `blocking-borrowed` as the default provider mode and same-run
oracle.

Why chosen: it is still the lowest-risk reference path for borrowed archive
callback lifetime and remains the simplest correctness baseline.

Alternatives: switch the default to `queued-owned`, hide the provider choice
behind execution mode, or remove the borrowed path.

Rejected because: `queued-owned` now has credible optimized contours, but it
still introduces retained-resource pressure, queue policy, and run-to-run
performance questions that should stay explicit before a default change.

Trade-offs/debt: users must select `--provider queued-owned` to exercise the
owned provider path. That is intentional until repeated production-shaped runs
justify a default-mode change.

Review explanation: "Borrowed remains the oracle; owned remains the measured
pipeline."

### Accept Pooled Copy As The First Optimized Retention Strategy

Decision: accept `pooled-copy` as the first lower-allocation retained payload
strategy and keep `snapshot-copy` as the compatibility comparison.

Why chosen: `pooled-copy` preserves the owned `RadarEventBatch` processing
contract while dramatically reducing retained allocation in the non-overlapped
cache contour: `9_947_507_832` bytes for snapshot-copy down to `102_811_264`
bytes for pooled-copy, about a `98.97%` reduction.

Alternatives: make builder transfer the first optimized strategy, weaken
immutability/lifetime boundaries, or keep only snapshot-copy.

Rejected because: builder transfer requires a stronger proof that provider
builders will not reuse moved storage; weakening lifetime boundaries would
violate the stream contract; snapshot-only retention leaves the milestone 009
allocation problem unresolved.

Trade-offs/debt: pooled-copy can allocate more in real overlap than in
non-overlap because more retained buffers are in flight before reuse. In the
repeated overlapped gate, pooled-copy retained allocation was about
`1_971_376_296` bytes versus `9_947_502_792` bytes for snapshot-copy, still
about an `80.18%` reduction on the same overlap shape.

Review explanation: "Pooled-copy is not zero-copy; it is the first safe lower
allocation owned strategy."

### Keep Resource Ownership Explicit

Decision: represent retained resources explicitly and release them after
processing, validation, and telemetry capture finish.

Why chosen: retained payload storage crosses the provider callback boundary.
The system needs a clear owner at enqueue, dequeue, consumer processing, and
release.

Alternatives: rely on GC, release immediately after dequeue, or let processing
internals own archive-retained storage.

Rejected because: GC does not give bounded retained pressure, release after
dequeue can invalidate processing input, and processing internals should not
own archive storage policy.

Trade-offs/debt: retained lifecycle adds more telemetry and failure states, but
it makes cleanup auditable. The gates reported clean cleanup: pooled runs
released all retained batches with `0` failed releases.

Review explanation: "If a queue retains payload, the release path must be just
as explicit as the enqueue path."

### Bound The Provider Queue By Items And Retained Bytes

Decision: keep item capacity required and add optional retained-byte capacity
as a visible backpressure dimension.

Why chosen: queue length alone does not describe retained payload pressure.
Large radar batches can make a shallow queue expensive.

Alternatives: item capacity only, retained bytes only, or unbounded retained
payload growth.

Rejected because: item-only limits hide payload size, byte-only limits do not
control scheduling depth, and unbounded growth is not acceptable for retained
owned input.

Trade-offs/debt: current high-water telemetry reports queued pending retained
bytes. It does not yet include consumer in-flight retained resources after
dequeue.

Review explanation: "A bounded queue needs both how many batches and how many
retained bytes."

### Preserve Ordered Rebalance Consumption

Decision: process queued-owned overlap with one ordered consumer and one active
rebalance-enabled batch at a time.

Why chosen: rebalance topology publication is deterministic only when batch N
finishes before batch N+1 captures its topology.

Alternatives: process multiple queued batches concurrently, let later batches
capture topology at enqueue time, or publish topology from worker threads.

Rejected because: those alternatives require a new ordered commit barrier,
stale-topology policy, and multi-batch rebalance semantics outside this
milestone.

Trade-offs/debt: producer/consumer overlap can improve replay and processing
stage overlap, but it does not yet make multiple rebalance batches execute
concurrently.

Review explanation: "Producer may run ahead; rebalance commits still happen in
provider order."

### Move From Per-File Overlap To Cache-Level Overlap

Decision: change the cache overlap contour to use one shared overlap runner
for the selected cache file set.

Why chosen: the initial performance gate showed producer/consumer task overlap
but no useful cache-level pipeline. The previous shape invoked the overlap
runner one file at a time, so the queue depth high watermark stayed `1`.

Alternatives: keep per-file overlap as the only archive contour, inflate queue
capacity, or claim overlap from task lifetime alone.

Rejected because: per-file overlap cannot prove multi-file producer run-ahead,
capacity does not help if the runner drains per file, and task lifetime overlap
alone is too weak for the milestone goal.

Trade-offs/debt: cache-level overlap increases retained in-flight pressure and
needs better in-flight high-water telemetry before a default-mode decision.

Review explanation: "The queue can only prove pipeline behavior when the
producer and consumer share one run across files."

### Treat Natural Cache-Level Overlap As Throughput-Useful

Decision: accept the repeated cache-level pooled overlap contour as
throughput-useful evidence, but not as default-readiness evidence.

Why chosen: after slice 11, the best repeated `queued-owned + pooled-copy +
producer-consumer` contour ran in `14_947.99 ms`, compared with
`16_915.80 ms` for borrowed async and `17_158.62 ms` for non-overlapped
queued-owned pooled-copy on the same KTLX input.

Alternatives: ignore the improvement because queue depth stayed `1`, or treat
the improvement as enough to change defaults.

Rejected because: the wall-clock improvement is real and should be recorded,
but queue depth `1`, no natural `HasQueuedAheadOverlap`, and incomplete
in-flight retained pressure telemetry are not enough for a default change.

Trade-offs/debt: this contour proves useful producer/consumer stage overlap,
not deep queued buffering under natural data.

Review explanation: "Natural overlap improved wall-clock time; it did not
prove queued-ahead buffering."

### Add Controlled Consumer Delay Only As A Proof Tool

Decision: add `--overlap-consumer-delay-ms` as a benchmark-only control that
slows the consumer after dequeue for producer-consumer overlap runs.

Why chosen: the natural cache contour did not fill the queue. A controlled slow
consumer proves that the producer can run ahead, hit bounded backpressure, and
still preserve validation and cleanup.

Alternatives: leave queued-ahead unproven, slow the producer, or treat test
doubles as sufficient.

Rejected because: leaving it unproven keeps an ambiguity in the milestone,
slowing the producer cannot fill the queue, and a real CLI/cache contour is a
stronger proof than unit-only behavior.

Trade-offs/debt: the delayed contour is not a production throughput benchmark.
It deliberately adds time: the full-cache control used `150 ms` per retained
batch across `220` published files.

Review explanation: "The delay proves queue-ahead mechanics, not production
speed."

### Accept Controlled Queue-Ahead Proof

Decision: accept the controlled delay contours as proof that bounded
queued-ahead overlap works mechanically.

Why chosen: the `max-files 32` control reached queue depth `8`, reported
`HasQueuedAheadOverlap = yes`, released `29` retained batches, and had `0`
failed releases. The full-cache `data\nexrad` control examined `244` files,
published `220` files, reached queue depth `8`, reported
`HasQueuedAheadOverlap = yes`, released `220` retained batches, and had `0`
failed releases.

Alternatives: require natural data to fill the queue before accepting the
mechanics, or mark queued-ahead as entirely unproven.

Rejected because: natural data may not make the consumer slower than replay on
this machine, while the controlled contour isolates the scheduler and
backpressure behavior under the required slow-consumer condition.

Trade-offs/debt: natural full-cache queued-ahead remains unproven without
synthetic delay. That is a workload signal, not a scheduler failure.

Review explanation: "When the consumer is slower, the queue fills, backpressure
applies, validation holds, and resources release."

### Keep Builder Transfer Unsupported

Decision: expose `builder-transfer` in the strategy vocabulary but reject it in
archive benchmark execution.

Why chosen: transfer semantics are useful architecture vocabulary, but the
implementation has not proven safe builder ownership transfer.

Alternatives: hide the strategy entirely or implement optimistic transfer
without full ownership proof.

Rejected because: hiding it loses a future direction, while optimistic transfer
risks use-after-release or builder-reuse bugs.

Trade-offs/debt: future milestones can implement transfer when construction
and reuse boundaries are explicit enough.

Review explanation: "Name the strategy now; do not pretend it is implemented."

### Keep Telemetry Verbose But Bounded

Decision: keep CLI and benchmark output explicit for provider mode, retention,
queue, overlap, allocation, worker, validation, rebalance, and cleanup fields.

Why chosen: milestone 010 moves cost between replay, retention, queueing,
processing, and overlap. Aggregate elapsed time alone cannot explain whether a
contour is cheaper, overlapped, or merely shifting work.

Alternatives: report only end-to-end time, hide retention allocation in replay
allocation, or omit queue and overlap counters unless recent telemetry is
enabled.

Rejected because: those alternatives would make weak overlap claims and hidden
allocation regressions too easy.

Trade-offs/debt: CLI output is long for diagnostic contours. Summary/recent
telemetry controls keep detail bounded.

Review explanation: "If the provider path changes lifetime and memory
pressure, the benchmark must say where the cost went."

### Do Not Change Defaults In Milestone 010

Decision: do not make `queued-owned`, `pooled-copy`, or producer-consumer
overlap the default in this milestone.

Why chosen: the optimized contour is promising and sometimes faster, but
default readiness needs repeated Release evidence, larger and varied corpora,
and better in-flight retained pressure telemetry.

Alternatives: default to the fastest measured contour, default to pooled-copy
only, or make producer-consumer overlap automatic for cache input.

Rejected because: one milestone gate is not enough to change default provider
lifetime behavior, and the controlled delay proof is explicitly synthetic.

Trade-offs/debt: the optimized path remains opt-in. That keeps production
behavior conservative while preserving a strong benchmark surface.

Review explanation: "Milestone 010 proves the path; a later milestone can
decide whether it becomes the path."

## 3. Performance Decisions

The milestone accepts these performance interpretations:

```text
pooled-copy non-overlap retained allocation:
  snapshot-copy: 9_947_507_832 bytes
  pooled-copy:     102_811_264 bytes
  interpretation: about 98.97% retained allocation reduction

repeated natural cache-level overlap:
  borrowed async:                    16_915.80 ms
  queued-owned pooled non-overlap:   17_158.62 ms
  queued-owned pooled overlap cap 8: 14_947.99 ms
  interpretation: useful wall-clock producer/consumer overlap

repeated natural queue-ahead signal:
  queue depth high watermark: 1
  HasQueuedAheadOverlap: no
  interpretation: natural data did not produce deep queued buffering

controlled full-cache queue-ahead signal:
  examined files: 244
  published files: 220
  queue depth high watermark: 8
  HasQueuedAheadOverlap: yes
  released retained batches: 220
  failed releases: 0
  interpretation: bounded queue-ahead mechanics work under slow consumer
```

These numbers support keeping the optimized contour and finishing the
milestone, but not changing provider defaults.

## 4. Preserved Invariants

Milestone 010 preserved these invariants:

```text
RadarEventBatch remains the processing input boundary.
Leased payload may not outlive the provider callback.
Only owned or retained-owned input may enter the provider queue.
Provider enqueue success remains distinct from processing completion.
Queued batches drain in provider sequence order.
One rebalance-enabled batch is processed and committed at a time.
Queued batches capture topology at processing time, not enqueue time.
Accepted topology changes publish only after successful processing.
Failed processing prevents later success claims.
Retained resources release after final use.
Provider queue and overlap telemetry remain bounded.
Borrowed blocking remains the correctness oracle and default provider mode.
Queued-owned remains opt-in.
```

## 5. Remaining Work

Milestone 010 intentionally leaves these items for closeout or later
milestones:

- Write the final closeout.
- Update handoff for the next milestone after closeout.
- Repeat natural Release gates across more runs and larger or different cache
  corpora before considering default-mode changes.
- Add in-flight retained-resource high-water telemetry so memory pressure after
  dequeue is visible alongside queued pending retained bytes.
- Decide whether the next milestone should focus on default-readiness,
  production configuration, live/durable ingestion, or ordered concurrent
  processing.
- Keep `builder-transfer` unsupported until ownership transfer can be proven.
- Defer durable broker integration, live ingestion, cross-process provider
  transport, concurrent rebalance-enabled processing, source-level migration,
  partition splitting, and complex radar algorithms to future milestones.
