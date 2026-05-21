# Milestone 012: Closeout

## Status

Milestone 012 is complete.

RadarPulse now has an explicit scoped default rollout for the archive
rebalance benchmark CLI surface. Omitting provider-related flags from
`processing benchmark rebalance-archive` selects the measured rollout contour:
`queued-owned + pooled-copy + producer-consumer`, with async execution,
workers 4, queue capacity 8, retained-byte budget `536_870_912`, queue
telemetry enabled, overlap telemetry enabled, and consumer delay disabled.

The important milestone result is:

```text
011 proved optimized queued-owned was credible enough to propose.
012 accepts it as the scoped rebalance-archive CLI default.
```

The rollout is deliberately scoped. Direct infrastructure method defaults,
synthetic benchmarks, non-benchmark archive publishing APIs, and live ingestion
do not change in milestone 012.

## Final Outcome

Implemented:

- Rollout thresholds recorded before final gate interpretation.
- `RadarProcessingQueuedProviderRolloutThresholds` for release health,
  retained cleanup, retained pressure budget, allocation ratio, elapsed ratio,
  and repeated-run spread.
- Readiness evaluator coverage for retained-resource cleanup completion and
  candidate run spread.
- CLI option provenance for current defaults, rollout defaults, explicit
  values, and not-applicable fields.
- Scoped omitted-provider default expansion for
  `processing benchmark rebalance-archive`.
- Operator output fields that distinguish rollout default expansion, explicit
  fallback, explicit opt-in diagnostic rows, and controlled proof rows.
- Explicit `--provider blocking-borrowed` fallback preservation.
- Same-run blocking-borrowed oracle preservation for future gates and
  regressions.
- Direct `MeasureFile()` and `MeasureCache()` default compatibility coverage;
  direct infrastructure defaults remain blocking-borrowed.
- Failure, cancellation, validation, release, and retained-cleanup guardrails
  under the default queued-owned CLI contour.
- Focused regression pass before performance gate capture.
- Natural rollout performance gate.
- Rollout decision trace.
- Closeout and handoff updates.

Not implemented:

- Direct `RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` or
  `MeasureCache()` default switch to queued-owned.
- Synthetic processing benchmark default changes.
- Non-benchmark archive publishing API default changes.
- Live ingestion/runtime provider default changes.
- `builder-transfer` retained payload execution.
- Durable queue or broker integration.
- Cross-process provider or worker transport.
- Multiple active rebalance-enabled processing batches.
- Ordered concurrent rebalance commit barrier.
- Source-level migration or partition splitting.
- Physical worker-local state transfer.
- Complex radar algorithms or product-facing radar analysis features.
- Automatic silent fallback from queued-owned failure to borrowed success.

## Final Default Posture

Scoped default:

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

Fallback:

```text
--provider blocking-borrowed
```

Borrowed oracle:

```text
same-run blocking-borrowed rows remain required for future benchmark gates,
performance regressions, and rollout re-validation
```

Out-of-scope defaults:

```text
direct MeasureFile()/MeasureCache() calls still default to blocking-borrowed
live ingestion has no provider default claim from milestone 012
automatic fallback after queued-owned failure remains unsupported
```

## Completion Checklist

```text
[x] baseline default surface audit is captured
[x] rollout thresholds are recorded before final gate interpretation
[x] default contour constants/provenance are implemented or documented
[x] CLI default expansion resolves to queued-owned rollout contour
[x] explicit blocking-borrowed fallback remains selectable
[x] same-run borrowed benchmark oracle remains available
[x] operator output distinguishes default expansion, fallback, rollout
    contour, opt-in diagnostic, and controlled proof
[x] invalid mixed provider options fail closed
[x] failure, cancellation, and cleanup guardrails remain covered
[x] focused regression pass succeeds before gate capture
[x] natural rollout performance gate is captured
[x] performance gate interprets correctness, cleanup, pressure, allocation,
    timing, and variance thresholds
[x] decision trace records whether the provider default changed
[x] closeout is written
[x] handoff is updated with the current default provider posture
```

