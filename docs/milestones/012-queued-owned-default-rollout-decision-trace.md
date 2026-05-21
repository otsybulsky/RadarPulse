# Milestone 012 Decision Trace

Date: 2026-05-21

Decision: queued-owned + pooled-copy + producer-consumer is accepted as the
scoped default for the `processing benchmark rebalance-archive` CLI omitted
provider path.

This decision applies to the CLI parse/default surface implemented in
milestone 012. It does not make direct infrastructure method defaults or any
live ingestion path implicitly queued-owned.

## Decision Matrix

```text
default provider posture:
  accepted for scoped CLI omitted-provider rebalance-archive runs

fallback posture:
  blocking-borrowed remains selectable through explicit
  --provider blocking-borrowed

borrowed oracle posture:
  same-run blocking-borrowed comparison remains required for future benchmark
  gates and rollout regressions

rollout threshold acceptance:
  accepted under the measured local contours in the performance gate

controlled proof separation:
  controlled consumer-delay mechanics remain separate from natural rollout
  evidence

residual allocation risk:
  accepted under the <= 1.10x borrowed threshold; still recorded as residual
  overhead, not as zero-cost behavior

out-of-scope runtime expansions:
  direct MeasureFile()/MeasureCache() defaults
  live ingestion
  durable broker or cross-process transport
  automatic fallback from queued-owned failure to borrowed success
```

## Included Surface

Included:

```text
processing benchmark rebalance-archive
  omitted --provider
  omitted --provider-overlap
  omitted --retention-strategy
  omitted --execution
  omitted --workers
  omitted --queue-capacity
  omitted --queue-retained-bytes
  omitted --queue-telemetry
  omitted --overlap-telemetry
  omitted --overlap-consumer-delay-ms
```

Effective scoped default:

```text
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

Excluded:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile() direct default
RadarProcessingArchiveRebalanceBenchmark.MeasureCache() direct default
synthetic processing benchmarks
non-benchmark archive publishing APIs
live ingestion/runtime provider defaults
builder-transfer retained payload execution
automatic failover from queued-owned to blocking-borrowed
```

Direct infrastructure defaults remain blocking-borrowed unless the caller
explicitly passes queued-owned options. This preserves direct API compatibility
and keeps the same-run borrowed oracle easy to invoke.

## Evidence

Primary source documents:

```text
docs/milestones/012-queued-owned-default-rollout.md
docs/milestones/012-queued-owned-default-rollout-plan.md
docs/milestones/012-queued-owned-default-rollout-performance-gate.md
```

Focused regression before the gate:

```text
25 passed, 0 failed, 0 skipped for focused CLI rebalance benchmark coverage.
22 passed, 0 failed, 0 skipped for focused readiness gate and overlap runner
coverage.
24 passed, 0 failed, 0 skipped for focused failure and cleanup coverage.
Release build succeeded with 0 warnings and 0 errors.
```

Natural performance gate:

```text
Release build succeeded with 0 warnings and 0 errors.

primary KTLX contour:
  --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
  three same-run borrowed/default pairs

mixed-cache contour:
  --cache data\nexrad --max-files 1000000
  one same-run borrowed/default pair across all local radar/date shapes
```

## Threshold Decisions

Validation parity: accepted.

```text
primary validation checksum:
  7_480_064_646_096_449_000

mixed-cache validation checksum:
  615_051_108_812_661_629

status:
  validation succeeded for every borrowed and default queued-owned row
  published file count, payload values, raw checksum, topology count,
  accepted moves, skipped decisions, failed migrations, validation checksum,
  and skipped reason counters matched same-run borrowed rows
```

Release health: accepted.

```text
retained payload failed releases: 0
provider overlap failed releases: 0
```

Retained cleanup: accepted.

```text
current pending retained batches at completion: 0
current pending retained payload bytes at completion: 0
current active retained batches at completion: 0
current active retained payload bytes at completion: 0
current combined retained batches at completion: 0
current combined retained payload bytes at completion: 0
```

Retained pressure budget: accepted.

```text
budget: 536_870_912 bytes
primary combined retained payload high watermark: 48_257_280 bytes
primary budget use: 8.99%
mixed-cache combined retained payload high watermark: 54_413_280 bytes
mixed-cache budget use: 10.14%
```

Allocation threshold: accepted with residual overhead.

```text
threshold: <= 1.10x borrowed
primary default queued-owned allocation ratio: 1.072x borrowed
mixed-cache default queued-owned allocation ratio: 1.064x borrowed
decision: pass
```

The allocation overhead is below the rollout threshold but remains real. Future
optimization work should continue to watch retained payload allocation, but it
is not a blocker for this scoped default decision.

Elapsed threshold: accepted.

```text
threshold: <= 1.00x borrowed
primary default queued-owned elapsed ratio: 0.855x borrowed
mixed-cache default queued-owned elapsed ratio: 0.777x borrowed
decision: pass
```

Run spread threshold: accepted.

```text
threshold: candidate spread <= 7.50% of candidate average
primary default queued-owned spread: 365.80 ms
primary default queued-owned spread / average: 2.39%
decision: pass
```

Default expansion evidence: accepted.

```text
candidate rows omitted provider, execution, worker, queue, retained budget,
and telemetry flags
Provider mode source: rollout-default
Provider default rollout contour: yes
Provider rollout default expansion: yes
Provider fallback contour: no
```

Fallback separation: accepted.

```text
borrowed rows used explicit --provider blocking-borrowed
Provider fallback contour: yes
queued-owned retained-resource telemetry was absent from borrowed rows
```

Natural evidence separation: accepted.

```text
Provider overlap consumer delay ms: 0.00
Provider overlap evidence contour: natural-default-candidate
Provider overlap evidence scope: natural-readiness
controlled consumer-delay proof rows were not mixed into the natural gate
```

## Operational Posture

Default posture:

```text
For processing benchmark rebalance-archive, omitting --provider now selects
the queued-owned rollout contour.
```

Fallback posture:

```text
Operators can still run the borrowed path with:
  --provider blocking-borrowed
```

Oracle posture:

```text
Same-run blocking-borrowed rows remain the comparison oracle for future
benchmark gates, performance regressions, and rollout re-validation.
```

Failure posture:

```text
queued-owned failures fail closed
there is no automatic borrowed fallback after queued-owned failure
fallback is an explicit operator/provider choice only
```

## Residual Risks And Limits

The decision is intentionally scoped.

```text
local gate only:
  the performance gate used the locally available NEXRAD cache shapes

benchmark surface only:
  the accepted default applies to the rebalance-archive CLI benchmark surface

direct API compatibility:
  direct MeasureFile()/MeasureCache() defaults remain blocking-borrowed

allocation overhead:
  candidate allocation is below threshold but still above borrowed

natural queue depth:
  natural runs kept queue depth at 1; this is favorable for pressure, while
  queued-ahead mechanics remain covered by controlled proof tests

no live ingestion claim:
  live ingestion, durable queues, brokers, and cross-process providers remain
  outside milestone 012
```

## Decision

Milestone 012 answers the closeout question with **yes** for the scoped CLI
surface:

```text
queued-owned + pooled-copy + producer-consumer becomes the scoped
processing benchmark rebalance-archive default selected by omitted provider
flags
```

Blocking-borrowed remains the explicit fallback and same-run oracle:

```text
--provider blocking-borrowed
```

The milestone can proceed to closeout without reverting the default expansion.
