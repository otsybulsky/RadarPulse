# Milestone 009: Closeout

## Status

Milestone 009 is complete.

RadarPulse now has the first explicit owned-payload provider decoupling
substrate between archive replay providers and processing. Borrowed archive
callbacks remain safe and blocking by default, while `queued-owned` runs
convert leased `RadarEventBatch` values into owned snapshots before enqueueing
them into a bounded provider-to-processing queue.

The important lifetime boundary is now explicit:

```text
leased payload may be processed only inside the provider callback.
owned payload may be retained, queued, and processed after enqueue.
```

Queued-owned is intentionally opt-in. It is accepted as a correctness-preserving
measurement and validation substrate, but not as the default path yet because
the performance gate exposed substantial owned-copy allocation cost and no
producer/consumer overlap in the current archive benchmark shape.

## Final Outcome

Implemented:

- Owned snapshot guardrails proving leased `RadarEventBatch.ToOwnedSnapshot()`
  preserves stream metadata, dictionary/source universe versions, events,
  payload bytes, precomputed metrics, owned lifetime, and buffer-reuse
  stability.
- Provider queue domain contracts:
  `RadarProcessingProviderQueueOptions`, full/shutdown modes, provider sequence
  ids, queued batch shape, enqueue/dequeue results, processing results, queued
  session results, and queue telemetry summaries.
- Bounded in-process `RadarProcessingOwnedBatchQueue` that accepts only owned
  batches, enforces capacity, supports wait/full behavior, closes/faults
  deterministically, drains accepted items, and reports queue telemetry.
- Queued processing session over owned batches, including synchronous and
  milestone 008 async processing execution.
- Queued rebalance session over owned batches, preserving topology publication
  ordering between processed batches.
- Archive queueing publisher that converts leased archive replay batches to
  owned snapshots before callback return and enqueues only owned input.
- Provider queue telemetry recorder, recent detail model, owned snapshot
  allocation summary, bounded retention, dropped detail counters, queue
  high-water marks, provider-to-processing latency, drain timing, and
  completion counters.
- Queued provider validation profiles and benchmark reference comparison
  helpers.
- Archive rebalance benchmark provider modes:
  `blocking-borrowed` and `queued-owned`.
- Archive benchmark result surfaces for provider mode, queue capacity, queue
  telemetry, owned snapshot allocation/time, enqueue wait, queue drain, worker
  telemetry, and rebalance parity.
- CLI provider controls for `processing benchmark rebalance-archive`:
  `--provider blocking-borrowed|queued-owned`, `--queue-capacity`,
  `--queue-timeout-ms`, and `--queue-telemetry none|summary|recent`.
- Release performance gate covering single-file and full local KTLX cache
  contours across blocking-borrowed sync, blocking-borrowed async,
  queued-owned sync, queued-owned async, and queue capacities `1` and `8`.
- Architecture document, implementation plan, performance gate, decision
  trace, closeout, and handoff update.

Not implemented:

- Queued-owned as the default provider mode.
- True producer/consumer overlap between archive replay and processing.
- Multiple active queued processing batches.
- Topology version pinning for concurrent queued batches.
- Buffer pooling, move/transfer semantics, or lower-allocation owned payload
  representation.
- Durable broker integration, live ingestion, cross-process workers, or
  distributed provider queues.
- Physical worker-local state transfer.
- Source-level migration, partition splitting, or repartitioning.
- Complex radar algorithms.

## Completion Checklist

```text
[x] owned snapshot guardrails are strengthened and tested
[x] provider queue contracts are implemented and tested
[x] bounded owned batch queue is implemented and tested
[x] queued processing consumer is implemented and tested
[x] queued rebalance consumer is implemented and tested
[x] archive provider adapter converts leased input to owned before enqueue
[x] provider enqueue and processing completion results are separated
[x] queue telemetry is bounded and tested
[x] queued validation proves parity against borrowed blocking references
[x] archive benchmark exposes blocking-borrowed and queued-owned provider modes
[x] CLI exposes provider mode, queue capacity, and queued telemetry options
[x] same-run single-file comparisons are captured
[x] same-run full-cache comparisons are captured where local data is available
[x] performance assessment interprets owned-copy, queue, worker, and rebalance costs
[x] decision trace is written
[x] closeout is written
[x] handoff is updated for the next milestone
```

