# Milestone 018: Closeout

## Status

Milestone 018 is complete.

RadarPulse keeps queued-owned runtime/live readiness as **explicit opt-in
only** for the scoped in-process runtime/archive replay surfaces. The
milestone accepts queued-owned as runtime-safe when selected explicitly with
startup retained payload prewarm and the existing queue, pressure,
cancellation, failure, release, cleanup, and observability guardrails.

The important milestone result is:

```text
017 accepted direct benchmark file/cache readiness with retained payload
    prewarm as evidence.
018 does not promote that evidence into omitted runtime/live defaults.
018 accepts scoped in-process runtime/archive replay readiness only as
    explicit opt-in, with gradual rollout required before default expansion.
```

Final readiness posture:

```text
explicit opt-in only, queued-owned is runtime-safe when selected explicitly
for scoped in-process runtime/archive replay surfaces, but it is not accepted
as the omitted runtime/live ingestion default
```

The accepted warnings and limits are:

```text
startup prewarm:
  accepted only as explicit lifecycle cost; not hidden runtime behavior

natural first-use:
  remains allocation-blocked for omitted default readiness

runtime default:
  omitted runtime/live defaults remain unchanged

coverage:
  true live ingestion, durable queues, brokers, cross-process workers,
  production runtime selection/reporting, and repeated variance gates remain
  future work
```

## Final Outcome

Implemented:

- Runtime/archive-provider surface and lifecycle audit.
- Runtime readiness contract and gate matrix.
- Reporting and temporary harness schema.
- Runtime prewarm posture: startup-owned retained payload prewarm selected as
  the explicit queued-owned gate candidate.
- Scoped `ShutdownMode.CancelQueued` cancellation guardrail fix.
- Guardrail coverage for queue pressure, retained-byte pressure,
  cancellation, drain, validation failure, release failure, cleanup, and no
  automatic borrowed fallback.
- Bounded runtime steady intake gate over deterministic local archive replay.
- Runtime pressure/backpressure/cancellation/failure gate over synthetic
  leased-batch lifecycle shapes.
- Gate interpretation with no production follow-up fix required.
- Formal decision trace in the standard milestone format.
- Handoff and project-progress updates.

Not implemented:

- Omitted runtime/live queued-owned default promotion.
- Hidden or implicit runtime prewarm.
- Natural first-use queued-owned default readiness.
- True live ingestion adapter evidence.
- Durable queue or broker integration.
- Cross-process provider or worker transport.
- Ordered concurrent rebalance commit barrier.
- Multiple active rebalance-enabled processing batches.
- `builder-transfer` retained payload execution.
- Source-level migration or partition splitting.
- Synthetic processing benchmark default migration.
- Production operator reporting, alerting, rollback, or deployment runbooks.
- Product-facing radar analysis or visualization features.
- Automatic silent borrowed fallback after queued-owned failure.
- Threshold changes after gate capture.

## Final Runtime Readiness Posture

Accepted explicit surface:

```text
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingArchiveQueuedOverlapRunner
RadarProcessingOwnedBatchQueue
RadarProcessingQueuedProcessingSession
RadarProcessingQueuedRebalanceSession
```

Accepted explicit candidate contour:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async shard transport
worker count: 4
worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536870912
startup retained payload prewarm: explicit candidate lifecycle only
```

Explicit prewarm sizing:

```text
event count: 65_536
payload bytes: 67_108_864
retained batch count: 1
```

Accepted answer:

```text
queued-owned is runtime-safe when selected explicitly for scoped in-process
runtime/archive replay surfaces with startup prewarm and existing guardrails
```

Rejected default posture:

```text
queued-owned is not accepted as the omitted runtime/live ingestion default
```

## Gate Summary

Steady intake gate:

```text
raw outputs:
  data\temp\m018-runtime-gate-runner\output\m018-runtime-20260522-134534.jsonl
  data\temp\m018-runtime-gate-runner\output\m018-runtime-20260522-134534.md

rows: 12
pass safety guardrails: 12
processing completeness failures: 0
worker failure rows: 0
release failure rows: 0
terminal pressure failure rows: 0
max queue depth high-watermark: 1
max combined retained bytes high-watermark: 48_342_240
```

Startup-prewarmed queued-owned candidate:

```text
elapsed ratios versus borrowed/reference:
  0.910x, 0.980x, 0.955x, 0.997x

