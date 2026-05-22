# Milestone 016: Closeout

## Status

Milestone 016 is complete.

RadarPulse accepts broader cache-level default readiness for the queued-owned
direct/default archive rebalance contour used by
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` and `MeasureCache()`
when provider/execution/queue/retention controls are omitted.

The important milestone result is:

```text
015 accepted cache-level allocation readiness for the queued-owned
    direct/default contour.
016 accepts broader cache-level benchmark/default readiness for available
    local cache workloads, with named scoped warnings.
```

Final readiness posture:

```text
yes with warnings, broader cache-level default readiness is accepted with
named scoped warnings
```

The decision is not clean-green. The accepted warnings are:

```text
primary spread warning:
  KTLX 2026-05-04 max-files 220 candidate spread was 12.01%, above the 7.50%
  threshold, while every individual candidate row remained faster than
  same-run borrowed and all safety/allocation guardrails passed

named-risk timing note:
  KTLX 2026-05-05 max-files 220 had one individual elapsed pair at 1.001x
  borrowed, while the repeated average passed at 0.822x and risk-440 passed at
  0.810x

mixed-cache worker-counter note:
  candidate worker failed batches/items were 221/881 while validation
  succeeded and failed migrations remained 0; borrowed worker failed counters
  were not recaptured in slice 5

file-smoke coverage-only scope:
  current single-file smoke did not reproduce the milestone 015 cold warning,
  but milestone 016 does not certify file-level default readiness
```

Live ingestion/runtime defaults, durable queues, cross-process workers,
ordered concurrent rebalance, builder-transfer, and file-level default
readiness remain out of scope.

## Final Outcome

Implemented:

- Cache corpus inventory and gate matrix design for available local NEXRAD
  roots.
- Existing contract and guardrail audit for direct defaults, explicit
  `BlockingBorrowed`, explicit queued-owned rollout, CLI/direct alignment,
  retained telemetry, cleanup, release, and fail-closed behavior.
- Reporting and harness readiness decision to use a temporary local direct API
  gate runner without committing a product reporting surface.
- Focused regression and cache sanity pass before Release gate capture.
- Broader cache-level Release gate over primary KTLX, named-risk KTLX, KINX,
  mixed-cache, optional larger cache slices, CLI spot-checks, and
  representative single-file smoke.
- Formal gate interpretation with no runtime changes, no targeted rerun, and
  no borrowed worker-counter recapture required before decision trace.
- Broader cache-level readiness decision trace in the standard milestone
  format.
- Closeout and handoff updates.

Not implemented:

- New queued-owned rollout contour.
- Threshold changes after gate capture.
- Runtime behavior changes.
- Reporting contract or product CLI output changes.
- Targeted primary rerun before decision trace.
- Borrowed worker-counter recapture before decision trace.
- File-level default latency/allocation optimization.
- File-level default readiness certification.
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

## Final Readiness Posture

Broader cache-level default readiness is accepted with named scoped warnings.

Accepted clean cache-level rows:

```text
KINX 2026-05-04 max-files 220:
  elapsed ratio: 0.769x borrowed
  allocation ratio: 1.007x borrowed

KTLX 2026-05-04 full root max-files 244:
  elapsed ratio: 0.887x borrowed
  allocation ratio: 1.008x borrowed

KINX 2026-05-04 larger slice max-files 440:
  elapsed ratio: 0.782x borrowed
  allocation ratio: 1.000x borrowed

KTLX 2026-05-05 larger named-risk slice max-files 440:
  elapsed ratio: 0.810x borrowed
  allocation ratio: 1.008x borrowed
```

Accepted warnings:

```text
primary KTLX 2026-05-04 max-files 220:
  elapsed ratio: 0.881x borrowed average
  allocation ratio: 1.028x borrowed average
  candidate spread: 12.01%, above the 7.50% threshold
  status: accepted with scoped spread warning

KTLX 2026-05-05 named-risk max-files 220:
  elapsed ratio: 0.822x borrowed average
  allocation ratio: 1.021x borrowed average
  candidate spread: 7.42%, below but near the 7.50% threshold
  individual pair 2 elapsed ratio: 1.001x borrowed
  status: accepted with timing note