## Final Verification

Focused closeout verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
47 passed, 0 failed, 0 skipped.
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
751 passed, 0 failed, 3 skipped.
```

## Performance Gate Summary

The natural rollout performance gate is captured in
`012-queued-owned-default-rollout-performance-gate.md`.

Primary KTLX contour:

```text
input: data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
borrowed rows: 3
omitted-provider rollout-default rows: 3
```

Primary result:

```text
borrowed average elapsed ms: 17_865.16
default queued-owned average elapsed ms: 15_274.16
default queued-owned elapsed ratio: 0.855x borrowed
default queued-owned allocation ratio: 1.072x borrowed
default queued-owned run spread: 2.39% of average
```

Mixed-cache result:

```text
input: data\nexrad --max-files 1000000
borrowed elapsed ms: 77_542.34
default queued-owned elapsed ms: 60_229.87
default queued-owned elapsed ratio: 0.777x borrowed
default queued-owned allocation ratio: 1.064x borrowed
```

Gate interpretation:

```text
validation parity: pass
release failures: pass, 0 failed releases
cleanup at completion: pass, current retained pressure returns to 0
retained pressure budget: pass, max observed combined retained payload
  high-water mark is 54_413_280 bytes of the 536_870_912 byte budget
allocation ratio: pass, <= 1.10x borrowed
elapsed ratio: pass, <= 1.00x borrowed
candidate spread: pass, <= 7.50% of candidate average
default expansion evidence: pass
fallback separation: pass
```

## Decision Trace

The decision trace is written in
`012-queued-owned-default-rollout-decision-trace.md`.

Final decision:

```text
queued-owned + pooled-copy + producer-consumer becomes the scoped
processing benchmark rebalance-archive default selected by omitted provider
flags
```

Blocking-borrowed remains explicit fallback and same-run oracle:

```text
--provider blocking-borrowed
```

## Preserved Invariants

Milestone 012 preserves:

```text
blocking-borrowed remains explicitly selectable
same-run borrowed comparison remains available and required for gates
queued-owned failures fail closed
no automatic borrowed fallback follows queued-owned failure
controlled consumer-delay rows remain mechanics-only proof
direct infrastructure defaults remain blocking-borrowed
builder-transfer remains unsupported
retained cleanup must return current pending, active, and combined pressure
  to zero at completion
release failures remain rollout blockers
```

## Residual Risks And Limits

The default decision is scoped by evidence:

```text
local gate only:
  the performance gate used the locally available NEXRAD cache shapes

benchmark surface only:
  the accepted default applies to the rebalance-archive CLI benchmark surface

direct API compatibility:
  direct MeasureFile()/MeasureCache() defaults remain blocking-borrowed

allocation overhead:
  queued-owned remains below the 1.10x threshold but still allocates more than
  borrowed on measured rows

natural queue depth:
  natural runs kept queue depth at 1; queued-ahead mechanics remain covered by
  controlled proof tests

no live ingestion claim:
  live ingestion, durable queues, brokers, and cross-process providers remain
  outside milestone 012
```

## Carry Forward

Recommended next milestone inputs:

- Decide whether the next scope is broader default rollout, direct API default
  migration, live/durable ingestion, or ordered concurrent rebalance execution.
- Keep `--provider blocking-borrowed` and same-run borrowed comparisons for
  benchmark gates and regressions.
- Re-run the rollout gate when local data shape, runtime, .NET version, or
  retained payload implementation changes materially.
- Treat direct API and live ingestion defaults as separate decisions requiring
  their own compatibility and failure evidence.
- Continue watching residual allocation overhead even though it passed the
  milestone 012 threshold.
- Keep `builder-transfer` unsupported until safe ownership transfer is proven.
