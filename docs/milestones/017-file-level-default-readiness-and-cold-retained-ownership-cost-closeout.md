# Milestone 017: Closeout

## Status

Milestone 017 is complete.

RadarPulse accepts file-level and small-file default readiness for the
queued-owned direct/default archive rebalance contour used by
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` and `MeasureCache()`
when provider/execution/queue/retention controls are omitted, with retained
payload prewarm as the scoped direct benchmark default-equivalent path.

The important milestone result is:

```text
016 accepted broader cache-level benchmark/default readiness with named
    scoped warnings.
017 accepts file-level and small-file benchmark/default readiness for the
    same queued-owned rollout contour after scoped retained payload prewarm,
    with named scoped warnings.
```

Final readiness posture:

```text
yes with warnings, file-level and small-file default readiness is accepted for
the prewarmed direct benchmark default-equivalent contour
```

The accepted warnings and limits are:

```text
prewarm cost:
  retained payload prewarm is a real up-front default cost and must remain
  explicitly reported outside measured row allocation

natural cold allocation:
  natural unprewarmed MeasureFile and low-count MeasureCache rows remain
  allocation-blocked and are not the accepted readiness contour

filesystem timing note:
  fail-level prewarmed elapsed outliers did not reproduce in targeted repeats,
  but local file I/O timing variance remains visible

scope:
  readiness is local-corpus and direct benchmark/CLI benchmark scoped; runtime,
  durable, cross-process, and ordered concurrent rebalance defaults remain out
  of scope
```

## Final Outcome

Implemented:

- File-level corpus inventory and selected file matrix for KTLX 2026-05-04,
  KINX 2026-05-04, and KTLX 2026-05-05.
- File-level threshold policy recorded before Release gate interpretation.
- Natural `MeasureFile()` Release gate over cold, warm, small,
  representative, large, primary, cross-radar, and named-risk rows.
- Natural small-file `MeasureCache()` gate over 2/4/8 published base-data
  slices.
- Opt-in retained payload prewarm mechanics for retained event arrays,
  retained payload byte arrays, retained payload factories, and direct archive
  rebalance calls.
- Prewarmed `MeasureFile()` comparison and targeted file timing rerun.
- Prewarmed small-file `MeasureCache()` comparison.
- Scoped default prewarm for the direct benchmark default-equivalent
  queued-owned contour.
- Result and CLI prewarm attribution.
- Mixed-cache source-universe auto-sizing for unfiltered mixed-radar cache
  rows.
- Processing completeness reporting and validation gates for archive
  rebalance rows.
- Full post-default cache regression matrix after prewarm default and
  mixed-cache source-universe fix.
- Formal decision trace in the standard milestone format.
- Handoff and project-progress updates.

Not implemented:

- Natural unprewarmed file/small-cache default readiness.
- Runtime/live ingestion prewarm or runtime provider default migration.
- Durable queue or broker integration.
- Cross-process provider or worker transport.
- Ordered concurrent rebalance commit barrier.
- Multiple active rebalance-enabled processing batches.
- `builder-transfer` retained payload execution.
- Source-level migration or partition splitting.
- Synthetic processing benchmark default migration.
- Non-benchmark archive publishing API default migration.
- Product-facing radar analysis, visualization, or alerting features.
- Automatic silent borrowed fallback after queued-owned failure.
- Threshold changes after gate capture.

## Final Readiness Posture

Accepted default surface:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
CLI rebalance-archive benchmark paths that use the same direct APIs
```

Accepted direct/default contour:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async shard transport
worker count: 4
worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536870912
overlap consumer delay: 0
retained payload prewarm: enabled for the direct benchmark
  default-equivalent contour
