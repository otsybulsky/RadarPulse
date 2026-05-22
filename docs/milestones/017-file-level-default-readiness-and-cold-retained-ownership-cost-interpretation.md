# Milestone 017: Gate Interpretation And Prewarm Default Posture

Status: captured with scoped prewarm default implementation.

This document records the slice 7 interpretation of the milestone 017
`MeasureFile()` and low-count `MeasureCache()` evidence, and the follow-up
default-posture change accepted from that evidence.

## Evidence Summary

Natural direct archive rebalance defaults were safety-clean but
allocation-blocked for file and small-file readiness.

```text
MeasureFile natural gate:
  all selected rows passed correctness, validation, release, cleanup, pressure,
    fallback, and topology guardrails
  all selected rows failed allocation thresholds
  retained owned snapshot allocation was the named blocker

Small-file MeasureCache natural gate:
  all 18 borrowed/default pairs passed safety guardrails
  elapsed ratios were at or below borrowed averages
  allocation ratios ranged from 1.176x to 2.168x
  KINX 2-file and all KTLX 2026-05-05 low-count rows failed allocation
```

Explicit prewarmed comparison rows removed the measured allocation blocker.

```text
MeasureFile prewarmed comparison:
  measured allocation ratios: 0.980x to 1.026x
  retained pool misses: 0
  fail-level timing outliers did not reproduce in targeted repeats
  remaining elapsed jitter carried as a non-blocking filesystem timing note

Small-file MeasureCache prewarmed comparison:
  measured allocation ratios: 0.818x to 1.002x
  elapsed ratios: 0.454x to 0.979x
  retained pool misses: 0
```

## Decision

Make retained payload prewarm the scoped default for direct archive rebalance
file and small-file readiness.

```text
included default surface:
  RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
  RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
  CLI rebalance-archive path that uses the same direct benchmark APIs

included contour:
  effective queued-owned rollout-default contour
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  execution: async shard transport
  worker count: 4
  worker queue capacity: 8
  provider queue capacity: 8
  retained-byte budget: 536870912
  overlap consumer delay: 0

excluded surfaces:
  live ingestion/runtime defaults
  durable queues or brokers
  cross-process workers
  ordered concurrent rebalance runtime
  non-benchmark archive publishing APIs
```

The implementation applies prewarm when the effective contour matches the
rollout default and no caller supplies a retained payload factory. Explicit
`BlockingBorrowed` remains the same-run fallback/oracle and does not prewarm.
Explicit custom queued-owned contours remain explicit; prewarm is not applied
unless the effective contour is the rollout default.

## Implementation Shape

```text
default prewarm sizing:
  event count: 65_536
  payload bytes: 67_108_864
  retained batch count: 1

new result attribution:
  RetainedPayloadPrewarm
  HasRetainedPayloadPrewarm
  RetainedPayloadPrewarmAllocatedBytes
  RetainedPayloadPrewarmRetainedBytes
  ProcessingSucceeded
  ProcessingValidationFailedBatchCount
  WorkerFailedBatchCount
  WorkerFailedWorkItemCount

CLI attribution:
  retained payload prewarm enabled/disabled
  prewarm event count
  prewarm payload bytes
  prewarm batch count
  prewarm elapsed ms
  prewarm allocated bytes
  prewarm retained bytes
  processing completeness
  processing validation failed batches
```

The prewarm allocation is deliberately not folded into measured row
allocation. It is reported separately so the default path is fast in the
measured operation while still exposing the up-front allocation cost.

## Follow-Up Fix Posture

```text
implemented:
  default-equivalent queued-owned direct archive rebalance contour now creates
    and prewarms a retained payload factory automatically
  explicit retained payload factory passthrough remains supported
  result contracts expose prewarm attribution
  CLI output exposes prewarm attribution
  cache `MeasureCache()` auto-sizes the source universe for mixed-radar
    selected base-data files when no radar filter is provided
  archive rebalance result contracts and CLI expose processing completeness
  generic archive rebalance validation now includes processing-result validity
    instead of only rebalance session validation
  focused tests pin default prewarm constants and result behavior

not implemented:
  runtime/live ingestion prewarm
  durable/cross-process prewarm
  hidden prewarm without result attribution
  automatic borrowed fallback
  builder-transfer retained payload execution
```

## Post-Fix Cache Regression Matrix

After scoped default prewarm and the mixed-cache source-universe fix were
implemented, the broader cache-level and small-file `MeasureCache()` matrix
was rerun against same-run `BlockingBorrowed` oracle rows with processing
completeness as a safety blocker.

