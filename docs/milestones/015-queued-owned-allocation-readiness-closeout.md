# Milestone 015: Closeout

## Status

Milestone 015 is complete.

RadarPulse accepts cache-level allocation readiness for the queued-owned
direct/default archive rebalance contour used by
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` and `MeasureCache()`
when provider/execution/queue/retention controls are omitted.

The important milestone result is:

```text
014 migrated direct MeasureFile()/MeasureCache() defaults to the queued-owned
    rollout contour.
015 reduced and bounded the queued-owned direct/default allocation warning
    enough to support broader cache-level benchmark/default-readiness work.
```

The KTLX 2026-05-05 allocation warning that drove the milestone is reduced
from `1.0997x` borrowed in milestone 014 to `1.0392x` borrowed in milestone
015, with both repeated rows below the `1.10x` threshold.

The single-file cold allocation warning remains visible as expected
retained-ownership cost for `queued-owned + pooled-copy`. It does not block
cache-level allocation readiness, but it is not a file-level readiness claim.

Live ingestion/runtime defaults, durable queues, cross-process workers,
ordered concurrent rebalance, and builder-transfer remain out of scope.

## Final Outcome

Implemented:

- Baseline allocation attribution audit for direct archive rebalance results.
- Allocation instrumentation and contract decision with stable public result
  contracts.
- Standard allocation optimization pass for bounded recent-detail copying,
  provider queue recent-detail summary copying, and not-required retained
  resource release delegates.
- Experimental retained payload release-owner implementation that removes the
  pooled-copy closure-backed release callback path.
- Retained payload pool rent/miss/return telemetry for pooled-copy rows.
- Dedicated retained `RadarStreamEvent[]` pool with split event-array and
  byte-array telemetry.
- Focused fallback, failure, cleanup, cancellation, release, and drift
  guardrails.
- Direct queued-owned rollout contour checks for omitted defaults and explicit
  queued-owned calls.
- CLI/direct rollout contour alignment checks.
- Focused regression and Release build before gate capture.
- Direct API Release allocation-readiness gate over KTLX 2026-05-04, KTLX
  2026-05-05, KINX 2026-05-04, mixed-cache, and representative single-file
  contours.
- Allocation-readiness decision trace with required decision explanation
  fields.
- Closeout and handoff updates.

Not implemented:

- New queued-owned rollout contour.
- Synthetic processing benchmark default migration.
- Non-benchmark archive publishing API default migration.
- Live ingestion/runtime provider default migration.
- Durable queue or broker integration.
- Cross-process provider or worker transport.
- Ordered concurrent rebalance commit barrier.
- Multiple active rebalance-enabled processing batches.
- `builder-transfer` retained payload execution.
- Source-level migration or partition splitting.
- Physical worker-local state transfer.
- Complex radar algorithms or product-facing radar analysis features.
- Automatic silent fallback from queued-owned failure to borrowed success.
- File-level default latency/allocation optimization.
- Prewarm/shared-pool contract for hiding single-file cold retained snapshot
  cost.

## Final Allocation Posture

Cache-level allocation readiness is accepted.

Release gate allocation ratios:

```text
primary KTLX 2026-05-04: 1.042x borrowed
KTLX 2026-05-05 average: 1.0392x borrowed
KTLX 2026-05-05 row 1: 1.0404x borrowed
KTLX 2026-05-05 row 2: 1.0381x borrowed
KINX 2026-05-04: 1.042x borrowed
mixed cache: 1.021x borrowed
single-file cold smoke: 1.512x borrowed
```

Allocation movement versus milestone 014:

```text
primary KTLX 2026-05-04: 1.071x -> 1.042x borrowed
KTLX 2026-05-05 average: 1.0997x -> 1.0392x borrowed
KINX 2026-05-04: 1.069x -> 1.042x borrowed
mixed cache: 1.066x -> 1.021x borrowed
```

Interpretation:

```text
the named KTLX 2026-05-05 allocation warning is reduced and bounded
both repeated KTLX 2026-05-05 rows are below threshold
the retained event-array pool is the dominant accepted allocation improvement
cache-level allocation readiness is accepted for the measured local contours
single-file cold retained ownership remains a scope limit, not a cache-level
  blocker
```

## Optimization Summary

Accepted standard optimizations:

```text
RadarProcessingArchiveRebalanceBenchmark.CreateBoundedRecentDetails copies
bounded recent details directly into one destination array instead of using
Concat/Skip/ToArray

RadarProcessingProviderQueueTelemetrySummary.CopyRequired validates and copies
recent details directly into one array instead of using List plus a second
ToArray allocation