mixed local cache:
  elapsed ratio: 0.873x borrowed
  allocation ratio: 1.006x borrowed
  candidate worker failed batches/items: 221/881
  validation: succeeded
  failed migrations: 0
  status: accepted with worker-counter note

representative single-file smoke:
  elapsed ratio: 0.675x borrowed
  allocation ratio: 1.041x borrowed
  status: coverage-only, not file-level default readiness proof
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
broader cache coverage, performance regressions, allocation follow-up, and
rollback diagnosis
```

CLI alignment:

```text
processing benchmark rebalance-archive omitted-provider cache path remains
aligned with the same queued-owned rollout contour
```

Failure posture:

```text
queued-owned failures fail closed
there is no automatic borrowed fallback after queued-owned failure
fallback is an explicit provider choice only
```

## Completion Checklist

```text
[x] cache corpus inventory is captured with radar/date/file-count/selection
    details
[x] selected cache-level shapes are broad enough for the available-workload
    readiness decision
[x] same-run explicit BlockingBorrowed oracle rows remain available,
    documented, and visibly separate
[x] direct MeasureCache() omitted defaults still resolve to the accepted
    queued-owned rollout contour
[x] CLI omitted-provider cache benchmark remains aligned with direct API
    defaults
[x] correctness parity against borrowed rows is preserved for every readiness
    row
[x] retained cleanup returns current pressure to zero in natural
    direct/default rows
[x] release failures remain 0
[x] retained pressure stays within the configured 536870912 byte budget
[x] allocation overhead is classified per cache shape against the recorded
    threshold
[x] elapsed timing and variance are classified per cache shape against the
    recorded thresholds
[x] single-file retained-ownership cost remains explicitly scoped as a
    file-level concern, not cache-level blocker
[x] queued-owned failures remain fail-closed with no automatic borrowed
    fallback
[x] performance gate is captured
[x] decision trace records the broader cache-level default-readiness decision
[x] closeout records verification, gate results, residual risks, and carry
    forward items
[x] handoff states the current broader cache-level readiness posture and
    recommended next milestone unambiguously
```

## Final Verification

Focused regression before the gate:

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

Temporary direct API gate runner build:

```powershell
dotnet build data\temp\m016-gate-runner\M016GateRunner.csproj -c Release --no-restore
```

Recorded result:

```text
Temporary runner build succeeded with 0 warnings and 0 errors.
```

Full test project before closeout:

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

The Release broader cache-level default-readiness gate is captured in
`016-broader-cache-level-default-readiness-performance-gate.md`.

Gate status:

```text
captured with primary spread warning
```

Primary KTLX 2026-05-04 matrix:

```text
borrowed/default pairs: 3
direct default elapsed ratio: 0.881x borrowed average
direct default allocation ratio: 1.028x borrowed average
direct default timing spread: 12.01%
combined retained payload high watermark: 48_257_280 bytes
validation checksum: 7_480_064_646_096_449_000
```

Broader rows:

```text
KTLX 2026-05-05 named risk:
  elapsed ratio: 0.822x borrowed average
  allocation ratio: 1.021x borrowed average
  individual pair 2 elapsed ratio: 1.001x borrowed
  validation checksum: 11_084_221_590_146_245_827

KINX 2026-05-04:
  elapsed ratio: 0.769x borrowed
  allocation ratio: 1.007x borrowed
  validation checksum: 1_465_969_045_420_103_918

mixed cache:
  elapsed ratio: 0.873x borrowed
  allocation ratio: 1.006x borrowed
  validation checksum: 615_051_108_812_661_629
  candidate worker failed batches/items: 221/881

KTLX 2026-05-04 full root:
  elapsed ratio: 0.887x borrowed
  allocation ratio: 1.008x borrowed

KINX 2026-05-04 larger slice:
  elapsed ratio: 0.782x borrowed
  allocation ratio: 1.000x borrowed

