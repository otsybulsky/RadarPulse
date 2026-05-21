# Milestone 013: Closeout

## Status

Milestone 013 is complete.

RadarPulse now has post-rollout hardening evidence for the milestone 012
scoped queued-owned default. The scoped
`processing benchmark rebalance-archive` CLI omitted-provider path remains:
`queued-owned + pooled-copy + producer-consumer`, with async execution,
workers 4, queue capacity 8, retained-byte budget `536_870_912`, queue
telemetry enabled, overlap telemetry enabled, and consumer delay disabled.

The important milestone result is:

```text
012 made queued-owned the scoped rebalance-archive CLI default.
013 keeps that scoped CLI default and approves direct archive rebalance API
    default migration as the next milestone input.
```

The KTLX 2026-05-05 allocation warning is accepted as the cost of proceeding
to the direct API migration milestone. It is a tracked risk, not a rollback
reason for the scoped CLI default.

## Final Outcome

Implemented:

- Post-rollout surface audit for the scoped CLI omitted-provider default.
- Default contour drift guardrails for the full rollout contour.
- Direct `MeasureFile()` and `MeasureCache()` compatibility guardrails;
  direct defaults remain blocking-borrowed.
- Operator help and output cleanup for scoped default, explicit fallback,
  direct API compatibility, and controlled proof separation.
- Allocation attribution output for retained owned snapshot allocation,
  non-owned callback allocation, replay/build allocation, and CLI formatting.
- Failure, cleanup, fallback, and fail-closed regression coverage.
- Focused regression pass before the broader gate.
- Broader natural Release gate over KTLX 2026-05-04, KINX 2026-05-04,
  KTLX 2026-05-05, and mixed-cache contours.
- Stability decision trace with 001-006 style decision explanations.
- Closeout and handoff updates.

Not implemented:

- Direct `RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` or
  `MeasureCache()` default migration.
- Synthetic benchmark default migration.
- Non-benchmark archive publishing API default migration.
- Live ingestion/runtime provider default migration.
- Durable queue or broker integration.
- Cross-process provider or worker transport.
- Ordered concurrent rebalance commit barrier.
- Multiple active rebalance-enabled processing batches.
- `builder-transfer` retained payload execution.
- Source-level migration or partition splitting.
- Physical worker-local state transfer.
- Automatic silent fallback from queued-owned failure to borrowed success.

## Final Default Posture

Scoped CLI default:

```text
surface:
  processing benchmark rebalance-archive CLI omitted-provider path

effective contour:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  execution: async
  worker count: 4
  provider queue capacity: 8
  retained-byte budget: 536870912
  queue telemetry: summary
  overlap telemetry: summary
  overlap consumer delay: 0
  provider source: rollout-default
```

Fallback and oracle:

```text
--provider blocking-borrowed

same-run blocking-borrowed rows remain the oracle for benchmark gates,
performance regressions, allocation follow-up, and direct API migration
```

Direct API compatibility:

```text
direct MeasureFile()/MeasureCache() defaults remain blocking-borrowed
explicit queued-owned options remain available for direct benchmark calls
direct default migration is the recommended next milestone input
```

## Completion Checklist

```text
[x] post-rollout surface audit is captured
[x] milestone 012 rollout contour is pinned against drift
[x] explicit blocking-borrowed fallback remains selectable and visible
[x] direct MeasureFile()/MeasureCache() defaults remain borrowed
[x] operator help/output makes scoped default and fallback reproducible
[x] allocation attribution is visible enough to explain residual overhead
[x] failure, cancellation, release, and cleanup guardrails remain covered
[x] focused regression pass succeeds before gate capture
[x] broader natural Release gate is captured
[x] performance gate interprets correctness, cleanup, pressure, allocation,
    timing, variance, provenance, and attribution
[x] decision trace records the post-rollout stability decision
[x] closeout is written
[x] handoff is updated with current default, fallback/oracle, compatibility,
    and next-milestone posture
```

## Final Verification

Focused closeout verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
```

Recorded result:

```text
79 passed, 0 failed, 0 skipped.
```

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Recorded result:

```text
Release build succeeded with 0 warnings and 0 errors.
```

Full test project:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
756 passed, 0 failed, 3 skipped.
```

## Performance Gate Summary