RadarProcessingRetainedBatchResource uses static per-strategy not-required
release delegates instead of creating a capturing default release lambda per
resource
```

Accepted experimental optimizations:

```text
explicit pooled retained payload release owner:
  replaces closure-backed pooled-copy release callback and preserves release
  through the existing retained resource state machine

dedicated retained RadarStreamEvent[] pool:
  pools retained event metadata arrays separately from retained payload bytes
  and reports split event-array and byte-array rent/return/miss telemetry
```

Rejected or deferred optimization approaches:

```text
wait-mode synchronous enqueue fast path:
  reverted before the Release gate because the allocation/timing signal was
  noisy and the queue-semantics risk was not justified

retained byte-pool capacity increase:
  reverted because retained byte-pool misses were too low to explain the
  remaining warning and a larger pool retained more memory without clearing
  the blocker

pooled telemetry accumulators:
  deferred because immutable summary boundaries and recorder reset ownership
  need a broader design

struct-backed queued work items:
  deferred because queue/channel semantics and sequence ownership would widen
  the change beyond current evidence

additional source-local allocation probes:
  deferred because the Release gate attribution was sufficient for the
  cache-level decision

unsafe memory, stack-lifetime tricks, and measured-window shifting:
  rejected
```

## Final Direct API Posture

Direct API omitted-default contour:

```text
surface:
  RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
  RadarProcessingArchiveRebalanceBenchmark.MeasureCache()

omitted controls:
  providerMode
  executionMode
  asyncExecution
  queueCapacity
  providerOverlapMode
  retentionStrategy
  queueRetainedPayloadBytes
  overlapConsumerDelay

effective contour:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  execution: async shard transport
  worker count: 4
  worker queue capacity: 8
  provider queue capacity: 8
  retained-byte budget: 536870912
  overlap consumer delay: 0
```

Explicit fallback and oracle:

```text
providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed

same-run BlockingBorrowed rows remain the oracle for benchmark gates,
performance regressions, allocation follow-up, and rollback diagnosis
```

CLI alignment:

```text
processing benchmark rebalance-archive omitted-provider path remains aligned
with the same queued-owned rollout contour
```

Failure posture:

```text
queued-owned failures fail closed
there is no automatic borrowed fallback after queued-owned failure
fallback is an explicit provider choice only
```

## Completion Checklist

```text
[x] allocation baseline audit is captured
[x] attribution sufficiency decision is recorded
[x] allocation instrumentation and contract check is complete
[x] standard allocation optimization pass is complete or explicitly rejected
[x] experimental optimization research/spike pass is complete
[x] adopted optimizations are integrated with focused tests
[x] rejected standard and experimental approaches are recorded
[x] direct MeasureFile()/MeasureCache() defaults remain queued-owned rollout
[x] explicit direct BlockingBorrowed fallback remains selectable and covered
[x] direct explicit queued-owned rollout calls match omitted direct defaults
[x] CLI omitted-provider rollout contour remains aligned with direct defaults
[x] failure, cancellation, release, and cleanup guardrails remain covered
[x] focused regression pass succeeds before gate capture
[x] allocation readiness Release gate is captured
[x] KTLX 2026-05-05 allocation warning is repeated and interpreted
[x] performance gate interprets correctness, cleanup, pressure, allocation,
    timing, variance, fallback/oracle posture, attribution, and optimization
    posture
[x] decision trace records the allocation readiness decision
[x] closeout is written
[x] handoff is updated with current allocation posture, fallback/oracle,
    standard and experimental optimization posture, allocation risk, and next
    milestone recommendation
```

## Final Verification

Focused closeout verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
```

Recorded result:

```text
112 passed, 0 failed, 0 skipped.
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
768 passed, 0 failed, 3 skipped.
```

Skipped tests:

```text
AwsNexradArchiveClientIntegrationTests.BuildManifestAsyncListsPublicAwsArchive
AwsNexradArchiveClientIntegrationTests.DownloadFileAsyncDownloadsSmallPublicAwsObject
NexradArchiveDecompressionValidatorCorpusTests.ValidateCachedArchiveCorpusAgainstSharpZipLib
```

## Performance Gate Summary

The Release allocation-readiness gate is captured in
`015-queued-owned-allocation-readiness-performance-gate.md`.

Gate status:

```text
ready with file-level allocation warning
```

Primary KTLX 2026-05-04 matrix:

```text
borrowed rows: 3
direct default rows: 3
direct default elapsed ratio: 0.889x borrowed
direct default allocation ratio: 1.042x borrowed
direct default timing spread: 1.10%
combined retained payload high watermark: 48_257_280 bytes, 8.99% of budget
validation checksum: 7_480_064_646_096_449_000
```