```

Default prewarm sizing:

```text
event count: 65_536
payload bytes: 67_108_864
retained batch count: 1
```

Accepted answer:

```text
file-level and small-file default readiness is accepted with named scoped
warnings for the prewarmed direct benchmark default-equivalent contour
```

Rejected readiness posture:

```text
natural unprewarmed file and low-count cache defaults are not accepted because
they remain allocation-blocked
```

## Gate Summary

Natural `MeasureFile()`:

```text
selected file rows: 10
borrowed/default pairs: 20
safety guardrails: all passed
worker failed batches/items: 0/0
release failures: 0
current retained pressure at completion: 0
cold representative allocation ratios: 1.958x to 2.995x
warm representative allocation ratios: 1.916x to 2.186x
result: allocation-blocked
```

Prewarmed `MeasureFile()`:

```text
borrowed/prewarmed-candidate pairs: 20
safety guardrails: all passed
measured allocation ratios: 0.980x to 1.026x
retained pool misses: 0
release failures: 0
worker failed batches/items: 0/0
prewarm allocation: 35_651_808 to 71_303_392 bytes
result: allocation-ready with non-blocking filesystem timing note
```

Natural small-file `MeasureCache()`:

```text
borrowed/natural-candidate pairs: 18
published base-data counts: 2, 4, and 8
safety guardrails: all passed
elapsed ratios: all <= 0.986x borrowed average
allocation ratios: 1.176x to 2.168x
result: allocation-blocked
```

Prewarmed small-file `MeasureCache()`:

```text
borrowed/prewarmed-candidate pairs: 18
safety guardrails: all passed
measured allocation ratios: 0.818x to 1.002x
elapsed ratios: 0.454x to 0.979x
candidate spread: <= 4.18%
retained pool misses: 0
result: measured allocation and elapsed pass
```

Post-default full cache regression:

```text
raw outputs:
  data\temp\m017-cache-regression-runner\output\m017-cache-regression-20260522-110241.jsonl
  data\temp\m017-cache-regression-runner\output\m017-cache-regression-20260522-110241.md

group rows: 16 passed, 0 warning, 0 optimize, 0 failed
pairs: 28 passed safety, 0 failed safety
measurements: 56
worst measured allocation ratio: 1.009x on mixed-cache-all
worst elapsed ratio: 0.988x on KTLX 2026-05-05 2-file small-cache row
worst candidate spread: 4.60%
validation failures: 0
processing completeness failures: 0
processing validation failed batches: 0
worker failed batches/items: 0/0
retained payload pool misses: 0
release failures: 0
current retained bytes after rows: 0
```

## Preserved Invariants

```text
same-run BlockingBorrowed remains the benchmark oracle
explicit BlockingBorrowed remains selectable and unprewarmed
queued-owned failures fail closed
there is no automatic borrowed fallback after queued-owned failure
retained resources release only after final use
retained pressure returns to zero after measured rows
retained payload failed releases remain 0
provider overlap failed releases remain 0
worker failed batches/items are processing-completeness blockers
processing validation failed batches are processing-completeness blockers
CLI omitted-provider benchmark paths remain aligned with direct defaults
broader cache-level readiness from milestone 016 remains accepted
runtime/live/durable surfaces remain out of scope
```

## Residual Risks And Limits

```text
local corpus:
  evidence covers the available local NEXRAD cache and selected file rows only

prewarm lifecycle:
  direct benchmark prewarm is accepted, but runtime startup/prewarm lifecycle
  policy is not designed

filesystem timing:
  local file I/O variance remains visible and should stay in future gates

source-universe guardrail:
  unfiltered mixed-radar cache auto-sizing is capped at 256 distinct radar ids

runtime expansion:
  direct benchmark readiness is input evidence, not approval for live/runtime
  defaults
```

## Verification

Final verification used for closeout:

```text
Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused mixed-radar/default regression:
  result: passed

full test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  result: 771 passed, 0 failed, 3 skipped on stable rerun

mixed-cache-all borrowed async spot check:
  source count: 46080
  examined/skipped/published files: 1554/726/828
  processing completeness: succeeded
  worker failed batches/items: 0/0

mixed-cache-all omitted default spot check:
  source count: 46080
  examined/skipped/published files: 1554/726/828
  processing completeness: succeeded
  worker failed batches/items: 0/0
  retained payload pool misses: 0
  release failures: 0

post-default full cache regression matrix:
  16/16 groups passed
  28/28 safety pairs passed
  processing completeness failures: 0
  worker failed batches/items: 0/0
```

This closeout slice is documentation-only.

## Decision Trace

The decision trace is written in
`017-file-level-default-readiness-and-cold-retained-ownership-cost-decision-trace.md`.

Final closeout answer:

```text
yes with warnings, file-level and small-file default readiness is accepted for
the queued-owned direct benchmark default-equivalent contour with retained
payload prewarm
```

Recommended next milestone input:

```text
design runtime/live ingestion readiness as a separate milestone. Treat the
accepted direct benchmark file/cache default posture as evidence, not as
automatic approval for runtime defaults.
```
