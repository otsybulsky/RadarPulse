# Milestone 014: Closeout

## Status

Milestone 014 is complete.

RadarPulse now uses the accepted queued-owned rollout contour as the direct
archive rebalance benchmark API default for
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` and `MeasureCache()`
when provider/execution/queue/retention controls are omitted.

The important milestone result is:

```text
012 made queued-owned the scoped rebalance-archive CLI default.
013 kept that scoped CLI default and approved direct API migration.
014 migrates direct MeasureFile()/MeasureCache() defaults to the same
    queued-owned rollout contour.
```

The direct API migration is accepted with the repeated KTLX 2026-05-05
allocation warning. The warning is tracked debt, not a rollback reason for the
direct API default.

Live ingestion/runtime defaults, durable queues, cross-process workers,
ordered concurrent rebalance, and builder-transfer remain out of scope.

## Final Outcome

Implemented:

- Direct API baseline audit for `MeasureFile()` and `MeasureCache()`.
- Shared `RadarProcessingArchiveRebalanceRolloutDefaults` contour contract.
- Direct `MeasureFile()` omitted-default migration to queued-owned rollout.
- Direct `MeasureCache()` omitted-default migration to queued-owned rollout.
- Direct explicit `BlockingBorrowed` fallback/oracle coverage.
- Direct explicit queued-owned rollout equivalence coverage.
- CLI/direct rollout contour alignment checks.
- Operator help cleanup so direct defaults are no longer described as
  blocking-borrowed.
- Failure, cancellation, release, cleanup, and fail-closed guardrails.
- Focused regression pass before Release gate capture.
- Direct API Release gate over KTLX 2026-05-04, KTLX 2026-05-05,
  KINX 2026-05-04, and mixed-cache contours.
- Direct API migration decision trace with standard decision explanations.
- Closeout and handoff updates.

Not implemented:

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
- Allocation optimization; allocation remains the recommended next risk target.

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
with the same queued-owned rollout contour accepted in milestone 012 and
hardened in milestone 013
```

Failure posture:

```text
queued-owned failures fail closed
there is no automatic borrowed fallback after queued-owned failure
fallback is an explicit provider choice only
```

## Completion Checklist

```text
[x] direct API baseline audit is captured
[x] shared rollout contour contract is pinned against drift
[x] direct MeasureFile() omitted defaults migrate to queued-owned rollout
[x] direct MeasureCache() omitted defaults migrate to queued-owned rollout
[x] explicit direct blocking-borrowed fallback remains selectable and covered
[x] direct explicit queued-owned rollout calls match omitted direct defaults
[x] CLI omitted-provider rollout contour remains aligned with direct defaults
[x] operator help/docs no longer claim direct defaults remain borrowed
[x] failure, cancellation, release, and cleanup guardrails remain covered
[x] focused regression pass succeeds before gate capture
[x] direct API Release gate is captured
[x] KTLX 2026-05-05 allocation warning is repeated and interpreted
[x] performance gate interprets correctness, cleanup, pressure, allocation,
    timing, variance, fallback/oracle posture, and attribution
[x] decision trace records the direct API default decision
[x] closeout is written
[x] handoff is updated with current direct default, fallback/oracle,
    allocation-risk, and next-milestone posture
```

## Final Verification

Focused closeout verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
```

Recorded result:

```text
84 passed, 0 failed, 0 skipped.
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
761 passed, 0 failed, 3 skipped.
```

Skipped tests:

```text
AwsNexradArchiveClientIntegrationTests.BuildManifestAsyncListsPublicAwsArchive
AwsNexradArchiveClientIntegrationTests.DownloadFileAsyncDownloadsSmallPublicAwsObject
NexradArchiveDecompressionValidatorCorpusTests.ValidateCachedArchiveCorpusAgainstSharpZipLib
```

## Performance Gate Summary

The direct API Release gate is captured in
`014-direct-archive-rebalance-api-default-migration-performance-gate.md`.

Gate status:

```text
captured with allocation warning
```

Primary KTLX 2026-05-04 matrix:

```text
borrowed rows: 4
direct default rows: 4
direct default elapsed ratio: 0.911x borrowed
direct default allocation ratio: 1.071x borrowed
all-row direct default timing spread: 10.41%
stabilized rows 2-4 timing spread: 0.39%
combined retained payload high watermark: 48_257_280 bytes, 8.99% of budget
validation checksum: 7_480_064_646_096_449_000
```

Broader rows:

```text
KTLX 2026-05-05:
  elapsed ratio: 0.960x borrowed average
  allocation ratio: 1.0997x borrowed average
  run 1 allocation ratio: 1.1018x borrowed
  run 2 allocation ratio: 1.0976x borrowed
  validation checksum: 11_084_221_590_146_245_827

