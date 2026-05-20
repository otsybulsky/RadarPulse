# Milestone 010: Closeout

## Status

Milestone 010 is complete.

RadarPulse now has an optimized, explicit, resource-owned provider overlap
contour on top of the milestone 009 owned provider boundary. The default
archive provider remains `blocking-borrowed`; `queued-owned` remains opt-in and
now supports lower-allocation retained payloads, cache-level producer/consumer
overlap, retained-byte queue limits, overlap telemetry, and controlled
queue-ahead proof.

The important milestone result is:

```text
009 proved owned provider handoff is safe and measurable.
010 proved owned provider handoff can be much cheaper and genuinely overlapped.
```

The milestone does not make queued-owned the default. The optimized path is
accepted as a credible benchmark and validation contour, not as a production
default.

## Final Outcome

Implemented:

- Retained payload strategy contracts for `snapshot-copy`, `pooled-copy`, and
  guarded unsupported `builder-transfer`.
- Explicit retained resource lifecycle contracts, release status, release
  failure counters, and cleanup telemetry.
- `pooled-copy` retained payload implementation that keeps owned payload valid
  until processing, validation, telemetry capture, and release complete.
- Provider queue retained-byte accounting and retained-byte high-water
  telemetry.
- Producer/consumer archive overlap runner with producer, consumer, queue,
  retention, allocation, and overlap telemetry.
- Ordered queued consumer integration that preserves provider sequence order
  and rebalance topology publication boundaries.
- Optimized queued validation against the borrowed blocking reference.
- Archive benchmark and CLI controls:
  `--provider-overlap`, `--retention-strategy`, `--queue-retained-bytes`,
  `--overlap-telemetry`, and `--overlap-consumer-delay-ms`.
- Cache-level producer pipeline that uses one shared overlap runner across the
  selected cache file set.
- Controlled consumer-delay contour proving queued-ahead overlap mechanics
  under a slow consumer.
- Release performance gate, repeated cache-level gate, controlled queue-ahead
  proof, decision trace, closeout, and handoff updates.

Not implemented:

- `queued-owned` as the default provider mode.
- Natural full-cache queued-ahead overlap without synthetic consumer delay.
- In-flight retained-resource high-water telemetry after dequeue.
- `builder-transfer` retained payload execution.
- Multiple active rebalance-enabled processing batches.
- Ordered concurrent rebalance commit barrier.
- Durable queue or broker integration.
- Live ingestion.
- Cross-process provider or worker transport.
- Source-level migration, partition splitting, or repartitioning.
- Complex radar algorithms.

## Completion Checklist

```text
[x] milestone 009 cost anchors are confirmed and preserved as comparison fields
[x] retained payload strategy contracts are implemented and tested
[x] retained resource lifecycle is implemented and tested
[x] one lower-allocation retained payload strategy is implemented and tested
[x] retained-byte-aware queue window is implemented and tested where practical
[x] producer/consumer archive overlap runner is implemented and tested
[x] ordered consumer topology pinning rules are implemented and tested
[x] overlap telemetry and allocation attribution are implemented and tested
[x] optimized queued validation proves borrowed-reference parity
[x] archive benchmark exposes retention strategy and overlap contours
[x] CLI exposes retention strategy, overlap mode, and retained-byte controls
[x] same-run single-file compatibility comparisons are captured
[x] same-run full-cache overlap comparisons are captured where local data exists
[x] performance assessment interprets allocation, overlap, queue, worker,
    validation, rebalance, and resource lifecycle costs
[x] cache-level producer pipeline is implemented and tested
[x] cache-level producer pipeline proves useful wall-clock overlap
[x] repeated performance gate captures the cache-level overlap contour
[x] controlled consumer-delay contour proves queued-ahead overlap
[ ] natural full-cache contour proves queued-ahead overlap without synthetic delay
[x] decision trace is written
[x] closeout is written
[ ] handoff is updated for the next milestone
```

The unchecked natural queued-ahead item is explicitly deferred. The controlled
consumer-delay contour proves the queue-ahead mechanism, but the natural
full-cache contour on the current local data stayed at queue depth `1`.

## Final Verification

Latest full implementation verification captured before closeout:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
704 passed, 3 skipped for the full test project.
```

Focused verification captured for the final controlled-overlap slice:

```text
4 passed for focused CLI/archive controlled-delay coverage.
42 passed for CLI rebalance benchmark, archive benchmark, and overlap runner
coverage.
```

Release build captured before the final controlled performance contour:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Recorded result:

```text
Release build succeeded with 0 warnings and 0 errors.
```

## Performance Gate Summary

The full gate is captured in
`010-owned-provider-overlap-cost-reduction-performance-gate.md`.

### Allocation Reduction

Non-overlapped full-cache retained allocation:

```text
snapshot-copy retained allocation: 9_947_507_832 bytes
pooled-copy retained allocation:     102_811_264 bytes
reduction: about 98.97%
```

Interpretation:

- Pooled-copy solves the milestone 009 retained snapshot allocation problem for
  the non-overlapped contour.
- Snapshot-copy remains available as the compatibility comparison.
- Builder-transfer remains unsupported until ownership transfer is proven.

### Natural Cache-Level Overlap

Repeated Release KTLX contour:

```text
input: data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
published files: 198
payload values: 7_660_888_320
validation checksum: 7_480_064_646_096_449_000
accepted moves: 2
skipped decisions: 392
failed migrations: 0
```

Key measurements:

```text
blocking-borrowed async:                    16_915.80 ms
queued-owned pooled-copy non-overlap:       17_158.62 ms
queued-owned pooled-copy overlap capacity 8: 14_947.99 ms

