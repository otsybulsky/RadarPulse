# Milestone 003: Decision Trace

## 1. What Was Implemented

Milestone 003 implemented the publisher-facing historical replay foundation:

- `IArchiveReplayEventPublisher` as the explicit replay event boundary.
- `ArchiveReplayPublishOptions`, publish result models, cache result models,
  and benchmark result models.
- `ArchiveReplayCountingPublisher` for deterministic validation.
- Sequential single-file replay publishing.
- Ordered parallel replay publishing.
- Cache-selection replay with non-base-data skip behavior.
- Reusable `NexradArchiveReplayPublishSession` for steady-state benchmarking.
- CLI smoke commands for `archive replay --file` and `archive replay --cache`.
- CLI benchmarks for `archive benchmark replay-publish --file` and `--cache`.
- Focused tests for ordering, counters, diagnostics, cancellation, reusable
  sessions, cache selection, and benchmark consistency.

Verified results:

```text
single file, p24: 362_695_693.02 published events/s
single file allocation: 0.07 allocated bytes/event
cache-wide KTLX, p24: 310_665_492.15 published events/s
cache-wide allocation: 0.06 allocated bytes/event
cache-wide chronology checksum: 9_060_754_844_693_896_318
```

## 2. Decision Matrix

### Explicit Publisher Contract

Decision: introduce `IArchiveReplayEventPublisher` and publish
`ArchiveTwoGateMomentEvent` values through that boundary.

Why chosen: replay needed to become a production-facing input path, not only a
benchmark or validator loop.

Alternatives: keep replay-shape as benchmark-only, call downstream engine code
directly, or return full in-memory event arrays.

Rejected because: benchmark-only code is not an integration boundary, direct
engine calls would couple milestones, and full arrays would be memory-heavy.

Trade-offs/debt: the first event shape is semantic and publisher-facing, not
the final hot-path processing-core transport.

Review explanation: "I extracted replay into an explicit publisher API so later
pipeline work could consume the same ordered event stream."

### Counting/Checksum Publisher First

Decision: make the first concrete publisher a deterministic counting/checksum
sink.

Why chosen: it validates event counts, status totals, raw checksum, calibrated
checksum, and chronology without introducing downstream engine behavior.

Alternatives: implement an alerting/processing publisher immediately or write
events to a durable broker.

Rejected because: those choices would add unproven downstream concerns before
the replay boundary itself was validated.

Trade-offs/debt: milestone 003 proves publisher-path throughput, not full
processing latency.

Review explanation: "The first publisher was intentionally boring: it made
correctness and ordering measurable before adding behavior."

### Ordered Parallel Replay

Decision: allow workers to decompress/project records concurrently, but publish
or aggregate only in original source order.

Why chosen: parallelism is needed for throughput, while historical replay must
remain deterministic.

Alternatives: publish directly from workers, run everything sequentially, or
sort by message timestamp after projection.

Rejected because: worker completion order is not replay order, sequential-only
underuses the machine, and timestamp sorting is not the source-position
contract.

Trade-offs/debt: custom publishers need per-record buffering before ordered
drain; count-only publishing can use ordered accumulator merge.

Review explanation: "The implementation gets parallel speed without letting
parallel completion order leak into replay semantics."

### Reusable Publish Session For Benchmarks

Decision: add `NexradArchiveReplayPublishSession` so benchmark loops reuse
workers, decompressor sessions, projectors, accumulators, and buffers.

Why chosen: the benchmark should measure steady-state replay publishing, not
per-command or per-iteration setup churn.

Alternatives: benchmark the one-shot publisher API only or keep using external
process timing.

Rejected because: one-shot/process timing mixed setup cost with replay cost and
obscured the real steady-state throughput.

Trade-offs/debt: the reusable session is more complex than the one-shot API and
must be disposed correctly.

Review explanation: "I separated smoke testing from steady-state benchmarking,
so the performance numbers describe the replay path itself."

### Cache Replay As Count-Only Aggregation

Decision: implement cache-selection replay using the reusable session and
aggregate per-file publish results in selected cache order.

Why chosen: the milestone needed to prove replay over a realistic local corpus
without buffering billions of events.

Alternatives: publish every cache event to a custom external sink, or restrict
the milestone to single-file replay.

Rejected because: full event buffering is impractical, and single-file replay
does not prove corpus selection/skip/aggregation behavior.

Trade-offs/debt: cache replay in milestone 003 is validation/count focused, not
a full downstream processing run.

Review explanation: "Cache replay proves the same ordered contract over a real
corpus while keeping memory bounded."

### Preserve Chronology Checksum As A Gate

Decision: keep the order-sensitive chronology checksum as the required
sequential/parallel equivalence check.

Why chosen: commutative totals can match even when event order is wrong.
Chronology checksum detects order changes.

Alternatives: compare only counts and value checksums, or rely on visual/manual
inspection.

Rejected because: neither catches ordering regressions reliably.

Trade-offs/debt: checksum logic must be treated as part of the replay
verification surface.

Review explanation: "I used an order-sensitive checksum so parallel replay is
validated for ordering, not just totals."

### Performance Decisions

Decision: measure publisher-path performance with an in-process reusable
session and count-only publisher, while preserving the one-shot publisher API
for simple use.

Why chosen: external CLI timing and per-iteration setup made throughput numbers
noisy. Reusing workers, decompressor sessions, projectors, accumulators, and
buffers made the benchmark describe steady-state replay publishing.

Alternatives: keep only process-level smoke timings, benchmark only the older
replay-shape loop, or push every event through a custom downstream sink.

Rejected because: process timing includes startup/setup cost, replay-shape does
not prove the publisher boundary, and arbitrary sinks would measure downstream
behavior rather than replay publication.

Trade-offs/debt: count-only throughput is not full engine throughput. Remaining
allocation sources include descriptor/metadata arrays, scheduling overhead, and
custom-publisher per-record buffers.

Review explanation: "I separated smoke commands from steady-state benchmarks,
which let me prove the publisher path itself could exceed 300M events/s without
claiming that number as downstream engine throughput."

## 3. Remaining Risks And Debt

- `ArchiveTwoGateMomentEvent` is a semantic publisher-facing shape, not the
  final high-rate processing-core transport. Milestone 004 later addressed this
  with normalized batches.
- Older replay-shape benchmarks still have separate projection loops.
- Remaining allocation contributors include record descriptor/metadata arrays,
  scheduling infrastructure, and per-record buffers in custom publisher paths.
- Milestone 003 does not implement downstream processing, partitioning, durable
  broker publishing, or live ingestion.

## 4. Portfolio Review Summary

Milestone 003 converted the decoder foundation into an explicit historical
replay publisher path. The main decisions were a clear publisher contract,
counting/checksum validation first, ordered parallel replay, reusable benchmark
sessions, cache-wide count aggregation, and chronology checksum verification.
This gave the project a correct, measured, publisher-facing replay boundary
before designing the lower-level processing-core stream in milestone 004.