KINX 2026-05-04:
  elapsed ratio: 0.906x borrowed
  allocation ratio: 1.069x borrowed
  validation checksum: 1_465_969_045_420_103_918

mixed cache:
  elapsed ratio: 0.878x borrowed
  allocation ratio: 1.066x borrowed
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
primary run spread: accepted with favorable-outlier note
allocation: accepted with KTLX 2026-05-05 warning
direct default versus explicit queued-owned contour: accepted
explicit borrowed fallback/oracle separation: accepted
allocation attribution: accepted as explanatory evidence
```

The primary all-row timing spread exceeded the 7.50% threshold because the
first direct-default row was a favorable timing outlier. Stabilized rows 2-4
spread by 0.39%, and every direct default row was faster than its same-run
borrowed row. This is recorded as a variance note, not a slowdown blocker.

## Decision Trace

The decision trace is written in
`014-direct-archive-rebalance-api-default-migration-decision-trace.md`.

Final closeout answer:

```text
yes, direct RadarProcessingArchiveRebalanceBenchmark.MeasureFile() and
MeasureCache() omitted defaults migrate symmetrically to the accepted
queued-owned rollout contour
```

Explicit borrowed fallback and oracle remain preserved:

```text
providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed
```

KTLX 2026-05-05 allocation is accepted with warning:

```text
the direct API migration proceeds, but KTLX 2026-05-05 remains an explicit
allocation warning and should be carried into the next allocation/readiness
milestone
```

Runtime/live ingestion default migration remains out of scope.

## Preserved Invariants

Milestone 014 preserves:

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
```

## Residual Risks And Limits

```text
allocation warning:
  KTLX 2026-05-05 averaged 1.0997x borrowed allocation in the direct gate,
  with one row above the 1.10x threshold; this is accepted as direct API
  migration debt, not as clean green

allocation attribution:
  direct default allocation overhead is concentrated in processing callback
  allocation and retained/owned snapshot work; optimization is not completed
  in milestone 014

primary favorable timing outlier:
  all-row timing spread was 10.41%, while stabilized rows 2-4 spread 0.39%;
  every direct default row was still faster than same-run borrowed

local gate only:
  the Release gate used locally available NEXRAD cache shapes on 2026-05-21

cache natural gate:
  direct MeasureCache() received natural Release gate rows; direct
  MeasureFile() is covered by focused regression tests

optional parameter compatibility:
  source callers recompiled after this milestone observe the new direct
  omitted defaults; already-compiled external assemblies may retain old
  optional argument constants

benchmark API only:
  live ingestion, durable queues, brokers, cross-process providers, and
  runtime defaults remain outside milestone 014
```

## Recommended Next Milestone

Recommended next milestone input:

```text
targeted allocation reduction or allocation-readiness for the queued-owned
direct/default contour before any live/runtime default expansion
```

If broader benchmark expansion is chosen before allocation reduction, it must
keep:

```text
same-run BlockingBorrowed oracle rows
KTLX 2026-05-05 allocation warning visibility
correctness, release, cleanup, pressure, elapsed, spread, and attribution
threshold interpretation
```

Still deferred unless explicitly reprioritized:

```text
live ingestion/runtime default migration
durable queues
cross-process workers
ordered concurrent rebalance
builder-transfer
source-level migration
partition splitting
complex radar algorithms
```