Broader rows:

```text
KTLX 2026-05-05:
  elapsed ratio: 0.943x borrowed average
  allocation ratio: 1.0392x borrowed average
  run 1 allocation ratio: 1.0404x borrowed
  run 2 allocation ratio: 1.0381x borrowed
  validation checksum: 11_084_221_590_146_245_827

KINX 2026-05-04:
  elapsed ratio: 0.899x borrowed
  allocation ratio: 1.042x borrowed
  validation checksum: 1_465_969_045_420_103_918

mixed cache:
  elapsed ratio: 0.871x borrowed
  allocation ratio: 1.021x borrowed
  validation checksum: 615_051_108_812_661_629

single-file cold smoke:
  elapsed ratio: 1.072x borrowed
  allocation ratio: 1.512x borrowed
  validation checksum: 3_750_039_633_875_006_276
```

Gate interpretation:

```text
correctness parity: accepted
release health: accepted, 0 failed releases
cleanup: accepted, current retained pressure returns to 0
pressure budget: accepted, max observed combined retained payload high
  watermark is 54_413_280 bytes of the 536_870_912 byte budget
elapsed timing: accepted for cache-level readiness
primary run spread: accepted
allocation: accepted for cache-level readiness
KTLX 2026-05-05 warning: reduced and bounded
single-file cold smoke: accepted as scope limit, not cache-level blocker
direct default versus explicit queued-owned contour: accepted
explicit borrowed fallback/oracle separation: accepted
allocation attribution: accepted as explanatory evidence
```

## Decision Trace

The decision trace is written in
`015-queued-owned-allocation-readiness-decision-trace.md`.

Final closeout answer:

```text
yes, the queued-owned direct/default allocation profile is ready to support
the next broader cache-level benchmark/default-readiness decision
```

KTLX 2026-05-05 allocation is reduced and bounded:

```text
the named risk contour moved from 1.0997x borrowed to 1.0392x borrowed, and
both repeated rows are below the 1.10x threshold
```

Single-file cold allocation is accepted with scope limit:

```text
the file-level cold warning is expected retained-ownership cost and does not
block cache-level readiness, but file-level default latency/allocation remains
a separate future target if that surface is chosen later
```

Explicit borrowed fallback and oracle remain preserved:

```text
providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed
```

Runtime/live ingestion default migration remains out of scope.

## Preserved Invariants

Milestone 015 preserves:

```text
blocking-borrowed remains explicitly selectable
same-run borrowed comparison remains available and required for gates
CLI omitted-provider rebalance-archive contour remains aligned with direct
  defaults
queued-owned failures fail closed
no automatic borrowed fallback follows queued-owned failure
controlled consumer-delay rows remain mechanics-only proof
builder-transfer remains unsupported
retained cleanup must return current pending, active, and combined pressure
  to zero at completion
release failures remain migration blockers
live ingestion/runtime defaults remain out of scope
thresholds were not raised after gate capture
allocation optimization must not hide cost by moving it outside the measured
  contract
```

## Residual Risks And Limits

```text
single-file cold allocation:
  expected retained-ownership cost for queued-owned pooled-copy; not a
  cache-level blocker, but a future file-level default target if that surface
  matters

processing callback allocation attribution:
  remains the largest candidate-side bucket; accepted because cache-level
  ratios passed and retained pool telemetry explains the dominant improvement

local gate only:
  the Release gate used locally available NEXRAD cache shapes on 2026-05-21

natural queue depth:
  natural direct default rows kept queue depth at 1; queue-ahead mechanics
  remain covered by controlled tests rather than this natural gate

mixed-cache worker failure counters:
  mixed-cache rows reported matching worker failed batch/item counters in both
  borrowed and direct default rows while validation still succeeded

benchmark API only:
  live ingestion, durable queues, brokers, cross-process providers, and
  runtime defaults remain outside milestone 015
```

## Recommended Next Milestone

Recommended next milestone input:

```text
broader cache-level benchmark/default-readiness
```

The next milestone should keep:

```text
same-run BlockingBorrowed oracle rows
broader local or available cache shapes
correctness, release, cleanup, pressure, elapsed, spread, allocation, and
  attribution threshold interpretation
explicit scope language that single-file cold retained-ownership cost remains
  a file-level concern rather than a cache-level blocker
```

Still deferred unless explicitly reprioritized:

```text
file-level default latency/allocation optimization
live ingestion/runtime default migration
durable queues
cross-process workers
ordered concurrent rebalance
builder-transfer
source-level migration
partition splitting
complex radar algorithms
```