natural overlap queue depth high watermark: 1
natural HasQueuedAheadOverlap: no
pooled overlap released batches: 198
pooled overlap failed releases: 0
```

Interpretation:

- Cache-level producer/consumer overlap is throughput-useful on the measured
  natural KTLX contour.
- The best pooled overlap contour was about `1_967.81 ms` faster than borrowed
  async and about `2_210.63 ms` faster than non-overlapped queued-owned
  pooled-copy.
- Natural data did not create deep queued buffering; the queue depth high-water
  mark stayed `1`.
- This supports keeping the optimized contour, but not changing defaults.

### Controlled Queue-Ahead Proof

Controlled full-cache contour:

```text
input: data\nexrad --max-files 1000000
consumer delay: 150 ms per dequeued retained batch
examined files: 244
skipped files: 24
published files: 220
payload values: 8_513_587_200
validation checksum: 12_759_860_675_563_334_608
accepted moves: 2
skipped decisions: 436
failed migrations: 0
```

Key measurements:

```text
end-to-end elapsed: 37_967.62 ms
queue depth high watermark: 8
HasQueuedAheadOverlap: yes
retained payload bytes high watermark: 386_058_240
retained byte budget: 536_870_912
provider blocked: 16_548.62 ms
consumer idle: 422.86 ms
released retained batches: 220
failed releases: 0
```

Interpretation:

- The queue-ahead mechanism works when the consumer is slower than archive
  replay.
- Bounded backpressure works: the queue reached capacity `8`, provider blocked
  time became visible, and retained bytes stayed below the configured budget.
- Correctness and cleanup held across the full local cache.
- This is a controlled mechanics proof, not a production throughput result.

## Decision Trace

The decision trace is written in
`010-owned-provider-overlap-cost-reduction-decision-trace.md`.

Important decisions:

- `blocking-borrowed` remains the default and same-run oracle.
- `pooled-copy` is accepted as the first optimized retained payload strategy.
- Retained resource ownership and release lifecycle remain explicit.
- Provider queues are bounded by item count and retained bytes.
- Rebalance consumption remains ordered by provider sequence.
- Cache-level overlap uses one shared runner across selected files.
- Natural cache-level overlap is accepted as wall-clock useful.
- Controlled consumer delay is accepted only as a queue-ahead proof tool.
- Controlled queued-ahead proof is accepted mechanically.
- `builder-transfer` remains guarded unsupported.
- Telemetry remains verbose enough to explain cost movement and bounded enough
  for long runs.
- No provider defaults change in milestone 010.

## Preserved Invariants

Milestone 010 preserves:

```text
RadarEventBatch remains the processing input boundary.
Leased payload may not outlive the provider callback.
Only owned or retained-owned input may enter the provider queue.
Provider enqueue success is distinct from processing completion.
Queued batches drain in provider sequence order.
One rebalance-enabled batch is processed and committed at a time.
Queued batches capture topology at processing time, not enqueue time.
Accepted topology changes publish only after successful processing.
Failed processing prevents later success claims.
Retained resources release only after final use.
Provider queue and overlap telemetry remain bounded.
Blocking-borrowed remains the default provider mode.
Queued-owned remains opt-in.
```

## Next Milestone Input

Recommended next milestone focus:

- Decide whether the next milestone is default-readiness, production
  configuration, live/durable ingestion, or ordered concurrent processing.
- Add in-flight retained-resource high-water telemetry so retained memory held
  by the active consumer is visible alongside queued pending retained bytes.
- Repeat natural Release gates across more runs, larger corpora, and different
  data shapes before any default provider change.
- Keep `queued-owned + pooled-copy + producer-consumer` available as the
  optimized benchmark contour.
- Keep `blocking-borrowed` as the default until repeated evidence and memory
  pressure telemetry justify a change.
- Keep `builder-transfer` unsupported until safe ownership transfer from
  archive builders is proven.

Deferred beyond the immediate next milestone unless explicitly reprioritized:

- Durable broker integration.
- Live ingestion.
- Cross-process/distributed workers.
- Concurrent rebalance-enabled batch processing.
- Source-level migration and partition splitting.
- Physical worker-local state transfer.
- Complex radar algorithms.