The broader natural Release gate is captured in
`013-post-rollout-hardening-broader-validation-performance-gate.md`.

Primary KTLX 2026-05-04 matrix:

```text
borrowed rows: 3
rollout-default rows: 3
candidate elapsed ratio: 0.911x borrowed
candidate allocation ratio: 1.071x borrowed
candidate spread: 5.41% of candidate average
combined retained payload high watermark: 48_257_280 bytes, 8.99% of budget
validation checksum: 7_480_064_646_096_449_000
```

Broader rows:

```text
KINX 2026-05-04:
  elapsed ratio: 0.939x borrowed
  allocation ratio: 1.070x borrowed
  validation checksum: 1_465_969_045_420_103_918

KTLX 2026-05-05:
  elapsed ratio: 0.943x borrowed average
  allocation ratio: 1.1005x borrowed average
  run 1 allocation ratio: 1.101x borrowed
  run 2 allocation ratio: 1.0996x borrowed
  validation checksum: 11_084_221_590_146_245_827

mixed cache:
  elapsed ratio: 0.907x borrowed
  allocation ratio: 1.062x borrowed
  validation checksum: 615_051_108_812_661_629
```

Gate interpretation:

```text
correctness parity: accepted
release health: accepted, 0 failed releases
cleanup: accepted, current retained pressure returns to 0
pressure budget: accepted, max observed combined retained payload high
  watermark is 54_413_280 bytes of the 536_870_912 byte budget
elapsed timing: accepted across captured rows
primary run spread: accepted, <= 7.50%
allocation: accepted for primary, KINX, and mixed cache; KTLX 2026-05-05 is
  accepted as an explicit migration cost
fallback separation: accepted
operator provenance: accepted
```

## Decision Trace

The decision trace is written in
`013-post-rollout-hardening-broader-validation-decision-trace.md`.

Final closeout answer:

```text
yes, the milestone 012 scoped queued-owned default is stable enough to remain
the processing benchmark rebalance-archive omitted-provider default and to
serve as the baseline for the next expansion decision
```

The next expansion decision is direct archive rebalance API default migration:

```text
direct MeasureFile()/MeasureCache() default migration can proceed as a
separate milestone with explicit allocation-cost acceptance, same-run
blocking-borrowed oracle coverage, and repeated KTLX 2026-05-05 allocation
tracking
```

Runtime/live ingestion default migration remains out of scope.

## Preserved Invariants

Milestone 013 preserves:

```text
blocking-borrowed remains explicitly selectable
same-run borrowed comparison remains available and required for gates
direct MeasureFile()/MeasureCache() defaults remain blocking-borrowed
queued-owned failures fail closed
no automatic borrowed fallback follows queued-owned failure
controlled consumer-delay rows remain mechanics-only proof
builder-transfer remains unsupported
retained cleanup must return current pending, active, and combined pressure
  to zero at completion
release failures remain rollout blockers
```

## Residual Risks And Limits

```text
allocation warning:
  KTLX 2026-05-05 reached a two-run average allocation ratio of 1.1005x
  borrowed, with one run above the 1.10x threshold; this is accepted as a
  direct API migration cost and remains tracked

local gate only:
  the broader gate used locally available NEXRAD cache shapes

benchmark surface only:
  the accepted default remains scoped to the rebalance-archive CLI benchmark
  omitted-provider path

direct API compatibility:
  direct MeasureFile()/MeasureCache() defaults remain blocking-borrowed until
  the next milestone changes them

natural queue depth:
  natural runs kept queue depth at 1; queue-ahead mechanics remain covered by
  controlled proof tests

no runtime ingestion claim:
  live ingestion, durable queues, brokers, and cross-process providers remain
  outside milestone 013
```

## Carry Forward

Recommended next milestone input:

```text
direct archive rebalance API default migration for MeasureFile()/MeasureCache()
```

Carry these facts forward:

- Preserve explicit blocking-borrowed direct and CLI comparison paths.
- Keep same-run borrowed rows in the migration gate.
- Repeat KTLX 2026-05-05 and keep its `1.1005x` allocation warning visible.
- Treat the allocation warning as accepted migration cost, not as a hidden
  pass.
- Do not bundle direct API migration with live ingestion, durable broker,
  cross-process worker, builder-transfer, or ordered concurrent rebalance work.