## Final Verification

Latest implementation verification captured before closeout:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
660 passed, 3 skipped for the full test project.
```

Focused verification captured during the milestone:

```text
11 passed for RadarEventBatchBuilderTests.
11 passed for provider queue contract coverage.
11 passed for owned batch queue coverage.
6 passed for queued processing session coverage.
6 passed for queued rebalance session coverage.
4 passed for archive owned queueing publisher coverage.
3 passed for provider queue telemetry recorder coverage.
10 passed for queued provider validator coverage.
8 passed for archive rebalance benchmark queued-provider coverage.
16 passed for CLI rebalance benchmark coverage.
97 passed, 3 skipped for Archive-focused coverage.
487 passed for Processing-focused coverage.
```

Release performance gate build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Recorded result:

```text
Release build succeeded with 0 warnings and 0 errors.
```

## Performance Gate Summary

The Release performance gate is captured in
`009-owned-payload-provider-decoupling-performance-gate.md`.

Single-file contour:

```text
input: data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
payload values: 38_759_040
validation checksum: 3_750_039_633_875_006_276
accepted moves: 1
skipped decisions: 0
failed migrations: 0
```

Full-cache contour:

```text
input: data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
examined files: 220
skipped files: 22
published files: 198
payload values: 7_660_888_320
validation checksum: 7_480_064_646_096_449_000
accepted moves: 2
skipped decisions: 392
failed migrations: 0
```

Key measurements:

```text
blocking-borrowed sync full-cache end-to-end: 16_957.99 ms
blocking-borrowed async full-cache end-to-end: 16_955.62 ms
queued-owned sync q1 full-cache end-to-end: 17_986.70 ms
queued-owned async q1 full-cache end-to-end: 17_587.79 ms
queued-owned sync q8 full-cache end-to-end: 17_971.88 ms
queued-owned async q8 full-cache end-to-end: 17_569.64 ms

queued-owned full-cache owned snapshot allocation: about 9.95 GB
queued-owned full-cache owned snapshot elapsed: about 529-576 ms
queued-owned full-cache enqueue wait: about 2 ms total
queued-owned full-cache queue depth high-water mark: 1 for capacity 1 and 8
```

Interpretation:

- Queued-owned preserved deterministic parity against the borrowed reference
  across provider and execution modes.
- Owned snapshot allocation is the dominant cost. It is explicit and measured,
  not hidden in processing callback allocation.
- Provider enqueue wait is negligible on the measured archive contours.
- Queue capacity `8` did not improve throughput because the current benchmark
  drains after each file publish; it validates bounded behavior but does not
  create replay/processing overlap.
- Queued-owned is acceptable as an explicit validation and measurement mode,
  but not as the default path until owned-copy allocation and overlap strategy
  improve.

## Decision Trace

The decision trace is written in
`009-owned-payload-provider-decoupling-decision-trace.md`.

Important decisions:

- Ownership is explicit at the provider boundary.
- The first queue is bounded and in-process.
- The provider queue accepts only owned batches.
- Enqueue success is distinct from processing completion.
- Queued batches drain in provider sequence order.
- Existing processing and rebalance sessions remain the semantic model.
- Blocking-borrowed remains the default provider mode.
- Copy, queue, worker, and rebalance costs are attributed separately.
- Provider queue telemetry is bounded.
- Queued output is validated against borrowed references.
- True producer/consumer overlap is deferred.
- Queued-owned is accepted as a substrate, not a default.

## Next Milestone Input

Milestone 010 should start from the fact that the owned provider boundary is
correct and measurable, but expensive.

Recommended focus:

- Reduce owned snapshot allocation with pooling, transfer/move semantics, or a
  more selective retained payload representation.
- Add a true producer/consumer benchmark contour that overlaps archive replay
  and processing across files or batches.
- Define topology version pinning and publication ordering for more than one
  active queued batch.
- Keep `blocking-borrowed` as the default provider mode until queued-owned
  throughput and allocation are improved.
- Preserve the borrowed-reference parity checks for any new overlapped queue
  shape.

Deferred beyond the immediate next milestone unless explicitly reprioritized:

- Durable queue or broker integration.
- Live ingestion.
- Cross-process/distributed workers.
- Source-level migration and partition splitting.
- Physical worker-local state transfer.