```text
runner:
  data\temp\m017-cache-regression-runner

raw outputs:
  data\temp\m017-cache-regression-runner\output\m017-cache-regression-20260522-110241.jsonl
  data\temp\m017-cache-regression-runner\output\m017-cache-regression-20260522-110241.md

matrix:
  broader primary KTLX 2026-05-04 max-files 220: 3 pairs
  broader named-risk KTLX 2026-05-05 max-files 220: 2 pairs
  broader KINX 2026-05-04 max-files 220: 1 pair
  mixed full local cache max-files 1000000: 1 pair
  optional broader 244/440-file rows: 3 pairs
  low-count small-cache rows: 18 pairs across 9 slices

result:
  group rows: 16 passed, 0 warning, 0 optimize, 0 failed
  pairs: 28 passed safety, 0 failed safety
  measurements: 56
  worst measured allocation ratio: 1.009x on mixed-cache-all
  worst elapsed ratio: 0.988x on KTLX 2026-05-05 2-file small-cache row
  worst candidate spread: 4.60% on KTLX 2026-05-05 2-file small-cache row,
    below the 7.50% spread threshold
  retained payload pool misses: 0
  retained event-array pool misses: 0
  retained byte-array pool misses: 0
  validation failures: 0
  processing completeness failures: 0
  processing validation failed batches: 0
  worker failed batches/items: 0/0
  release failures: 0
  current retained bytes after rows: 0
  default prewarm allocation: approximately 71_303_392 bytes per candidate row

interpretation:
  no cache-level performance regression was found after making prewarm the
    scoped direct benchmark default and fixing mixed-cache source sizing
  measured allocation is now near parity with borrowed on small-cache rows
    and remains below the broader cache threshold
  mixed-cache-all elapsed ratio is 0.934x and allocation ratio is 1.009x,
    still well inside cache-level thresholds
  mixed-cache-all worker failed batches/items are 0/0 after auto-sizing the
    source universe to the selected radar count
```

## Mixed-Cache Source-Universe Follow-Up

The post-default matrix reproduced the milestone 016 `mixed-cache-all`
worker-counter note:

```text
worker failed batches/items: 221/881
failure kind: SourceOrderViolation
first failed batch: KTLX20260504_000245_V06
last failed batch: KTLX20260505_000026_V06
```

Diagnosis:

```text
mixed-cache-all selected KINX and KTLX files
MeasureCache() used DefaultSingleRadar for all cache workloads
DefaultSingleRadar has one radar ordinal
ArchiveTwoRadarEventBatchProjector resets the identity normalizer on radar
  changes in a one-radar universe
processing state was not reset between radars
therefore KTLX events reused source ids already advanced by KINX end-of-day
  timestamps, so source-local timestamp order was violated
```

Counterfactual verification with the same files and a two-radar source
universe produced:

```text
valid processing batches: 828
invalid processing batches: 0
worker completed/failed batches: 828/0
worker succeeded/failed items: 3312/0
```

Implementation fix:

```text
MeasureCache() keeps DefaultSingleRadar when a radar filter is supplied
MeasureCache() scans the selected base-data file set when no radar filter is
  supplied
the cache source universe uses max(1, distinct selected radar ids)
auto-sizing has a guardrail of 256 distinct radar ids
the same source universe is passed to archive publishing and processing
```

Gate/reporting fix:

```text
archive rebalance result contracts expose ProcessingSucceeded
ProcessingSucceeded requires:
  ValidationSucceeded
  ProcessingValidationFailedBatchCount == 0
  WorkerFailedBatchCount == 0
  WorkerFailedWorkItemCount == 0
ValidationSucceeded now includes processing-result validity for rebalance
  session rows, preventing processing-invalid batches from looking green
CLI output prints processing completeness and processing validation failed
  batch counts
```

Verification after implementation:

```text
focused synthetic mixed-radar regression:
  KINX late-day file followed by KTLX early-day file
  unfiltered MeasureCache() source count: 46080
  radar-filtered KTLX MeasureCache() source count: 23040
  processing completeness: succeeded
  worker failed batches/items: 0/0

local mixed-cache-all borrowed async spot check:
  source count: 46080
  examined/skipped/published files: 1554/726/828
  validation: succeeded
  processing completeness: succeeded
  processing validation failed batches: 0
  worker completed/failed batches: 828/0
  worker succeeded/failed items: 3312/0

local mixed-cache-all omitted default candidate spot check:
  source count: 46080
  examined/skipped/published files: 1554/726/828
  validation: succeeded
  processing completeness: succeeded
  processing validation failed batches: 0
  worker completed/failed batches: 828/0
  worker succeeded/failed items: 3312/0
  retained payload pool misses: 0
  retained payload failed releases: 0
  provider overlap failed releases: 0
```

## Guardrails

```text
correctness:
  unchanged; same-run BlockingBorrowed oracle remains required for gates
  worker failed batches/items are processing-completeness blockers

release and cleanup:
  unchanged; retained payload and provider overlap releases must remain 0

pressure:
  unchanged; retained pressure must return to 0 after measured rows

attribution:
  prewarm cost is a first-class result field and CLI output, not hidden inside
  measured allocation

scope:
  default change is direct benchmark/file-small-cache scoped, not a runtime
  provider default expansion
```

## Decision-Trace Input

```text
natural file/small-file contour:
  historical baseline evidence remains allocation-blocked

current file/small-file default contour:
  queued-owned rollout-default contour with retained payload prewarm

file/small-file readiness posture:
  ready for decision trace if the up-front prewarm cost is accepted as named
  default cost and the filesystem timing note remains non-blocking

broader cache-level readiness:
  milestone 016 accepted posture remains intact

next step:
  write the formal decision trace and closeout using the prewarmed default
  posture
```