allocation ratios versus borrowed/reference:
  1.000x, 1.001x, 1.000x, 1.002x

prewarm allocated bytes per row:
  71_303_392

prewarm retained bytes per row:
  71_303_168
```

Natural first-use queued-owned control:

```text
allocation ratios versus borrowed/reference:
  1.196x, 2.040x, 1.284x, 1.373x

result:
  allocation warning/optimize/fail control evidence; not omitted default proof
```

Pressure/failure gate:

```text
raw outputs:
  data\temp\m018-runtime-pressure-gate-runner\output\m018-pressure-20260522-135835.jsonl
  data\temp\m018-runtime-pressure-gate-runner\output\m018-pressure-20260522-135835.md

rows: 11
pass: 11
fail: 0
operator-visible rows: 11
terminal pressure clean rows: 11
backpressure rows: 3
cancellation rows: 4
failure rows: 6
release-failure visible rows: 1
max queue depth high-watermark: 3
max combined retained bytes high-watermark: 6
```

Covered pressure/failure shapes:

```text
return-full queue capacity rejection
retained-byte budget rejection
wait-on-full queue timeout
enqueue cancellation before start and while waiting
cancel-queued shutdown for accepted pending work
archive overlap cancellation after accepted enqueue
active consumer cancellation with active retained resource release
drain with pending work
processing validation failure without borrowed fallback
retained release failure visibility and readiness blocking
producer failure pending-resource cleanup
```

## Preserved Invariants

```text
direct benchmark readiness from milestone 017 remains evidence, not automatic
  runtime approval
explicit BlockingBorrowed remains fallback/oracle where supported
queued-owned failures fail closed
there is no automatic silent borrowed fallback after queued-owned failure
provider enqueue success is not reported as processing completion
processing completeness is required for accepted runtime rows
processing validation failed batches remain readiness blockers
worker failed batches/items remain readiness blockers
release failures remain readiness blockers
retained pressure returns to zero after success, cancellation, failure, drain,
  and cleanup gates
startup/prewarm cost remains separate from steady measured allocation
durable/cross-process/ordered-concurrent surfaces remain out of scope
```

## Residual Risks And Limits

```text
true live ingestion:
  not proven; deterministic archive replay was used as live-input stand-in

durable/cross-process:
  queues, brokers, and cross-process provider/worker behavior remain out of
  scope

natural first-use:
  allocation-blocked for omitted default readiness

startup prewarm:
  explicit lifecycle cost only; production startup ownership/reporting is not
  wired

repeatability:
  bounded steady gate did not include repeated variance classification

synthetic pressure/failure rows:
  prove lifecycle shapes, not real network input behavior

operator reporting:
  production runtime reporting and default selection surfaces were not added

full-suite allocation sensitivity:
  one synthetic benchmark allocation-threshold test remains sensitive in the
  full suite but passes in isolated rerun
```

## Verification

Final verification used for closeout:

```text
Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused runtime guardrail suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests"
  result: 56 passed, 0 failed

full test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  result: 774 passed, 1 failed, 3 skipped
  failure:
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
    expected bounded allocation, got 269_460_016 bytes

isolated rerun of failing test:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed
```

The full-suite failure is the same allocation-sensitive synthetic benchmark
test observed earlier in milestone 018. It is outside the runtime
queue/session/archive-overlap guardrail surface, and the isolated rerun passed.

This closeout slice is documentation-only.

## Decision Trace

The decision trace is written in
`018-runtime-live-ingestion-readiness-decision-trace.md`.

Final closeout answer:

```text
explicit opt-in only, queued-owned is runtime-safe when selected explicitly
for scoped in-process runtime/archive replay surfaces with startup prewarm and
existing guardrails, but it is not accepted as the omitted runtime/live
ingestion default
```

Recommended next milestone input:

```text
design gradual runtime rollout for queued-owned explicit opt-in. Add
production runtime provider selection, operator-visible provider/prewarm/
pressure/failure reporting, explicit startup prewarm lifecycle wiring,
repeatability gates, and true live ingestion or narrower archive-runtime
rollout evidence before revisiting runtime default promotion.
```