KTLX 2026-05-05 larger named-risk slice:
  elapsed ratio: 0.810x borrowed
  allocation ratio: 1.008x borrowed

single-file smoke:
  elapsed ratio: 0.675x borrowed
  allocation ratio: 1.041x borrowed
```

Gate interpretation:

```text
correctness parity: accepted
release health: accepted, 0 failed releases
cleanup: accepted, current retained pressure returns to 0
pressure budget: accepted, max observed combined retained payload high
  watermark is 54_413_280 bytes of the 536_870_912 byte budget
allocation: accepted for cache-level readiness
elapsed timing: accepted on cache-level averages
primary run spread: accepted with scoped warning
named-risk individual elapsed pair: accepted with timing note
mixed-cache worker counters: accepted with explicit note
file-smoke row: coverage-only, not file-level readiness proof
direct default and CLI omitted-provider alignment: accepted
explicit borrowed fallback/oracle separation: accepted
```

## Decision Trace

The decision trace is written in
`016-broader-cache-level-default-readiness-decision-trace.md`.

Final closeout answer:

```text
yes with warnings, broader cache-level default readiness is accepted with
named scoped warnings
```

Primary spread is accepted as a scoped warning:

```text
primary candidate spread was 12.01%, above the 7.50% threshold, but every
individual candidate run remained faster than same-run borrowed and all
correctness, lifecycle, pressure, and allocation guardrails passed
```

Named-risk timing is accepted as a note:

```text
one individual KTLX 2026-05-05 pair was 1.001x borrowed, but the repeated
average was 0.822x and the larger same-shape row was 0.810x
```

Mixed-cache worker counters are accepted with explicit scope:

```text
candidate worker failed batches/items were 221/881 while validation succeeded
and failed migrations remained 0; borrowed worker failed counters were not
recaptured in slice 5
```

File-smoke is coverage-only:

```text
the file-smoke row is useful visibility and did not reproduce the milestone
015 cold warning, but it does not certify file-level default readiness
```

Runtime/live ingestion default migration remains out of scope.

## Preserved Invariants

Milestone 016 preserves:

```text
direct MeasureFile()/MeasureCache() omitted defaults remain queued-owned
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
shape-specific warnings must not be hidden behind mixed-cache aggregate
  success
```

## Residual Risks And Limits

```text
local corpus only:
  the decision covers the available local NEXRAD cache shapes captured in the
  milestone 016 gate; it does not certify absent radar sites, absent dates, or
  non-local corpora

primary spread warning:
  primary candidate spread was 12.01%, above the 7.50% threshold; accepted as
  scoped warning because relative timing and safety guardrails still passed

named-risk timing note:
  one KTLX 2026-05-05 individual pair measured 1.001x borrowed; accepted
  because repeated average and larger same-shape row passed

mixed-cache worker counters:
  candidate worker failed batches/items were 221/881 while validation
  succeeded; slice 5 did not recapture borrowed worker failed counters for
  that row

single-file scope:
  current single-file smoke did not reproduce the milestone 015 cold warning,
  but file-level default readiness remains outside this decision

natural queue depth:
  natural direct default rows kept queue depth at 1; queue-ahead mechanics
  remain covered by controlled tests rather than this natural gate

benchmark API only:
  live ingestion, durable queues, brokers, cross-process providers, ordered
  concurrent rebalance, builder-transfer, and runtime defaults remain outside
  milestone 016
```

## Recommended Next Milestone

Recommended next milestone input:

```text
File-Level Default Readiness And Cold Retained-Ownership Cost
```

The next milestone should decide whether the queued-owned direct/default
contour is ready for file-level `MeasureFile()` and small-file workloads, or
whether file-level needs a scoped optimization/default decision before any
runtime expansion.

The next milestone should keep:

```text
same-run BlockingBorrowed oracle rows
cold and repeated/warm file-level rows
small-file cache slices where retained cold cost is only partially amortized
explicit file-level thresholds before measurement interpretation
the accepted cache-level contour unchanged unless evidence justifies a new
  scoped decision
live/runtime/durable defaults out of scope unless explicitly replanned
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
