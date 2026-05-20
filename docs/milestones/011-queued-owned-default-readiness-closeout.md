# Milestone 011: Closeout

## Status

Milestone 011 is complete.

RadarPulse now has a measured queued-owned default-readiness candidate:
`queued-owned + pooled-copy + producer-consumer` with async execution, workers
4, queue capacity 8, retained-byte budget `536_870_912`, queue telemetry
enabled, overlap telemetry enabled, and consumer delay disabled.

The important milestone result is:

```text
010 proved optimized queued-owned is worth keeping.
011 proved optimized queued-owned is credible enough to propose for an explicit
    default-rollout milestone.
```

The milestone does not make `queued-owned` the default. `blocking-borrowed`
remains the default provider mode and same-run oracle.

## Final Outcome

Implemented:

- Baseline readiness audit and frozen default-candidate contour.
- Retained-resource pressure contracts for pending, active, and combined
  retained batches and payload bytes.
- Active consumer retained-resource lifecycle accounting after dequeue.
- Provider queue telemetry compatibility extensions for active and combined
  retained pressure.
- Overlap telemetry and benchmark result propagation for retained pressure,
  release health, allocation, and evidence labels.
- Candidate configuration output and CLI controls for repeatable gates.
- Readiness validation contracts and evaluator coverage.
- Failure, cancellation, and cleanup gate coverage.
- CLI and operator telemetry output for queue pressure, overlap pressure,
  retained payload release health, candidate labels, and evidence scope.
- Natural Release gate matrix with same-run borrowed reference rows.
- Expanded-cache follow-up over multiple local radar/date shapes.
- Retained payload allocation optimization through a bounded large byte-array
  pool.
- Controlled proof separation hardening in CLI output and tests.
- Decision trace, closeout, and handoff updates.

Not implemented:

- `queued-owned` as the default provider mode.
- Silent provider fallback or automatic default rollout.
- `builder-transfer` retained payload execution.
- Multiple active rebalance-enabled processing batches.
- Ordered concurrent rebalance commit barrier.
- Durable queue or broker integration.
- Live ingestion.
- Cross-process provider or worker transport.
- Source-level migration or partition splitting.
- Physical worker-local state transfer.
- Complex radar algorithms or product-facing radar analysis features.

## Completion Checklist

```text
[x] baseline readiness audit is captured
[x] default-candidate contour is frozen and reproducible
[x] retained-resource pressure contracts are implemented and tested
[x] provider queue telemetry exposes pending, active, and combined retained
    pressure without breaking milestone 010 fields
[x] active consumer retained-resource accounting is wired into queued drains
[x] pending cleanup and active release paths update pressure and release
    telemetry correctly
[x] overlap telemetry carries active and combined retained pressure
[x] archive benchmark file/cache result shapes carry readiness pressure fields
[x] CLI output prints active and combined retained pressure fields
[x] candidate configuration output is explicit and reproducible
[x] readiness validation/gate contracts are implemented and tested
[x] failure and cancellation cleanup paths are tested
[x] controlled consumer-delay proof is labeled and separated from natural gates
[x] repeated natural Release gate matrix is captured
[x] performance gate interprets correctness, allocation, memory pressure,
    cleanup, throughput, and variance
[x] expanded-cache follow-up closes local input-diversity gap
[x] retained payload allocation optimization reduces the allocation blocker
[x] controlled-proof separation is hardened in CLI output and tests
[x] decision trace is written
[x] closeout is written
[x] handoff is updated for the next milestone
```

## Final Verification

Latest focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
38 passed, 0 failed, 0 skipped for focused CLI, readiness gate, and overlap
runner coverage.
```

Latest Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Recorded result:

```text
Release build succeeded with 0 warnings and 0 errors.
```

Latest full test project:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
740 passed, 0 failed, 3 skipped.
```

## Performance Gate Summary

The full gate is captured in
`011-queued-owned-default-readiness-performance-gate.md`.

### Natural Readiness

The natural gate keeps consumer delay disabled and labels the candidate as
`natural-default-candidate`.

Evidence:

```text
correctness parity: passed
topology/rebalance parity: passed
release failures: 0
active and combined retained pressure: visible
natural producer-consumer overlap: yes
controlled proof rows: excluded from natural performance aggregates
```

Repeated primary KTLX contour:

```text
candidate average elapsed ms: 15_635.33
borrowed average elapsed ms: 17_281.31
candidate delta: -9.52%
candidate spread: 783.40 ms, 5.01% of average
borrowed spread: 336.70 ms, 1.95% of average
```

Expanded mixed-cache contour:

```text
radar/date shapes:
  2026-05-04/KINX
  2026-05-04/KTLX
  2026-05-05/KTLX
examined files: 1_554
published base-data files: 828
payload values: 32_306_203_200
raw value checksum: 958_518_408_830
validation checksum: 615_051_108_812_661_629
combined retained payload high-water: 54_413_280 bytes
retained-byte budget: 536_870_912 bytes
release failures: 0
```

### Allocation Optimization

Expanded mixed-cache allocation movement:

```text
borrowed end-to-end allocated bytes: 3_811_549_280
pre-optimization candidate allocated bytes: 5_897_703_080
post-optimization candidate allocated bytes: 4_063_709_976
pre-optimization candidate ratio to borrowed: 1.547x
post-optimization candidate ratio to borrowed: 1.066x
candidate excess allocation reduction: 87.91%
retained payload allocation reduction: 88.12%
end-to-end candidate allocation reduction: 31.10%
```

Interpretation: the allocation blocker is reduced to a residual rollout
caution. The optimized candidate still allocates about `6.6%` more than
borrowed on the measured expanded-cache contour.

### Natural Queue Depth

Natural rows did not accumulate queued-ahead backlog:

```text
provider overlap has producer-consumer overlap: yes
provider overlap has queued-ahead overlap: no
queue depth high watermark: 1
```

Interpretation: this is balanced natural pipeline behavior on the measured
local contours. Controlled delay remains the mechanics proof that queue-ahead
works under intentional consumer slowdown.

### Evidence Labels

CLI output now separates contour and scope:

```text
natural-default-candidate -> natural-readiness
controlled-proof -> controlled-mechanics-proof
natural-opt-in -> opt-in-diagnostic
not-applicable -> not-applicable
```

Controlled delay remains rejected unless the command uses
`--provider queued-owned --provider-overlap producer-consumer`, and controlled
rows remain excluded from natural readiness.

## Final Decision

`queued-owned + pooled-copy + producer-consumer` is accepted as a future
default-rollout candidate under the measured contour. The provider default does
not change in milestone 011.

The next milestone should be an explicit default rollout decision. It should:

- choose whether and how to switch the default provider mode;
- preserve `blocking-borrowed` as an operator-selectable fallback;
- keep same-run borrowed comparison available for benchmark gates;
- define rollout guardrails for allocation ratio, retained pressure, release
  failures, validation parity, and run variance;
- record residual allocation overhead as a rollout risk;
- avoid bundling default rollout with builder transfer, durable queues, live
  ingestion, or concurrent rebalance execution.

## Carry Forward

Carry these facts into the next milestone:

```text
default remains blocking-borrowed at 011 closeout
queued-owned candidate contour is ready to propose for explicit rollout
residual allocation overhead is 1.066x borrowed on expanded mixed-cache
combined retained pressure stayed within budget on measured rows
release failures stayed at 0
natural queued-ahead backlog was not observed and is not treated as a blocker
controlled queue-ahead proof remains mechanics-only evidence
builder-transfer remains unsupported
```
