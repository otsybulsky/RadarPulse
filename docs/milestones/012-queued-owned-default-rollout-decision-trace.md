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

## Decision Explanations

### Scoped CLI Default

Decision: make queued-owned + pooled-copy + producer-consumer the scoped
default only for `processing benchmark rebalance-archive` when provider-related
flags are omitted.

Why chosen: the milestone 012 gate proved the full omitted-provider contour on
the target benchmark surface. Correctness matched same-run borrowed rows,
retained resources were released, retained pressure stayed under budget,
allocation stayed below the rollout threshold, and elapsed time improved.

Alternatives: keep omitted provider on blocking-borrowed, require users to pass
the full queued-owned option set, or also change direct `MeasureFile()` /
`MeasureCache()` defaults in the same milestone.

Rejected because: keeping the old omitted-provider behavior would leave the
rollout candidate hidden behind an expert-only command; requiring the full
option set would make routine revalidation fragile; changing direct APIs at the
same time would expand the blast radius before the CLI default was stable.

Trade-offs/debt: the decision creates a split surface. CLI omitted-provider
runs use rollout-default provenance, while direct APIs remain
blocking-borrowed. That split must stay visible in help, output, tests, and
handoff.

Review explanation: "I accepted the queued-owned default only where the gate
proved it: the rebalance-archive CLI omitted-provider path."

### Explicit Borrowed Fallback And Oracle

Decision: keep `--provider blocking-borrowed` as the explicit fallback and
same-run oracle.

Why chosen: default migration needs a stable comparison path. Borrowed rows
make correctness, allocation, elapsed time, provenance, and retained-resource
behavior comparable inside the same run shape.

Alternatives: remove the borrowed path from the CLI, keep it only in direct
APIs, or allow the CLI to silently fall back to borrowed after queued-owned
failure.

Rejected because: removing the fallback would make rollback and benchmark
diagnosis harder; keeping it only in direct APIs would make operator validation
less reproducible; silent fallback would hide queued-owned failures.

Trade-offs/debt: future gates must keep running explicit borrowed rows. The
fallback path remains part of the supported benchmark surface even though the
omitted-provider path moved forward.

Review explanation: "The old path stays reachable, but only as an explicit
fallback and measurement oracle."

### Controlled Proof Separation

Decision: keep controlled consumer-delay proof rows separate from natural
rollout evidence.

Why chosen: controlled delay proves queue mechanics under forced overlap, while
the rollout default must be judged by the natural omitted-provider contour with
consumer delay `0`.

Alternatives: combine controlled proof rows with the performance gate, remove
controlled proof after default rollout, or treat forced delay as part of the
default contour.

Rejected because: mixing forced-delay rows with natural rows would overstate the
natural rollout signal; removing the proof would weaken mechanics coverage; and
making delay part of the default would change the actual runtime contour being
rolled out.

Trade-offs/debt: natural runs may show low queue depth. Queue-ahead mechanics
remain covered by controlled tests rather than by the natural gate.

Review explanation: "The gate measures the real default; controlled delay only
proves the mechanics that may appear under different workload timing."

### Allocation Overhead Is Accepted But Not Hidden

Decision: accept the measured allocation overhead because it stayed inside the
`<= 1.10x` borrowed threshold.

Why chosen: allocation was higher than borrowed but within the predeclared
rollout budget, while correctness, cleanup, release health, retained pressure,
and elapsed time passed.

Alternatives: block the rollout until allocation matches borrowed, raise the
threshold after seeing the data, or treat allocation as irrelevant because
elapsed time improved.

Rejected because: exact allocation parity was not the gate contract; changing
the threshold after measurement would weaken the decision; ignoring allocation
would hide a real owned-retention cost.

Trade-offs/debt: queued-owned remains more allocation-heavy than borrowed. The
cost is accepted for the scoped CLI default, but it remains a follow-up signal
for later expansion.

Review explanation: "The rollout passed the allocation gate, but the extra
allocation remains part of the decision, not something to erase from the
record."

### Direct API Defaults Remain Out Of Scope

Decision: leave direct `MeasureFile()` and `MeasureCache()` defaults on
blocking-borrowed.

Why chosen: the milestone changed the CLI parse/default surface only. Direct
API callers have compatibility expectations that should be migrated through a
separate milestone with their own guardrails.

Alternatives: migrate direct API defaults together with the CLI, make direct
APIs infer the CLI rollout default, or require every direct caller to pass an
explicit provider immediately.

Rejected because: the gate did not prove direct default migration as a behavior
change; coupling direct APIs to CLI parsing would blur ownership; forcing all
direct callers to pass provider options would be a disruptive compatibility
change.

Trade-offs/debt: the codebase carries two default postures for one more phase.
Tests and documentation must keep that boundary explicit until a direct API
migration milestone changes it.

Review explanation: "The CLI default can move first because it has a bounded
operator surface; direct API compatibility gets its own migration decision."

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
