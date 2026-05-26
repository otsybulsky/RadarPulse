# Handoff: Milestone 031 Planned

## Current State

Milestone 031 has been selected after milestone 030 closeout. The
architecture/concept document and implementation plan are written. Slice 1
URL state and validation hardening is complete. Slice 2 browser smoke harness
is complete. Slice 3 integrated static UI delivery is complete.

Stop point:

```text
milestone 031 slice 3 complete; start implementation slice 4
```

Most recently closed milestone:

```text
030 Product Operator Angular SPA
```

Current milestone:

```text
031 Operator UI Hardening And Integrated Local Delivery
```

Milestone 031 goal:

```text
make the accepted Angular operator UI the stable local product surface, with
browser-level smoke coverage, URL-restorable operator state, stricter
form/control validation, polished failure posture, and integrated local
same-origin delivery through RadarPulse.Http
```

Milestone 031 selected implementation direction:

```text
keep the Angular SPA in src/Presentation/OperatorUi
keep RadarPulse.Http as the accepted product HTTP host
add real-browser smoke coverage for critical operator workflows
make selected run and active detail tab restorable from URL state
harden local form validation and control request posture
serve the built Angular SPA from RadarPulse.Http as an integrated local
  same-origin delivery path
keep the Angular dev-server CORS bridge scoped to local development
do not reopen accepted milestone 020-030 backend or UI boundary decisions
```

Milestone 031 documents:

```text
docs/milestones/031-operator-ui-hardening-and-integrated-local-delivery.md
docs/milestones/031-operator-ui-hardening-and-integrated-local-delivery-plan.md
```

Milestone 031 planned slices:

```text
1. URL state and validation hardening [complete]
2. Browser smoke harness [complete]
3. Integrated static UI delivery [complete]
4. Same-origin smoke and local workflow docs [planned]
5. Gate evidence and handoff [planned]
```

Latest verification:

```text
milestone 031 slice 1:
  Angular gate:
    18 passed, 0 failed
    production build succeeded, 0 warnings

milestone 031 slice 2:
  browser smoke gate:
    4 passed, 0 failed
  Angular gate:
    18 passed, 0 failed
    production build succeeded, 0 warnings

milestone 031 slice 3:
  focused .NET product HTTP host Release gate:
    9 passed, 0 failed, 0 skipped
  Release build:
    succeeded, 0 warnings, 0 errors

inherited from milestone 030:
  focused .NET product HTTP/API Release gate:
    14 passed, 0 failed, 0 skipped
  post-refactor focused presentation Release gate:
    18 passed, 0 failed, 0 skipped
  Release build:
    succeeded, 0 warnings, 0 errors
```

Milestone 031 expected gate:

```text
Angular:
  cd src/Presentation/OperatorUi
  npm test -- --watch=false
  npm run build
  npm run smoke

.NET focused product HTTP/static-delivery Release gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

.NET Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
```

Milestone 031 planned implementation:

```text
URL-restorable selected run and active detail tab
input validation for product HTTP base URL, archive run, and handler lookup
control disabled/loading/rejected/blocked posture hardening
selected run not-found posture from URL state
Playwright-style browser smoke coverage for critical operator workflows
deterministic browser API route fixtures for operator UI smoke tests
RadarPulse.Http local static delivery for the built Angular SPA
route fallback that does not intercept /product/pipeline API routes
configured local static asset root for Angular dist output
README updates for dev-server and integrated same-origin workflows
milestone 031 gate evidence
```

Milestone 030 accepted implementation baseline:

```text
Angular 21 operator SPA in src/Presentation/OperatorUi
typed TypeScript product HTTP client over milestone 029 routes
runtime API base URL override through localStorage and topbar input
operator overview, run list, latest run, selected run detail, tabs,
  diagnostics, handler output, capacity evidence, and controls
explicit loading, empty, not-found, blocked, rejected, bad-request, and
  unreachable-host posture
presentation sibling projects under src/Presentation:
  OperatorUi
  RadarPulse.Cli
  RadarPulse.Http
```

Milestone 029 accepted product HTTP surface:

```text
RadarPulse.Http local ASP.NET Core host project
product HTTP routes for demo/archive runs, run list/latest/detail, batches,
  sources, handler output, diagnostics, capacity evidence, host readiness,
  and controls
deterministic local file-backed product run history that survives service
  recreation
```

Milestone 031 scope boundary:

```text
do not reopen accepted runtime/default/durable/handler/BFF/production
pipeline/product/HTTP host/UI decisions from milestones 020-030. Do not
expand this milestone into true live network ingestion, external broker/cloud
queue/database adapter certification, deployment automation, public hosted
production readiness, auth/TLS/production CORS hardening, cross-machine
throughput certification, exactly-once production delivery, or rich radar
visualization.
```

Current next action:

```text
implement slice 4:
  Same-origin smoke and local workflow docs
```

Decision trace posture:

```text
milestone 031 decision trace has not been written
stop before decision trace after implementation slices and gate evidence are
  complete
```

Previous milestone closeout:

```text
milestone 030 closeout written:
  accepted with scoped warnings for product operator Angular SPA over the
  local product HTTP host for deterministic archive-shaped workflows

recommended next milestone input:
  operator UI hardening and integrated local delivery
```

## Previous Closed Milestone Context

### Current State

Milestone 025 is complete. Implementation slices, gate evidence, the
requested full-cache handler performance matrix, the follow-up merge-state
optimization, decision trace, and closeout are complete.

RadarPulse has accepted the scoped handler delta/merge contract for fast
custom analytics over deterministic archive-shaped MVP workloads.

Most recently closed milestone:

```text
025 Handler Delta/Merge Contract For Fast Custom Analytics
```

Recommended next milestone input:

```text
persistent durable adapter readiness
```

Milestone 025 current status:

```text
architecture/concept document: written
implementation plan: written
implementation: slices complete
handler classification contract: complete
per-batch handler delta contract: complete
deterministic ordered merge coordinator: complete
MVP runtime integration and fallback policy: complete
BFF compatibility and diagnostics: complete
handler-heavy performance gate: complete
pre-decision trace gate: captured
full-cache handler performance matrix: captured
merge-state optimization: captured
decision trace: written
closeout: written
recommended next milestone input: persistent durable adapter readiness
```

Milestone 025 documents:

```text
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-plan.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-gate.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-full-cache-performance-matrix.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-decision-trace.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-closeout.md
```

Milestone 025 goal:

```text
make stateful custom analytics fast on large volumes without weakening the
accepted ordered commit and handler output contracts
```

Milestone 025 planned slices:

```text
1. Handler classification contract [complete]
2. Per-batch handler delta contract [complete]
3. Deterministic ordered merge coordinator [complete]
4. MVP runtime integration and fallback policy [complete]
5. BFF compatibility and diagnostics [complete]
6. Handler-heavy performance gate [complete]
7. Pre-decision trace review point [complete]
```

Milestone 025 latest verification:

```text
focused milestone 025 Release gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarProcessingHandlerDeltaClassificationTests|FullyQualifiedName~RadarProcessingHandlerDeltaContractTests|FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests|FullyQualifiedName~RadarProcessingHandlerDeltaBffCompatibilityTests|FullyQualifiedName~RadarProcessingHandlerDeltaPerformanceGateTests"
  result: 26 passed, 0 failed, 0 skipped

Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused handler/full-cache CLI Release suite after merge-state optimization:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-build
    --filter "FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests|FullyQualifiedName~RadarProcessingSyntheticBenchmarkTests|FullyQualifiedName~RadarProcessingHandlerDeltaPerformanceGateTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
  result: 53 passed, 0 failed, 0 skipped

optimized full-cache handler performance matrix:
  cache: data\nexrad
  rows:
    counter-checksum active=1: 61_373.01 ms, 4_671_386_960 allocated bytes
    counter-checksum active=4: 61_588.17 ms, 8_188_695_464 allocated bytes
    counter-checksum-heavy active=1: 62_806.15 ms, 4_675_001_328 allocated bytes
    counter-checksum-heavy active=4: 62_687.17 ms, 12_209_454_512 allocated bytes
  active=4 optimization versus previous matrix:
    counter-checksum elapsed 0.785x, allocation 0.243x
    counter-checksum-heavy elapsed 0.756x, allocation 0.216x
  correctness:
    4/4 rows completed
    processing completeness succeeded
    processing validation failed batches: 0
    terminal retained pressure: 0
  decision-trace warning:
    optimized active=4 handler delta/merge is correct and full-cache elapsed
    time is flat versus active=1; allocation remains higher than active=1
    and should stay a scoped warning unless parity is required

full Release test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
  result: 890 passed, 1 failed, 3 skipped
  known allocation-sensitive synthetic benchmark caveat:
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
    Expected bounded benchmark aggregation allocation, got 894196968 bytes.

known allocation-sensitive synthetic test isolated rerun:
  result: 1 passed, 0 failed, 0 skipped

milestone 025 decision trace:
  docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-decision-trace.md
  decision:
    accepted with scoped warnings for handler delta/merge contract and fast
    custom analytics over deterministic archive-shaped MVP workloads
  recommended next milestone input:
    persistent durable adapter readiness

milestone 025 closeout:
  docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-closeout.md
  final closeout answer:
    accepted with scoped warnings for handler delta/merge contract and fast
    custom analytics over deterministic archive-shaped MVP workloads
```

Milestone 025 key scope:

```text
define mergeable, snapshot-only, and unsupported handler classification;
per-batch handler deltas; deterministic provider-sequence merge;
serialization/versioning boundaries; retry, replay, duplicate delta, and
idempotency behavior; failure diagnostics; sequential fallback parity gates;
BFF output compatibility; and a handler-heavy large-volume performance gate.
```

Milestone 025 out of scope unless explicitly pulled forward:

```text
frontend application
production HTTP BFF host
persistent durable adapter
true live network ingestion
production deployment, rollback, autoscaling, alerts, and runbooks
exactly-once production delivery claims
```

Milestone 024 documents:

```text
docs/milestones/024-custom-handler-output-contract-and-bff-readiness.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-plan.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-gate.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-full-cache-performance-matrix.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-decision-trace.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-closeout.md
```

Historical MVP planning decision:

```text
the earlier reason to defer persistent durable adapter readiness is now
satisfied: milestone 024 added custom handler output/BFF read models, and
milestone 025 added fast handler delta/merge semantics. The recommended next
milestone input is persistent durable adapter readiness.
```

Milestone 023 historical closeout documents:

```text
docs/milestones/023-durable-cross-process-runtime-readiness.md
docs/milestones/023-durable-cross-process-runtime-readiness-architecture-decision.md
docs/milestones/023-durable-cross-process-runtime-readiness-plan.md
docs/milestones/023-durable-cross-process-runtime-readiness-gate.md
docs/milestones/023-durable-cross-process-runtime-readiness-decision-trace.md
docs/milestones/023-durable-cross-process-runtime-readiness-closeout.md
```

Milestone 023 purpose:

```text
implement durable/cross-process runtime readiness over the accepted
runtime/archive baseline using a broker-neutral durable envelope contract and
deterministic in-process durable harness
```

Accepted implementation direction:

```text
accept owned runtime/archive batches into durable envelopes
assign stable batch ids and provider sequences
make worker claim, completion, failure, abandon, retry, poison, commit, and
release states explicit
allow worker completion out of provider order while committing processing and
rebalance/topology state only by provider sequence
preserve fail-closed queued-owned behavior, visible startup prewarm, no
silent borrowed fallback, worker telemetry, release health, terminal retained
pressure cleanup, and operator-visible recovery state
```

Milestone 023 planned slices:

```text
1. Durable envelope contract and queue harness
2. Durable ordered processing runtime
3. Retry, recovery, cancellation, and cleanup
4. Durable ordered rebalance runtime
5. Operator summary and gate evidence
6. Pre-decision trace review point
```

Current implementation status:

```text
architecture document: written
architecture decision: written
implementation plan: written
implementation: complete
durable envelope contract and queue harness: complete
durable ordered processing runtime: complete
retry, recovery, cancellation, and cleanup: complete
durable ordered rebalance runtime: complete
operator summary and gate evidence: complete
pre-decision trace review point: reached
gate: written
decision trace: written
closeout: written
```

Latest verification:

```text
slice 1 focused durable queue contract suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingDurableEnvelopeQueueTests"
  result: 8 passed, 0 failed, 0 skipped

Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

slice 2 focused durable processing suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingDurableProcessingSessionTests"
  result: 6 passed, 0 failed, 0 skipped

slice 1+2 durable-focused suites:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingDurableProcessingSessionTests"
  result: 14 passed, 0 failed, 0 skipped

latest Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

slice 3 focused durable recovery suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingDurableRecoveryTests"
  result: 4 passed, 0 failed, 0 skipped

slice 1-3 durable-focused suites:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingDurableRecoveryTests"
  result: 18 passed, 0 failed, 0 skipped

latest Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

slice 4 focused durable rebalance suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingDurableRebalanceSessionTests"
  result: 4 passed, 0 failed, 0 skipped

slice 1-4 durable-focused suites:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingDurableRecoveryTests|FullyQualifiedName~RadarProcessingDurableRebalanceSessionTests"
  result: 22 passed, 0 failed, 0 skipped

latest Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

slice 5 focused durable readiness summary suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingDurableRuntimeReadinessSummaryTests"
  result: 4 passed, 0 failed, 0 skipped

slice 1-5 durable-focused suites:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingDurableRecoveryTests|FullyQualifiedName~RadarProcessingDurableRebalanceSessionTests|FullyQualifiedName~RadarProcessingDurableRuntimeReadinessSummaryTests"
  result: 26 passed, 0 failed, 0 skipped

Release durable-focused gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
    --filter "FullyQualifiedName~RadarProcessingDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingDurableRecoveryTests|FullyQualifiedName~RadarProcessingDurableRebalanceSessionTests|FullyQualifiedName~RadarProcessingDurableRuntimeReadinessSummaryTests"
  result: 26 passed, 0 failed, 0 skipped

full Release test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
  result: 847 passed, 1 failed, 3 skipped
  known allocation-sensitive synthetic benchmark caveat:
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
    Expected bounded benchmark aggregation allocation, got 1134179616 bytes.

known allocation-sensitive synthetic test isolated rerun:
  1 passed, 0 failed, 0 skipped
```

Carry-forward boundaries:

```text
milestone 020 provider/execution baseline remains closed
milestone 021 non-mutating processing delta plus ordered commit remains the
  foundation
milestone 022 ordered rebalance/topology commit remains the foundation
external broker/database adapters are not planned for this project
true live network ingestion, production deployment/rollback/runbooks,
handler-state delta/merge, cross-machine performance certification, and
exactly-once production delivery claims remain future work
```

Stop point:

```text
milestone 023 implementation, gate capture, warning review, decision trace,
and closeout are complete.
```

Decision trace:

```text
accepted with scoped warnings for durable/cross-process runtime readiness over
the broker-neutral durable envelope contract and deterministic in-process
durable harness

warnings:
  external broker/database adapters are not planned for this project
  in-process durable harness is a contract gate, not a production durability
    claim
  true live network ingestion is not implemented
  production deployment, rollback, autoscaling, and runbooks are not
    implemented
  handler-state delta/merge is not implemented
  exactly-once production delivery is not claimed
  known full-suite allocation-sensitive synthetic benchmark caveat remains

recommended next milestone input:
  persistent durable adapter readiness
```

Current planning note:

```text
the milestone 023 closeout recommendation is preserved as historical input,
but milestone 024 custom handler output contract and BFF readiness was
selected as the immediate MVP slice and is now closed; persistent durable
adapter readiness remains deferred to a later reliability milestone unless
MVP scope changes again
```

Closeout:

```text
accepted with scoped warnings for durable/cross-process runtime readiness over
the broker-neutral durable envelope contract and deterministic in-process
durable harness

recommended next milestone input:
  persistent durable adapter readiness. Validate one concrete persistent or
  local file-backed adapter against the milestone 023 durable envelope
  contract, including serialization compatibility, restart recovery,
  duplicate delivery,
  lease or abandoned-attempt recovery, poison/dead-letter mapping,
  provider-sequence ordered commit, retained ownership cleanup, and
  operator-readable adapter state.
```

Milestone 024 original input:

```text
custom handler output contract and BFF readiness. Define stable handler
output DTOs, handler metadata discovery, handler-state safety posture,
processing result read models, BFF query surfaces, diagnostics, and
archive-shaped MVP gates for a future frontend.
```

Milestone 024 current status:

```text
implementation: complete
slice 1 handler output contract audit: complete
slice 2 processing output read models: complete
slice 3 BFF application read surface: complete
slice 4 handler execution posture gate: complete
slice 5 archive-shaped MVP gate: complete
slice 6 decision trace: written
slice 6 closeout: written

stop point:
  milestone 024 is closed
```

Milestone 024 implemented surfaces:

```text
RadarProcessingHandlerOutputContract
RadarProcessingHandlerOutputDescriptor
RadarProcessingHandlerOutputField
RadarProcessingHandlerStatePosture
RadarProcessingRunReadModel
RadarProcessingBatchReadModel
RadarProcessingSourceOutputReadModel
RadarProcessingHandlerOutputValueReadModel
RadarProcessingRunDiagnosticsReadModel
RadarProcessingRunReadModelBuilder
RadarProcessingBffReadModelStore
RadarProcessingMvpRuntimePlan
RadarProcessingMvpRuntimeResult
RadarProcessingArchiveQueuedOverlapRunner.RunMvpProcessingAsync
```

Milestone 024 latest verification:

```text
focused Debug slice suites:
  RadarProcessingHandlerOutputContractTests: 5 passed
  RadarProcessingRunReadModelTests: 4 passed
  RadarProcessingBffReadModelStoreTests: 4 passed
  RadarProcessingMvpRuntimePlanTests: 3 passed
  RadarProcessingMvpArchiveGateTests: 1 passed

Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused milestone 024 Release gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarProcessingHandlerOutputContractTests|FullyQualifiedName~RadarProcessingRunReadModelTests|FullyQualifiedName~RadarProcessingBffReadModelStoreTests|FullyQualifiedName~RadarProcessingMvpRuntimePlanTests|FullyQualifiedName~RadarProcessingMvpArchiveGateTests"
  result: 17 passed, 0 failed, 0 skipped

full Release test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
  result: 865 passed, 0 failed, 3 skipped
```

Optional milestone 024 full-cache performance matrix:

```text
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-full-cache-performance-matrix.md

status:
  captured and referenced by the decision trace and closeout

rebalance-archive:
  default queued-owned stayed faster than explicit BlockingBorrowed in
  static, sampling, and rebalance-session modes
  default elapsed ratios: 0.812x static, 0.931x sampling,
    0.885x rebalance-session
  default allocation ratios: 1.002x static, 1.003x sampling,
    1.000x rebalance-session
  validation, processing completeness, checksum parity, accepted move parity,
    worker health, retained pool health, release health, and terminal retained
    pressure cleanup passed

ordered-archive-processing:
  active=4 elapsed ratio versus active=1: 0.982x
  active=4 steady allocation ratio versus active=1: 1.007x
  final processing checksum matched
  processing completeness passed
  worker failed batches/items 0/0
  retained payload pool misses 0
  release failures 0
  terminal combined retained pressure 0
```

Milestone 024 decision trace:

```text
accepted with scoped warnings for custom handler output contract and BFF
readiness over deterministic archive-shaped MVP workloads

accepted readiness answer:
  yes with scoped warnings, RadarPulse is ready to expose MVP processing
  results through stable custom handler output contracts and application-level
  BFF read models for a future frontend, using committed snapshot export and
  explicit sequential fallback for stateful handlers while preserving the
  accepted handler-free ordered concurrent runtime foundations

warnings:
  stateful handlers do not yet participate in ordered concurrent delta
    compute
  handler delta/merge is not implemented
  high-volume custom analytics performance readiness is not accepted yet
  the BFF surface is an application read-model query surface, not a
    production HTTP API host
  the frontend application is not implemented
  persistent durable adapter readiness remains future reliability work
  true live network ingestion is not implemented
  production deployment, rollback, autoscaling, alerts, and runbooks are not
    implemented
  exactly-once production delivery is not claimed
  the optional full-cache matrix is regression evidence, not proof of future
    handler-heavy analytics throughput

recommended next milestone input:
  handler delta/merge contract for fast custom analytics
```

Milestone 024 closeout:

```text
accepted with scoped warnings for custom handler output contract and BFF
readiness over deterministic archive-shaped MVP workloads

accepted result:
  RadarPulse is ready to expose MVP processing results through stable custom
  handler output contracts and application-level BFF read models for a future
  frontend, using committed snapshot export and explicit sequential fallback
  for stateful handlers while preserving the accepted handler-free ordered
  concurrent runtime foundations

recommended next milestone input:
  handler delta/merge contract for fast custom analytics
```

## Milestone 022 Complete Baseline

Milestone 022 is complete. The primary milestone documents are:

```text
docs/milestones/022-ordered-rebalance-topology-commit.md
docs/milestones/022-ordered-rebalance-topology-commit-architecture-decision.md
docs/milestones/022-ordered-rebalance-topology-commit-plan.md
docs/milestones/022-ordered-rebalance-topology-commit-gate.md
docs/milestones/022-ordered-rebalance-topology-commit-processing-bottleneck-performance-matrix.md
docs/milestones/022-ordered-rebalance-topology-commit-full-cache-performance-matrix.md
docs/milestones/022-ordered-rebalance-topology-commit-decision-trace.md
docs/milestones/022-ordered-rebalance-topology-commit-closeout.md
```

Milestone 022 purpose:

```text
implement ordered rebalance/topology commit over the milestone 021 ordered
processing foundation, and collect processing-bottleneck performance evidence
before any broader default-promotion decision
```

Accepted implementation direction:

```text
compute handler-free processing deltas for multiple active rebalance batches
without mutating shared processing or rebalance state
commit processing deltas strictly by provider sequence
run pressure, policy, quarantine, telemetry, decision, migration, validation,
and topology mutation only after ordered processing commit
validate active delta topology version at commit
recompute stale active deltas when an earlier ordered rebalance migration
changed topology
preserve fail-closed queued-owned behavior, visible startup prewarm, no
silent borrowed fallback, worker telemetry, release health, and terminal
retained pressure cleanup
```

Milestone 022 planned slices:

```text
1. Ordered rebalance commit contract
2. Queued rebalance ordered concurrent drain
3. Stale topology recompute
4. Runtime/archive integration
5. Processing-bottleneck evidence
6. Gate documentation
```

Current implementation status:

```text
architecture document: written
architecture decision: written
implementation plan: written
implementation: complete
gate: written
decision trace: written
closeout: written
```

Carry-forward boundaries:

```text
milestone 020 provider/execution baseline remains closed
milestone 021 non-mutating processing delta plus ordered commit remains the
  foundation
handler-state delta/merge is not implemented
durable queues, brokers, cross-process workers, true live network ingestion,
production operator/deployment/rollback surfaces, and product-facing
workflows remain future work
```

Milestone 022 gate evidence:

```text
docs/milestones/022-ordered-rebalance-topology-commit-gate.md
docs/milestones/022-ordered-rebalance-topology-commit-processing-bottleneck-performance-matrix.md
docs/milestones/022-ordered-rebalance-topology-commit-full-cache-performance-matrix.md
docs/milestones/022-ordered-rebalance-topology-commit-decision-trace.md
docs/milestones/022-ordered-rebalance-topology-commit-closeout.md
```

Current verification:

```text
Release build:
  succeeded, 0 warnings, 0 errors

focused milestone 022 Release gate suite:
  76 passed, 0 failed, 0 skipped

known allocation-sensitive synthetic test isolated rerun:
  1 passed, 0 failed, 0 skipped

full Release test project:
  821 passed, 1 failed, 3 skipped
  known allocation-sensitive synthetic benchmark caveat:
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

post-gate full-cache performance matrix:
  no full-cache performance regression observed
  rebalance-archive default elapsed ratios:
    0.883x static, 0.891x sampling, 0.871x rebalance-session
  rebalance-archive default allocation ratios:
    1.002x static, 1.001x sampling, 1.002x rebalance-session
  ordered-archive-processing active=4 elapsed ratio versus active=1:
    0.999x
  ordered-archive-processing active=4 steady allocation ratio versus
    active=1:
    1.007x
  validation and processing completeness passed
  worker failed batches/items 0/0
  retained payload pool misses 0
  release failures 0
  terminal combined retained pressure 0
```

Decision trace:

```text
accepted with scoped warnings for ordered rebalance/topology commit over the
scoped in-process runtime/archive queued-overlap path

the accepted surface can keep multiple accepted rebalance batches active for
handler-free processing-delta compute while committing processing,
rebalance decisions, validation, and topology mutation deterministically in
provider sequence

important warnings:
  handler-state delta/merge is not implemented
  topology churn can increase stale-delta recompute, worker dispatches, and
    allocation under active-batch overlap
  full-cache rows remain archive-producer dominated, so they are regression
    evidence rather than processing-bottleneck proof
  durable queues, brokers, cross-process workers, true live network
    ingestion, production operator/deployment/rollback surfaces, and
    product-facing workflows remain future work
  one full-suite allocation-sensitive synthetic benchmark caveat remains
```

Closeout:

```text
accepted with scoped warnings, the scoped in-process runtime/archive
rebalance path is ready to keep multiple accepted batches active for
handler-free processing-delta compute while committing processing,
rebalance decisions, validation, and topology mutation deterministically in
provider sequence

important warnings:
  handler-state delta/merge is not implemented
  topology churn can increase stale-delta recompute, worker dispatches, and
    allocation under active-batch overlap
  full-cache rows remain archive-producer dominated, so they are regression
    evidence rather than processing-bottleneck proof
  durable queues, brokers, cross-process workers, true live network
    ingestion, production operator/deployment/rollback surfaces, and
    product-facing workflows remain future work
  one full-suite allocation-sensitive synthetic benchmark caveat remains
```

Recommended next milestone input:

```text
move to durable/cross-process runtime readiness. Design durable queues,
brokers, or cross-process providers/workers using the accepted prewarmed
queued-owned baseline, ordered processing commit, and ordered
rebalance/topology commit unless a concrete ownership-boundary
incompatibility is proven.
```

## Milestone 021 Complete Baseline

Milestone 021 is complete. The milestone documents are:

```text
docs/milestones/021-ordered-concurrent-runtime-archive-processing.md
docs/milestones/021-ordered-concurrent-runtime-archive-processing-plan.md
docs/milestones/021-ordered-concurrent-runtime-archive-processing-slice-3-blocker.md
docs/milestones/021-ordered-concurrent-runtime-archive-processing-architecture-decision.md
docs/milestones/021-ordered-concurrent-runtime-archive-processing-gate.md
docs/milestones/021-ordered-concurrent-runtime-archive-processing-full-cache-performance-matrix.md
docs/milestones/021-ordered-concurrent-runtime-archive-processing-ordered-full-cache-performance-matrix.md
docs/milestones/021-ordered-concurrent-runtime-archive-processing-decision-trace.md
docs/milestones/021-ordered-concurrent-runtime-archive-processing-closeout.md
```

Milestone 021 purpose:

```text
implement ordered concurrent runtime/archive processing over the accepted
milestone 020 default baseline while preserving deterministic publication,
topology and rebalance safety, fail-closed queued-owned behavior, no silent
borrowed fallback, visible startup prewarm, release/cleanup pressure
invariants, and separate provider/execution provenance
```

Milestone 021 starts from the milestone 020 accepted baseline:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
provider queue capacity: 8
retained-byte budget: 536870912
startup retained payload prewarm: enabled
execution: async shard transport
worker count: 4
worker queue capacity: 8
```

Milestone 021 concurrency target:

```text
accept provider batches in input order
bound active concurrent processing batches separately from provider queue and
  async worker queue capacity
allow processing completion to occur out of order where the surface is safe
publish externally visible processing results in deterministic provider
  sequence order
preserve processing completeness, checksum parity, topology safety, failure
  cleanup, and retained pressure cleanup
```

Milestone 021 completed slices:

```text
1. Ordered concurrency contract
2. Ordered result coordinator
3. Processing session ordered concurrency
4. Runtime/archive integration
5. Failure, cancellation, and cleanup hardening
6. Gate capture and documentation checkpoint
```

Current implementation status:

```text
architecture document: complete
implementation plan: complete
implementation: complete through closeout
blocker: resolved by snapshot/delta/ordered commit decision
gate: written
post-gate full-cache performance matrix: written
post-gate ordered processing full-cache performance matrix: written
decision trace: written
closeout: written
```

Implemented in milestone 021:

```text
RadarProcessingOrderedConcurrencyOptions:
  explicit active batch capacity contract
  default active batch capacity 4
  sequential capacity helper for explicit non-concurrent behavior

RadarProcessingRuntimeArchiveBaseline:
  exposes OrderedConcurrencyOptions separately from queued-overlap provider
    options and async execution options
  exposes OrderedActiveBatchCapacity
  can assert ordered-concurrency baseline matches independently from provider
    queue capacity and worker queue capacity

RadarProcessingOrderedResultCoordinator:
  accepts queued batch processing completions out of provider sequence order
  publishes only contiguous results in provider sequence order
  blocks unpublished later successes after a terminal failure boundary
  allows explicit canceled/skipped records to publish after terminal failure
    when they are in sequence

RadarProcessingBatchDelta and core ordered commit foundation:
  computes handler-free per-batch processing deltas without mutating shared
    RadarProcessingCore state
  uses pooled dense source-indexed arrays for per-source event counts,
    payload counts, raw checksums, and timestamp bounds
  commits deltas into shared core state only after ordered source-local
    timestamp validation
  rejects handler cores until a handler-delta contract exists

RadarProcessingQueuedProcessingSession.DrainOrderedConcurrentAsync:
  opt-in ordered concurrent drain path for handler-free processing cores
  bounds active batch compute by RadarProcessingOrderedConcurrencyOptions
  computes active batch deltas without shared core mutation
  commits and records results strictly by provider sequence
  skips later active successes after an earlier failure boundary
  leaves existing sequential DrainAsync behavior separate

Async ordered delta support:
  RadarProcessingAsyncWorkerGroup keeps the one-in-flight guard for normal
    mutating dispatch unless a caller explicitly opts into concurrent dispatch
  RadarProcessingAsyncCoreSession.ComputeDeltaAsync uses concurrent worker
    dispatch only for non-mutating delta compute
  ordered concurrent processing sessions preserve async worker telemetry when
    the core uses AsyncShardTransport

RadarProcessingArchiveQueuedOverlapRunner.RunProcessingAsync:
  explicit runtime/archive queued-overlap path for processing cores
  consumes ordered concurrent processing drain with provider defaults omitted
  keeps milestone 020 queued-owned pooled-copy startup prewarm visible
  preserves AsyncShardTransport worker telemetry through async delta compute
  does not enable rebalance/topology ordered commit yet
```

Milestone 021 blocker:

```text
docs/milestones/021-ordered-concurrent-runtime-archive-processing-slice-3-blocker.md
```

Blocker summary:

```text
RadarProcessingCore mutates cumulative state while processing a batch and
creates RadarProcessingResult from the current cumulative metrics.
RadarProcessingAsyncCoreSession.ProcessAsync still applies shard work into
that same shared core before CompleteAsyncBatch creates the result.

RadarProcessingRebalanceSession.ProcessCompletedResult mutates pressure
window, policy state, quarantine lifecycle, telemetry, decision id, and
potentially topology through migration.

The ordered result coordinator can preserve publication order, but it cannot
isolate per-batch state, prevent cumulative metrics from seeing partial
concurrent mutation, or undo a later batch mutation after an earlier failure.
```

Accepted architecture decision:

```text
docs/milestones/021-ordered-concurrent-runtime-archive-processing-architecture-decision.md

implement snapshot/delta/ordered commit as a per-batch non-mutating delta
pipeline:
  concurrent compute validates immutable batch shape, routes work, and
    computes per-batch source deltas without mutating shared core state
  ordered commit validates source-local ordering against current committed
    state, applies deltas, increments processed batch count, and creates the
    cumulative result strictly by provider sequence
  rebalance pressure/topology decisions run only after ordered processing
    commit permits them

performance constraints:
  avoid per-event heap allocation
  avoid LINQ in hot paths
  use dense source-indexed arrays or pooled buffers
  keep startup prewarm outside steady allocation
  measure allocation/performance before gate
```

Final milestone 021 verification:

```text
slice 1 focused baseline tests:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests"
  result: 11 passed, 0 failed, 0 skipped

slice 2 focused coordinator tests:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingOrderedResultCoordinatorTests"
  result: 5 passed, 0 failed, 0 skipped

processing delta focused tests:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingBatchDeltaTests"
  result: 4 passed, 0 failed, 0 skipped

processing session ordered concurrency focused suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests|FullyQualifiedName~RadarProcessingBatchDeltaTests"
  result: 16 passed, 0 failed, 0 skipped

async ordered delta focused suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests|FullyQualifiedName~RadarProcessingAsyncWorkerGroupTests"
  result: 21 passed, 0 failed, 0 skipped

runtime/archive ordered processing focused suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests|FullyQualifiedName~RadarProcessingAsyncWorkerGroupTests|FullyQualifiedName~RadarProcessingBatchDeltaTests"
  result: 29 passed, 0 failed, 0 skipped

ordered processing lifecycle hardening suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingBatchDeltaTests"
  result: 22 passed, 0 failed, 0 skipped

Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused milestone 021 Release gate suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
    --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests|FullyQualifiedName~RadarProcessingBatchDeltaTests|FullyQualifiedName~RadarProcessingOrderedResultCoordinatorTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests|FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarProcessingAsyncWorkerGroupTests"
  result: 46 passed, 0 failed, 0 skipped

full Release test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
  result: 805 passed, 1 failed, 3 skipped
  known allocation-sensitive synthetic benchmark failure:
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

isolated rerun of full-suite failure:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed, 0 skipped

post-gate full-cache performance matrix:
  docs/milestones/021-ordered-concurrent-runtime-archive-processing-full-cache-performance-matrix.md

  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

  borrowed oracle:
    dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll
      processing benchmark rebalance-archive --cache data\nexrad
      --max-files 1000000 --mode all --provider blocking-borrowed
      --execution async --workers 4 --iterations 1
      --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

  omitted/default:
    dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll
      processing benchmark rebalance-archive --cache data\nexrad
      --max-files 1000000 --mode all --iterations 1
      --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

  cache shape:
    examined files 1_554
    skipped files 726
    published base-data files 828
    stream events 27_254_760
    payload values 32_306_203_200

  end-to-end default elapsed ratios versus borrowed:
    static: 0.965x
    sampling: 0.878x
    rebalance-session: 0.884x

  end-to-end default total allocation ratios versus borrowed:
    static: 1.003x
    sampling: 1.001x
    rebalance-session: 1.000x

  result:
    no end-to-end full-cache regression observed
    validation and processing completeness passed
    checksum parity matched in all modes
    accepted moves matched at 4 vs 4 for rebalance-session
    worker failed batches/items 0/0
    release failures 0
    current combined retained pressure 0

  scope note:
    this matrix exercises the existing rebalance-archive CLI benchmark
    contour after milestone 021 changes.

post-gate ordered processing full-cache performance matrix:
  docs/milestones/021-ordered-concurrent-runtime-archive-processing-ordered-full-cache-performance-matrix.md

  implementation:
    added processing benchmark ordered-archive-processing
    command exercises RadarProcessingArchiveQueuedOverlapRunner.RunProcessingAsync
    command exposes --active-batches for active batch capacity comparison
    ordered path scales startup retained payload prewarm to active capacity
    ordered path uses a retained payload factory sized to avoid active-batch
      large-array pool misses

  focused Debug suite:
    dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
      --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests"
    result: 38 passed, 0 failed, 0 skipped

  Release build:
    dotnet build RadarPulse.sln -c Release --no-restore
    result: succeeded, 0 warnings, 0 errors

  focused Release suite:
    dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
      --no-restore --no-build
      --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests"
    result: 38 passed, 0 failed, 0 skipped

  ordered concurrent default:
    dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll
      processing benchmark ordered-archive-processing --cache data\nexrad
      --max-files 1000000 --iterations 1 --warmup-iterations 0
      --parallelism 24 --partitions 24 --shards 4

  sequential same-path baseline:
    dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll
      processing benchmark ordered-archive-processing --cache data\nexrad
      --max-files 1000000 --iterations 1 --warmup-iterations 0
      --parallelism 24 --partitions 24 --shards 4 --active-batches 1

  cache shape:
    examined files 1_554
    skipped files 726
    published base-data files 828
    stream events 27_254_760
    payload values 32_306_203_200

  ordered active=4 versus same-path active=1:
    elapsed ratio: 0.994x
    steady allocation ratio: 1.006x
    final processing checksum matched:
      2_294_439_733_285_583_699
    processing completeness passed
    worker failed batches/items 0/0
    retained payload pool misses 0
    event array pool misses 0
    byte array pool misses 0
    release failures 0
    current combined retained pressure 0

  active=4 lifecycle:
    startup prewarm batch count 4
    startup prewarm allocated bytes 285_213_112
    startup prewarm retained bytes 285_212_672
    active retained batches high watermark 4
    active retained payload bytes high watermark 213_402_240
    steady measured allocation excludes startup prewarm

  interpretation:
    direct RunProcessingAsync full-cache CLI evidence is now captured.
    This cache shape remains archive-producer dominated, so active-batch
    concurrency has little end-to-end elapsed leverage here, but the ordered
    active=4 path completes deterministically with clean retained pressure and
    zero retained pool misses.
```

Milestone 021 decision trace:

```text
docs/milestones/021-ordered-concurrent-runtime-archive-processing-decision-trace.md

decision:
  accepted with scoped warnings for processing-core runtime/archive ordered
  concurrency

accepted:
  RunProcessingAsync is the explicit ordered runtime/archive processing path
  active batch capacity defaults to 4 and remains separate from provider
    queue capacity 8 and worker queue capacity 8
  non-mutating per-batch delta compute plus provider-sequence ordered commit
    is accepted as the safe architecture for overlapping processing-core
    batches
  ordered active=4 direct full-cache evidence completed with processing
    completeness, checksum parity against active=1, clean retained pressure,
    zero worker failures, zero release failures, and zero retained pool misses

warnings:
  ordered concurrent rebalance/topology commit is not implemented
  handler-state delta/merge is not implemented
  the measured full-cache workload is archive-producer dominated, so
    processing-bottleneck matrices remain useful before broad default
    promotion
  true live network ingestion, durable queues, brokers, cross-process
    workers, and production operator/deployment/rollback surfaces remain
    future work
  known full-suite allocation-sensitive synthetic benchmark caveat remains
    isolated
```

Milestone 021 closeout:

```text
docs/milestones/021-ordered-concurrent-runtime-archive-processing-closeout.md

final answer:
  accepted with scoped warnings, the scoped in-process runtime/archive
  processing-core path is ready to keep multiple accepted batches active,
  compute them concurrently, and publish externally visible processing
  results in deterministic provider sequence order over the accepted
  milestone 020 baseline

recommended next milestone input:
  implement ordered rebalance/topology commit over the ordered processing
  foundation, and collect processing-bottleneck performance evidence before
  broader default promotion
```

## Milestone 020 Closeout Baseline

### Current State

Milestone 020 is complete. The milestone documents are:

```text
docs/milestones/020-default-baseline-runtime-archive-integration.md
docs/milestones/020-default-baseline-runtime-archive-integration-plan.md
docs/milestones/020-default-baseline-runtime-archive-integration-provenance-audit.md
docs/milestones/020-default-baseline-runtime-archive-integration-gate.md
docs/milestones/020-default-baseline-runtime-archive-integration-full-cache-performance-matrix.md
docs/milestones/020-default-baseline-runtime-archive-integration-decision-trace.md
docs/milestones/020-default-baseline-runtime-archive-integration-closeout.md
```

Milestone 020 purpose:

```text
integrate the accepted prewarmed queued-owned runtime/archive default baseline
into remaining scoped in-process runtime/archive construction surfaces without
reopening the provider default decision
```

Milestone 020 starts from the milestone 019 accepted baseline:

```text
provider path: queued-owned by construction of queued-overlap runner
provider overlap: producer-consumer
retention strategy: pooled-copy
provider queue capacity: 8
retained-byte budget: 536870912
startup retained payload prewarm: enabled
prewarm event count: 65_536
prewarm payload bytes: 67_108_864
prewarm retained batch count: 1
```

Milestone 020 integration target:

```text
add a named runtime/archive baseline profile
compose the accepted provider default with async shard transport defaults
use worker count 4 and worker queue capacity 8 where the runtime/archive
  surface owns processing core or session construction
keep provider provenance and execution provenance separately visible
keep startup retained payload prewarm cost separate from steady allocation
preserve explicit diagnostic/no-prewarm and BlockingBorrowed/reference paths
```

Milestone 020 planned slices:

```text
1. Baseline profile contract
2. Runtime/archive owned construction integration
3. Live-adapter-shaped integration evidence
4. Reporting and provenance pass
5. Gate capture and documentation checkpoint
6. Decision trace and closeout
```

Current implementation status:

```text
architecture document: complete
implementation plan: complete
implementation: complete through slice 5 gate capture
decision trace: written
closeout: written
```

Implemented in milestone 020:

```text
RadarProcessingRuntimeArchiveBaseline:
  named runtime/archive baseline profile
  default async execution options
  default processing core options for supplied topology shape
  default processing core construction
  default rebalance session construction
  provider/default and execution/default match helpers

focused integration evidence:
  baseline-created rebalance sessions use async shard transport with worker
    count 4 and worker queue capacity 8
  baseline-created sessions compose with omitted queued-overlap provider
    defaults
  caller-supplied rebalance sessions keep explicit execution mode
  deterministic live-adapter-shaped steady intake completes
  deterministic live-adapter-shaped validation failure cleans retained
    pressure without borrowed fallback
```

Milestone 020 closeout result:

```text
decision:
  accepted with scoped warnings

accepted:
  the scoped in-process runtime/archive integration boundary is ready to
  consume the accepted prewarmed queued-owned plus async execution default
  baseline without reopening the provider default decision

accepted construction profile:
  RadarProcessingRuntimeArchiveBaseline is accepted as the named construction
  profile for composing queued-owned provider defaults with async shard
  transport execution defaults

accepted owned-construction execution defaulting:
  surfaces that own processing core or rebalance session construction can use
  async shard transport with worker count 4 and worker queue capacity 8

preserved explicit ownership:
  caller-supplied processing cores and rebalance sessions are not silently
  rewritten into async shard transport

not implemented here:
  ordered concurrent multi-batch processing
  true live network ingestion
  durable queues, brokers, cross-process workers
  production operator/deployment/rollback surfaces

closeout:
  complete
```

Final verification used for milestone 020 closeout:

```text
Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused milestone 020 gate suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests|FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveRebalanceBenchmarkCommandUsesRolloutDefaultsWhenProviderOmitted|FullyQualifiedName~ArchiveRebalanceBenchmarkCommandLabelsDefaultCandidateContour"
  result: 24 passed, 0 failed, 0 skipped

full test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  result: 787 passed, 1 failed, 3 skipped
  known allocation-sensitive synthetic benchmark failure:
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

isolated rerun of full-suite failure:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed, 0 skipped
```

Post-gate full-cache performance matrix:

```text
Release CLI matrix:
  processing benchmark rebalance-archive --cache data\nexrad
  --max-files 1000000 --mode all

compared:
  explicit BlockingBorrowed oracle with async workers 4
  omitted-provider queued-owned rollout default

cache shape:
  examined files 1_554
  skipped files 726
  published base-data files 828
  stream events 27_254_760
  payload values 32_306_203_200

end-to-end default elapsed ratios versus borrowed:
  static: 0.793x
  sampling: 0.890x
  rebalance-session: 0.881x

end-to-end default total allocation ratios versus borrowed:
  static: 1.000x
  sampling: 1.002x
  rebalance-session: 1.003x

result:
  no end-to-end full-cache regression observed
  validation and processing completeness passed
  rebalance-session checksum parity matched
  accepted moves matched at 4 vs 4
  failed migrations 0
  worker failed batches/items 0/0
  release failures 0
  current combined retained pressure 0

carry-forward note:
  queued-owned processing callback attribution is heavier, about 3.25x-3.28x
  callback allocation and 1.30x-1.34x callback elapsed versus borrowed, while
  end-to-end rows remain faster with flat total allocation
```

Warnings carried after closeout:

```text
startup retained payload prewarm remains a visible lifecycle cost
true live network ingestion is not implemented
durable queues, brokers, cross-process workers, and ordered concurrent
  rebalance are not implemented
production operator/deployment/rollback surfaces are not implemented
full-suite allocation sensitivity remains for one synthetic benchmark test
```

Milestone 020 final closeout answer:

```text
accepted with scoped warnings, the scoped in-process runtime/archive
integration boundary is ready to consume the accepted prewarmed queued-owned
plus async execution default baseline without reopening the provider default
decision
```

Recommended next milestone input:

```text
implement ordered concurrent runtime/archive processing over the accepted
default baseline. Preserve deterministic result ordering, topology and
rebalance safety, fail-closed queued-owned behavior, no silent borrowed
fallback, visible startup prewarm, release/cleanup pressure invariants, and
separate provider/execution provenance.
```

## Milestone 019 Closeout Baseline

### Current State

Milestone 019 is complete. The milestone documents are:

```text
docs/milestones/019-prewarmed-queued-owned-runtime-default-promotion.md
docs/milestones/019-prewarmed-queued-owned-runtime-default-promotion-plan.md
docs/milestones/019-prewarmed-queued-owned-runtime-default-promotion-gate.md
docs/milestones/019-prewarmed-queued-owned-runtime-default-promotion-decision-trace.md
docs/milestones/019-prewarmed-queued-owned-runtime-default-promotion-closeout.md
```

Milestone 019 purpose:

```text
promote the benchmark-proven and runtime-explicit startup-prewarmed
queued-owned contour into the scoped runtime/archive queued-overlap
omitted-default path
```

Implemented in milestone 019 so far:

```text
RadarProcessingArchiveQueuedOverlapOptions.Default now represents the runtime
  rollout contour:
    provider queue capacity 8
    retained-byte budget 536870912
    retained payload strategy pooled-copy
    retained payload prewarm options rollout default

RadarProcessingArchiveQueuedOverlapRunner now applies startup retained payload
  prewarm before steady overlap allocation capture when options request it

RadarProcessingArchiveQueuedOverlapResult now surfaces
  RadarProcessingRetainedPayloadPrewarmResult separately from steady overlap
  telemetry

explicit constructed options remain diagnostic/no-prewarm unless the caller
  explicitly requests prewarm
```

Milestone 019 focused gate result:

```text
omitted runtime queued-overlap path reports retained payload prewarm applied
prewarm sizing matches rollout defaults:
  event count 65_536
  payload bytes 67_108_864
  retained batch count 1
prewarm allocation and retained bytes are visible
steady measured allocation is separate from startup prewarm allocation
retention strategy is pooled-copy
release attempts/releases/failures are 1/1/0 in the focused default row
terminal combined retained pressure returns to 0
explicit no-prewarm options remain snapshot-copy/no-prewarm
```

Scoped warning carried forward:

```text
milestone 019 promotes queued-overlap provider/retention/prewarm defaults.
It does not automatically rewrite an already constructed processing core or
rebalance session into async shard transport. Execution mode and async worker
sizing remain owned by the supplied processing core/rebalance session.
Future processing-core default work should adopt the accepted baseline
explicitly rather than reopen the provider default decision.
```

Final verification used for milestone 019 closeout:

```text
Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused Debug runtime/prewarm suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests"
  result: 41 passed, 0 failed

focused Release runtime/prewarm suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release --no-restore
    --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests"
  result: 41 passed, 0 failed

full test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  result: 776 passed, 1 failed, 3 skipped
  failure:
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
    expected bounded allocation, got 469_019_824 bytes

isolated rerun of failing test:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed
```

Milestone 019 final closeout answer:

```text
accepted with scoped warnings, startup-prewarmed queued-owned is accepted as
the omitted default for the scoped in-process runtime/archive queued-overlap
provider path and as the default baseline for remaining runtime/archive work
```

Accepted by decision trace:

```text
omitted RadarProcessingArchiveQueuedOverlapOptions now use provider queue
capacity 8, retained-byte budget 536870912, pooled-copy retention, and rollout
startup retained payload prewarm

startup retained payload prewarm is visible on
RadarProcessingArchiveQueuedOverlapResult and remains separate from steady
overlap allocation

explicit constructed options remain snapshot-copy/no-prewarm unless prewarm
is requested explicitly
```

Not implemented by milestone 019, but inheriting the accepted baseline:

```text
automatic processing-core execution mode or async worker sizing defaulting
true live network ingestion implementation
durable queues or brokers
cross-process provider or worker transport
ordered concurrent rebalance
builder-transfer retained payload execution
production operator/deployment/rollback surfaces
```

Still rejected:

```text
automatic silent borrowed fallback
```

Recommended current next action:

```text
begin the next milestone:
  default-baseline runtime/archive integration

use the accepted prewarmed queued-owned runtime/archive default baseline for
remaining integration work. Add processing-core execution defaulting only in
the surface that owns core construction, add live adapter or production
pipeline evidence as integration evidence, and keep prewarm, pressure,
cancellation, failure, release, cleanup, and explicit borrowed fallback/oracle
visible without reopening the provider default decision.
```

## Milestone 018 Closeout Baseline

Milestone 018 is complete and remains the baseline for milestone 019.

Milestone 018 is complete. The milestone documents are:

```text
docs/milestones/018-runtime-live-ingestion-readiness.md
docs/milestones/018-runtime-live-ingestion-readiness-plan.md
docs/milestones/018-runtime-live-ingestion-readiness-lifecycle-audit.md
docs/milestones/018-runtime-live-ingestion-readiness-gate-matrix.md
docs/milestones/018-runtime-live-ingestion-readiness-reporting-harness.md
docs/milestones/018-runtime-live-ingestion-readiness-prewarm-posture.md
docs/milestones/018-runtime-live-ingestion-readiness-lifecycle-guardrails.md
docs/milestones/018-runtime-live-ingestion-readiness-steady-intake-gate.md
docs/milestones/018-runtime-live-ingestion-readiness-pressure-failure-gate.md
docs/milestones/018-runtime-live-ingestion-readiness-gate-interpretation.md
docs/milestones/018-runtime-live-ingestion-readiness-decision-trace.md
docs/milestones/018-runtime-live-ingestion-readiness-closeout.md
```

Milestone 018 is the runtime and live ingestion readiness milestone. It starts
from the closed milestone 017 file-level and small-file direct benchmark
default readiness result.

Milestone 018 current status:

```text
architecture document:
  drafted in docs/milestones/018-runtime-live-ingestion-readiness.md

implementation plan:
  drafted in docs/milestones/018-runtime-live-ingestion-readiness-plan.md

implementation:
  complete through decision trace and closeout

runtime behavior changes:
  explicit ShutdownMode.CancelQueued cancellation shutdown now clears accepted
  pending queued work before dequeue and records canceled sequence ids

current closeout question:
  Is the queued-owned contour ready for runtime/live ingestion defaults?

current decision-trace answer:
  explicit opt-in only

final closeout answer:
  explicit opt-in only, queued-owned is runtime-safe when selected explicitly
  for scoped in-process runtime/archive replay surfaces with startup prewarm
  and existing guardrails, but it is not accepted as the omitted runtime/live
  ingestion default
```

Milestone 018 final posture:

```text
direct benchmark readiness from milestone 017 is accepted evidence, not
automatic runtime approval

omitted runtime/live defaults remain unchanged

runtime startup prewarm is selected only as the explicit queued-owned
gate candidate; it is not accepted as an omitted runtime default

queued-owned runtime outcome:
  explicit opt-in only

recommended next milestone:
  gradual runtime rollout for queued-owned explicit opt-in, including
  production runtime provider selection, operator reporting, explicit startup
  prewarm lifecycle wiring, repeatability gates, and true live ingestion or
  narrower archive-runtime rollout evidence
```

Milestone 018 planned slices:

```text
1. Runtime surface inventory and lifecycle audit (complete)
2. Runtime readiness contract and gate matrix design (complete)
3. Reporting, contract, and harness gap closure (complete)
4. Runtime prewarm lifecycle decision and guardrails (complete)
5. Backpressure, failure, cancellation, and cleanup guardrails (complete)
6. Runtime steady intake gate (complete)
7. Runtime pressure, backpressure, cancellation, and failure gate (complete)
8. Gate interpretation and follow-up fixes (complete)
9. Runtime readiness decision trace (complete)
10. Closeout, handoff, and project progress (complete)
```

Milestone 018 implementation rules:

```text
preserve explicit BlockingBorrowed as fallback/oracle where supported
preserve queued-owned fail-closed behavior
do not add automatic silent borrowed fallback
do not hide retained payload prewarm or startup cost
do not claim provider enqueue success as processing completion
require processing completeness for runtime success
require worker failed batches/items and processing validation failed batches
  to be visible and gateable
require retained pressure cleanup and release health through success,
  cancellation, failure, drain, and dispose paths
use deterministic archive replay as live-input stand-in only through
  runtime-shaped lifecycle gates
do not broaden into durable/cross-process/ordered-concurrent surfaces
```

Milestone 018 out-of-scope surfaces:

```text
durable queues
brokers
cross-process providers/workers
ordered concurrent rebalance
multiple active rebalance-enabled processing batches
builder-transfer retained payload execution
source-level migration
partition splitting
physical worker-local state transfer
synthetic processing benchmark default migration
product-facing radar workflows
automatic silent borrowed fallback
```

Milestone 018 current recommended next action:

```text
begin the next milestone: gradual runtime rollout for queued-owned explicit
opt-in
```

Milestone 018 slice 1 completion:

```text
audit document:
  docs/milestones/018-runtime-live-ingestion-readiness-lifecycle-audit.md

runtime behavior changes:
  none

audited foundation:
  archive provider can retain leased input and enqueue owned batches
  owned queue is bounded and retained-byte-budget aware
  queued processing/rebalance sessions drain sequence-ordered owned batches
  producer and consumer results are separate
  retained cleanup and pressure telemetry exist
  worker and processing failure vocabulary exists
  direct benchmark prewarm and processing completeness are visible

main gaps carried to slice 2:
  no runtime default selection decision
  no runtime prewarm lifecycle policy
  shutdownMode CancelQueued is contractual but not wired into audited runtime
    drain behavior
  no single runtime readiness result/operator surface
  no true live ingestion adapter evidence
  integrated runtime-shaped gates still need to be designed
```

Milestone 018 slice 2 completion:

```text
gate matrix document:
  docs/milestones/018-runtime-live-ingestion-readiness-gate-matrix.md

runtime behavior changes:
  none

decision boundary:
  queued-owned is a runtime candidate selected explicitly for gates
  runtime default posture remains undecided
  runtime prewarm posture remains undecided until slice 4

gate groups:
  A. contract and provenance
  B. steady intake
  C. first-use and prewarm-sensitive rows
  D. queue pressure and retained-byte pressure
  E. cancellation
  F. fault and failure
  G. drain, stop, and dispose

thresholds recorded before gate capture:
  steady allocation pass <= 1.10x reference, warning <= 1.20x, optimize
    <= 1.35x, fail > 1.35x
  steady elapsed pass <= 1.00x reference, warning <= 1.10x, optimize
    <= 1.20x, fail > 1.20x
  repeated session spread pass <= 7.50%, warning <= 12.50%, optimize
    <= 20.00%, fail > 20.00%

carried gaps:
  CancelQueued shutdown behavior must be implemented/tested or explicitly
    carried as blocker/coverage gap
  true live ingestion remains a coverage gap unless new scope is added
  runtime-shaped gates need reviewable JSONL/Markdown output
```

Milestone 018 slice 3 completion:

```text
reporting/harness document:
  docs/milestones/018-runtime-live-ingestion-readiness-reporting-harness.md

runtime behavior changes:
  none

production code changes:
  none

contract mapping:
  existing lower-level contracts expose enough provider, consumer, queue,
    retained payload, pressure, worker, processing-completeness, release,
    and lifecycle data to proceed to runtime gate capture

temporary harness schema:
  JSONL record types and Markdown summary requirements are defined for the
    runtime gate harness

product reporting decision:
  no production runtime API or CLI reporting change is required before gate
    capture; temporary local harness output is sufficient unless slice 6/7
    gates reveal attribution gaps

carried gaps:
  actual temporary runner implementation/capture remains for slice 6/7
  runtime prewarm policy remains for slice 4
  CancelQueued behavior remains for slice 5
  true live ingestion remains a coverage gap unless new scope is added
  durable operator/runtime reporting surface remains future work unless gates
    show product reporting is required before decision trace
```

Milestone 018 slice 4 completion:

```text
prewarm posture document:
  docs/milestones/018-runtime-live-ingestion-readiness-prewarm-posture.md

runtime behavior changes:
  none

production code changes:
  none

chosen candidate:
  startup-owned retained payload prewarm is the explicit queued-owned
    runtime-shaped gate candidate

runtime default migration:
  not accepted yet; omitted runtime defaults remain unchanged and undecided
    until the milestone 018 decision trace

initial sizing:
  65_536 events
  67_108_864 payload bytes
  1 retained batch

control separation:
  natural first-use queued-owned rows remain unprewarmed control evidence
  explicit BlockingBorrowed/reference rows remain unprewarmed and visibly
    separate

failure policy:
  prewarm failure fails the candidate row before intake and does not allow
    hidden borrowed/reference fallback

slice 5 carry-forward:
  add or verify guardrails for no silent fallback, retained cleanup,
    cancellation and fault with a prewarmed factory, borrowed/reference
    unprewarmed separation, natural first-use separation, prewarm failure
    policy, and the existing ShutdownMode.CancelQueued gap
```

Milestone 018 slice 5 completion:

```text
lifecycle guardrails document:
  docs/milestones/018-runtime-live-ingestion-readiness-lifecycle-guardrails.md

runtime behavior changes:
  scoped guardrail fix for explicit ShutdownMode.CancelQueued cancellation
    shutdown

implemented:
  RadarProcessingOwnedBatchQueue.CancelQueued() closes intake, wakes waiters,
    clears accepted pending batches before dequeue, returns canceled sequence
    ids, and leaves later enqueue attempts closed
  queued processing and rebalance sessions apply ShutdownMode.CancelQueued on
    cancellation and record canceled processing results for accepted pending
    batches canceled before dequeue
  archive queued overlap runner applies the same cancellation shutdown policy
    on producer or consumer cancellation
  provider queue telemetry allows canceled accepted work before dequeue while
    preserving completed/failed/skipped-after-fault <= dequeued

verified:
  focused queue, session, rebalance, telemetry, and archive-overlap guardrail
    tests pass: 54 passed, 0 failed
  full test project was attempted twice; both runs had the same single
    allocation threshold failure in
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
  isolated rerun of that failing test passed

carried to gates:
  startup-prewarmed candidate rows, natural first-use control rows,
    borrowed/reference rows, release-failure runtime replay evidence, and true
    live-ingestion coverage remain for slice 6/7 gate capture and decision
    trace interpretation
```

Milestone 018 slice 6 completion:

```text
steady intake gate document:
  docs/milestones/018-runtime-live-ingestion-readiness-steady-intake-gate.md

runtime behavior changes:
  none

temporary runner:
  data\temp\m018-runtime-gate-runner

raw output:
  data\temp\m018-runtime-gate-runner\output\m018-runtime-20260522-134534.jsonl
  data\temp\m018-runtime-gate-runner\output\m018-runtime-20260522-134534.md

safety result:
  12 rows passed
  processing completeness failures 0
  worker failure rows 0
  release failure rows 0
  terminal pressure failure rows 0

startup-prewarmed queued-owned candidate:
  passed bounded steady elapsed and allocation bands in B1-B4
  allocation ratios versus borrowed/reference: 1.000x, 1.001x, 1.000x,
    1.002x

natural first-use queued-owned control:
  allocation ratios versus borrowed/reference: 1.196x, 2.040x, 1.284x,
    1.373x
  remains allocation warning/optimize/fail evidence and does not support
    runtime default readiness by itself

carried to slice 7:
  pressure, backpressure, cancellation, failure, drain, cleanup, release
    failure replay evidence, and true live-ingestion coverage remain open
```

Milestone 018 slice 7 completion:

```text
pressure/failure gate document:
  docs/milestones/018-runtime-live-ingestion-readiness-pressure-failure-gate.md

runtime behavior changes:
  none

temporary runner:
  data\temp\m018-runtime-pressure-gate-runner

raw output:
  data\temp\m018-runtime-pressure-gate-runner\output\m018-pressure-20260522-135835.jsonl
  data\temp\m018-runtime-pressure-gate-runner\output\m018-pressure-20260522-135835.md

gate result:
  11 rows passed
  0 rows failed
  11 rows terminal pressure clean
  3 backpressure rows
  4 cancellation rows
  6 failure rows

covered:
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

focused verification:
  focused queue, provider, rebalance-session, archive-overlap, and provider
    contract tests pass: 56 passed, 0 failed

carried to slice 8:
  gate interpretation, follow-up fix triage, true live-ingestion coverage gap,
    runtime default posture, and decision-trace input remain open
```

Milestone 018 slice 8 completion:

```text
gate interpretation document:
  docs/milestones/018-runtime-live-ingestion-readiness-gate-interpretation.md

runtime behavior changes:
  none

recommended decision-trace posture:
  explicit opt-in only

runtime default:
  keep omitted runtime defaults unchanged

queued-owned explicit candidate:
  runtime-safe for scoped in-process runtime/archive replay surfaces when
    selected explicitly with startup prewarm and existing guardrails

follow-up fix posture:
  no production follow-up fix required before decision trace

reason:
  slice 6 startup-prewarmed candidate passes bounded steady evidence
  natural first-use remains allocation warning/optimize/fail control evidence
  slice 7 pressure, cancellation, failure, drain, release, and cleanup gates
    pass with terminal retained pressure clean in all rows
  release failure remains visible and readiness-blocking
  no automatic borrowed fallback was introduced or observed

carried to slice 9:
  formal decision trace, accepted warnings, residual coverage gaps, and next
    milestone input
```

Milestone 018 slice 9 completion:

```text
decision trace document:
  docs/milestones/018-runtime-live-ingestion-readiness-decision-trace.md

runtime behavior changes:
  none

decision:
  explicit opt-in only

accepted surface:
  queued-owned is runtime-safe for scoped in-process runtime/archive replay
    surfaces when selected explicitly with startup prewarm and existing
    guardrails

not accepted:
  queued-owned is not accepted as the omitted runtime/live ingestion default

default posture:
  omitted runtime defaults remain unchanged

accepted warnings:
  startup prewarm is explicit lifecycle cost only
  natural first-use remains allocation-blocked for omitted default readiness
  true live ingestion, durable queues, cross-process providers/workers,
    production operator reporting, and repeated variance gates remain open

next milestone input:
  gradual runtime rollout for queued-owned explicit opt-in
  production runtime provider selection and operator reporting
  explicit startup prewarm lifecycle wiring where selected
  repeatability gates and true live ingestion or narrower archive-runtime
    rollout evidence
```

Milestone 018 slice 10 completion:

```text
closeout document:
  docs/milestones/018-runtime-live-ingestion-readiness-closeout.md

runtime behavior changes:
  none

final answer:
  explicit opt-in only

accepted:
  queued-owned is runtime-safe when selected explicitly for scoped in-process
    runtime/archive replay surfaces with startup prewarm and existing
    guardrails

not accepted:
  queued-owned is not accepted as the omitted runtime/live ingestion default

final verification:
  Release build succeeded, 0 warnings, 0 errors
  focused runtime guardrail suite passed: 56 passed, 0 failed
  full test project had the known allocation-sensitive synthetic benchmark
    failure: 774 passed, 1 failed, 3 skipped
  isolated rerun of that synthetic benchmark test passed

project progress:
  docs/project-progress.md updated after milestone 018 closeout

recommended next milestone:
  gradual runtime rollout for queued-owned explicit opt-in
```

## Milestone 017 Baseline

Milestone 017 is complete. It remains the baseline evidence for milestone 018.

Milestone 017 documents:

```text
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost.md
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-plan.md
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-measurefile-gate.md
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-prewarmed-measurefile-gate.md
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-small-cache-gate.md
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-interpretation.md
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-decision-trace.md
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-closeout.md
```

Milestone 017 is the file-level default readiness and cold
retained-ownership cost milestone. It starts from the closed milestone 016
broader cache-level default readiness result.

Milestone 017 closeout answer:

```text
yes with warnings, file-level and small-file default readiness is accepted for
the queued-owned direct benchmark default-equivalent contour with retained
payload prewarm
```

Milestone 017 current status:

```text
architecture document: complete in
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost.md
implementation plan: complete in
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-plan.md
implementation: complete through decision trace and closeout
runtime behavior changes:
  direct MeasureFile()/MeasureCache() default-equivalent queued-owned contour
    now prewarms retained payload resources before measured rows
  direct MeasureCache() auto-sizes its source universe to the selected
    distinct radar ids when no radar filter is supplied; radar-filtered cache
    rows remain single-radar
  archive rebalance result/CLI reporting now exposes processing completeness,
    processing validation failed batches, and worker failure counts as
    processing-completeness blockers
  runtime/live ingestion, durable queues, and cross-process surfaces remain
    unchanged
performance gate:
  MeasureFile gate captured in
    docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-measurefile-gate.md
  small-file MeasureCache gate captured in
    docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-small-cache-gate.md
  post-fix full cache regression matrix captured in
    data\temp\m017-cache-regression-runner\output\m017-cache-regression-20260522-110241.md
    result: 16/16 group rows pass, 28/28 pairs pass safety, processing
      completeness failures 0, worker failed batches/items 0/0, worst elapsed
      0.988x, worst allocation 1.009x
prewarm follow-up:
  opt-in retained payload prewarm prototype implemented and full prewarmed
  MeasureFile gate plus targeted timing rerun captured; explicit prewarmed
  MeasureCache comparison also captured; prewarm removed the measured
  allocation blocker for file and selected small-cache rows, with up-front
  prewarm allocation cost kept explicit; slice 7 promoted this to the scoped
  direct benchmark default-equivalent posture with result and CLI attribution
file-level readiness:
  natural unprewarmed MeasureFile and low-count MeasureCache rows remain
    allocation-blocked
  accepted readiness contour is queued-owned rollout default plus retained
    payload prewarm
  prewarm cost is explicit and not folded into measured row allocation
  fail-level prewarmed elapsed outliers did not reproduce in targeted timing
    repeats; remaining elapsed variance is a non-blocking filesystem timing
    note
mixed-cache follow-up:
  mixed-cache-all worker failures were diagnosed as SourceOrderViolation from
  running KINX and KTLX through DefaultSingleRadar; MeasureCache now
  self-sizes mixed-radar source universes and result validation includes
  processing-result validity
  verification after the fix:
    borrowed async mixed-cache-all: source count 46080, published files 828,
      processing validation failed batches 0, worker failed batches/items 0/0
    omitted default candidate mixed-cache-all: source count 46080, published
      files 828, processing validation failed batches 0, worker failed
      batches/items 0/0, retained pool misses 0, release failures 0
decision trace: written in
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-decision-trace.md
closeout: written in
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-closeout.md
project progress ledger at milestone 017 closeout:
  docs/project-progress.md recorded the milestone 017 closeout state at that
  time; the current project progress ledger is now updated after milestone 018
  closeout
recommended next milestone:
  runtime/live ingestion readiness, scoped separately from direct benchmark
  defaults
```

Milestone 017 final direct/default contour:

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
  retained payload prewarm: on for the direct benchmark default-equivalent
    contour
  retained payload prewarm sizing: 65_536 events, 67_108_864 payload bytes,
    1 retained batch
```

Milestone 017 starting evidence from milestone 016:

```text
broader cache-level default readiness:
  accepted with named scoped warnings

explicit fallback/oracle:
  providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed
  same-run BlockingBorrowed rows remain required for readiness gates

representative single-file smoke:
  data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
  elapsed ratio: 0.675x borrowed
  allocation ratio: 1.041x borrowed
  status: coverage-only, not file-level default readiness proof

prior file-level warning from milestone 015:
  representative KTLX single-file cold smoke allocation ratio 1.512x borrowed
  representative KTLX single-file cold smoke elapsed ratio 1.072x borrowed
  interpretation: expected cold retained-ownership price for the current
  queued-owned pooled-copy architecture, not a cache-level blocker
```

Milestone 017 known local corpus at planning start:

```text
data\nexrad\level2\2026\05\04\KTLX:
  244 files, 1_347_625_897 bytes
data\nexrad\level2\2026\05\04\KINX:
  462 files, 1_404_452_903 bytes
data\nexrad\level2\2026\05\05\KTLX:
  848 files, 2_232_493_336 bytes
data\nexrad total:
  1_554 files, 4_984_572_136 bytes
```

Milestone 017 seed file candidates recorded in the plan:

```text
prior file-smoke candidate:
  data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
  size: 5_406_854 bytes

KTLX 2026-05-04 smaller non-MDM candidates:
  KTLX20260504_220338_V06, 4_403_971 bytes
  KTLX20260504_123715_V06, 4_494_334 bytes

KTLX 2026-05-04 larger non-MDM candidates:
  KTLX20260504_034117_V06, 7_757_670 bytes
  KTLX20260504_035526_V06, 7_755_692 bytes

KINX 2026-05-04 smaller non-MDM candidates:
  KINX20260504_124819_V06, 5_012_884 bytes
  KINX20260504_123431_V06, 5_016_231 bytes

KINX 2026-05-04 larger non-MDM candidates:
  KINX20260504_035026_V06, 8_453_655 bytes
  KINX20260504_034322_V06, 8_452_883 bytes

KTLX 2026-05-05 larger non-MDM candidates:
  KTLX20260505_034612_V06, 8_656_438 bytes
  KTLX20260505_034226_V06, 8_633_851 bytes
```

Milestone 017 slice 1 completion:

```text
status:
  complete

runtime behavior changes:
  none

local corpus inventory:
  data\nexrad\level2\2026\05\04\KTLX:
    total files 244, base-data 220, MDM 24, metadata 0
  data\nexrad\level2\2026\05\04\KINX:
    total files 462, base-data 207, MDM 24, metadata 231
  data\nexrad\level2\2026\05\05\KTLX:
    total files 848, base-data 401, MDM 23, metadata 424

file selection rule:
  primary MeasureFile() readiness rows use non-MDM Archive Two base-data files
  only; _MDM and .metadata.json files are excluded from primary MeasureFile()
  readiness

base-data signature spot-check:
  selected MeasureFile() candidates exist locally and start with AR2V

selected file-level gate matrix:
  prior representative KTLX 2026-05-04:
    KTLX20260504_000245_V06, 5_406_854 bytes
  KTLX 2026-05-04 small/representative/large:
    KTLX20260504_220338_V06, 4_403_971 bytes
    KTLX20260504_144229_V06, 6_087_636 bytes
    KTLX20260504_034117_V06, 7_757_670 bytes
  KINX 2026-05-04 small/representative/large:
    KINX20260504_124819_V06, 5_012_884 bytes
    KINX20260504_093652_V06, 6_775_011 bytes
    KINX20260504_035026_V06, 8_453_655 bytes
  KTLX 2026-05-05 small/representative/large:
    KTLX20260505_220542_V06, 2_120_538 bytes
    KTLX20260505_154040_V06, 5_094_087 bytes
    KTLX20260505_034612_V06, 8_656_438 bytes

small-file cache transition matrix:
  KTLX 2026-05-04 max-files 2/4/8 publishes expected base-data 2/4/8
  KINX 2026-05-04 max-files 4/8/16 publishes expected base-data 2/4/8
    because metadata interleaves in sorted order
  KTLX 2026-05-05 max-files 4/8/16 publishes expected base-data 2/4/8
    because metadata interleaves in sorted order
  optional KTLX 2026-05-04 max-files 16 skip-visibility row examines 16 and
    publishes expected base-data 15 because one MDM file appears in the window

coverage posture:
  broad enough to start file-level readiness work over available local data;
  still local-corpus-only and not a certification of absent radar/date shapes
```

Milestone 017 slice 2 completion:

```text
status:
  complete

runtime behavior changes:
  none

direct API contract:
  MeasureFile() and MeasureCache() omitted controls still resolve to the
  accepted queued-owned rollout contour
  explicit BlockingBorrowed remains available as the same-run fallback/oracle
  contour
  queued-only controls remain guarded and queued-owned failures remain
  fail-closed without automatic borrowed fallback

result/reporting posture:
  direct MeasureFile() and MeasureCache() result contracts expose the fields
  needed for file-level gate capture: effective contour, validation,
  rebalance counters, skipped reasons, worker telemetry, elapsed time,
  allocation attribution, queue telemetry, retained payload telemetry,
  retained pressure, and provider overlap telemetry
  MeasureCache() additionally exposes examined/skipped/published file counts
  needed for low-count small-file slice interpretation
  CLI rebalance-archive output exposes omitted-provider provenance,
  default-candidate contour, rollout-default expansion, fallback contour,
  evidence contour/scope, allocation attribution, retained payload telemetry,
  retained pressure, and overlap/queue telemetry

attribution limit:
  retained telemetry splits event-array and byte-array pool rent/return/miss
  counts, but does not split exact allocated bytes by event-array versus
  byte-array pool
  this is acceptable unless slice 3 defines a threshold that requires exact
  per-pool allocated-byte attribution

guardrail tests:
  existing CLI, queued overlap, retained payload, and retained resource tests
  cover default/fallback provenance, telemetry visibility, cleanup, release,
  cold/warm pooled-copy behavior, builder-transfer unsupported behavior, and
  fail-closed queued-owned failure behavior

measurement posture:
  no committed code or product-reporting fix is required before measurement
  use a temporary direct API gate runner for the full Release gate so paired
  borrowed/default rows can be captured from structured result fields
  keep CLI output for slice 4 direct/CLI alignment spot-checks
```

Milestone 017 slice 3 completion:

```text
status:
  complete

runtime behavior changes:
  none

threshold posture:
  cold MeasureFile() allocation:
    pass <= 1.10x, warning <= 1.50x, optimize <= 1.75x, fail > 1.75x
  cold MeasureFile() elapsed:
    pass <= 1.00x, warning <= 1.10x, optimize <= 1.25x, fail > 1.25x
  warm MeasureFile() allocation:
    pass <= 1.10x, warning <= 1.20x, optimize <= 1.35x, fail > 1.35x
  warm MeasureFile() elapsed average:
    pass <= 1.00x, warning <= 1.10x, optimize <= 1.20x, fail > 1.20x
  small-file MeasureCache():
    classify by expected published base-data count; 2-file slices get the
    widest transition bands, 4-file slices narrower bands, and 8-file-or-larger
    slices converge on cache-level expectations
  repeated candidate elapsed spread:
    <= 7.50% of candidate average; candidate-first cold rows are excluded
    from spread

pair ordering:
  cold probes run queued-owned omitted-default first, then explicit
  BlockingBorrowed in the same runner invocation
  warm pairs run explicit BlockingBorrowed first, then queued-owned
  omitted-default
  queued-owned failures remain fail-closed and must not be retried as borrowed
  success inside the row

required cold probes:
  KTLX20260504_000245_V06
  KTLX20260504_144229_V06
  KINX20260504_093652_V06
  KTLX20260505_154040_V06

repeat policy:
  prior KTLX representative and KTLX 2026-05-04 representative:
    1 candidate-first cold pair and 3 warm pairs
  KINX representative and KTLX 2026-05-05 representative:
    1 candidate-first cold pair and 2 warm pairs
  small/large file rows:
    1 warm pair each, with predefined conditional repeats if they enter a
    warning/optimize band or land near a band boundary
  primary KTLX 2/4/8 small-cache slices:
    2 warm pairs each
  KINX and KTLX 2026-05-05 small-cache slices:
    1 warm pair each, with the same conditional repeat rule

runner posture:
  build a temporary direct API runner under data\temp\m017-gate-runner or an
  equivalent ignored workspace path
  emit JSONL rows plus a generated Markdown summary
  use direct MeasureFile() and MeasureCache() result fields for the full gate
  keep CLI as a spot-check for omitted-provider alignment and explicit
  BlockingBorrowed visibility
```

Milestone 017 slice 4 completion:

```text
status:
  complete

runtime behavior changes:
  none

focused regression:
  command:
    dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
  result:
    passed, 112 passed, 0 failed, 0 skipped

CLI omitted-provider file spot-check:
  representative file:
    data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
  result:
    provider mode queued-owned
    provider mode source rollout-default
    provider rollout default expansion yes
    default-candidate contour yes
    fallback contour no
    provider overlap evidence scope natural-readiness
    validation succeeded
    retained payload failed releases 0
    provider overlap failed releases 0
    current pending/active/combined retained pressure returned to 0

CLI explicit BlockingBorrowed file spot-check:
  representative file:
    data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
  result:
    provider mode blocking-borrowed
    provider mode source explicit
    provider default rollout contour no
    provider fallback contour yes
    validation succeeded
    validation checksum matched the omitted-provider spot-check
  guardrail note:
    explicit blocking-borrowed with --overlap-telemetry summary was rejected
    because overlap telemetry requires producer-consumer overlap, preserving
    queued-only option validation

selected file static sanity:
  all 10 selected MeasureFile() readiness files published with
  omitted-provider queued-owned mode and Validation: succeeded
  selected checksums/events/payload bytes were recorded in the implementation
  plan slice 4 notes

low-count cache static sanity:
  all selected low-count MeasureCache() slices published with omitted-provider
  queued-owned mode and Validation: succeeded
  KTLX 2026-05-04 max-files 2/4/8 published 2/4/8
  KINX 2026-05-04 max-files 4/8/16 published 2/4/8 because skipped metadata
    interleaves in sorted order
  KTLX 2026-05-05 max-files 4/8/16 published 2/4/8 because skipped metadata
    interleaves in sorted order

blockers before Release gate:
  none found
```

Milestone 017 slice 5 completion:

```text
status:
  complete

runtime behavior changes:
  none

gate document:
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-measurefile-gate.md

raw local outputs:
  data\temp\m017-gate-runner\output\m017-measurefile-20260522-083951.jsonl
  data\temp\m017-gate-runner\output\m017-measurefile-20260522-083951.md

runner contour:
  surface: RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
  mode: RebalanceSession
  iterations: 1
  warmup iterations: 0
  parallelism: 24
  partitions: 24
  shards: 4
  candidate rows omitted provider/execution/retention controls and resolved to
    queued-owned rollout defaults
  borrowed oracle rows used explicit BlockingBorrowed with AsyncShardTransport,
    worker count 4, queue capacity 1

result:
  captured with file-level allocation blocker

cold representative rows:
  prior KTLX representative:
    fail, elapsed 1.507x, allocation 2.995x
  KTLX 2026-05-04 representative:
    fail, elapsed 1.735x, allocation 2.060x
  KINX 2026-05-04 representative:
    fail, elapsed 0.585x, allocation 1.958x
  KTLX 2026-05-05 representative:
    fail, elapsed 1.809x, allocation 2.235x

warm representative rows:
  prior KTLX representative:
    fail, elapsed 1.077x, allocation 2.128x, spread 6.94%
  KTLX 2026-05-04 representative:
    fail, elapsed 1.018x, allocation 2.012x, spread 3.90%
  KINX 2026-05-04 representative:
    fail, elapsed 0.961x, allocation 1.916x, spread 1.42%
  KTLX 2026-05-05 representative:
    fail, elapsed 1.029x, allocation 2.186x, spread 4.37%

small and large warm file rows:
  all selected rows failed allocation thresholds
  allocation ratio range 1.443x to 2.207x
  elapsed ratio range 0.950x to 1.160x

safety guardrails:
  all 20 borrowed/default pairs passed
  validation/checksum parity passed
  stable totals and topology parity passed
  retained payload failed releases 0
  provider overlap failed releases 0
  current retained pressure returned to 0
  max candidate combined retained high-water 51_484_320, below the
    536_870_912 retained-byte budget
  worker failed batches/items 0/0

interpretation:
  file-level readiness is not accepted by slice 5 evidence alone
  the named blocker is retained owned snapshot allocation cost in direct
    MeasureFile() rows
  no correctness, release, cleanup, pressure, fallback, or validation blocker
    was found
  slice 6 remains required to determine whether low-count MeasureCache()
    slices amortize the retained cost or preserve the same blocker
```

Milestone 017 cold-start prewarm follow-up:

```text
status:
  opt-in prototype implemented, prewarmed MeasureFile and small-cache
  comparisons captured, and slice 7 scoped default prewarm implemented

gate document:
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-prewarmed-measurefile-gate.md

current default posture:
  direct MeasureFile()/MeasureCache() default-equivalent queued-owned contour
  prewarms retained payload resources automatically before measured rows
  prewarm is not silent: result contracts and CLI output expose the up-front
  allocated and retained bytes

implemented mechanics:
  RadarProcessingRetainedEventArrayPool.Prewarm(...)
  RadarProcessingRetainedPayloadByteArrayPool.Prewarm(...)
  RadarProcessingRetainedPayloadFactory.Prewarm(...)
  optional retained payload factory passthrough for MeasureFile(),
    MeasureCache(), and queued overlap options
  automatic default prewarm for the direct benchmark default-equivalent
    queued-owned contour when no caller supplied retained payload factory

full prewarmed gate:
  raw primary outputs:
    data\temp\m017-prewarmed-gate-runner\output\m017-prewarmed-measurefile-20260522-091327.jsonl
    data\temp\m017-prewarmed-gate-runner\output\m017-prewarmed-measurefile-20260522-091327.md
  all 20 borrowed/prewarmed-candidate pairs passed safety guardrails
  all prewarmed candidate rows had retained pool misses 0
  measured allocation ratios across the selected file matrix were 0.980x to
    1.026x against same-run borrowed rows
  explicit prewarm allocation remained real and ranged from 35_651_808 to
    71_303_392 bytes by file shape
  initial timing variance was captured for targeted recheck:
    prior representative prewarmed-probe failed elapsed at 3.632x borrowed
    KINX small prewarmed-warm failed elapsed at 1.253x borrowed
    KTLX 2026-05-04 representative prewarmed-probe optimized at 1.167x
    KTLX 2026-05-05 small prewarmed-warm optimized at 1.140x

targeted timing rerun:
  raw outputs:
    data\temp\m017-prewarmed-timing-runner\output\m017-prewarmed-timing-20260522-092557.md
    data\temp\m017-prewarmed-timing-runner\output\m017-prewarmed-timing-20260522-092557.csv
  rows repeated:
    prior representative probe and warm
    KTLX 2026-05-04 representative probe and warm
    KINX 2026-05-04 small warm
    KTLX 2026-05-05 small warm
  result:
    all repeated scenarios are interpreted as filesystem timing notes, not
    fail-level timing regressions
    average elapsed ratios ranged 1.017x to 1.056x
    max elapsed ratios ranged 1.040x to 1.143x
    average allocation ratios ranged 0.995x to 1.003x
    retained pool misses 0
    release failures 0
    worker failures 0/0

verification:
  focused regression passed:
    54 passed, 0 failed, 0 skipped
  Release build passed:
    0 warnings, 0 errors

interpretation:
  prewarm removes the measured MeasureFile() allocation blocker
  the prewarm cost remains real and must stay explicit
  fail-level timing outliers did not reproduce in targeted repeats
  the prewarmed MeasureFile contour is allocation-ready with a non-blocking
    filesystem timing note
  slice 7 accepts scoped default prewarm for the direct file/small-cache
    benchmark default-equivalent contour
```

Milestone 017 slice 6 small-file MeasureCache gate:

```text
status:
  complete

runtime behavior changes:
  none

gate document:
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-small-cache-gate.md

natural raw outputs:
  data\temp\m017-small-cache-gate-runner\output\m017-small-cache-20260522-094609.jsonl
  data\temp\m017-small-cache-gate-runner\output\m017-small-cache-20260522-094609.md

prewarmed comparison raw outputs:
  data\temp\m017-prewarmed-small-cache-gate-runner\output\m017-prewarmed-small-cache-20260522-094843.jsonl
  data\temp\m017-prewarmed-small-cache-gate-runner\output\m017-prewarmed-small-cache-20260522-094843.md

selected slices:
  KTLX 2026-05-04 max-files 2/4/8:
    published 2/4/8, skipped 0/0/0
  KINX 2026-05-04 max-files 4/8/16:
    published 2/4/8, skipped 2/4/8
  KTLX 2026-05-05 max-files 4/8/16:
    published 2/4/8, skipped 2/4/8

natural gate result:
  all 18 borrowed/default pairs passed safety guardrails
  validation/checksum parity passed
  stable totals and topology parity passed
  release failures 0
  worker failed batches/items 0/0
  current retained pressure returned to 0
  elapsed ratios ranged 0.521x to 0.986x borrowed average
  allocation ratios ranged 1.176x to 2.168x borrowed average
  status summary:
    KTLX 2026-05-04: warning/optimize/warning for 2/4/8 published files
    KINX 2026-05-04: fail/optimize/optimize for 2/4/8 published files
    KTLX 2026-05-05: fail/fail/fail for 2/4/8 published files

natural interpretation:
  low-count MeasureCache() does not amortize retained owned snapshot
    allocation enough for small-file readiness
  elapsed is not the blocker
  natural file/small-file readiness remains allocation-blocked

prewarmed comparison:
  all 18 borrowed/prewarmed-candidate pairs passed safety guardrails
  all selected slices passed measured allocation and elapsed thresholds
  measured allocation ratios ranged 0.818x to 1.002x
  elapsed ratios ranged 0.454x to 0.979x
  retained pool misses were 0 for every prewarmed candidate row
  explicit prewarm allocation remained real at about 69_206_240 bytes per
    measured candidate row

slice 6 interpretation:
  MeasureFile() and low-count MeasureCache() now point at the same posture:
  natural defaults are safety-clean but allocation-blocked for file and
  small-file readiness, while explicit prewarm removes the measured allocation
  blocker if its up-front cost is accepted and attributed
  slice 7 accepted that cost as the scoped direct benchmark default posture
```

Milestone 017 slice 7 interpretation and default prewarm:

```text
status:
  complete

interpretation document:
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-interpretation.md

implemented default contour:
  effective queued-owned rollout-default contour
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  execution: async shard transport
  worker count: 4
  worker queue capacity: 8
  provider queue capacity: 8
  retained-byte budget: 536870912
  overlap consumer delay: 0

default prewarm sizing:
  event count: 65_536
  payload bytes: 67_108_864
  retained batch count: 1

product behavior:
  MeasureFile() and MeasureCache() create a prewarmed retained payload factory
    automatically when the effective contour matches the rollout default and
    no caller supplied a retained payload factory
  explicit BlockingBorrowed remains unprewarmed
  caller-supplied retained payload factories remain caller-owned and are not
    reported as automatic default prewarm
  result contracts expose RetainedPayloadPrewarm,
    HasRetainedPayloadPrewarm, RetainedPayloadPrewarmAllocatedBytes, and
    RetainedPayloadPrewarmRetainedBytes
  CLI rebalance-archive output prints retained payload prewarm attribution

verification:
  focused regression passed:
    89 passed, 0 failed, 0 skipped
  post-default cache regression matrix:
    data\temp\m017-cache-regression-runner\output\m017-cache-regression-20260522-110241.jsonl
    data\temp\m017-cache-regression-runner\output\m017-cache-regression-20260522-110241.md
    16 group rows passed, 0 warning, 0 optimize, 0 failed
    28 borrowed/candidate pairs passed safety, 0 failed
    worst measured allocation ratio 1.009x on mixed-cache-all
    worst elapsed ratio 0.988x on KTLX 2026-05-05 2-file small-cache row
    worst candidate spread 4.60%
    pool misses 0, validation failures 0, processing completeness failures
      0, worker failed batches/items 0/0, release failures 0, current
      retained bytes 0

decision-trace input:
  natural file/small-file defaults remain historical evidence for the cold
    retained allocation blocker
  current file/small-cache default posture is queued-owned rollout default
    plus retained payload prewarm
  prewarm allocation is accepted as a named up-front default cost for this
    direct benchmark surface, not hidden inside measured allocation
  post-default cache regression matrix found no cache-level performance
    regression after the mixed-cache source-universe fix; mixed-cache-all
    worker failed batches/items are 0/0
  broader cache-level milestone 016 readiness remains accepted
```

Milestone 017 planned slices:

```text
1. file corpus inventory and gate matrix design complete
2. existing contract, reporting, and guardrail audit complete
3. threshold and runner design complete
4. focused regression and file sanity pass complete
5. cold and warm MeasureFile Release gate complete
6. small-file cache transition gate complete
7. gate interpretation and follow-up fixes complete
8. file-level readiness decision trace complete
9. closeout, handoff, and project progress complete
```

Milestone 017 closeout question:

```text
Is the queued-owned direct/default contour ready for file-level MeasureFile()
and small-file workloads?
```

Valid milestone 017 closeout answers:

```text
yes, file-level default readiness is accepted

yes with warnings, file-level default readiness is accepted with named scoped
warnings

optimize, file-level default readiness requires a named optimization before
runtime expansion

posture change, file-level defaults should split from cache-level defaults or
move behind explicit opt-in with a named reason

no, file-level default readiness is rejected with a named file, small-file
slice, threshold, lifecycle, validation, or attribution blocker

coverage insufficient, file-level default readiness cannot be decided from
the available workload evidence

defer, file-level default readiness cannot be decided because correctness,
cleanup, release health, pressure, fail-closed behavior, timing variance, or
benchmark repeatability regressed
```

Milestone 017 preserved guardrails:

```text
direct MeasureFile()/MeasureCache() omitted defaults start queued-owned with
scoped retained-payload prewarm on the direct benchmark default-equivalent
contour
explicit BlockingBorrowed remains fallback/oracle
same-run BlockingBorrowed rows remain required for readiness gates
CLI omitted-provider rebalance-archive remains aligned with direct defaults
including scoped retained-payload prewarm attribution
queued-owned failures fail closed
no automatic borrowed fallback follows queued-owned failure
controlled consumer delay remains mechanics-only proof
builder-transfer remains unsupported
broader cache-level readiness remains accepted with named scoped warnings
live ingestion/runtime defaults remain out of scope
durable queues, cross-process workers, and ordered concurrent rebalance remain
out of scope
thresholds must not be raised after gate capture
cold retained-ownership cost must not be hidden behind aggregate small-cache
success; scoped prewarm cost must remain explicit in result and CLI
```

Recommended current next action:

```text
start the next milestone:
  design runtime/live ingestion readiness separately from direct benchmark
  defaults
  treat accepted cache/file/small-cache direct benchmark readiness as evidence,
    not automatic runtime approval
  define runtime lifecycle, startup/prewarm timing, backpressure, cleanup,
    fallback, failure, cancellation, and observability contracts before any
    runtime default migration
```

## Milestone 016 Baseline

Milestone 016 is complete. The milestone documents are:

```text
docs/project-progress.md
docs/milestones/016-broader-cache-level-default-readiness.md
docs/milestones/016-broader-cache-level-default-readiness-plan.md
docs/milestones/016-broader-cache-level-default-readiness-performance-gate.md
docs/milestones/016-broader-cache-level-default-readiness-decision-trace.md
docs/milestones/016-broader-cache-level-default-readiness-closeout.md
```

Milestone 016 is the broader cache-level default readiness milestone. It
starts from the closed milestone 015 queued-owned allocation readiness result.

Milestone 016 target:

```text
decide whether the queued-owned direct/default contour is ready as the broader
cache-level benchmark/default posture for available cache workloads
```

Milestone 016 current status:

```text
architecture document: complete
implementation plan: complete
implementation: complete through slice 8 closeout and handoff
runtime behavior changes so far: none
performance gate: captured in
  docs/milestones/016-broader-cache-level-default-readiness-performance-gate.md
gate posture: captured with primary spread warning
decision trace: written in
  docs/milestones/016-broader-cache-level-default-readiness-decision-trace.md
closeout: written in
  docs/milestones/016-broader-cache-level-default-readiness-closeout.md
decision:
  accept broader cache-level default readiness with named scoped warnings
final closeout answer:
  yes with warnings, broader cache-level default readiness is accepted with
  named scoped warnings
recommended next milestone:
  File-Level Default Readiness And Cold Retained-Ownership Cost
project progress ledger:
  docs/project-progress.md
```

Milestone 016 keeps this accepted direct/default contour:

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

Milestone 016 starting evidence from milestone 015:

```text
cache-level allocation readiness: accepted for measured local contours
primary KTLX 2026-05-04 allocation ratio: 1.042x borrowed
KTLX 2026-05-05 allocation ratio: 1.0392x borrowed average
KTLX 2026-05-05 rows: 1.0404x and 1.0381x borrowed
KINX 2026-05-04 allocation ratio: 1.042x borrowed
mixed-cache allocation ratio: 1.021x borrowed
file-level warning:
  representative KTLX single-file cold smoke allocation ratio 1.512x borrowed
  representative KTLX single-file cold smoke elapsed ratio 1.072x borrowed
  interpretation: file-level concern, not cache-level blocker
```

Known local cache data at milestone 016 start:

```text
data\nexrad\level2\2026\05\04\KTLX:
  244 files, 1_347_625_897 bytes
data\nexrad\level2\2026\05\04\KINX:
  462 files, 1_404_452_903 bytes
data\nexrad\level2\2026\05\05\KTLX:
  848 files, 2_232_493_336 bytes
data\nexrad total:
  1_554 files, 4_984_572_136 bytes
```

Milestone 016 captured gate matrix:

```text
primary drift/spread row:
  data\nexrad --date 2026-05-04 --radar KTLX --max-files 220,
  repeated 3 pairs, warning because candidate spread was 12.01%

named allocation-risk row:
  data\nexrad --date 2026-05-05 --radar KTLX --max-files 220,
  repeated 2 pairs, pass with timing note because one individual pair was
  1.001x borrowed

cross-radar row:
  data\nexrad --date 2026-05-04 --radar KINX --max-files 220, pass

mixed-cache row:
  data\nexrad --max-files 1000000, pass with mixed-cache worker-counter note

optional coverage rows:
  data\nexrad --date 2026-05-04 --radar KTLX --max-files 244, pass
  data\nexrad --date 2026-05-04 --radar KINX --max-files 440, pass
  data\nexrad --date 2026-05-05 --radar KTLX --max-files 440, pass

CLI/direct alignment spot-check:
  processing benchmark rebalance-archive --cache with omitted provider passed
  explicit --provider blocking-borrowed visibility spot-check passed

file-level warning visibility:
  representative KTLX MeasureFile() single-file smoke passed in this run but
  remains coverage-only, not a file-level default readiness claim
```

Milestone 016 slice 6 interpretation:

```text
result:
  broader cache-level evidence is positive but not clean-green

decision-trace input:
  accept broader cache-level default readiness with named scoped warnings

runtime behavior changes:
  none

follow-up fixes:
  none

targeted rerun before decision trace:
  not required

borrowed worker-counter recapture before decision trace:
  not required

accepted clean pass rows:
  KINX 2026-05-04 max-files 220
  KTLX 2026-05-04 max-files 244
  KINX 2026-05-04 max-files 440
  KTLX 2026-05-05 max-files 440

named warnings/notes to carry:
  primary KTLX 2026-05-04 max-files 220 spread warning:
    candidate spread 12.01%, above 7.50%, accepted as scoped warning because
    every individual candidate run remained faster than same-run borrowed and
    correctness, allocation, release, cleanup, and pressure all passed

  KTLX 2026-05-05 named-risk timing note:
    one individual pair was 1.001x borrowed, accepted because the repeated
    average passed at 0.822x and the larger risk-440 row passed at 0.810x

  mixed-cache worker-counter note:
    candidate worker failed batches/items were 221/881 while validation
    succeeded and failed migrations remained 0; accepted without borrowed
    counter recapture before decision trace, but the decision trace must state
    that slice 5 did not recapture borrowed worker counters for this row

  file-smoke coverage-only note:
    current single-file smoke did not reproduce the milestone 015 cold warning,
    but it remains insufficient for a file-level default readiness claim
```

Milestone 016 slice 7 decision trace:

```text
document:
  docs/milestones/016-broader-cache-level-default-readiness-decision-trace.md

decision:
  accept broader cache-level default readiness with named scoped warnings

closeout answer:
  yes with warnings, broader cache-level default readiness is accepted with
  named scoped warnings

named warnings and scope limits:
  primary spread warning:
    candidate spread was 12.01%, above the 7.50% threshold, accepted as
    scoped warning because every individual candidate run remained faster than
    same-run borrowed and correctness, lifecycle, pressure, and allocation
    guardrails passed

  named-risk timing note:
    one KTLX 2026-05-05 individual pair was 1.001x borrowed, accepted because
    the repeated average was 0.822x and the larger same-shape row was 0.810x

  mixed-cache worker-counter note:
    candidate worker failed batches/items were 221/881 while validation
    succeeded and failed migrations remained 0; borrowed worker failed
    counters were not recaptured in slice 5

  file-smoke coverage-only note:
    current single-file smoke did not reproduce the milestone 015 cold warning
    but does not certify file-level default readiness

runtime expansion:
  not approved; live ingestion, durable queues, brokers, cross-process
  providers, ordered concurrent rebalance, builder-transfer, and runtime
  defaults remain out of scope
```

Milestone 016 preserved guardrails:

```text
direct MeasureFile()/MeasureCache() omitted defaults remain queued-owned
explicit BlockingBorrowed remains fallback/oracle
same-run BlockingBorrowed rows remain required for every readiness gate
CLI omitted-provider rebalance-archive remains aligned with direct defaults
queued-owned failures fail closed
no automatic borrowed fallback follows queued-owned failure
controlled consumer delay remains mechanics-only proof
builder-transfer remains unsupported
live ingestion/runtime defaults remain out of scope
durable queues, cross-process workers, and ordered concurrent rebalance remain
  out of scope
single-file cold retained-ownership cost remains a file-level concern
thresholds must not be raised after gate capture
shape-specific warnings must not be hidden behind mixed-cache aggregate success
```

Milestone 016 planned slices:

```text
1. corpus inventory and gate matrix design complete
2. existing contract and guardrail audit complete
3. reporting and harness readiness complete
4. focused regression and cache sanity pass complete
5. broader cache-level Release gate complete
6. gate interpretation and follow-up fixes complete
7. broader cache-level readiness decision trace complete
8. closeout and handoff complete
```

Milestone 016 closeout question:

```text
Is the queued-owned direct/default contour ready as the broader cache-level
benchmark/default posture for available cache workloads?
```

Valid milestone 016 closeout answers:

```text
yes, broader cache-level default readiness is accepted
yes with warnings, broader cache-level default readiness is accepted with
  named scoped warnings
no, broader cache-level default readiness is rejected with a named cache
  shape, threshold, or attribution blocker
coverage insufficient, broader cache-level default readiness cannot be decided
  from the available workload evidence
defer, broader cache-level default readiness cannot be decided because
  correctness, cleanup, release health, pressure, fail-closed behavior, timing
  variance, or benchmark repeatability regressed
```

Recommended current next action:

```text
begin milestone 017 architecture:
  File-Level Default Readiness And Cold Retained-Ownership Cost

recommended first document:
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost.md

target question:
  decide whether the queued-owned direct/default contour is ready for
  file-level MeasureFile() and small-file workloads, or whether file-level
  needs a scoped optimization/default decision before runtime expansion
```

Milestone 016 gate capture posture after slice 5:

```text
primary Release gate capture:
  completed with a temporary local direct API gate runner, built in Release and
  not committed as a product surface

direct gate rows:
  paired MeasureCache() rows with omitted provider-related arguments for
  queued-owned default rows and explicit providerMode: BlockingBorrowed for
  oracle rows

file-level warning row:
  MeasureFile() single-file smoke only for retained-ownership warning
  visibility

CLI role:
  omitted-provider cache spot-check and explicit borrowed fallback visibility
  passed; CLI was not the primary paired-row aggregation mechanism
```

Milestone 016 latest verification:

```text
slice 8 closeout:
  document:
    docs/milestones/016-broader-cache-level-default-readiness-closeout.md

  final closeout answer:
    yes with warnings, broader cache-level default readiness is accepted with
    named scoped warnings

  full test project before closeout:
    dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore

  result:
    768 passed, 0 failed, 3 skipped

  skipped tests:
    AwsNexradArchiveClientIntegrationTests.BuildManifestAsyncListsPublicAwsArchive
    AwsNexradArchiveClientIntegrationTests.DownloadFileAsyncDownloadsSmallPublicAwsObject
    NexradArchiveDecompressionValidatorCorpusTests.ValidateCachedArchiveCorpusAgainstSharpZipLib

slice 7 decision trace:
  document:
    docs/milestones/016-broader-cache-level-default-readiness-decision-trace.md

  decision:
    accept broader cache-level default readiness with named scoped warnings

  runtime behavior changes: none

slice 6 gate interpretation:
  runtime behavior changes: none
  follow-up fixes: none
  targeted rerun before decision trace: not required
  borrowed worker-counter recapture before decision trace: not required
  decision-trace posture:
    accept broader cache-level default readiness with named scoped warnings

slice 5 Release build:
  dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors

slice 5 temporary direct API gate runner build:
  dotnet build data\temp\m016-gate-runner\M016GateRunner.csproj
    -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors

slice 5 broader cache-level Release gate:
  document:
    docs/milestones/016-broader-cache-level-default-readiness-performance-gate.md

  gate status:
    captured with primary spread warning

  correctness/release/cleanup/pressure:
    validation succeeded across captured rows
    same-run borrowed/candidate counters and checksums matched in gate output
    retained payload failed releases: 0
    provider overlap failed releases: 0
    current combined retained bytes returned to 0
    max retained high-water: 54_413_280 bytes
    retained-byte budget: 536_870_912 bytes

  cache-level allocation:
    max average ratio: 1.028x borrowed
    max individual measured pair ratio: 1.040x borrowed
    threshold: <= 1.10x borrowed

  cache-level elapsed:
    primary KTLX 2026-05-04 max-files 220 average: 0.881x borrowed
    KTLX 2026-05-05 max-files 220 average: 0.822x borrowed
    KINX 2026-05-04 max-files 220: 0.769x borrowed
    mixed local cache: 0.873x borrowed
    optional KTLX 244: 0.887x borrowed
    optional KINX 440: 0.782x borrowed
    optional KTLX 2026-05-05 440: 0.810x borrowed

  warnings/notes for slice 6:
    primary candidate spread: 12.01%, above 7.50% threshold
    named-risk candidate spread: 7.42%, below but near 7.50% threshold
    named-risk individual pair 2 elapsed: 1.001x borrowed
    mixed-cache candidate worker failed batches/items: 221/881 while
      validation succeeded and failed migrations remained 0
    representative single-file smoke did not reproduce the milestone 015 cold
      warning but remains coverage-only

  CLI spot-check:
    omitted-provider cache command passed with queued-owned rollout-default
    provenance and natural-readiness telemetry visible
    explicit --provider blocking-borrowed command passed with fallback
    provenance visible and queued/retained telemetry absent as expected

slice 4 focused regression and cache sanity:
  local cache selectors present:
    KTLX 2026-05-04: 244 files, 1_347_625_897 bytes
    KINX 2026-05-04: 462 files, 1_404_452_903 bytes
    KTLX 2026-05-05: 848 files, 2_232_493_336 bytes
    representative KTLX single-file smoke path exists

  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"

result:
  112 passed, 0 failed, 0 skipped

slice 3 focused verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"

result:
  35 passed, 0 failed, 0 skipped

slice 2 focused verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"

result:
  69 passed, 0 failed, 0 skipped
```

## Milestone 015 Closed Baseline

Milestone 015 is complete. The milestone documents are:

```text
docs/milestones/015-queued-owned-allocation-readiness.md
docs/milestones/015-queued-owned-allocation-readiness-plan.md
docs/milestones/015-queued-owned-allocation-readiness-performance-gate.md
docs/milestones/015-queued-owned-allocation-readiness-decision-trace.md
docs/milestones/015-queued-owned-allocation-readiness-closeout.md
```

Milestone 015 is the queued-owned allocation readiness milestone. It starts
from the closed milestone 014 direct archive rebalance API default migration.

Milestone 015 target:

```text
reduce, bound, or deliberately accept the queued-owned direct/default
allocation warning with stronger attribution before any live/runtime default
expansion
```

Milestone 015 current status:

```text
architecture document: complete
implementation plan: complete
implementation: complete through slice 10 closeout and handoff
runtime behavior changes so far: allocation-only optimization, including
  cheaper bounded recent-detail copying and explicit pooled retained payload
  release ownership and a dedicated retained event-array pool; retained
  payload pool rent/miss/return telemetry is now split for event arrays and
  byte arrays; the wait-mode provider enqueue fast path was reverted before
  the Release gate; no direct/default contour, fallback, or release lifecycle
  changes
performance gate: captured in
  docs/milestones/015-queued-owned-allocation-readiness-performance-gate.md
gate posture: ready with file-level allocation warning
cache-level allocation posture:
  primary KTLX 2026-05-04 allocation ratio 1.042x borrowed
  KTLX 2026-05-05 allocation ratio 1.0392x borrowed average, with both rows
  below the 1.10x threshold
  KINX 2026-05-04 allocation ratio 1.042x borrowed
  mixed-cache allocation ratio 1.021x borrowed
file-level warning:
  representative KTLX single-file cold smoke allocation ratio 1.512x
  borrowed and elapsed ratio 1.072x borrowed because the first retained
  event-array and byte-array snapshot is not amortized across many retained
  batches; this is an expected cold retained-ownership price for the current
  queued-owned pooled-copy architecture, not a JIT warmup artifact
decision trace: written in
  docs/milestones/015-queued-owned-allocation-readiness-decision-trace.md
closeout: written in
  docs/milestones/015-queued-owned-allocation-readiness-closeout.md
final closeout answer:
  yes, the queued-owned direct/default allocation profile is ready to support
  the next broader cache-level benchmark/default-readiness decision
final verification:
  focused closeout regression passed, 112 passed, 0 failed, 0 skipped
  Release build succeeded, 0 warnings, 0 errors
  full test project passed, 768 passed, 0 failed, 3 skipped
recommended next milestone input:
  broader cache-level benchmark/default-readiness
```

Milestone 015 preserved these guardrails:

```text
direct MeasureFile()/MeasureCache() omitted defaults remain queued-owned
explicit BlockingBorrowed remains fallback/oracle
same-run BlockingBorrowed rows remain required for allocation gates
CLI omitted-provider rebalance-archive remains aligned with direct defaults
queued-owned failures fail closed
no automatic borrowed fallback follows queued-owned failure
controlled consumer delay remains mechanics-only proof
builder-transfer remains unsupported
live ingestion/runtime defaults remain out of scope
durable queues, cross-process workers, and ordered concurrent rebalance remain
  out of scope
thresholds must not be raised after gate capture
KTLX 2026-05-05 allocation warning must remain visible until reduced,
  bounded, or deliberately accepted for a named next surface
```

Milestone 015 optimization posture:

```text
use the best standard .NET allocation-reduction practices that fit the
  existing codebase
actively search for, investigate, prototype, and evaluate non-standard or
  experimental allocation-reduction approaches where standard practice is not
  enough
accept experimental approaches only when they preserve lifetime, ownership,
  cleanup, release, correctness, attribution, and maintainability guardrails
record adopted, deferred, and rejected standard/experimental approaches in the
  plan, decision trace, or closeout
```

Milestone 015 planned slices:

```text
1. baseline and attribution audit complete
2. allocation instrumentation and contract check complete
3. standard allocation optimization pass complete
4. experimental optimization research and spikes complete
5. adopted optimization integration complete
6. fallback, failure, cleanup, and drift guardrails complete
7. focused regression and allocation sanity pass complete
8. allocation readiness Release gate complete
9. allocation readiness decision trace complete
10. closeout and handoff complete
```

Milestone 015 starting allocation posture:

```text
KTLX 2026-05-05 allocation warning:
  direct gate average from milestone 014: 1.0997x borrowed
  row 1: 1.1018x borrowed
  row 2: 1.0976x borrowed
  threshold: <= 1.10x borrowed
  interpretation: accepted as direct API migration warning, not clean green

primary attribution hypothesis:
  direct default allocation overhead is concentrated in processing callback
  allocation and retained/owned snapshot work
```

Milestone 015 slice 1 baseline and attribution audit:

```text
status: complete
runtime behavior changes: none
allocation/result contract posture:
  current result contracts separate measured end-to-end allocation,
  processing callback allocation, replay/build allocation, owned snapshot
  allocation, and callback non-owned snapshot allocation
  CLI output already prints these archive rebalance allocation fields
attribution sufficiency:
  sufficient for the first standard optimization pass
  not fine-grained enough to independently prove retained resource wrapper,
  release callback, queued batch object, recent-detail, bounded snapshot, and
  telemetry-summary copy allocation without either code inspection or added
  instrumentation
primary hot-path targets:
  RadarProcessingRetainedPayloadFactory.RetainPooledCopy retained batch and
  closure-backed release resource creation
  RadarProcessingRetainedBatchResource.NotRequired default release callback
  ArchiveOwnedRadarEventBatchQueueingPublisher.Publish capturing onAccepted
  callback
  RadarProcessingOwnedBatchQueue per-accepted-batch queued object creation
  RadarProcessingProviderQueueTelemetryRecorder recent-detail records and
  snapshot allocation
  RadarProcessingProviderQueueTelemetrySummary defensive recent-detail copy
  RadarProcessingArchiveRebalanceBenchmark.AddQueueTelemetry
  Concat/Skip/ToArray aggregation for bounded recent details
slice 2 input:
  keep current result contracts for the first pass unless callback residual
  needs finer attribution before a safe optimization decision can be made
```

Milestone 015 slice 2 allocation instrumentation and contract check:

```text
status: complete
runtime behavior changes: none
contract decision:
  no new public result contract fields or CLI output fields before the first
  standard optimization pass
why:
  direct file/cache rebalance result contracts already expose measured,
  processing callback, replay/build, owned snapshot, and callback non-owned
  snapshot allocation
  allocation summary contract tests already cover non-negative derived buckets
  CLI output already prints the operator-visible allocation attribution fields
  direct tests already tie queued-owned owned snapshot allocation to provider
  queue telemetry
residual posture:
  retained resource wrapper/release callback allocation, queued batch object
  allocation, recent-detail records, bounded recent-detail snapshots,
  defensive telemetry-summary copies, and AddQueueTelemetry
  Concat/Skip/ToArray allocation remain inside processing callback residual
  for slice 3
rejected for slice 2:
  adding retained payload resource wrapper allocated bytes
  adding provider queue item allocated bytes
  adding telemetry recorder allocated bytes
  changing CLI allocation output labels
  changing archive rebalance result constructor shape
slice 3 input:
  proceed to standard allocation optimization with stable public contracts;
  add temporary micro-harnesses or permanent attribution later only if current
  residual attribution cannot support a safe optimization decision
```

Milestone 015 slice 3 standard allocation optimization pass:

```text
status: complete
runtime behavior changes:
  allocation profile should improve on bounded recent-detail aggregation,
  defensive recent-detail copy, and not-required retained-resource release
  delegate paths
  direct/default contour, fallback behavior, telemetry semantics, release
  lifecycle, and result contracts did not change
accepted standard optimizations:
  RadarProcessingArchiveRebalanceBenchmark.CreateBoundedRecentDetails now
  copies bounded recent details directly into one destination array instead of
  using Concat/Skip/ToArray
  RadarProcessingProviderQueueTelemetrySummary.CopyRequired now validates and
  copies recent details directly into one array instead of using List plus a
  second ToArray allocation
  RadarProcessingRetainedBatchResource now uses static per-strategy
  not-required release delegates instead of creating a capturing default
  release lambda per resource
deferred to slice 4:
  explicit pooled retained payload release owner remains an experimental
  candidate because it touches pooled array ownership and release timing
  pooled telemetry accumulator and struct-backed queued work item ideas remain
  experimental
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests"
  76 passed, 0 failed, 0 skipped
slice 4 input:
  investigate non-standard or experimental approaches, especially explicit
  pooled retained payload release ownership, and adopt only if lifetime,
  release, cleanup, and failure guardrails remain clearer than the current
  callback path
```

Milestone 015 slice 4 experimental optimization research and spikes:

```text
status: complete
runtime behavior changes:
  allocation profile should improve on pooled retained payload release owner
  creation because the pooled-copy path no longer creates a closure-backed
  release callback and delegate per retained batch
  retained payload release semantics, resource state transitions, cleanup,
  result contracts, and direct/default contour did not change
adopted experiment:
  explicit pooled retained payload release owner
implementation:
  Domain adds an internal IRadarProcessingRetainedPayloadReleaseOwner
  contract, visible to RadarPulse.Infrastructure through the existing
  InternalsVisibleTo boundary
  RadarProcessingRetainedBatchResource keeps the public Func-based
  constructor for existing callers/tests and adds an internal owner-based
  constructor for infrastructure-owned release implementations
  RadarProcessingRetainedPayloadFactory.RetainPooledCopy now uses a private
  PooledRetainedPayloadReleaseOwner to own rented event/payload arrays, pools,
  and retained payload byte count
release posture:
  pooled release still returns rented arrays exactly once through the existing
  RadarProcessingRetainedBatchResource.Release() state machine
  second release attempts still return AlreadyReleased without invoking the
  owner again
deferred experiments:
  pooled telemetry accumulator remains deferred because immutable summary
  boundaries and recorder reset ownership need a broader design
  struct-backed queued work item remains deferred because queue/channel
  semantics and sequence ownership would make the change wider than current
  evidence justifies
  allocation probe split remains deferred until Release gate evidence shows
  current residual attribution is insufficient
rejected for slice 4:
  no public result or CLI attribution fields were added
  no unsafe memory, stack-lifetime, or measured-window-shifting approach was
  used
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests"
  56 passed, 0 failed, 0 skipped
slice 5 input:
  integrate the accepted standard and experimental allocation optimizations
  through broader focused contour, fallback/oracle, retained cleanup, and
  allocation summary guardrails
```

Milestone 015 slice 5 adopted optimization integration, completed status:

```text
status: complete
runtime behavior changes:
  direct/default contour, fallback behavior, and release lifecycle did not
  change
  retained payload retention/release result contracts now carry pool
  rent/miss/return counts, and the existing retained telemetry summary/CLI
  pool fields are populated for pooled-copy rows
reverted standard optimization:
  RadarProcessingOwnedBatchQueue.EnqueueAsync wait-mode synchronous fast path
  was reverted before the Release gate
  reason:
    the measured benefit was noisy and not the dominant allocation source
    after retained event-array pooling
    carrying a special wait-mode pre-write path made queue semantics harder to
    reason about before the allocation readiness decision
  current posture:
    ReturnFull mode still uses the immediate non-waiting enqueue path because
    that is its explicit behavior
    Wait mode uses the async wait loop for capacity, retained-byte budget,
    state changes, timeout, and cancellation behavior
attribution refinement:
  RadarProcessingBenchmarkAllocationSnapshot now records global versus
  current-thread counter scope and rejects mixed-scope deltas
  retained payload factory allocation telemetry now uses current-thread
  snapshots because retained snapshot-copy and pooled-copy execute
  synchronously on the producer thread
  end-to-end, processing callback, and provider overlap measured allocation
  remain global-counter measurements, so the Release gate allocation ratio is
  not hidden or moved out of the measured contract
  CLI output labels the counter scopes for allocation attribution, retained
  payload telemetry, and provider overlap telemetry
  RadarProcessingRetainedPayloadByteArrayPool now counts rent attempts,
  return attempts, and large-array cold misses
  RadarProcessingRetainedPayloadFactory.RetainPooledCopy records per-retain
  pool rents and exact retained byte-pool misses; pooled resource release
  records returned arrays; ArchiveOwnedRadarEventBatchQueueingPublisher
  aggregates those counts into RadarProcessingRetainedPayloadTelemetrySummary
interim allocation sanity, not final gate:
  Release CLI omitted-provider KTLX 2026-05-05 --max-files 220 same-run
  borrowed/default pairs were captured to decide whether more optimization is
  needed before the direct API Release gate
  after slice 4 before enqueue fast path:
    allocation ratios: 1.0943x, 1.0960x borrowed
    average allocation ratio: 1.0951x borrowed
    average elapsed ratio: 0.9370x borrowed
  after enqueue fast path, before it was reverted:
    allocation ratios: 1.0947x, 1.1014x borrowed
    average allocation ratio: 1.0980x borrowed
    average elapsed ratio: 0.9251x borrowed
interpretation:
  interim allocation around the enqueue fast path was better than the
  milestone 014 KTLX 2026-05-05 average of 1.0997x borrowed, but not clean
  because one post-fast-path row still exceeded the 1.10x threshold; the
  wait-mode fast path is not accepted milestone output
  the next optimization blocker is not provider enqueue overhead; it is
  retained pooled-copy cold/churn allocation plus overlap attribution
  ambiguity from the global allocation counter
retained allocation profile after current-thread attribution:
  one Release CLI omitted-provider KTLX 2026-05-05 --max-files 220 default row
  reported retained payload allocated bytes 222323496, provider overlap
  measured allocated bytes 2577831248, provider overlap unattributed
  allocated bytes 2355507752, end-to-end allocated bytes 2581186808, and
  processing callback non-owned snapshot bytes 1999761400
  interpretation: retained pooled-copy cold/churn allocation is real, while
  the much larger processing callback non-owned snapshot and overlap
  unattributed buckets are global-counter overlap attribution buckets
retained pooled-copy micro-harness:
  deterministic synthetic retained-copy coverage now separates cold and warm
  large retained byte-array behavior
  cold same-shape retain records two pool rents, one retained byte-pool miss,
  and two pool returns after release
  warm same-shape retain records two pool rents, zero retained byte-pool
  misses, and two pool returns after release
  cold allocation is higher than warm allocation, confirming that same-shape
  pool reuse works and the first large retained byte-array rent is a real
  cold allocation source
KTLX 2026-05-05 pool-miss sanity, not final gate:
  Release CLI same-run borrowed/default pairs were rerun after retained
  payload pool telemetry was populated
  row 1:
    borrowed elapsed ms 10804.30, default elapsed ms 10211.49,
    elapsed ratio 0.9451x borrowed
    borrowed allocated bytes 2340781368, default allocated bytes 2577312360,
    allocation ratio 1.1010x borrowed
    retained payload allocated bytes 220226320
    retained payload pool rents 208, returns 208, misses 1
  row 2:
    borrowed elapsed ms 10448.97, default elapsed ms 10123.30,
    elapsed ratio 0.9688x borrowed
    borrowed allocated bytes 2344037920, default allocated bytes 2565238576,
    allocation ratio 1.0944x borrowed
    retained payload allocated bytes 211837640
    retained payload pool rents 208, returns 208, misses 1
  average:
    elapsed ratio 0.9568x borrowed
    allocation ratio 1.0977x borrowed
    borrowed allocated bytes average 2342409644
    default allocated bytes average 2571275468
    retained payload allocated bytes average 216031980
    retained payload pool miss rate 2 / 416 rents, 0.4808%
  safety:
    validation succeeded and checksum matched borrowed:
      11084221590146245827
    retained payload failed copies 0, retained failed releases 0, provider
    overlap failed releases 0, current retained pressure returned to 0
  interpretation:
    allocation is still not clean green because row 1 remained slightly above
    1.10x borrowed, but the two-row average stayed below threshold and elapsed
    remained faster than borrowed
    retained byte-pool churn is not the blocker; each default row had only one
    retained byte-pool miss for 104 retained batches and 208 rents, with all
    rents returned
    do not tune retained byte-pool capacity or eviction policy from this
    evidence; the remaining warning is cold retained representation cost plus
    global overlap/non-pool allocation attribution unless later gate data says
    otherwise
retained event-array allocation hypothesis:
  KTLX 2026-05-05 default rows retained 3581280 RadarStreamEvent values
  RadarStreamEvent is 64 bytes, so retained event metadata alone is about
  229201920 bytes before array/object overhead
  measured retained payload allocation after pool-miss telemetry was
  211837640 to 220226320 bytes, while retained byte-pool misses were only 1
  per row
  interpretation:
    the remaining retained-copy allocation is most likely dominated by
    retained event-array cold allocation/reuse behavior, not payload byte-pool
    churn
retained event-array pool spike:
  status: accepted into the milestone implementation; broader focused
  regression passed and final Release gate confirmation is pending
  implementation:
    RadarProcessingRetainedEventArrayPool was added as a dedicated
    RadarStreamEvent[] pool for retained pooled-copy event metadata
    it mirrors the retained byte-array pool: best-fit reuse, retained array
    count cap, retained byte cap, rent/return/miss counters, and small-array
    fallback to ArrayPool<RadarStreamEvent>.Shared
    RadarProcessingRetainedPayloadFactory now uses this event pool by default
    retained telemetry now splits total pool counters into retained event
    array pool rents/returns/misses and retained byte array pool
    rents/returns/misses
    CLI retained payload telemetry prints the split counters
  focused coverage:
    cold/warm retained event-array micro-harness
    event-array pool large reuse, small fallback, and retained-byte bounds
    publisher aggregation for split retention/release counters
  KTLX 2026-05-05 Release CLI same-run pairs after event-array pool:
    row 1:
      elapsed ratio 0.9922x borrowed
      allocation ratio 1.0400x borrowed
      borrowed/default allocated bytes 2339719360 / 2433261904
      retained payload allocated bytes 73424544
      event array pool rents/returns/misses 104 / 104 / 2
      byte array pool rents/returns/misses 104 / 104 / 1
    row 2:
      elapsed ratio 0.9219x borrowed
      allocation ratio 1.0384x borrowed
      borrowed/default allocated bytes 2342342616 / 2432259200
      retained payload allocated bytes 73424544
      event array pool rents/returns/misses 104 / 104 / 2
      byte array pool rents/returns/misses 104 / 104 / 1
    average:
      elapsed ratio 0.9565x borrowed
      allocation ratio 1.0392x borrowed
      retained payload allocated bytes average 73424544
  interpretation:
    the event-array hypothesis was confirmed by the retained allocation drop:
    previous post-telemetry retained allocation averaged about 216031980
    bytes, and event-array pooling reduced it to 73424544 bytes on the same
    KTLX 2026-05-05 shape
    both KTLX allocation rows moved well below the 1.10x warning threshold in
    this interim CLI sanity pass
    release and cleanup remained clean: retained failed copies 0, retained
    failed releases 0, provider overlap failed releases 0, current retained
    pressure returned to 0
    builder-transfer and RadarStreamEvent packing/redesign remain deferred
    unless the final Release gate exposes a new retained representation
    blocker
rejected spike:
  increasing RadarProcessingRetainedPayloadByteArrayPool default retained
  bytes from 128 MiB to 256 MiB was tested and reverted because it retained
  more memory without reducing the KTLX 2026-05-05 retained snapshot
  allocation enough to clear the warning
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests"
  83 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
  82 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedPayloadContractTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests"
  30 passed, 0 failed, 0 skipped
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedPayloadContractTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
  125 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedPayloadContractTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
  129 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  768 passed, 0 failed, 3 skipped
next:
  capture slice 8 allocation readiness Release gate with direct API default
  rows and same-run explicit BlockingBorrowed oracle rows
  do not tune retained byte-pool shape retention from current evidence; pool
  misses are too low to explain the remaining KTLX allocation warning
```

Milestone 015 slice 6 fallback/failure/cleanup/drift guardrails:

```text
status: complete
runtime behavior changes: none
guardrails confirmed:
  direct MeasureFile()/MeasureCache() omitted defaults remain queued-owned
  explicit BlockingBorrowed remains selectable as fallback/oracle
  queued-owned direct default failure fails closed and does not fall back to
  borrowed
  builder-transfer remains unsupported
  controlled consumer delay remains mechanics-only proof
  retained payload and provider-overlap release failures remain visible
  cancellation and validation failure release retained resources and clear
  retained pressure
  CLI omitted-provider rollout remains aligned with shared defaults
new drift coverage:
  direct queued-owned rollout contour tests require split retained event/byte
  pool telemetry to sum to total pool telemetry and require rents to be
  returned at completion
  CLI omitted-provider output asserts retained event-array and byte-array
  pool telemetry labels
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
  77 passed, 0 failed, 0 skipped
```

Milestone 015 slice 7 focused regression pass before gate:

```text
status: complete
runtime behavior changes: none
gate precondition:
  focused regression passed and Release build succeeded, so slice 8 Release
  allocation-readiness gate capture is unblocked
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
  112 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
```

Milestone 015 post-slice 7 follow-up: wait-mode enqueue fast-path revert:

```text
status: complete
runtime behavior changes:
  RadarProcessingOwnedBatchQueue.EnqueueAsync no longer uses the special
  wait-mode synchronous pre-write fast path
  ReturnFull mode still uses the immediate non-waiting enqueue path because
  that is the explicit ReturnFull behavior
  direct/default contour, fallback behavior, retained ownership, and release
  lifecycle did not change
reason:
  the fast-path measurement was noisy and did not become the dominant
  allocation win after retained event-array pooling
  removing it keeps the Release gate focused on retained representation
  optimization and avoids carrying an extra queue-semantics risk into the
  allocation readiness decision
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"
  105 passed, 0 failed, 0 skipped
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
  112 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
KTLX 2026-05-05 one-row Release CLI same-run sanity after revert, not final
gate:
  borrowed/default elapsed ms: 10121.18 / 8947.04
  elapsed ratio: 0.8840x borrowed
  borrowed/default allocated bytes: 2347585456 / 2432466384
  allocation ratio: 1.0362x borrowed
  retained payload allocated bytes: 73424544
  retained event array pool rents/returns/misses: 104 / 104 / 2
  retained byte array pool rents/returns/misses: 104 / 104 / 1
  validation succeeded and checksum matched borrowed:
    11084221590146245827
  retained payload failed releases: 0
  provider overlap failed releases: 0
  current retained pressure returned to 0
interpretation:
  reverting the wait-mode fast path did not remove the event-array pool
  allocation improvement on the KTLX 2026-05-05 risk shape
  this is only a sanity row; slice 8 still needs the planned direct API
  Release allocation-readiness gate
```

Milestone 015 slice 8 allocation readiness Release gate:

```text
status: complete
runtime behavior changes: none
gate document:
  docs/milestones/015-queued-owned-allocation-readiness-performance-gate.md
gate status:
  ready with file-level allocation warning
verification before capture:
  focused slice 7 regression passed, 112 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
capture method:
  temporary direct API harness called MeasureCache()/MeasureFile() directly
  default rows omitted provider-related direct API arguments
  borrowed rows used explicit providerMode: BlockingBorrowed
  explicit rollout row supplied all queued-owned rollout controls
primary KTLX 2026-05-04:
  elapsed ratio: 0.889x borrowed
  allocation ratio: 1.042x borrowed
  direct-default timing spread: 1.10%
KTLX 2026-05-05:
  elapsed ratio: 0.943x borrowed average
  allocation ratio: 1.0392x borrowed average
  allocation rows: 1.0404x and 1.0381x borrowed
  interpretation: named risk contour moved materially below the 1.10x
  threshold and both repeated rows passed
broader rows:
  KINX 2026-05-04 allocation ratio: 1.042x borrowed
  mixed-cache allocation ratio: 1.021x borrowed
retained pressure and cleanup:
  max combined retained payload high watermark: 54413280 bytes, 10.14% of
  the 536870912 byte budget
  retained payload failed releases: 0
  provider overlap failed releases: 0
  pending, active, and combined retained pressure returned to 0
  event-array and byte-array pool rents were returned
direct/rollout/fallback posture:
  omitted direct defaults resolved to the queued-owned rollout contour
  explicit queued-owned rollout spot-check matched deterministic totals and
  contour fields
  explicit BlockingBorrowed remained a separate oracle
  no automatic borrowed fallback was used
file-level smoke warning:
  representative KTLX single-file cold smoke allocation ratio: 1.512x
  borrowed
  single-file elapsed ratio: 1.072x borrowed
  retained allocated bytes: 69206320
  event-array pool rents/returns/misses: 1 / 1 / 1
  byte-array pool rents/returns/misses: 1 / 1 / 1
  interpretation: one retained snapshot copy is not amortized on the
  single-file shape; this is the expected cold retained-ownership price for
  queued-owned pooled-copy in the current architecture, not a JIT warmup
  artifact
  cache-level readiness can be accepted with this warning because cache
  contours amortize the cold retained snapshot cost across many batches
  file-level default latency/allocation should be named as a separate future
  blocker or optimization target if that surface is chosen next
  prewarming or shared pools would need an explicit contract change and must
  not hide allocation outside the measured window
mixed-cache note:
  worker failed batches/items were 221 / 881 in both borrowed and direct
  default rows; validation succeeded and the counters were not
  candidate-specific
next slice:
  write the allocation-readiness decision trace
  decide whether cache-level allocation readiness is accepted with the
  expected file-level cold retained-ownership warning, and explicitly name
  file-level default latency/allocation as a separate future target if that
  surface is chosen next
```

Milestone 015 slice 9 allocation readiness decision trace:

```text
status: complete
runtime behavior changes: none
decision trace:
  docs/milestones/015-queued-owned-allocation-readiness-decision-trace.md
format:
  each decision explanation uses the required Decision / Why chosen /
  Alternatives / Rejected because / Trade-offs/debt / Review explanation
  structure
decision:
  cache-level allocation readiness is accepted for the queued-owned
  direct/default archive rebalance contour
  KTLX 2026-05-05 warning is reduced and bounded for cache-level readiness
  single-file cold warning is accepted as expected retained-ownership cost
  and a scope limit, not a cache-level blocker
  standard and adopted experimental optimizations are sufficient for the
  current cache-level readiness decision
  explicit BlockingBorrowed remains fallback and same-run oracle
  CLI and direct omitted defaults remain aligned to the shared queued-owned
  rollout contour
  broader cache-level benchmark/default-readiness work is approved as the
  next named input
  live/runtime defaults remain out of scope and are not approved
recommended next milestone input:
  broader cache-level benchmark/default-readiness work with same-run
  BlockingBorrowed oracle rows and explicit scope language for the
  single-file cold retained-ownership warning
  if file-level default latency/allocation is chosen instead, treat the
  single-file cold warning as the named optimization target
next slice:
  write closeout and update handoff with final milestone posture
```

Milestone 015 slice 10 closeout and handoff:

```text
status: complete
runtime behavior changes: none
closeout:
  docs/milestones/015-queued-owned-allocation-readiness-closeout.md
final closeout answer:
  yes, the queued-owned direct/default allocation profile is ready to support
  the next broader cache-level benchmark/default-readiness decision
final allocation posture:
  cache-level allocation readiness is accepted
  KTLX 2026-05-05 warning is reduced and bounded
  single-file cold allocation remains an expected retained-ownership cost and
  scope limit, not a cache-level blocker
final verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
  passed, 112 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  passed, 768 passed, 0 failed, 3 skipped
recommended next milestone input:
  broader cache-level benchmark/default-readiness
```

Milestone 015 likely implementation targets:

```text
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Infrastructure/Processing/RadarProcessingRetainedPayloadFactory.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapRunner.cs
src/Infrastructure/Processing/RadarProcessingOwnedBatchQueue.cs
src/Infrastructure/Processing/RadarProcessingQueuedProcessingSession.cs
src/Infrastructure/Processing/RadarProcessingQueuedRebalanceSession.cs
src/Infrastructure/Archive/ArchiveOwnedRadarEventBatchQueueingPublisher.cs
src/Domain/Processing/RadarProcessingRebalanceAllocationSummary.cs
src/Domain/Processing/RadarProcessingOwnedSnapshotAllocationSummary.cs
src/Domain/Processing/IRadarProcessingRetainedPayloadReleaseOwner.cs
src/Domain/Processing/RadarProcessingRetainedBatchResource.cs
src/Domain/Processing/RadarProcessingRetainedPayloadRetentionResult.cs
src/Domain/Processing/RadarProcessingRetainedPayloadReleaseResult.cs
src/Domain/Processing/RadarProcessingRetainedPayloadTelemetrySummary.cs
src/Domain/Processing/RadarProcessingProviderQueueTelemetrySummary.cs
src/Infrastructure/Processing/RadarProcessingRetainedPayloadByteArrayPool.cs
src/Infrastructure/Processing/RadarProcessingRetainedEventArrayPool.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Archive/ArchiveOwnedRadarEventBatchQueueingPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceAllocationSummaryTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedPayloadFactoryTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedPayloadContractTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedBatchResourceTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProviderReadinessGateTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Milestone 014 remains the closed baseline. The milestone 014 documents are:

```text
docs/milestones/014-direct-archive-rebalance-api-default-migration.md
docs/milestones/014-direct-archive-rebalance-api-default-migration-plan.md
docs/milestones/014-direct-archive-rebalance-api-default-migration-performance-gate.md
docs/milestones/014-direct-archive-rebalance-api-default-migration-decision-trace.md
docs/milestones/014-direct-archive-rebalance-api-default-migration-closeout.md
```

Milestone 014 is the direct archive rebalance API default migration milestone.
It starts from the closed milestone 013 post-rollout hardening result and
targets only direct
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` and `MeasureCache()`
defaults.

Milestone 014 final result:

```text
direct MeasureFile()/MeasureCache() omitted provider/execution/queue/retention
arguments migrated from blocking-borrowed partitioned-barrier behavior to the
accepted queued-owned rollout contour
```

Target direct default contour:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async
worker count: 4
async worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536870912
queue telemetry: summary where available in result contracts
overlap telemetry: summary where available in result contracts
overlap consumer delay: 0
```

Final milestone 014 posture:

```text
direct MeasureFile() omitted defaults:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  execution: async
  worker count: 4
  async worker queue capacity: 8
  provider queue capacity: 8
  retained-byte budget: 536870912

direct MeasureCache() omitted defaults:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  execution: async
  worker count: 4
  async worker queue capacity: 8
  provider queue capacity: 8
  retained-byte budget: 536870912

CLI omitted-provider rebalance-archive path:
  already uses the queued-owned rollout contour accepted in milestone 012 and
  hardened in milestone 013

operator help:
  rebalance-archive usage now states direct MeasureFile()/MeasureCache()
  defaults use the same queued-owned rollout contour
  --provider blocking-borrowed remains the documented fallback/oracle path
```

Milestone 014 must preserve these guardrails:

```text
explicit direct blocking-borrowed remains fallback/oracle
same-run borrowed rows remain required for migration gates
direct file and cache defaults should migrate symmetrically unless a decision
  trace names a blocker
CLI omitted-provider rollout contour remains aligned with direct defaults
queued-owned failures fail closed
no automatic borrowed fallback follows queued-owned failure
controlled consumer delay remains mechanics-only proof
builder-transfer remains unsupported
live ingestion/runtime defaults remain out of scope
durable queues, cross-process workers, and ordered concurrent rebalance remain
  out of scope
```

Milestone 014 completed slices:

```text
1. direct API baseline audit
2. shared rollout contour contract
3. direct file default migration
4. direct cache default migration
5. fallback, failure, and cleanup guardrails
6. operator help and documentation cleanup
7. focused regression pass before gate
8. direct API Release gate
9. direct API migration decision trace
10. closeout and handoff
```

Milestone 014 implementation plan status:

```text
status: complete
slice 1: direct API baseline audit complete
slice 2: shared rollout contour contract complete
slice 3: direct file default migration complete
slice 4: direct cache default migration complete
slice 5: fallback, failure, and cleanup guardrails complete
slice 6: operator help and documentation cleanup complete
slice 7: focused regression pass before gate complete
slice 8: direct API Release gate complete
slice 9: direct API migration decision trace complete
slice 10: closeout and handoff complete
runtime behavior changes so far:
  direct MeasureFile() omitted defaults now use the queued-owned rollout
  contour
  direct MeasureCache() omitted defaults now use the queued-owned rollout
  contour
  slice 5 adds guardrail coverage only; no runtime behavior changed
  slice 6 updates operator help/docs only; no runtime behavior changed
  slice 7 is verification-only; no runtime behavior changed
  slice 8 is gate documentation only; no runtime behavior changed
  slice 9 is decision documentation only; no runtime behavior changed
  slice 10 is closeout documentation only; no runtime behavior changed
final closeout verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
  84 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  761 passed, 0 failed, 3 skipped
latest Release gate:
  docs/milestones/014-direct-archive-rebalance-api-default-migration-performance-gate.md
  gate status: captured with allocation warning
  primary elapsed ratio: 0.911x borrowed
  primary allocation ratio: 1.071x borrowed
  KTLX 2026-05-05 allocation ratio: 1.0997x borrowed average, with one row
    above and one row below the 1.10x threshold
  max combined retained payload high watermark: 54413280 bytes of the
    536870912 byte budget
  release failures: 0 across direct default rows
  retained pressure at completion: 0 across direct default rows
latest decision trace:
  docs/milestones/014-direct-archive-rebalance-api-default-migration-decision-trace.md
  decision: direct MeasureFile()/MeasureCache() omitted defaults are accepted
    as the queued-owned rollout contour
  file/cache symmetry: accepted
  explicit BlockingBorrowed fallback/oracle: preserved
  CLI/direct rollout contour alignment: accepted
  KTLX 2026-05-05 allocation: accepted as warning, not clean green
  live/runtime defaults: out of scope and not approved
  recommended next milestone input: targeted allocation reduction or
    allocation-readiness before any live/runtime default expansion; if broader
    benchmark expansion is chosen first, keep same-run BlockingBorrowed oracle
    rows and the KTLX warning visible
latest closeout:
  docs/milestones/014-direct-archive-rebalance-api-default-migration-closeout.md
  final status: complete
  final direct API posture: MeasureFile()/MeasureCache() omitted defaults use
    the queued-owned rollout contour
  explicit fallback/oracle posture: providerMode BlockingBorrowed remains
    selectable and remains the same-run comparison oracle
  KTLX 2026-05-05 allocation posture: accepted as tracked allocation warning,
    not clean green
  next milestone recommendation: targeted allocation reduction or
    allocation-readiness before live/runtime expansion
```

Milestone 014 slice 1 baseline capture:

```text
MeasureFile() still defaults to BlockingBorrowed, PartitionedBarrier, queue
  capacity parameter 1, provider overlap None, retention SnapshotCopy,
  retained-byte budget null, and overlap consumer delay 0
MeasureCache() still defaults to the same borrowed partitioned-barrier contour
direct file/cache tests already cover borrowed omitted defaults and explicit
  queued-owned rollout contours
result fields are sufficient to prove direct contours without adding direct
  API provenance fields
Program usage and RadarPulseCliRebalanceBenchmarkTests still say direct
  MeasureFile()/MeasureCache() defaults remain blocking-borrowed; slice 6
  updates this after migration
```

Milestone 014 slice 2 shared rollout contour contract:

```text
runtime behavior changes: none
new shared contract:
  src/Infrastructure/Processing/RadarProcessingArchiveRebalanceRolloutDefaults.cs
contract values:
  queued-owned, producer-consumer, pooled-copy, async shard transport,
  worker count 4, worker queue capacity 8, provider queue capacity 8,
  retained-byte budget 536870912, overlap consumer delay 0
CLI alignment:
  ProcessingBenchmarkArchiveRebalanceOptions rollout constants and omitted
  provider expansion now read the shared contract
direct test alignment:
  explicit queued-owned file/cache rollout tests now use the shared contract
  direct queued-owned contour assertion helpers verify result fields against
  the shared contract
new drift guards:
  RebalanceArchiveBenchmarkRolloutDefaultContractPinsAcceptedContour
  ArchiveRebalanceBenchmarkOptionsRolloutContourMatchesSharedContract
next:
  migrate direct MeasureFile() omitted defaults using the shared rollout
  contract
```

Milestone 014 slice 3 direct file default migration:

```text
runtime behavior changes:
  direct MeasureFile() omitted provider/execution/queue/retention arguments now
  resolve to the shared queued-owned rollout contour
  direct MeasureCache() omitted defaults remained blocking-borrowed before
  slice 4
implementation:
  MeasureFile() now resolves nullable direct control arguments into effective
  values so omitted provider uses rollout defaults while explicit
  providerMode: BlockingBorrowed remains a borrowed fallback contour
tests:
  RebalanceArchiveBenchmarkFileUsesRolloutDefaultAndPreservesBorrowedFallback
  proves omitted MeasureFile() is rollout, explicit BlockingBorrowed is
  borrowed, explicit queued-owned rollout matches omitted default, and stable
  totals/checksums match the borrowed oracle
next:
  slice 4 migrated direct MeasureCache() omitted defaults using the same
  effective default pattern
```

Milestone 014 slice 4 direct cache default migration:

```text
runtime behavior changes:
  direct MeasureCache() omitted provider/execution/queue/retention arguments
  now resolve to the shared queued-owned rollout contour
  direct MeasureFile() and MeasureCache() omitted defaults are now symmetric
implementation:
  MeasureCache() now resolves nullable direct control arguments into effective
  values so omitted provider uses rollout defaults while explicit
  providerMode: BlockingBorrowed remains a borrowed fallback contour
tests:
  RebalanceArchiveBenchmarkCacheUsesRolloutDefaultAndPreservesBorrowedFallback
  proves omitted MeasureCache() is rollout, explicit BlockingBorrowed is
  borrowed, explicit queued-owned rollout matches omitted default, and stable
  cache totals/checksums match the borrowed oracle
  borrowed cache comparison rows now request providerMode: BlockingBorrowed
  explicitly
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
  23 passed, 0 failed, 0 skipped
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
  27 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
next:
  slice 5 added fallback, failure, cancellation, release, and cleanup
  guardrails around the now-migrated direct defaults
```

Milestone 014 slice 5 fallback, failure, and cleanup guardrails:

```text
runtime behavior changes:
  none beyond the direct default migrations from slices 3 and 4
direct API guardrails:
  RebalanceArchiveBenchmarkDirectBorrowedFallbackOmitsQueuedTelemetry proves
  explicit BlockingBorrowed file/cache calls remain selectable and do not
  report queue, retention, overlap, retained-pressure, or worker telemetry
  RebalanceArchiveBenchmarkDirectDefaultFailureDoesNotFallbackToBorrowed
  proves an omitted-provider queued-owned retained-byte-budget failure is
  surfaced as a failure instead of silently rerunning the borrowed path
  RebalanceArchiveBenchmarkDirectDefaultRejectsBuilderTransfer proves
  omitted-provider direct file/cache calls still reject builder-transfer
  retained payload execution
reused lower-level guardrails:
  RadarProcessingArchiveQueuedOverlapRunnerTests cover producer failure,
  cancellation after accepted enqueue, validation failure, release of pending
  and active retained resources, retained-pressure cleanup, and no fallback
  success after queued-owned faults
  RadarProcessingQueuedProviderReadinessGateTests cover queued validation
  failures, retention/release failures, cleanup incompleteness, natural
  evidence exclusion for controlled delay, and threshold interpretation
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
  50 passed, 0 failed, 0 skipped
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
  27 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
next:
  slice 6 updated operator help and CLI tests to describe direct
  MeasureFile()/MeasureCache() defaults as the queued-owned rollout contour
```

Milestone 014 slice 6 operator help and documentation cleanup:

```text
runtime behavior changes:
  none
operator help:
  rebalance-archive usage still names the omitted-provider rollout default as
  queued-owned + pooled-copy + producer-consumer, async workers 4, queue
  capacity 8, retained-byte budget 536870912
  rebalance-archive usage now states direct MeasureFile()/MeasureCache()
  defaults use the same queued-owned rollout contour
  rebalance-archive usage still names --provider blocking-borrowed as the
  fallback/oracle path for same-run comparison
  controlled overlap consumer delay remains documented as mechanics proof
  rather than natural rollout evidence
tests:
  RadarPulseCliRebalanceBenchmarkTests asserts the new direct default help
  posture
docs:
  this handoff and the milestone 014 plan record slice 6 completion;
  historical milestone 012 and 013 statements remain closed-context history
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
  27 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  761 passed, 0 failed, 3 skipped
next:
  run the focused regression pass before Release gate capture
```

Milestone 014 slice 7 focused regression pass before gate:

```text
runtime behavior changes:
  none
focused regression:
  direct MeasureFile()/MeasureCache() default migration tests passed
  explicit blocking-borrowed fallback/oracle tests passed
  explicit queued-owned equivalence tests passed
  CLI help and rollout contour alignment tests passed
  queued-owned failure, cancellation, cleanup, and fallback tests passed
  readiness threshold interpretation tests passed
  allocation summary attribution tests passed
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
  84 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
next:
  capture the direct API Release gate
```

Milestone 014 slice 8 direct API Release gate:

```text
runtime behavior changes:
  none
gate document:
  docs/milestones/014-direct-archive-rebalance-api-default-migration-performance-gate.md
gate status:
  captured with allocation warning
capture posture:
  Release build succeeded with 0 warnings and 0 errors before measurements
  temporary local harness called MeasureCache() directly so the gate measured
  direct omitted defaults instead of CLI-expanded effective options
  same-run explicit BlockingBorrowed rows remained the borrowed oracle
  explicit queued-owned rollout spot-check matched direct default deterministic
  output and contour fields
key results:
  correctness parity passed across captured rows
  retained payload and provider overlap failed releases were 0 across direct
  default rows
  current pending, active, and combined retained pressure returned to 0 across
  direct default rows
  max combined retained payload high watermark was 54413280 bytes of the
  536870912 byte budget
  primary KTLX 2026-05-04 elapsed ratio was 0.911x borrowed over four pairs
  primary KTLX 2026-05-04 allocation ratio was 1.071x borrowed
  KINX 2026-05-04 allocation ratio was 1.069x borrowed
  mixed-cache allocation ratio was 1.066x borrowed
  KTLX 2026-05-05 allocation ratio averaged 1.0997x borrowed, with row 1 at
  1.1018x and row 2 at 1.0976x
  primary direct-default timing had a favorable outlier: all four rows spread
  10.41%, while stabilized rows 2-4 spread 0.39%; every direct default row was
  faster than same-run borrowed, so this is a variance note rather than a
  slowdown blocker
next:
  write the direct API migration decision trace and decide how to carry the
  KTLX 2026-05-05 allocation warning into the final posture
```

Milestone 014 gate posture after slice 8:

```text
direct API default rows were captured through direct MeasureCache() calls
same-run explicit blocking-borrowed oracle rows were captured
KTLX 2026-05-05 allocation warning repeated and remains visible
primary favorable timing outlier should be named but is not a slowdown blocker
slice 9 recorded the direct API migration decision without recapturing the gate
```

Milestone 014 slice 9 direct API migration decision trace:

```text
runtime behavior changes:
  none
decision trace:
  docs/milestones/014-direct-archive-rebalance-api-default-migration-decision-trace.md
standard format:
  top-level decision, decision matrix, decision explanations with Decision,
  Why chosen, Alternatives, Rejected because, Trade-offs/debt, and Review
  explanation fields, evidence, threshold decisions, allocation decision,
  operational posture, residual risks, and final decision
decision:
  direct RadarProcessingArchiveRebalanceBenchmark.MeasureFile() and
  MeasureCache() omitted defaults are accepted as the queued-owned rollout
  contour
file/cache symmetry:
  accepted; both direct surfaces migrated to the same shared rollout contour
fallback/oracle posture:
  accepted; explicit BlockingBorrowed remains selectable, tested, documented,
  and required for same-run comparison gates
CLI/direct alignment:
  accepted; CLI omitted-provider rollout constants and direct omitted defaults
  share the same accepted contour
KTLX 2026-05-05 allocation:
  accepted as warning, not clean green; direct gate average was 1.0997x
  borrowed with one row at 1.1018x and one row at 1.0976x
runtime expansion:
  not approved; live ingestion/runtime defaults, durable queues,
  cross-process workers, ordered concurrent rebalance, and builder-transfer
  remain out of scope
recommended next milestone input:
  targeted allocation reduction or allocation-readiness for the queued-owned
  direct/default contour before any live/runtime default expansion; if broader
  benchmark expansion is chosen first, it must keep same-run BlockingBorrowed
  oracle rows and the KTLX 2026-05-05 warning visible
next:
  slice 10 wrote closeout and finalized handoff with accepted direct API
  posture, explicit fallback/oracle posture, allocation warning, and next
  milestone recommendation
```

Milestone 014 decision posture after slice 9:

```text
direct API default migration is accepted
explicit BlockingBorrowed remains fallback/oracle
KTLX 2026-05-05 allocation warning remains tracked debt
live/runtime defaults remain out of scope
slice 10 closed the milestone and finalized handoff
```

Milestone 014 slice 10 closeout and handoff:

```text
runtime behavior changes:
  none
closeout:
  docs/milestones/014-direct-archive-rebalance-api-default-migration-closeout.md
handoff:
  docs/handoff.md
final status:
  milestone 014 complete
final direct API posture:
  direct RadarProcessingArchiveRebalanceBenchmark.MeasureFile() and
  MeasureCache() omitted defaults migrate symmetrically to the accepted
  queued-owned rollout contour
explicit fallback/oracle posture:
  providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed remains
  selectable and remains the same-run comparison oracle
KTLX 2026-05-05 allocation posture:
  accepted as tracked allocation warning, not clean green; direct gate average
  was 1.0997x borrowed with one row above and one row below the 1.10x
  threshold
recommended next milestone input:
  targeted allocation reduction or allocation-readiness for the queued-owned
  direct/default contour before any live/runtime default expansion; if broader
  benchmark expansion is chosen first, keep same-run BlockingBorrowed oracle
  rows and the KTLX 2026-05-05 warning visible
```

Milestone 014 final closeout verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
84 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore
succeeded, 0 warnings, 0 errors

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
761 passed, 0 failed, 3 skipped
```

Milestone 014 final closeout answer:

```text
yes:
  direct file/cache defaults migrate symmetrically, fallback/oracle posture is
  preserved, KTLX allocation warning is accepted as tracked debt, and the next
  milestone input is targeted allocation reduction or allocation-readiness
  before live/runtime expansion
```

## Milestone 013 Complete

Milestone 013 is complete. The milestone documents are:

```text
docs/milestones/013-post-rollout-hardening-broader-validation.md
docs/milestones/013-post-rollout-hardening-broader-validation-plan.md
docs/milestones/013-post-rollout-hardening-broader-validation-performance-gate.md
docs/milestones/013-post-rollout-hardening-broader-validation-decision-trace.md
docs/milestones/013-post-rollout-hardening-broader-validation-closeout.md
```

Milestone 013 is the post-rollout hardening and broader validation milestone
for the scoped queued-owned default accepted in milestone 012. It did not
broaden into direct API defaults, live ingestion, durable queues, cross-process
workers, builder-transfer, or ordered concurrent rebalance.

Milestone 013 final result:

```text
the milestone 012 scoped queued-owned default remains stable enough to keep as
the processing benchmark rebalance-archive omitted-provider default and to use
as the baseline for the next expansion decision
```

Recommended next milestone input:

```text
direct archive rebalance API default migration for MeasureFile()/MeasureCache()
with same-run blocking-borrowed oracle coverage and repeated KTLX 2026-05-05
allocation tracking
```

Current milestone 013 scoped subject:

```text
surface: processing benchmark rebalance-archive CLI omitted-provider path
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

Milestone 013 preserves these guardrails:

```text
blocking-borrowed remains explicit fallback through
  --provider blocking-borrowed
same-run blocking-borrowed remains the benchmark oracle
direct RadarProcessingArchiveRebalanceBenchmark.MeasureFile()/MeasureCache()
  defaults remain blocking-borrowed unless a later milestone migrates them
queued-owned failures fail closed
no automatic borrowed fallback follows queued-owned failure
controlled consumer delay remains mechanics-only proof
builder-transfer remains unsupported
```

Milestone 013 closeout answer:

```text
yes for keeping the existing scoped CLI default
yes for using direct archive rebalance API default migration as the next
  milestone input
no for live/runtime default migration in milestone 013
KTLX 2026-05-05 allocation warning at 1.1005x borrowed average is accepted as
  direct API migration cost and remains a tracked risk
```

Decision trace base format:

```text
status:
  007-011 decision trace documents have been checked against the base format.
  008 Performance Guardrail Interpretation was normalized with the missing
  Why chosen, Alternatives, and Rejected because fields.
  012 and 013 decision trace documents now include 001-006 style decision
  explanations in addition to gate evidence.

canonical sections:
  # Milestone 0XX Decision Trace
  Date, status, or closeout pointer
  Top-level Decision when the milestone is a gate or rollout decision
  ## 1. What Was Implemented or ## Included Surface
  ## 2. Decision Matrix
  ## Decision Explanations
  ## Evidence and ## Threshold Decisions when a gate exists
  ## Operational Posture when operator behavior changes or must be preserved
  ## Residual Risks And Limits or ## Remaining Risks And Debt
  ## Decision or ## Portfolio Review Summary

per-decision explanation fields:
  Decision
  Why chosen
  Alternatives
  Rejected because
  Trade-offs/debt
  Review explanation

rule:
  gate-style milestones may keep evidence-first sections, but every durable
  decision should also include the 001-006 explanation fields so reviewers can
  see why the path was chosen, what was rejected, and what debt remains.
```

Milestone 013 implementation plan status:

```text
status: complete
slice 1: post-rollout surface audit complete
slice 2: default contour drift guardrails complete
slice 3: direct API compatibility guardrails complete
slice 4: operator help and output compatibility cleanup complete
slice 5: allocation attribution pass complete
slice 6: failure, cleanup, and fallback regression pass complete
slice 7: focused regression pass before gate complete
slice 8: broader natural Release gate captured with allocation warning
slice 9: stability decision trace complete
slice 10: closeout and handoff complete
final closeout verification:
  79 passed, 0 failed, 0 skipped for focused closeout verification
  Release build succeeded with 0 warnings and 0 errors
  756 passed, 0 failed, 3 skipped for the full test project
```

Planned milestone 013 slices:

```text
1. post-rollout surface audit
2. default contour drift guardrails
3. direct API compatibility guardrails
4. operator help and output compatibility cleanup
5. allocation attribution pass
6. failure, cleanup, and fallback regression pass
7. focused regression pass before gate
8. broader natural Release gate
9. stability decision trace
10. closeout and handoff
```

Milestone 013 slice 1 baseline capture:

```text
runtime changes: none
current CLI omitted-provider path still expands to the milestone 012 rollout
  contour
direct MeasureFile()/MeasureCache() defaults remain blocking-borrowed
existing allocation attribution fields cover measured, processing callback,
  replay/build, owned snapshot, retained payload, overlap retention, and
  overlap unattributed allocation
CLI help still names provider flags as optional without explaining the scoped
  rollout default and explicit fallback semantics
direct MeasureFile() default compatibility should get a more explicit guard in
  slice 3
```

Milestone 013 slice 1 local gate data availability:

```text
data\nexrad\level2\2026\05\04\KINX: 462 files
data\nexrad\level2\2026\05\04\KTLX: 244 files
data\nexrad\level2\2026\05\05\KTLX: 848 files
data\nexrad total files: 1554
```

Latest milestone 013 verification:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KINX --max-files 220 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KINX --max-files 220 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-05 --radar KTLX --max-files 220 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-05 --radar KTLX --max-files 220 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

Recorded result:

```text
Release build: succeeded, 0 warnings, 0 errors
primary KTLX 2026-05-04 matrix: elapsed 0.911x borrowed, allocation 1.071x
KINX 2026-05-04 row: elapsed 0.939x borrowed, allocation 1.070x
KTLX 2026-05-05 rows: elapsed 0.943x borrowed average, allocation 1.1005x
  average with one row above and one row below threshold
mixed-cache row: elapsed 0.907x borrowed, allocation 1.062x
all captured rollout-default rows: validation succeeded, release failures 0,
  current retained pressure 0, pressure under budget
```

Milestone 013 slice 2 default contour drift guard:

```text
runtime changes: none
rollout parse-level tests now assert the full effective contour as one
  regression contract:
  queued-owned, producer-consumer, pooled-copy, async, workers 4, queue
  capacity 8, retained-byte budget 536870912, queue telemetry summary,
  overlap telemetry summary, consumer delay 0, natural-readiness evidence
omitted-provider rows are rollout-default expansion
explicit queued-owned rows with the same effective shape are not
  rollout-default expansion
explicit borrowed fallback now has a full borrowed compatibility contour
  assertion
existing fail-closed coverage remains active for explicit borrowed mixed with
  queued-owned-only controls, builder-transfer, and invalid controlled delay
```

Milestone 013 slice 3 direct API compatibility guard:

```text
runtime changes: none
direct MeasureFile() without provider arguments remains blocking-borrowed,
  partitioned, and without queue/retention/overlap/worker telemetry
direct MeasureCache() without provider arguments remains blocking-borrowed,
  partitioned, and without queue/retention/overlap/worker telemetry
direct explicit queued-owned rollout contour remains available for both file
  and cache measurement
direct explicit rollout contour uses async execution, workers 4, queue
  capacity 8, producer-consumer overlap, pooled-copy retention, retained-byte
  budget 536870912, and zero consumer delay
direct file borrowed/default same-run parity is pinned for stable totals,
  rebalance counters, validation status, validation checksum, and skipped
  reason counters
direct cache borrowed/default same-run parity remains pinned for published
  files, batches, events, payload values, and validation checksum
```

Milestone 013 slice 4 operator help/output cleanup:

```text
benchmark behavior changes: none
usage now states that omitted-provider rebalance-archive selects the scoped
  queued-owned + pooled-copy + producer-consumer default with async workers 4,
  queue capacity 8, and retained-byte budget 536870912
usage now names --provider blocking-borrowed as the fallback/oracle path
usage now states that the CLI default is scoped and direct
  MeasureFile()/MeasureCache() defaults remain blocking-borrowed
usage now states that --overlap-consumer-delay-ms is controlled mechanics
  proof, not natural rollout evidence
existing per-run output source labels remain the source of truth for whether a
  run used rollout-default, explicit fallback, explicit queued-owned, or
  controlled proof
```

Milestone 013 slice 5 allocation attribution pass:

```text
runtime behavior changes: none
RadarProcessingRebalanceAllocationSummary now exposes
  ProcessingCallbackNonOwnedSnapshotAllocatedBytes
file and cache archive rebalance result contracts expose the new non-owned
  callback allocation values
archive rebalance file/cache CLI output now prints an explicit
  Allocation attribution: summary block
the attribution block includes measured, processing callback, replay/build,
  owned snapshot, non-owned callback, archive replay inclusion, CLI formatting,
  and per-payload owned/non-owned callback allocation rows
borrowed fallback rows report owned snapshot allocation as 0 and still do not
  print retained payload or overlap telemetry
queued-owned rows continue to print retained payload allocation and
  producer-consumer rows continue to print overlap retention, measured, and
  unattributed allocation
```

Milestone 013 slice 6 failure/cleanup/fallback regression pass:

```text
runtime behavior changes: none
retention failure still stops intake, leaves accepted retained resources for
  terminal cleanup, and now asserts readiness rejects retained-resource
  retention failure
producer failure, cancellation, and validation failure overlap tests now also
  assert current retained payload bytes return to zero
validation failure overlap remains fail-closed: consumer session stays faulted,
  queued-owned processing reports the validation error, and no borrowed success
  contour is reported
readiness tests now pin generic queued-provider validation failure mapping and
  retention-failure release-health rejection
CLI fallback tests continue to prove blocking-borrowed is selected only through
  explicit --provider blocking-borrowed
```

Milestone 013 slice 7 focused regression pass before gate:

```text
runtime behavior changes: none
focused CLI default/fallback/output tests passed
direct MeasureFile()/MeasureCache() compatibility tests passed
readiness, overlap, and allocation guardrail tests passed
failure and cleanup guardrail tests passed
Release build succeeded with 0 warnings and 0 errors
```

Milestone 013 slice 8 broader natural Release gate:

```text
runtime behavior changes: none
document:
  docs/milestones/013-post-rollout-hardening-broader-validation-performance-gate.md
gate status: captured with allocation warning
primary KTLX 2026-05-04 matrix passed with candidate elapsed 0.911x borrowed,
  candidate allocation 1.071x borrowed, and candidate spread 5.41%
KINX 2026-05-04 passed with elapsed 0.939x and allocation 1.070x borrowed
mixed-cache passed with elapsed 0.907x and allocation 1.062x borrowed
KTLX 2026-05-05 correctness, cleanup, pressure, and elapsed timing passed, but
  allocation sat on the threshold: run 1 was 1.101x, run 2 was 1.0996x, and
  the two-run average was 1.1005x borrowed
retained pressure stayed below 10.14% of the 536870912 byte budget across
  captured rollout-default rows
decision trace must decide whether the KTLX 2026-05-05 allocation signal blocks
  the next expansion, requires follow-up, or remains acceptable for the scoped
  CLI default
```

Milestone 013 slice 9 stability decision trace:

```text
runtime behavior changes: none
document:
  docs/milestones/013-post-rollout-hardening-broader-validation-decision-trace.md
decision: yes for the existing scoped CLI default; it remains in place and no
  rollback is recommended
decision: KTLX 2026-05-05 allocation warning is accepted as direct API
  migration cost
decision: direct archive rebalance API default migration is approved as the
  next milestone input; direct MeasureFile()/MeasureCache() defaults remain
  blocking-borrowed until that migration milestone changes them
fallback/oracle posture remains accepted through explicit
  --provider blocking-borrowed
allocation warning remains a tracked cost/risk for the next migration:
  KTLX 2026-05-05 averaged 1.1005x borrowed allocation with one row over the
  1.10x threshold
recommended next milestone input: direct archive rebalance API default
  migration for MeasureFile()/MeasureCache() with same-run borrowed oracle and
  repeated KTLX 2026-05-05 allocation tracking
```

Milestone 013 slice 10 closeout and handoff:

```text
runtime behavior changes: none
document:
  docs/milestones/013-post-rollout-hardening-broader-validation-closeout.md
decision: milestone 013 is complete
closeout answer: yes, the milestone 012 scoped queued-owned default is stable
  enough to remain in place and serve as the baseline for the next expansion
next milestone input: direct archive rebalance API default migration for
  MeasureFile()/MeasureCache()
tracked risk: KTLX 2026-05-05 allocation warning at 1.1005x borrowed average
  remains visible in the next migration gate
final verification:
  focused closeout verification: 79 passed, 0 failed, 0 skipped
  Release build: succeeded, 0 warnings, 0 errors
  full test project: 756 passed, 0 failed, 3 skipped
```

Milestone 013 closeout question:

```text
Is the milestone 012 scoped queued-owned default stable enough to be the
baseline for the next expansion decision?

Answer: yes.
```

Milestone 012 remains complete. The milestone documents are:

```text
docs/milestones/012-queued-owned-default-rollout.md
docs/milestones/012-queued-owned-default-rollout-plan.md
docs/milestones/012-queued-owned-default-rollout-performance-gate.md
docs/milestones/012-queued-owned-default-rollout-decision-trace.md
docs/milestones/012-queued-owned-default-rollout-closeout.md
```

Milestone 012 is the explicit queued-owned default rollout decision. It
answers **yes** for the scoped CLI surface:

```text
processing benchmark rebalance-archive omitted-provider path:
  queued-owned + pooled-copy + producer-consumer is now the scoped default
```

Current scoped default posture:

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

Fallback/oracle posture:

```text
blocking-borrowed remains explicit fallback through
  --provider blocking-borrowed
same-run blocking-borrowed comparison remains required for future benchmark
  gates and rollout regressions
```

Excluded default surfaces:

```text
direct RadarProcessingArchiveRebalanceBenchmark.MeasureFile()/MeasureCache()
  defaults
synthetic benchmark defaults
non-benchmark archive publishing APIs
live ingestion/runtime provider defaults
builder-transfer retained payload execution
automatic fallback from queued-owned to blocking-borrowed
```

Milestone 012 slice 1 baseline default surface audit is implemented in the
current working tree. There were no runtime changes. The plan now captures the
current provider-related defaults in
`ProcessingBenchmarkArchiveRebalanceOptions.Parse()`,
`ProcessingBenchmarkArchiveRebalanceOptions` constructor defaults,
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()`,
`RadarProcessingArchiveRebalanceBenchmark.MeasureCache()`, CLI help text, and
tests before any runtime default changes.

Current default posture before rollout implementation:

```text
provider mode: blocking-borrowed
provider overlap: none
retention strategy: snapshot-copy
provider queue capacity: 1
retained-byte budget: none
overlap consumer delay: 0
queue telemetry: summary
overlap telemetry: summary
execution: partitioned barrier unless --execution async is supplied
```

Milestone 012 slice 2 rollout threshold contracts are implemented in the
current working tree. There were no provider default changes.

New contract:

```text
RadarProcessingQueuedProviderRolloutThresholds
```

Milestone 012 rollout threshold defaults are now fixed in code before default
behavior changes:

```text
release failures: 0
current retained batch count at completion: 0
current retained payload bytes at completion: 0
combined retained payload byte budget: 536870912
candidate-to-borrowed allocation ratio: <= 1.10
candidate-to-borrowed elapsed ratio: <= 1.00
candidate run spread / average elapsed ratio: <= 0.075
```

`RadarProcessingQueuedProviderReadinessEvaluator` now includes:

```text
EvaluateRetainedResourceCleanupCompletion()
EvaluateRunSpread()
```

Cleanup completion now explicitly fails readiness when current pending,
active, or combined retained pressure remains non-zero at completion. Run
spread now evaluates repeated natural candidate spread against the milestone
012 rollout threshold. Existing readiness methods still cover release failures,
retained pressure budget, allocation movement, performance delta, and natural
evidence separation.

Milestone 012 slice 3 default contour constants and option provenance are
implemented in the current working tree. There were no provider default
changes in slice 3.

New presentation contracts:

```text
ProcessingBenchmarkOptionValueSource
ProcessingBenchmarkArchiveRebalanceOptionProvenance
```

`ProcessingBenchmarkArchiveRebalanceOptions` now exposes:

```text
DefaultRolloutWorkerCount = 4
DefaultRolloutProviderQueueCapacity = DefaultCandidateProviderQueueCapacity
DefaultRolloutRetainedPayloadBytes = DefaultCandidateRetainedPayloadBytes
EffectiveOptionProvenance
IsExplicitBlockingBorrowedFallback
IsRolloutDefaultExpandedContour
```

Slice 3 preserved the then-current parsing behavior:

```text
omitted --provider still resolves to blocking-borrowed
omitted --provider-overlap still resolves to none
omitted --retention-strategy still resolves to snapshot-copy
omitted --queue-capacity still resolves to provider queue capacity 1
omitted --queue-retained-bytes still resolves to none
omitted --execution still resolves to partitioned barrier
```

Provenance now marks omitted provider-related values as `CurrentDefault`,
explicitly supplied values as `Explicit`, and keeps the `RolloutDefault` source
available for later rollout expansion. Explicit `--provider blocking-borrowed`
is now visible as an explicit fallback through
`IsExplicitBlockingBorrowedFallback`.

Milestone 012 slice 4 CLI default expansion and validation rules are
implemented in the current working tree. This is the first milestone 012 slice
that changes provider default behavior for the scoped
`processing benchmark rebalance-archive` CLI parse surface.

Omitted provider flags now expand to:

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
```

Omitted rollout-expanded fields are marked `RolloutDefault` in
`EffectiveOptionProvenance`, and `IsRolloutDefaultExpandedContour` is true for
the omitted-provider rollout contour. Explicit `--provider blocking-borrowed`
continues to select the fallback path and sets
`IsExplicitBlockingBorrowedFallback` true.

Validation remains fail-closed:

```text
queued-owned-only controls without --provider are now valid because omitted
  provider means rollout-default queued-owned
the same controls remain rejected with explicit --provider blocking-borrowed
builder-transfer remains rejected
controlled consumer delay remains rejected outside queued-owned
  producer-consumer contours
```

Milestone 012 slice 5 operator output for default versus explicit selection is
implemented in the current working tree. There were no benchmark invocation
behavior changes.

`processing benchmark rebalance-archive` file and cache output now prints these
source fields:

```text
Provider mode source
Provider overlap source
Retention strategy source
Provider queue capacity source
Worker queue capacity source
Provider queue retained byte capacity source
Queue telemetry source
Provider overlap telemetry source
Provider overlap consumer delay source
Execution mode source
Worker count source
```

Source values are:

```text
rollout-default: selected by omitted-provider milestone 012 expansion
explicit: selected by an explicit CLI option
current-default: inherited from the non-rollout default under an explicit
  provider contour
not-applicable: queued-owned or async-only source is not active for this run
```

Output now also prints:

```text
Provider default rollout contour: yes|no
Provider rollout default expansion: yes|no
Provider fallback contour: yes|no
```

`Default-candidate contour`, `Provider overlap evidence contour`, and
`Provider overlap evidence scope` remain stable. CLI coverage now proves that
omitted provider defaults are visibly rollout-default, explicit queued-owned
rollout-shape runs are not treated as omitted-default expansion, explicit
`--provider blocking-borrowed` is visibly fallback, natural opt-in diagnostic
runs remain `opt-in-diagnostic`, and controlled proof rows remain
`controlled-mechanics-proof`.

Milestone 012 slice 6 benchmark invocation defaults and same-run oracle
preservation is implemented in the current working tree. There were no runtime
changes.

CLI smoke coverage now proves omitted-provider `rebalance-archive` cache output
reaches the queued-owned rollout invocation shape:

```text
queued-owned batch lifetime
queue telemetry: summary
retained payload telemetry: summary
overlap telemetry: summary
pooled-copy overlap retention strategy
async execution
rollout-default provenance
default rollout contour: yes
```

Explicit `--provider blocking-borrowed` cache output now proves the fallback
path remains borrowed:

```text
blocking-borrowed provider result
borrowed batch lifetime
provider queue capacity 0 in the benchmark result
no queue telemetry
no retained payload telemetry
no overlap telemetry
no worker telemetry
partitioned execution
Provider fallback contour: yes
```

Direct `RadarProcessingArchiveRebalanceBenchmark.MeasureCache()` coverage now
pins the same-run oracle posture:

```text
MeasureCache() without provider arguments remains blocking-borrowed
direct infrastructure defaults remain partitioned and borrowed
explicit queued-owned + producer-consumer + pooled-copy + async + workers 4
  + queue capacity 8 + retained bytes 536870912 returns queued-owned result
  state, worker telemetry, queue telemetry, retained telemetry, overlap
  telemetry, zero release failures, and the same stable totals/checksum as the
  same-run borrowed oracle
```

Milestone 012 slice 7 failure, cleanup, and fallback guardrails under default
queued-owned is implemented in the current working tree. There were no runtime
changes.

New active release failure coverage:

```text
ArchiveOwnedRadarEventBatchQueueingPublisher consumer-side retained payload
release failure increments release-failure telemetry, clears active and
combined current pressure, and fails
RadarProcessingQueuedProviderReadinessEvaluator release-health evaluation.
```

New overlap validation failure coverage:

```text
RadarProcessingArchiveQueuedOverlapRunner invalid queued-owned
producer-consumer rebalance input faults the consumer result, reports
FailedValidation, keeps the producer completed, releases the active retained
resource, leaves current pending/active/combined retained pressure at zero,
and does not convert the failure into borrowed success.
```

Existing guardrails remain covered:

```text
retention failure stops the current publish and leaves only already accepted
  resources for terminal cleanup
producer failure releases accepted pending retained resources
cancellation after accepted enqueue releases pending resources and leaves
  current retained pressure at zero
queued processing validation failure releases active retained resources
explicit borrowed fallback remains selected only through explicit
  --provider blocking-borrowed
```

Milestone 012 slice 8 focused regression pass before gate is implemented in
the current working tree. There were no runtime changes.

Slice 8 verification passed:

```text
focused CLI default/output/fallback tests
focused readiness and overlap tests
focused failure and cleanup tests
Release build with 0 warnings and 0 errors
```

Milestone 012 slice 9 natural rollout performance gate is implemented in the
current working tree. There were no runtime changes.

New gate document:

```text
docs/milestones/012-queued-owned-default-rollout-performance-gate.md
```

Release build before capture succeeded with 0 warnings and 0 errors.

Captured contours:

```text
primary KTLX:
  --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
  three same-run borrowed/default pairs

mixed cache:
  --cache data\nexrad --max-files 1000000
  one same-run borrowed/default pair across all local radar/date shapes
```

Primary matrix result:

```text
borrowed average elapsed ms: 17865.16
default queued-owned average elapsed ms: 15274.16
default queued-owned elapsed ratio: 0.855x borrowed
default queued-owned allocation ratio: 1.072x borrowed
default queued-owned run spread: 2.39% of average
```

Mixed-cache result:

```text
borrowed elapsed ms: 77542.34
default queued-owned elapsed ms: 60229.87
default queued-owned elapsed ratio: 0.777x borrowed
default queued-owned allocation ratio: 1.064x borrowed
```

Gate interpretation:

```text
validation parity: pass
release failures: pass, 0 failed releases
cleanup at completion: pass, current retained pressure returns to 0
retained pressure budget: pass, max observed combined retained payload
  high-water mark is 54413280 bytes of the 536870912 byte budget
allocation ratio: pass, <= 1.10x borrowed
elapsed ratio: pass, <= 1.00x borrowed
candidate spread: pass, <= 7.50% of candidate average
default expansion evidence: pass
fallback separation: pass
```

Milestone 012 slice 10 rollout decision trace is implemented in the current
working tree. There were no runtime changes.

New decision trace:

```text
docs/milestones/012-queued-owned-default-rollout-decision-trace.md
```

Decision:

```text
queued-owned + pooled-copy + producer-consumer is accepted as the scoped
default for processing benchmark rebalance-archive omitted provider flags
```

Included surface:

```text
processing benchmark rebalance-archive CLI omitted provider path
```

Excluded surfaces:

```text
direct RadarProcessingArchiveRebalanceBenchmark.MeasureFile()/MeasureCache()
  defaults
synthetic benchmark defaults
non-benchmark archive publishing APIs
live ingestion/runtime provider defaults
builder-transfer retained payload execution
automatic fallback from queued-owned to blocking-borrowed
```

Fallback/oracle posture:

```text
blocking-borrowed remains explicit fallback through
  --provider blocking-borrowed
same-run blocking-borrowed comparison remains required for future benchmark
  gates and rollout regressions
```

Threshold evidence recorded:

```text
validation parity: accepted
release health: accepted, failed releases 0
retained cleanup: accepted, current retained pressure returns to 0
retained pressure budget: accepted, max combined retained payload high
  watermark 54413280 bytes of 536870912 byte budget
allocation threshold: accepted, primary 1.072x borrowed and mixed-cache
  1.064x borrowed
elapsed threshold: accepted, primary 0.855x borrowed and mixed-cache
  0.777x borrowed
run spread threshold: accepted, primary candidate spread 2.39%
default expansion evidence: accepted
fallback separation: accepted
```

Milestone 012 slice 11 closeout and handoff is implemented in the current
working tree. There were no runtime changes.

New closeout:

```text
docs/milestones/012-queued-owned-default-rollout-closeout.md
```

Final closeout verification:

```text
47 passed, 0 failed, 0 skipped for focused closeout verification.
Release build succeeded with 0 warnings and 0 errors.
751 passed, 0 failed, 3 skipped for the full test project.
```

The next implementation step is not selected yet. Good next milestone inputs
are broader default rollout, direct API default migration, live/durable
ingestion, or ordered concurrent rebalance execution.

Latest verification after milestone 012 slice 1:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
38 passed, 0 failed, 0 skipped.
```

Latest verification after milestone 012 slice 2:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
14 passed, 0 failed, 0 skipped.
41 passed, 0 failed, 0 skipped.
```

Latest verification after milestone 012 slice 3:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
23 passed, 0 failed, 0 skipped.
44 passed, 0 failed, 0 skipped.
```

Latest verification after milestone 012 slice 4:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
24 passed, 0 failed, 0 skipped.
45 passed, 0 failed, 0 skipped.
```

Latest verification after milestone 012 slice 5:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
25 passed, 0 failed, 0 skipped.
46 passed, 0 failed, 0 skipped.
```

Latest verification after milestone 012 slice 6:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
46 passed, 0 failed, 0 skipped.
67 passed, 0 failed, 0 skipped.
```

Latest verification after milestone 012 slice 7:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"
```

Recorded result:

```text
71 passed, 0 failed, 0 skipped.
78 passed, 0 failed, 0 skipped.
```

Latest verification after milestone 012 slice 8:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Recorded result:

```text
25 passed, 0 failed, 0 skipped.
22 passed, 0 failed, 0 skipped.
24 passed, 0 failed, 0 skipped.
Release build succeeded with 0 warnings and 0 errors.
```

Latest verification after milestone 012 slice 9:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

Recorded result:

```text
Release build succeeded with 0 warnings and 0 errors.
Primary KTLX matrix: three borrowed/default same-run pairs captured.
Mixed-cache row: one borrowed/default same-run pair captured.
Natural rollout performance gate passes the measured local contours.
```

Milestone 011 remains complete. The closed documents are:

```text
docs/milestones/011-queued-owned-default-readiness.md
docs/milestones/011-queued-owned-default-readiness-plan.md
docs/milestones/011-queued-owned-default-readiness-performance-gate.md
docs/milestones/011-queued-owned-default-readiness-decision-trace.md
docs/milestones/011-queued-owned-default-readiness-closeout.md
```

Milestone 011 concludes that
`queued-owned + pooled-copy + producer-consumer` is credible enough to propose
for an explicit default-rollout milestone under the measured limits.
`blocking-borrowed` remains the current default provider and same-run oracle.
The provider default was not changed in milestone 011.

Milestone 011 slice 1 baseline readiness audit is complete. There were no
runtime changes. The current pending retained-byte field is
`RadarProcessingProviderQueueTelemetrySummary.QueuedPayloadBytesHighWatermark`;
the current compatibility alias
`RadarProcessingProviderQueueTelemetrySummary.RetainedPayloadBytesHighWatermark`
maps directly to that pending queue high-water field. Overlap telemetry exposes
the same queue-only retained-byte high-water through
`RadarProcessingArchiveOverlapTelemetrySummary.RetainedPayloadBytesHighWatermark`,
and `HasQueuedAheadOverlap` remains derived from `QueueDepthHighWatermark > 1`.
The active consumer retained-resource gap is now explicitly frozen: there is no
active retained-byte high-water field and no combined pending-plus-active
retained-byte high-water field yet.

The milestone 011 default-candidate contour is frozen for later implementation
and gates:

```text
provider mode: queued-owned
retention strategy: pooled-copy
provider overlap: producer-consumer
execution: async
queue capacity: 8
retained-byte budget: 536870912
queue telemetry: summary or recent as required by the gate
overlap telemetry: summary or recent as required by the gate
overlap consumer delay: disabled for readiness gates
```

Latest verification after milestone 011 slice 1:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
```

Recorded result:

```text
41 passed, 0 failed, 0 skipped.
```

Milestone 011 slice 2 retained-resource pressure contracts are implemented in
the current working tree. This slice adds domain contracts only; queue,
overlap, benchmark, and CLI runtime integration remains for later slices.
New domain contracts:

```text
RadarProcessingRetainedResourcePressureSnapshot
  -> immutable current pending/active batch and payload byte snapshot
  -> exposes computed combined pending-plus-active counts and bytes

RadarProcessingRetainedResourcePressureSummary
  -> immutable current pending, active, and combined pressure summary
  -> carries pending, active, and combined batch/byte high-water marks
  -> rejects negative values and impossible high-water shapes

RadarProcessingRetainedResourcePressureRecorder
  -> thread-safe AddPending, RemovePending, MovePendingToActive, RemoveActive,
     CreateSnapshot, and CreateSummary operations
  -> rejects negative payload bytes, non-positive operation batch counts, and
     underflowing pending or active removals
  -> updates combined high-water marks whenever pending or active state changes
```

Focused tests cover empty/default summaries, negative constructor validation,
pending-to-active movement, active release, combined high-water accounting,
invalid underflow transitions, and concurrent pending updates through the
recorder.

Latest verification after milestone 011 slice 2:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingRetainedResourcePressureContractTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRetainedResourcePressureContractTests|FullyQualifiedName~RadarProcessingRetainedPayloadContractTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
6 passed, 0 failed, 0 skipped for retained-resource pressure contracts.
11 passed, 0 failed, 0 skipped for retained-resource pressure plus retained
payload contracts.
710 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 011 slice 3 provider queue telemetry compatibility extensions are
implemented in the current working tree. `RadarProcessingProviderQueueTelemetrySummary`
now carries `RadarProcessingRetainedResourcePressureSummary` through
`RetainedResourcePressure` and exposes explicit pending, active, and combined
retained pressure fields:

```text
CurrentPendingRetainedBatchCount
CurrentPendingRetainedPayloadBytes
PendingRetainedBatchCountHighWatermark
PendingRetainedPayloadBytesHighWatermark
CurrentActiveRetainedBatchCount
CurrentActiveRetainedPayloadBytes
ActiveRetainedBatchCountHighWatermark
ActiveRetainedPayloadBytesHighWatermark
CurrentCombinedRetainedBatchCount
CurrentCombinedRetainedPayloadBytes
CombinedRetainedBatchCountHighWatermark
CombinedRetainedPayloadBytesHighWatermark
```

Compatibility is preserved: `QueuedPayloadBytesHighWatermark` remains the
queue-only pending byte high-water field, and
`RetainedPayloadBytesHighWatermark` remains the milestone 010 alias to
`QueuedPayloadBytesHighWatermark`. When callers do not supply explicit pressure
summary data, queue-only pressure is derived from `QueueDepthHighWatermark` and
`QueuedPayloadBytesHighWatermark`. `RadarProcessingOwnedBatchQueue.CreateTelemetrySummary()`
now supplies current pending count/bytes and queue-only pending/combined
high-water marks. Active retained pressure remains zero until slice 4 wires the
consumer resource lifecycle.

Latest verification after milestone 011 slice 3:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingProviderQueueContractTests|FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingRetainedResourcePressureContractTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
36 passed, 0 failed, 0 skipped for focused provider queue pressure coverage.
710 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 011 slice 4 active consumer retained-resource lifecycle integration
is implemented in the current working tree. `ArchiveOwnedRadarEventBatchQueueingPublisher`
now records retained-resource pressure for accepted resources, moves pressure
from pending to active when a consumer acquires a queued sequence, and releases
active resources through an idempotent consumer lease. `ReleasePendingResources()`
now removes pending pressure while recording release telemetry for resources
that never became active. Pressure bytes are tracked from the retained queued
batch payload length, so not-required release handles still report active batch
pressure.

Queued processing and rebalance sessions now accept an optional consumer
resource lease factory and wrap every dequeued batch, including
skipped-after-fault work. `RadarProcessingArchiveQueuedOverlapRunner.RunRebalanceAsync`
passes publisher leases into queued rebalance drains, and the runner overlays
the final provider pressure summary onto the final queue telemetry. The archive
rebalance benchmark queued-owned drains acquire the lease before processing and
release it after processing, cancellation, or failure.

New focused coverage validates the pending-to-active-to-released transition for
pooled-copy retained resources, session lease hooks for failed and skipped
batches, `RunRebalanceAsync` active pressure propagation, and the queue
telemetry pressure overlay helper.

Latest verification after milestone 011 slice 4:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
35 passed, 0 failed, 0 skipped for focused active retained-resource lifecycle
coverage.
713 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 011 slice 5 overlap telemetry and benchmark result propagation is
implemented in the current working tree. `RadarProcessingArchiveOverlapTelemetrySummary`
now exposes the retained-resource pressure summary and direct pending, active,
and combined current/high-water fields. The legacy
`RetainedPayloadBytesHighWatermark` property remains the milestone 010
queue-only compatibility alias.

`RadarProcessingArchiveQueuedOverlapResult`,
`RadarProcessingArchiveRebalanceBenchmarkResult`, and
`RadarProcessingArchiveRebalanceCacheBenchmarkResult` now carry direct retained
pressure accessors so callers do not need to reach into nested queue telemetry
to read active/combined readiness fields. File and cache benchmark tests now
assert that queue telemetry, overlap telemetry, and benchmark result shapes
preserve the same retained pressure summary. Allocation attribution remains
unchanged: active retained pressure is live memory pressure, not retained
allocation.

Latest verification after milestone 011 slice 5:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
53 passed, 0 failed, 0 skipped for focused overlap/benchmark retained pressure
propagation coverage.
713 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 011 slice 6 candidate configuration surface is implemented in the
current working tree. The CLI keeps the conservative explicit-flags-only
direction: no named profile was added and the blocking-borrowed/snapshot-copy
runtime defaults remain unchanged.

`ProcessingBenchmarkArchiveRebalanceOptions` now exposes
`IsDefaultCandidateContour`, `IsControlledProviderOverlapProof`, and
`ProviderOverlapEvidenceContour`. The exact milestone 011 candidate contour is
`queued-owned + producer-consumer + pooled-copy + async`, provider queue
capacity `8`, retained-byte budget `536870912`, non-`none` queue and overlap
telemetry, and no controlled consumer delay. Archive rebalance file/cache CLI
output now prints `Default-candidate contour: yes/no` and
`Provider overlap evidence contour` values of `natural-default-candidate`,
`controlled-proof`, `natural-opt-in`, or `not-applicable`.

New CLI coverage validates default/not-applicable output, controlled-proof
labeling when consumer delay is active, and exact candidate output for the
natural default-candidate contour.

Latest verification after milestone 011 slice 6:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
39 passed, 0 failed, 0 skipped for focused CLI/archive candidate surface
coverage.
715 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 011 slice 7 readiness validation and gate contracts are implemented
in the current working tree. This slice adds domain contracts and an evidence
interpreter only; it does not mutate runtime provider defaults, silently rerun
failed queued-owned work through the borrowed path, or add CLI readiness
reporting yet.

New domain contracts:

```text
RadarProcessingQueuedProviderReadinessStatus
  -> passed, failed, inconclusive, and not-evaluated outcomes

RadarProcessingQueuedProviderReadinessGate
  -> correctness parity, topology/rebalance parity, release health, retained
     pressure, allocation movement, performance delta, run variance, effective
     configuration, and natural evidence dimensions

RadarProcessingQueuedProviderReadinessError
  -> explicit failure/inconclusive reasons for missing borrowed reference,
     validation mismatch, checksum mismatch, topology/rebalance mismatch,
     release failure, cleanup incompleteness, missing pressure telemetry,
     combined retained payload budget excess, controlled-proof exclusion,
     candidate-contour mismatch, performance regression, variance, and
     allocation regression

RadarProcessingQueuedProviderReadinessResult
  -> carries gate, status, error, message, and checksum/count/byte/ratio
     diagnostics while rejecting invalid result shapes

RadarProcessingQueuedProviderReadinessEvaluator
  -> interprets queued-provider validation results, retained payload release
     telemetry, retained-resource pressure summaries, natural-vs-controlled
     evidence, same-run borrowed performance deltas, allocation movement
     ratios, and repeated-run variance
```

Focused coverage validates the stable contracts, missing borrowed-reference
inconclusive status, checksum and rebalance mismatch failures, failed retained
release failure even when correctness passes, controlled-delay exclusion from
natural readiness, combined retained payload budget failure, missing active
pressure telemetry as inconclusive, and performance regression independent of
correctness. Allocation movement handles missing reference measurements as
not-evaluated and allocation regressions as failed readiness. Run variance
requires repeated natural measurements and fails when the configured variance
threshold is exceeded.

Latest verification after milestone 011 slice 7:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingQueuedProviderValidatorTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
30 passed, 0 failed, 0 skipped for focused queued-provider readiness gate and
validator coverage.
726 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 011 slice 8 failure, cancellation, and cleanup gate coverage is
implemented in the current working tree. This slice is test-focused; it does
not change runtime provider defaults or make Release performance gates depend
on injected fault behavior.

New focused coverage:

```text
ArchiveOwnedRadarEventBatchQueueingPublisher
  -> release callback failure is recorded in release telemetry, current
     pending/active/combined pressure returns to zero, and readiness release
     health fails
  -> retained copy failure after an earlier accepted retained batch stops the
     current publish and leaves the earlier accepted resource visible for
     terminal cleanup

RadarProcessingQueuedProcessingSession
  -> consumer validation failure from a throwing handler releases the active
     retained resource and returns current retained pressure to zero

RadarProcessingArchiveQueuedOverlapRunner
  -> producer failure after an accepted enqueue releases pending retained
     resources, faults the overlap result, and leaves current retained pressure
     at zero
  -> cancellation after an accepted enqueue releases pending retained resources,
     returns a canceled overlap result, and leaves current retained pressure at
     zero

RadarProcessingRetainedBatchResource
  -> release callback exceptions become terminal cleanup failures and do not
     invoke the callback again
```

Latest verification after milestone 011 slice 8:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
49 passed, 0 failed, 0 skipped for focused failure/cancellation cleanup gate
coverage.
732 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 011 slice 9 CLI and operator telemetry output is implemented in the
current working tree. Provider queue telemetry now prints current pending,
active, and combined retained batch/payload pressure plus pending, active, and
combined retained batch/payload high-water marks. Provider overlap telemetry
prints the same retained pressure fields while preserving the milestone 010
queue-only retained payload high-water label.

Summary telemetry remains aggregate-only; no unbounded per-batch output was
added. CLI smoke coverage verifies the new queue and overlap pressure fields,
the controlled/default-candidate labels, and that `--queue-telemetry none`
plus `--overlap-telemetry none` suppresses optional queue/overlap pressure
blocks instead of presenting disabled telemetry as readiness evidence.

Latest verification after milestone 011 slice 9:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
20 passed, 0 failed, 0 skipped for focused CLI rebalance benchmark coverage.
733 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 011 slice 10 natural Release gate matrix is implemented in the
current working tree. The gate document is
`docs/milestones/011-queued-owned-default-readiness-performance-gate.md`.

The first natural candidate Release run exposed a retained-resource
registration race: a waiting consumer could dequeue an accepted queued batch
before `ArchiveOwnedRadarEventBatchQueueingPublisher` registered the retained
resource for that sequence. `RadarProcessingOwnedBatchQueue.EnqueueAsync` now
supports an accepted-batch callback that executes while the queue lock is still
held, before a waiting `DequeueAsync` can return to consumer code. The archive
queueing publisher uses that callback to register retained resources for
accepted publishes. Focused regression coverage verifies the callback/dequeue
ordering and that a waiting archive consumer can acquire the retained resource.

The natural Release matrix captures three repeated KTLX 2026-05-04
`--max-files 220` borrowed/candidate rows and one larger local `--max-files
1000000` row. Local cache availability was limited to one radar/date shape, so
cross-shape input diversity remains incomplete. All natural candidate rows kept
`--overlap-consumer-delay-ms` disabled and were labeled
`natural-default-candidate`.

Gate interpretation:

```text
correctness parity: passed on all captured rows
release health: passed, failed releases stayed at 0
retained pressure: passed, combined retained payload high-water was 48_257_280
  bytes, about 8.99% of the 536_870_912 byte budget
performance delta: favorable on measured local contours; repeated primary
  candidate average was 15_635.33 ms versus 17_281.31 ms borrowed, -9.52%
run variance: captured on the primary contour; borrowed spread 1.95% and
  candidate spread 5.01% of average
controlled queued-ahead overlap mechanics: already proven by controlled
  consumer-delay rows
natural queue backlog: not accumulated; queue depth high watermark stayed 1
  and HasQueuedAheadOverlap stayed no because the measured pipeline keeps up
allocation movement: failed for default-readiness; candidate allocation was
  about 2.03x borrowed on the repeated primary contour
```

Expanded-cache follow-up after slice 10:

```text
downloaded 2026-05-04/KINX: 231 files, 1_404_409_198 bytes
downloaded 2026-05-05/KTLX: 424 files, 2_232_413_173 bytes
expanded cache shapes: 2026-05-04/KINX, 2026-05-04/KTLX, 2026-05-05/KTLX
mixed-cache contour: --cache data\nexrad --max-files 1000000
examined files: 1_554
published base-data files: 828
payload values: 32_306_203_200
raw value checksum: 958_518_408_830
validation checksum: 615_051_108_812_661_629
borrowed async elapsed ms: 77_530.68
queued-owned candidate elapsed ms: 72_440.28
candidate release failures: 0
candidate combined retained payload high-water: 54_413_280 bytes
candidate queue depth high watermark: 1
candidate HasQueuedAheadOverlap: no
```

The local input-diversity gap is now closed for follow-up measurement: the
cache has multiple radars and dates. The expanded mixed-cache candidate, an
archive-parallelism-96 natural stress row, and a workers-1 natural stress row
all kept queue depth at 1 with no controlled delay. This is favorable pipeline
behavior, not a failed overlap proof: the controlled consumer-delay rows already
proved queue-ahead mechanics, while the natural rows show replay, retention,
queueing, and processing staying balanced enough that retained queue backlog
does not accumulate.

Latest verification after milestone 011 slice 10:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
Release build succeeded with 0 warnings and 0 errors before gate capture.
20 passed, 0 failed, 0 skipped for CLI rebalance benchmark coverage.
18 passed, 0 failed, 0 skipped for overlap runner and readiness gate coverage.
53 passed, 0 failed, 0 skipped for focused queue, publisher, overlap, and CLI
coverage after the retained-resource registration race fix.
Release build succeeded with 0 warnings and 0 errors after the fix.
735 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 011 slice 11 retained payload allocation optimization is implemented
in the current working tree. The default `RadarProcessingRetainedPayloadFactory`
now uses `RadarProcessingRetainedPayloadByteArrayPool` for retained payload byte
buffers. Small arrays still route through `ArrayPool<byte>.Shared`; large arrays
are retained in a bounded idle pool, rent capacity is rounded upward for reuse,
and idle eviction prefers keeping larger reusable arrays within the count/byte
budget. The defaults retain up to 4 idle large arrays and 128 MiB. Custom
injected payload pools remain supported for tests and fault injection.

The slice keeps the milestone 011 candidate contour unchanged:

```text
provider: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async
workers: 4
queue capacity: 8
retained-byte budget: 536870912
queue telemetry: summary
overlap telemetry: summary
overlap consumer delay: disabled
```

Post-optimization expanded mixed-cache result:

```text
examined files: 1_554
published base-data files: 828
payload values: 32_306_203_200
raw value checksum: 958_518_408_830
validation checksum: 615_051_108_812_661_629
end-to-end elapsed ms: 71_181.17
provider queue depth high watermark: 1
provider queue combined retained payload bytes high watermark: 54_413_280
retained payload allocated bytes: 247_679_944
retained payload released batches: 828
retained payload failed releases: 0
provider overlap has producer-consumer overlap: yes
provider overlap has queued-ahead overlap: no
end-to-end allocated bytes: 4_063_709_976
processing callback allocated bytes: 3_654_244_544
replay and batch construction allocated bytes: 409_465_432
end-to-end allocated bytes / payload value: 0.13
```

Allocation movement:

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

Interpretation: the retained-payload allocation regression is no longer a major
default-readiness blocker on the expanded mixed-cache contour. The optimized
candidate still allocates about 6.6% more than borrowed, so the decision trace
should record residual overhead instead of claiming allocation parity. Correctness
parity, release health, retained pressure, and the natural overlap
interpretation are unchanged.

Latest verification after milestone 011 slice 11:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests"

dotnet build RadarPulse.sln -c Release --no-restore

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --provider queued-owned --provider-overlap producer-consumer --retention-strategy pooled-copy --execution async --workers 4 --queue-capacity 8 --queue-retained-bytes 536870912 --queue-telemetry summary --overlap-telemetry summary --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

Recorded result:

```text
22 passed, 0 failed, 0 skipped for retained payload factory and archive queue
coverage after capacity rounding.
Release build succeeded with 0 warnings and 0 errors.
740 passed, 0 failed, 3 skipped for the full test project.
Expanded mixed-cache candidate validation succeeded with 0 failed releases and
4_063_709_976 end-to-end allocated bytes.
```

Milestone 011 slice 12 controlled proof separation hardening is implemented in
the current working tree. The CLI now prints a stable provider-overlap evidence
scope next to the existing evidence contour, and both parsed options and printed
benchmark output use the same `ProcessingBenchmarkArchiveRebalanceOptions`
formatter. This prevents the operator-facing labels from drifting away from the
readiness-gate interpretation.

Evidence labels after slice 12:

```text
natural default-candidate:
  Provider overlap evidence contour: natural-default-candidate
  Provider overlap evidence scope: natural-readiness
controlled consumer-delay proof:
  Provider overlap evidence contour: controlled-proof
  Provider overlap evidence scope: controlled-mechanics-proof
natural queued-owned producer-consumer opt-in diagnostic:
  Provider overlap evidence contour: natural-opt-in
  Provider overlap evidence scope: opt-in-diagnostic
not applicable:
  Provider overlap evidence contour: not-applicable
  Provider overlap evidence scope: not-applicable
```

`--overlap-consumer-delay-ms` remains rejected unless the command uses
`--provider queued-owned --provider-overlap producer-consumer`, and controlled
delay remains excluded from natural readiness by
`RadarProcessingQueuedProviderReadinessEvaluator`.

Latest focused verification after milestone 011 slice 12:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"

dotnet build RadarPulse.sln -c Release --no-restore

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
38 passed, 0 failed, 0 skipped for focused CLI, readiness gate, and overlap
runner coverage.
Release build succeeded with 0 warnings and 0 errors.
740 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 011 slice 13 decision trace, closeout, and final handoff are
implemented in the current working tree.
`docs/milestones/011-queued-owned-default-readiness-decision-trace.md` records
the final decision matrix, and
`docs/milestones/011-queued-owned-default-readiness-closeout.md` records the
closed milestone status, verification, performance gate summary, final decision,
and carry-forward items.

Final milestone 011 decision:

```text
queued-owned + pooled-copy + producer-consumer is accepted as a future
  default-rollout candidate under the measured contour
blocking-borrowed remains the current default provider and same-run oracle
the provider default does not change in milestone 011
residual allocation overhead is a rollout caution, not a milestone blocker
natural queue depth 1 is balanced measured pipeline behavior, not a failed
  queued-ahead proof
controlled delay remains mechanics-only evidence
```

Carry forward to the next milestone:

```text
decide whether and how to switch the provider default
preserve blocking-borrowed as an operator-selectable fallback
keep same-run borrowed comparison available for benchmark gates
define rollout thresholds for allocation ratio, retained pressure, release
  failures, validation parity, and run variance
avoid bundling default rollout with builder-transfer, durable queues, live
  ingestion, or concurrent rebalance execution
```

Milestone 010 remains complete. The architecture is recorded in
`docs/milestones/010-owned-provider-overlap-cost-reduction.md`, and the
implementation plan is recorded in
`docs/milestones/010-owned-provider-overlap-cost-reduction-plan.md`. Milestone
010 slices 1 through 12 plus the repeated performance gate and controlled
queue-ahead proof are complete: the milestone 009 cost anchors are confirmed,
retained payload strategy contracts are implemented and tested, and the
resource-owned queued batch lifecycle, lower-allocation retained payload
implementation, retained-byte-aware provider queue accounting,
producer/consumer archive overlap runner, ordered rebalance topology pinning,
overlap telemetry/allocation attribution, optimized queued validation, CLI
controls, cache-level producer pipeline, and benchmark-only overlap consumer
delay are implemented and tested. The decision trace is recorded in
`docs/milestones/010-owned-provider-overlap-cost-reduction-decision-trace.md`,
and the closeout is recorded in
`docs/milestones/010-owned-provider-overlap-cost-reduction-closeout.md`.

`blocking-borrowed` remains the default provider mode and same-run oracle.
`queued-owned` remains an explicit validation and measurement mode. The repeated
gate proves lower retained allocation, deterministic borrowed-reference parity,
ordered topology publication, complete retained resource cleanup, and useful
cache-level wall-clock producer/consumer overlap. The best repeated pooled-copy
overlap contour was 14_947.99 ms versus 16_915.80 ms for borrowed async and
17_158.62 ms for non-overlapped queued-owned pooled-copy. Queue depth still
peaks at 1 and `HasQueuedAheadOverlap` remains `no` on the natural contour.
The controlled slice 12 contour with `--overlap-consumer-delay-ms 150`,
`--max-files 32`, and queue capacity 8 reached queue depth 8 and
`HasQueuedAheadOverlap = yes`, while preserving validation success and releasing
29 retained batches with 0 failed releases. The full-cache controlled contour
over `data\nexrad` with `--max-files 1000000`, the same 150 ms consumer delay,
and queue capacity 8 examined 244 files, published 220 files, reached queue
depth 8, reported `HasQueuedAheadOverlap = yes`, preserved validation success,
and released 220 retained batches with 0 failed releases. In-flight
retained-resource high-water telemetry remains the immediate milestone 011
follow-up before any default change.

Milestone 009 remains complete. RadarPulse has the first explicit
owned-payload provider decoupling substrate: archive replay can remain on the
borrowed blocking path by default, or opt into `queued-owned` provider mode
where leased `RadarEventBatch` values are retained through the selected
strategy, enqueued into a bounded provider-to-processing queue, drained in
provider sequence order, and validated against the borrowed reference.

Milestone 004 is complete. RadarPulse now has a compact, deterministic,
normalized `RadarEventBatch` stream with append-only dense identity catalogs, an
identity normalization boundary, versioned dictionary/source-universe
visibility, explicit raw payload storage lifetime, sequential and ordered
parallel replay integration, cache replay, validation, CLI smoke commands, and
benchmarks.

Milestone 005 is complete. RadarPulse now has the first static processing core
over the closed milestone 004 stream contract: processing contracts, static
partition topology, dense source-local state, processing payload readers,
sequential processing, synchronous `PartitionedBarrier` routing, partition and
shard telemetry, processing-output validation helpers, source-local handler
slots, a synthetic processing-only benchmark harness, CLI benchmark command,
decision trace, closeout, and Release processing-only benchmark numbers.

Milestone 006 architecture and implementation planning are complete. The
milestone scope is cautious, synchronous, partition-level shard rebalance over
the measured static baseline: versioned `PartitionId -> ShardId` topology,
windowed pressure detection, direct hot-partition relief, cold-partition
evacuation when the hot partition cannot move safely, anti-churn policy,
migration lifecycle, state handoff validation, and rebalance telemetry.

Milestone 006 slice 1 is implemented in the current working tree. RadarPulse now
has the first versioned topology foundation: `RadarProcessingTopologyVersion`,
an immutable public topology partition view, monotonic topology snapshots,
validated partition owner move requests/results, and a
`RadarProcessingTopologyManager` that publishes version `N+1` snapshots while
preserving the stable `SourceId -> PartitionId` mapping.

Milestone 006 slice 2 is implemented in the current working tree. Routes,
partitioned telemetry, and processing results now record the topology version
captured for a batch. Partitioned telemetry and result construction validate
that telemetry topology version matches the result topology version.

Milestone 006 slice 3 is implemented in the current working tree. RadarPulse now
has pressure sample and score contracts over partitioned telemetry:
`RadarProcessingPressureSample`, shard and partition pressure samples,
`RadarProcessingPressureScore`, `RadarProcessingPressureBand`, and
`RadarProcessingPressureOptions`. Pressure samples copy numeric telemetry only
and do not retain `RadarEventBatch` payload references.

Milestone 006 slice 4 is implemented in the current working tree. RadarPulse now
has rolling pressure windows with explicit hysteresis:
`RadarProcessingPressureWindow`, window options, shard pressure state, and
partition pressure state. The window tracks recent pressure samples, exposes
rebalance eligibility only after the configured minimum sample count, preserves
latest topology version, and applies enter/exit thresholds so short spikes do
not automatically trigger rebalance.

Milestone 006 slice 5 is implemented in the current working tree. RadarPulse now
has deterministic anti-churn policy state:
`RadarProcessingRebalancePolicyState`, rebalance options, move policy input,
policy result/rejection contracts, partition residency/cooldown state, shard
cooldown state, and move budgets. The policy evaluates candidate moves without
mutating state, records only accepted moves, and applies logical-sequence based
minimum residency, cooldown, global/source/target budget, projected-benefit,
and target-headroom gates.

Milestone 006 slice 6 is implemented in the current working tree. RadarPulse now
has a stable rebalance decision and skipped-reason telemetry contract:
`RadarProcessingRebalanceDecision`, decision kind, move kind, skipped reason,
candidate, and projected pressure types. Decisions can now represent no-action,
accepted move, and rejected-candidate outcomes, carry topology/evaluation/window
context, expose move telemetry, and map policy rejections into explicit skipped
reasons.

Milestone 006 slice 7 is implemented in the current working tree. RadarPulse now
has a deterministic direct hot relief planner:
`RadarProcessingDirectHotReliefPlanner`. The planner reads the rolling pressure
window and anti-churn policy state, finds hot or super-hot source shards, builds
direct hot-partition relief candidates against cold target shards, projects
source/target pressure before and after, rejects unsafe target projections,
applies policy gates, and returns rebalance decisions without mutating topology
or policy state.

Milestone 006 slice 8 is implemented in the current working tree. RadarPulse now
has hot-partition classification state:
`RadarProcessingHotPartitionClassification`,
`RadarProcessingHotPartitionState`, and
`RadarProcessingHotPartitionClassifier`. Direct hot relief planning can now
record intrinsic hot partitions, skip intrinsic or quarantined partitions on
later evaluations, surface skipped classification reasons in decision telemetry,
and quarantine partitions whose recent movement produced insufficient actual
relief.

Milestone 006 slice 9 is implemented in the current working tree. RadarPulse now
has a cold evacuation fallback planner:
`RadarProcessingColdEvacuationPlanner`. The planner runs only against sustained
hot or super-hot source shards, selects low-pressure non-hot partitions on the
hot shard, projects source/target pressure before and after, rejects cosmetic
or unsafe target moves, applies anti-churn policy gates, and returns
`ColdEvacuation` rebalance decisions without mutating topology or policy state.

Milestone 006 slice 10 is implemented in the current working tree. RadarPulse
now has a synchronous migration lifecycle and coordinator:
`RadarProcessingPartitionMigrationState`, migration validation errors,
partition migration requests, migration validation results, migration results,
and `RadarProcessingMigrationCoordinator`. The coordinator accepts only
accepted rebalance decisions, validates the current topology version and source
shard ownership before publication, applies moves through
`RadarProcessingTopologyManager`, records previous/current topology versions,
and leaves topology unchanged on rejected or failed validation.

Milestone 006 slice 11 is implemented in the current working tree. RadarPulse
now has state handoff validation contracts:
`RadarProcessingPartitionStateSnapshot`,
`RadarProcessingPartitionStateChecksum`,
`RadarProcessingStateHandoffValidator`, validation result, and validation
errors. The validator captures partition-owned source-state summaries before
and after a migration and allows owner shard changes while rejecting partition
id, source range, count, raw checksum, processing checksum, last timestamp
checksum, and handler snapshot checksum mismatches.

Milestone 006 slice 12 is implemented in the current working tree. RadarPulse
now has a synchronous rebalance-aware processing session:
`RadarProcessingRebalanceSession` and
`RadarProcessingRebalanceSessionResult`. The session processes one batch
against one topology snapshot, converts partitioned telemetry into a pressure
sample, advances logical evaluation state, tries direct hot relief first, falls
back to cold evacuation when direct movement cannot publish a move, validates
state handoff, publishes accepted migrations only between batches, records the
accepted move in anti-churn policy state, and lets the next batch route against
the latest topology version.

Milestone 006 slice 13 is implemented in the current working tree. RadarPulse
now has explicit rebalance validation helpers:
`RadarProcessingRebalanceValidator`,
`RadarProcessingRebalanceValidationResult`, and
`RadarProcessingRebalanceValidationError`. The validator checks topology
sequence monotonicity, stable source-to-partition mapping, accepted move owner
changes, route/telemetry/topology ownership consistency, pressure sample parity,
session decision topology, migration result topology, and state handoff
diagnostics. `RadarProcessingRebalanceSessionResult` now carries a validation
result for session-level diagnostics.

Milestone 006 slice 14 is implemented in the current working tree. RadarPulse
now has deterministic synthetic rebalance workloads:
`RadarProcessingSyntheticRebalanceWorkloadKind`,
`RadarProcessingSyntheticRebalanceWorkload`,
`RadarProcessingSyntheticRebalanceWorkloadRunner`, and workload result
contracts. The workload catalog covers balanced no-move, sustained hot-shard
direct relief, intrinsic-hot fallback to cold evacuation, oscillating short
spikes with no churn, and cooldown storm rejection scenarios over prebuilt
`RadarEventBatch` values.

Milestone 006 slice 15 is implemented in the current working tree. RadarPulse
now has a processing-only synthetic rebalance benchmark harness:
`RadarProcessingSyntheticRebalanceBenchmarkMode`,
`RadarProcessingSyntheticRebalanceBenchmark`,
`RadarProcessingSyntheticRebalanceBenchmarkResult`, and accepted-move pressure
summary contracts. The benchmark measures static no-rebalance, pressure
sampling only, and full rebalance-session modes over the synthetic workload
catalog while reporting topology versions, rebalance evaluations, accepted
moves, skipped decisions, direct/cold move counts, failed migrations,
validation status, deterministic checksum, throughput, allocation ratios, and
accepted-move pressure projections.

Milestone 006 slice 16 is implemented in the current working tree. RadarPulse
now exposes the synthetic rebalance benchmark through
`processing benchmark rebalance-synthetic` with workload, mode, iteration, and
warmup options. The command can run static no-rebalance, pressure-sampling-only,
and full rebalance-session modes over one workload or the full workload catalog,
and prints topology, move, skipped-reason, throughput, allocation, validation,
and accepted-move pressure summary fields.

Milestone 006 Release benchmark capture is implemented in the current working
tree. The captured command is
`processing benchmark rebalance-synthetic --workload all --mode all --iterations 10000 --warmup-iterations 1000`
after a Release CLI build. The benchmark records same-run static, sampling, and
full rebalance contours for balanced, hot-shard, intrinsic-hot, oscillating, and
cooldown-storm workloads, with same-run static ratios and a diagnostic
comparison against the milestone 005 partitioned/no-handler baseline.

Milestone 006 real-data rebalance smoke and cache-wide benchmarking are
implemented in the current working tree. The new command is
`processing benchmark rebalance-archive` with `--file` or `--cache` input,
static, sampling, rebalance, or all modes plus
partition/shard/iteration/archive parallelism options. It streams real NEXRAD
archive data into leased `RadarEventBatch` callbacks, processes each batch
synchronously, and reports end-to-end archive replay timing separately from
processing callback timing.

Milestone 006 is complete. The decision trace is written in
`docs/milestones/006-partition-level-shard-rebalance-decision-trace.md`, and
the closeout is written in
`docs/milestones/006-partition-level-shard-rebalance-closeout.md`. The closeout
records the captured Release benchmark table, the real-data smoke and
cache-wide results, the same-run static overhead interpretation, and the caveat
that the milestone 006 synthetic rebalance catalog is a tiny behavioral contour
rather than the large milestone 005 throughput shape.

Milestone 007 architecture and implementation planning are complete. The
architecture is written in
`docs/milestones/007-rebalance-production-hardening.md`, and the implementation
plan is written in
`docs/milestones/007-rebalance-production-hardening-plan.md`.

Milestone 007 scope is production hardening for the synchronous rebalance
control plane before retained async worker transport: automatic quarantine
lifecycle, bounded telemetry retention, validation profiles, allocation
attribution/reduction, broader real-data contours, and a final comprehensive
performance comparison gate. The synchronous `PartitionedBarrier` path remains
the reference correctness boundary.

Milestone 007 is complete. The closeout is written in
`docs/milestones/007-rebalance-production-hardening-closeout.md`, and the
decision trace is written in
`docs/milestones/007-rebalance-production-hardening-decision-trace.md`.
The final Release performance gate passed: cache-wide no-skew rebalance over
the local KTLX cache processed `8,513,587,200` payload values with `2` accepted
direct-hot-relief moves, `436` skipped decisions, successful validation, zero
failed migrations, `3.36B` processing callback payload values/s, `0.03`
callback allocated bytes/payload, and bounded recent telemetry retention.
Counters-only validation profile sweeps preserved the same checksum and
decision counts, and the explicit hot-shard skew stress accepted `20` moves at
`3.24B` callback payload values/s with `0.04` callback allocated bytes/payload.

Milestone 008 architecture and implementation planning are complete. The
architecture is written in
`docs/milestones/008-retained-async-shard-transport.md`, and the implementation
plan is written in
`docs/milestones/008-retained-async-shard-transport-plan.md`.

Milestone 008 scope is the first retained async shard worker transport over the
closed milestone 007 synchronous rebalance boundary. The first implementation
target is conservative: one in-flight borrowed `RadarEventBatch` per worker
group, retained workers and bounded queues, coarse shard or partition-group
work items, no baseline payload copying, same-run synchronous versus async
benchmark comparison, and explicit worker lifecycle/failure/telemetry
contracts. The hard boundary is that retained workers are allowed, but retained
borrowed `RadarEventBatch` payload is not allowed.

Milestone 008 is complete. The decision trace is written in
`docs/milestones/008-retained-async-shard-transport-decision-trace.md`, and the
closeout is written in
`docs/milestones/008-retained-async-shard-transport-closeout.md`. The final
performance gate accepted async archive processing on the full local KTLX
`2026-05-04` contour because synchronous and async callback latency were parity
while preserving deterministic checksums, accepted/skipped rebalance behavior,
zero failed migrations, zero failed worker items, and bounded worker telemetry.
The measured production-shaped async cost is about `0.90%` additional
processing callback allocation from dispatch, completion, and worker telemetry
machinery. Synchronous execution remains the default and the correctness
oracle; async execution is selectable and explicitly benchmarked.

Milestone 009 architecture and implementation planning have started. The
concept document is written in
`docs/milestones/009-owned-payload-provider-decoupling.md`, and the plan draft
is written in
`docs/milestones/009-owned-payload-provider-decoupling-plan.md`. The milestone
scope is the first explicit owned payload boundary between replay providers and
processing: leased `RadarEventBatch` values must still finish inside the
provider callback, while owned batches may be enqueued into a bounded
provider-to-processing queue and processed after callback return. The first
target remains conservative: bounded in-process provider decoupling, one active
processing batch at a time, deterministic topology/rebalance publication
ordering, synchronous reference parity, and same-run benchmark contours that
separate owned-copy, enqueue, queue wait, worker, and rebalance costs.

Milestone 009 slice 1 owned snapshot guardrails are implemented in the current
working tree. `RadarEventBatchBuilderTests` now verify that a leased
`RadarEventBatch.ToOwnedSnapshot()` preserves stream metadata, dictionary and
source-universe versions, event fields, payload bytes, payload offsets,
precomputed payload metrics, owned lifetime, and stability after builder buffer
reuse. Empty leased batches are also covered and produce owned empty snapshots
with empty precomputed metrics. No production code changed in this slice.

Latest verification after milestone 009 slice 1:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarEventBatchBuilderTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Streaming
```

Recorded result:

```text
11 passed for RadarEventBatchBuilderTests.
71 passed for Streaming-focused coverage.
```

Milestone 009 slice 2 provider queue contract surface is implemented in the
current working tree. RadarPulse now has the first domain vocabulary for owned
provider decoupling: `RadarProcessingProviderQueueOptions`, queue full and
shutdown modes, queued batch sequence ids, owned-only
`RadarProcessingQueuedBatch`, enqueue statuses/results, processing
statuses/results, queued session statuses/results, and
`RadarProcessingProviderQueueTelemetrySummary`. The contracts separate enqueue
success from processing completion, reject leased queued batches at the
contract boundary, keep result snapshots immutable, and expose bounded
queue/backpressure counters. No runtime provider queue is implemented yet.

Latest verification after milestone 009 slice 2:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingProviderQueueContractTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
```

Recorded result:

```text
10 passed for provider queue contract coverage.
449 passed for Processing-focused coverage.
```

Milestone 009 slice 3 bounded owned batch queue foundation is implemented in
the current working tree. `RadarProcessingOwnedBatchQueue` now provides the
first in-process runtime queue for owned `RadarEventBatch` handoff. The queue
accepts only owned batches, assigns monotonic provider sequence ids, drains in
FIFO order, enforces bounded capacity, supports return-full and wait-on-full
enqueue modes, reports enqueue cancellation and timeout, rejects enqueue after
close or fault, allows accepted batches to drain after close/fault, exposes
typed dequeue statuses, clears pending batches on dispose, and emits
`RadarProcessingProviderQueueTelemetrySummary` counters for attempts,
accepted/full/timed-out/canceled/closed/faulted enqueue results, queue depth,
queued payload bytes, owned snapshot cost, and dequeued batch count. This slice
does not start processing or integrate archive replay yet.

Latest verification after milestone 009 slice 3:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingOwnedBatchQueueTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
```

Recorded result:

```text
11 passed for owned batch queue coverage.
460 passed for Processing-focused coverage.
```

Milestone 009 slice 4 queued processing consumer is implemented in the current
working tree. `RadarProcessingQueuedProcessingSession` now wraps the owned
batch queue and drains owned batches in provider sequence order into the
existing processing paths. Sequential and synchronous partitioned modes call
`RadarProcessingCore.Process`; async mode owns or consumes a
`RadarProcessingAsyncCoreSession` and calls `ProcessAsync`. The consumer keeps
one active batch at a time, records enqueue and processing result snapshots,
builds session telemetry with processing completion/failure/cancellation/skip
counts, faults the queue after invalid processing, rejects later enqueue after
fault, and marks already accepted remainder batches as `SkippedAfterFault`.
Cancellation before dequeue returns a canceled queued session. This slice does
not run the rebalance control plane, archive replay, or CLI integration.

Latest verification after milestone 009 slice 4:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
```

Recorded result:

```text
6 passed for queued processing session coverage.
466 passed for Processing-focused coverage.
```

Milestone 009 slice 5 queued rebalance consumer is implemented in the current
working tree. `RadarProcessingQueuedRebalanceSession` now wraps the owned
batch queue and drains owned batches in provider sequence order through the
existing rebalance control plane. Synchronous rebalance sessions call
`RadarProcessingRebalanceSession.Process`; async shard transport sessions own
or consume a `RadarProcessingAsyncRebalanceSession` and call `ProcessAsync`.
The consumer keeps one active batch at a time, preserves topology publication
ordering between dequeued batches, records rebalance results on queued batch
processing snapshots, reports final topology version on the queued session
result, faults after invalid processing, rebalance validation failure, or
failed migration, rejects later enqueue after fault, and marks already accepted
remainder batches as `SkippedAfterFault`. This slice does not integrate
archive replay providers or CLI benchmark contours yet.

Latest verification after milestone 009 slice 5:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
```

Recorded result:

```text
6 passed for queued rebalance session coverage.
472 passed for Processing-focused coverage.
```

Milestone 009 slice 6 archive provider adapter is implemented in the current
working tree. `ArchiveOwnedRadarEventBatchQueueingPublisher` now implements
the synchronous `IArchiveRadarEventBatchPublisher` callback boundary by taking
the leased archive `RadarEventBatch`, creating an owned snapshot before the
callback returns, and enqueueing only that owned batch into
`RadarProcessingOwnedBatchQueue`. The adapter records enqueue outcomes through
`RadarProcessingArchiveQueuedProviderResult`, exposes queue telemetry, reports
backpressure/full/closed/faulted/canceled publish attempts, rejects later
publish after queue fault or close, and keeps cancellation from enqueueing
partial work. The archive publisher path now has coverage proving fake
single-file archive publish preserves deterministic totals/checksum while the
queued provider receives an owned batch. This slice does not yet wire queued
provider mode into archive rebalance benchmarks or CLI options.

Latest verification after milestone 009 slice 6:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Archive
```

Recorded result:

```text
4 passed for archive owned queueing publisher coverage.
91 passed, 3 skipped for Archive-focused coverage.
```

Milestone 009 slice 7 queue telemetry and allocation attribution is
implemented in the current working tree. RadarPulse now has
`RadarProcessingProviderQueueTelemetryRecorder`,
`RadarProcessingProviderQueueRecentDetail`,
`RadarProcessingProviderQueueRecentDetailKind`, and
`RadarProcessingOwnedSnapshotAllocationSummary`. Provider queue telemetry now
tracks owned snapshot payload value count, provider-to-processing latency,
bounded recent details, dropped recent detail count, and owned snapshot
allocation ratios without retaining `RadarEventBatch` payload. The runtime
`RadarProcessingOwnedBatchQueue` now records enqueue and dequeue details using
the recorder while queued processing and queued rebalance sessions preserve
the new queue telemetry fields when adding processing completion/failure
counts. Recent detail retention is bounded by
`RadarProcessingProviderQueueOptions.RecentDetailCapacity`, including a
counters-only capacity of zero.

Latest verification after milestone 009 slice 7:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingProviderQueueContractTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingOwnedBatchQueueTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Archive
```

Recorded result:

```text
11 passed for provider queue contract coverage.
3 passed for provider queue telemetry recorder coverage.
11 passed for owned batch queue coverage.
477 passed for Processing-focused coverage.
91 passed, 3 skipped for Archive-focused coverage.
```

Milestone 009 slice 8 queued validation is implemented in the current working
tree. RadarPulse now has `RadarProcessingQueuedProviderValidator`,
`RadarProcessingQueuedProviderValidationProfile`,
`RadarProcessingQueuedProviderValidationError`,
`RadarProcessingQueuedProviderValidationResult`,
`RadarProcessingQueuedProviderReference`, and
`RadarProcessingQueuedProviderMetrics`. The validator supports off,
essential, diagnostic, and benchmark profiles. Essential validation checks the
owned batch boundary, accepted provider sequence monotonicity, processed
sequence monotonicity, missing completions for accepted batches unless the
session is canceled, and topology version regression. Diagnostic validation
adds telemetry counter parity and worker failure propagation checks. Benchmark
validation adds optional borrowed-reference parity for deterministic checksum,
accepted move count, skipped decision count, failed batch count, worker failed
batch count, and final topology version. The validator compares queued output
semantics only; it does not compare queue timing.

Latest verification after milestone 009 slice 8:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingQueuedProviderValidatorTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
```

Recorded result:

```text
10 passed for queued provider validator coverage.
487 passed for Processing-focused coverage.
```

Milestone 009 slice 9 archive benchmark integration is implemented in the
current working tree. `RadarProcessingArchiveRebalanceBenchmark` now exposes
`RadarProcessingArchiveProviderMode.BlockingBorrowed` and
`RadarProcessingArchiveProviderMode.QueuedOwned` for single-file and cache
archive rebalance benchmarks. Blocking borrowed mode preserves the milestone
008 callback path. Queued owned mode routes archive replay through
`ArchiveOwnedRadarEventBatchQueueingPublisher`, drains owned batches into the
archive rebalance processor after replay enqueue, and keeps sync and async
execution modes comparable.

Archive benchmark result records now label provider mode and provider queue
capacity, expose queue telemetry, and surface owned snapshot allocation,
owned snapshot elapsed time, enqueue wait time, and queue drain time separately
from replay, processing, worker, and rebalance timing. Archive allocation
summary now carries owned snapshot allocation so queued provider copy cost is
visible instead of hidden inside replay/processing totals. Cache benchmark
aggregation combines queue counters and keeps recent queue details bounded.

Latest verification after milestone 009 slice 9:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RebalanceArchiveBenchmark
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Archive
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
```

Recorded result:

```text
8 passed for archive rebalance benchmark queued-provider coverage.
95 passed, 3 skipped for Archive-focused coverage.
487 passed for Processing-focused coverage.
```

Milestone 009 slice 10 CLI surface is implemented in the current working
tree. `processing benchmark rebalance-archive` now accepts
`--provider blocking-borrowed|queued-owned`, `--queue-timeout-ms <ms>`, and
`--queue-telemetry none|summary|recent`. The existing `--queue-capacity`
flag remains compatible with milestone 008 async worker queues; when
`--provider queued-owned` is selected, the same flag also configures the
provider owned-batch queue, including sync execution where no worker queue is
present. Blocking borrowed remains the default provider mode.

The archive rebalance CLI output now labels provider mode and provider queue
capacity for file and cache benchmarks, adjusts the batch lifetime text for
queued-owned runs, and prints provider queue summary telemetry by default.
Queued output includes owned snapshot counts, payload bytes/values, owned
snapshot time and allocation, enqueue attempts and wait time, dequeue and
processing completion counters, drain time, queue high-water marks,
provider-to-processing latency, and bounded recent-detail retention. Passing
`--queue-telemetry recent` prints retained queue details; `none` suppresses
the queue telemetry block.

Latest verification after milestone 009 slice 10:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RebalanceArchiveBenchmark
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Presentation
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Archive
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
16 passed for CLI rebalance benchmark coverage.
8 passed for archive rebalance benchmark queued-provider coverage.
18 passed for Presentation-focused coverage.
97 passed, 3 skipped for Archive-focused coverage.
487 passed for Processing-focused coverage.
660 passed, 3 skipped for the full test project.
```

Milestone 009 performance gate is captured separately, without writing the
milestone closeout or decision trace yet:
`docs/milestones/009-owned-payload-provider-decoupling-performance-gate.md`.
The gate used a Release CLI build and compared blocking-borrowed sync,
blocking-borrowed async, queued-owned sync, and queued-owned async on both a
single KTLX Archive Two file and the local KTLX cache. The cache contour used
`data\nexrad --date 2026-05-04 --radar KTLX --max-files 220`, examined 220
files, skipped 22 non-base-data files, and published 198 Archive Two
base-data files.

The gate preserved deterministic parity across all provider and execution
contours: full-cache payload values stayed at `7_660_888_320`, validation
checksum stayed at `7_480_064_646_096_449_000`, accepted moves stayed at `2`,
skipped decisions stayed at `392`, and failed migrations stayed at `0`.
Queued-owned exposed the expected owned-copy cost: about `9.95 GB` of owned
snapshot allocation and about `529-576 ms` owned snapshot time on the full
cache. Provider enqueue wait was negligible at about `2 ms` total, while queue
depth high-water mark stayed at `1` for both queue capacity `1` and `8`.

Performance gate conclusion: queued-owned is acceptable as an explicit
correctness-preserving provider-decoupling measurement and validation substrate,
but it should not become the default path yet. The current benchmark drains
after each file, so larger queue capacity validates bounded behavior but does
not produce provider/processing overlap. Next optimization targets are reducing
owned snapshot allocation, adding a producer/consumer overlap contour, and
keeping blocking-borrowed as the default until those costs improve.

Milestone 009 decision trace is written:
`docs/milestones/009-owned-payload-provider-decoupling-decision-trace.md`.
It records the owned-at-provider-boundary decision, bounded in-process queue
scope, owned-only queue invariant, separation of enqueue success from
processing completion, provider sequence ordering, reuse of existing processing
and rebalance sessions, explicit provider mode CLI/benchmark surface,
bounded telemetry, borrowed-reference validation, deferral of true
producer/consumer overlap, and the performance-gate conclusion that
queued-owned is accepted as an explicit substrate but not the default path.

Milestone 009 closeout is written:
`docs/milestones/009-owned-payload-provider-decoupling-closeout.md`. It records
the final implemented scope, non-goals, checklist, latest full-suite
verification, Release performance gate summary, decision trace, and next
milestone input. The recommended next milestone focus is reducing owned
snapshot allocation and adding a real producer/consumer overlap contour while
keeping `blocking-borrowed` as the default provider mode.

Milestone 010 architecture is drafted:
`docs/milestones/010-owned-provider-overlap-cost-reduction.md`. It defines the
owned-provider overlap and cost-reduction concepts: lower-allocation retained
payload strategy, resource-owned queued batches, bounded producer/consumer
overlap, retained-byte queue pressure, topology pins, ordered commit boundary,
resource lifecycle, telemetry, validation, benchmark scope, and completion
criteria.

Milestone 010 implementation plan is drafted:
`docs/milestones/010-owned-provider-overlap-cost-reduction-plan.md`. The plan
breaks the work into implementation slices: milestone 009 cost anchors,
retained payload strategy contracts, resource-owned queued batch lifecycle,
lower-allocation retention implementation, retained-byte-aware queue window,
producer/consumer overlap runner, ordered consumer topology pinning, overlap
telemetry and allocation attribution, optimized queued validation, archive
benchmark/CLI integration, and the Release performance gate.

Milestone 010 slice 1 baseline audit is complete. Confirmed baseline cost
anchors and code paths:

```text
RadarEventBatch.ToOwnedSnapshot()
  -> current snapshot-copy retention path for leased provider batches

ArchiveOwnedRadarEventBatchQueueingPublisher.Publish()
  -> captures allocation delta and elapsed time around ToOwnedSnapshot()
  -> enqueues only owned RadarEventBatch input

RadarProcessingQueuedBatch
  -> carries OwnedSnapshotTime and OwnedSnapshotAllocatedBytes

RadarProcessingProviderQueueTelemetryRecorder/Summary
  -> accumulates owned snapshot count, payload bytes, payload values,
     allocated bytes, elapsed time, enqueue wait, depth high-water mark,
     queued payload bytes high-water mark, drain time, and recent details

RadarProcessingArchiveRebalanceBenchmark
  -> passes queue telemetry owned allocation into
     RadarProcessingRebalanceAllocationSummary.ForArchiveReplay()
  -> current cache queued-owned contour still drains after each file publish

RadarProcessingArchiveRebalanceBenchmarkResult and CacheBenchmarkResult
  -> expose OwnedSnapshotAllocatedBytes, OwnedSnapshotElapsed,
     EnqueueWaitElapsed, QueueDrainElapsed, and queue telemetry

RadarPulse.Cli rebalance-archive output
  -> prints provider mode, queue capacity, owned snapshot allocation/time,
     enqueue counters, enqueue wait, drain, queue depth high-water mark,
     payload-byte high-water mark, provider-to-processing latency, and recent
     detail counts
```

Latest verification after milestone 010 slice 1:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarEventBatchBuilderTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests|FullyQualifiedName~RadarProcessingQueuedProviderValidatorTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
```

Recorded result:

```text
72 passed, 0 failed, 0 skipped.
```

Milestone 010 slice 2 retained payload strategy contracts are implemented in
the current working tree. New domain contracts:

```text
RadarProcessingRetainedPayloadStrategy
  -> SnapshotCopy, PooledCopy, BuilderTransfer

RadarProcessingRetainedPayloadOptions
  -> default SnapshotCopy behavior, optional max retained payload byte limit,
     and strategy validation

RadarProcessingRetainedPayloadRetentionStatus
RadarProcessingRetainedPayloadRetentionResult
  -> separates succeeded, unsupported strategy, failed copy, canceled, and
     invalid input outcomes
  -> successful retention requires an owned RadarEventBatch and carries elapsed
     time, allocated bytes, event count, payload bytes, payload values, and raw
     checksum

RadarProcessingRetainedPayloadReleaseStatus
RadarProcessingRetainedPayloadReleaseResult
  -> separates released, already released, failed, and not-required release
     outcomes

RadarProcessingRetainedPayloadTelemetrySummary
  -> carries strategy name, retention/release counters, retained event/payload
     counts, allocated bytes, retention/release elapsed time, transfer counts,
     pool rent/return/miss counts, failure counters, and allocation ratios
```

The slice intentionally does not change `ToOwnedSnapshot()`,
`ArchiveOwnedRadarEventBatchQueueingPublisher`, queue mechanics, benchmark
behavior, or CLI output. `SnapshotCopy` remains the snapshot-compatible
baseline for later retention implementations.

Latest verification after milestone 010 slice 2:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingRetainedPayloadContractTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRetainedPayloadContractTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
5 passed, 0 failed, 0 skipped.
16 passed, 0 failed, 0 skipped for retained payload plus provider queue contract coverage.
665 passed, 3 skipped for the full test project.
```

Milestone 010 slice 3 resource-owned queued batch lifecycle is implemented in
the current working tree. New domain contracts:

```text
RadarProcessingRetainedBatchResourceState
  -> ProviderOwned, QueueOwned, ConsumerOwned, Released, ReleaseFailed

RadarProcessingRetainedBatchResource
  -> owns a retained payload release callback, tracks strategy/payload bytes,
     transfers provider ownership to queue ownership and queue ownership to
     consumer ownership, releases exactly once, records terminal state, and
     reports release failure without reinvoking cleanup callbacks

RadarProcessingRetainedQueuedBatch
  -> wraps an existing RadarProcessingQueuedBatch with a retained resource and
     transfers accepted provider ownership into queue ownership

RadarProcessingRetainedBatchLease
  -> exposes the processing-facing RadarEventBatch while holding consumer
     ownership and releases retained resources on explicit release or dispose

RadarProcessingRetainedResourceCleanupResult
  -> copies release result snapshots, counts released/already-released/failed/
     not-required outcomes, and provides ReleaseAll() for pending queue cleanup
```

This slice intentionally keeps `RadarProcessingOwnedBatchQueue`,
`RadarProcessingQueuedProcessingSession`,
`RadarProcessingQueuedRebalanceSession`,
`ArchiveOwnedRadarEventBatchQueueingPublisher`, benchmark behavior, and CLI
output unchanged. The new lifecycle wrapper is ready for slice 4 to connect a
lower-allocation retained payload implementation without weakening the current
snapshot-copy baseline.

Latest verification after milestone 010 slice 3:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRetainedPayloadContractTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRetainedPayloadContractTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
13 passed, 0 failed, 0 skipped.
36 passed, 0 failed, 0 skipped for retained payload/resource plus queue/session-adjacent coverage.
673 passed, 3 skipped for the full test project.
```

Milestone 010 slice 4 lower-allocation retained payload implementation is
implemented in the current working tree. New infrastructure and contract
extension:

```text
RadarProcessingRetainedPayloadRetentionResult
  -> successful retention now carries the retained resource handle alongside
     the owned RadarEventBatch, defaulting to a not-required resource for
     snapshot-compatible paths

RadarProcessingRetainedPayloadFactory
  -> keeps SnapshotCopy as the default compatibility strategy through
     RadarEventBatch.ToOwnedSnapshot()
  -> implements PooledCopy by copying leased event and payload memory into
     rented arrays, preserving stream schema, dictionary version,
     source-universe version, event order, payload offsets, precomputed payload
     metrics, owned lifetime, and payload stability after builder reuse
  -> returns rented arrays through a RadarProcessingRetainedBatchResource
     release callback after the consumer lifecycle finishes
  -> avoids rentals for owned input and empty leased batches
  -> reports unsupported BuilderTransfer explicitly until ownership-transfer
     semantics are proven
  -> returns rented storage before reporting copy failure when a pool rent or
     copy path throws
```

This slice intentionally keeps `RadarProcessingOwnedBatchQueue`,
`RadarProcessingQueuedProcessingSession`,
`RadarProcessingQueuedRebalanceSession`,
`ArchiveOwnedRadarEventBatchQueueingPublisher`, benchmark behavior, and CLI
output unchanged. The lower-allocation retained batch factory is available for
the next queue-window and runtime-integration slices, while snapshot-copy
remains the current wired provider behavior.

Latest verification after milestone 010 slice 4:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedPayloadContractTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
21 passed, 0 failed, 0 skipped for retained payload factory/contracts/resource coverage.
681 passed, 3 skipped for the full test project.
```

Milestone 010 slice 5 retained-byte-aware queue window is implemented in the
current working tree. Queue contract and runtime changes:

```text
RadarProcessingProviderQueueOptions
  -> adds optional MaxRetainedPayloadBytes with positive-value validation and
     conservative null-by-default behavior

RadarProcessingOwnedBatchQueue
  -> enforces item capacity through the existing bounded Channel and enforces
     retained payload byte capacity through explicit pending-byte accounting
  -> treats batch payload length as the retained-byte budget unit for the
     current owned RadarEventBatch queue shape
  -> rejects impossible oversized batches immediately with Full status
  -> returns Full in ReturnFull mode when either item capacity or retained-byte
     capacity blocks acceptance
  -> waits in Wait mode when retained-byte capacity is exhausted and wakes
     waiting enqueues on dequeue, close, fault, or dispose
  -> decrements pending retained bytes at dequeue, the current ownership
     transfer point from provider queue to consumer
  -> exposes PendingRetainedPayloadBytes as a retained-byte alias over the
     existing PendingPayloadBytes accounting

RadarProcessingProviderQueueTelemetrySummary
  -> exposes RetainedPayloadBytesHighWatermark as an explicit alias over the
     existing queued payload byte high-water mark
```

This slice intentionally keeps `ArchiveOwnedRadarEventBatchQueueingPublisher`,
`RadarProcessingQueuedProcessingSession`,
`RadarProcessingQueuedRebalanceSession`, benchmark behavior, and CLI output
unchanged. The runtime queue can now enforce retained-byte pressure internally,
but archive/CLI controls for the byte budget remain part of later integration
slices.

Latest verification after milestone 010 slice 5:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests|FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
30 passed, 0 failed, 0 skipped for provider queue contract/runtime/telemetry coverage.
686 passed, 3 skipped for the full test project.
```

Milestone 010 slice 6 producer/consumer archive overlap runner is implemented
in the current working tree. New infrastructure contracts and runtime:

```text
RadarProcessingArchiveQueuedOverlapOptions
  -> wraps the provider queue options used by the overlap runner

RadarProcessingArchiveQueuedOverlapStatus
RadarProcessingArchiveQueuedOverlapProducerStatus
RadarProcessingArchiveQueuedOverlapProducerResult
RadarProcessingArchiveQueuedOverlapConsumerResult
RadarProcessingArchiveQueuedOverlapResult
  -> separate producer publish outcome, consumer drain outcome, final overlap
     status, queue telemetry, provider enqueue results, elapsed time, and
     diagnostic message

RadarProcessingArchiveQueuedOverlapRunner
  -> creates the owned provider queue and archive queueing publisher
  -> starts consumer drain before archive production starts
  -> runs archive production concurrently through IArchiveRadarEventBatchPublisher
  -> closes the queue only after producer completion
  -> faults the queue on producer or consumer failure so the other side stops
     without accepting more work
  -> waits for both producer and consumer before reporting success
  -> preserves provider sequence order through the existing queue drain
```

Focused tests prove the runner can let a fast producer queue ahead of a slow
consumer with queue depth greater than one, and that consumer failure faults
intake so later producer publish attempts are rejected. This slice intentionally
keeps `RadarProcessingArchiveRebalanceBenchmark`, CLI flags/output, retained
payload strategy selection, and benchmark result fields unchanged. Those are
left for the ordered consumer, telemetry attribution, validation, and final
archive benchmark integration slices.

Latest verification after milestone 010 slice 6:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
3 passed, 0 failed, 0 skipped for overlap runner focused coverage.
29 passed, 0 failed, 0 skipped for overlap runner plus queue/session/archive publisher coverage.
689 passed, 3 skipped for the full test project.
```

Milestone 010 slice 7 ordered consumer and topology pinning is implemented in
the current working tree. Runtime integration and guardrails:

```text
RadarProcessingArchiveQueuedOverlapRunner.RunRebalanceAsync()
  -> wires archive overlap production to RadarProcessingQueuedRebalanceSession
     as the ordered consumer
  -> creates the queued rebalance consumer over the same owned provider queue
  -> keeps one active rebalance-enabled processing batch at a time through the
     existing queued rebalance session
  -> supports async-shard rebalance sessions by creating and owning the
     required RadarProcessingAsyncRebalanceSession wrapper

Queued topology semantics
  -> enqueue still stores only owned payload and provider sequence
  -> queued-ahead batches do not capture topology while waiting in the queue
  -> topology is captured by RadarProcessingRebalanceSession.Process() at
     dequeue/processing time
  -> accepted migration from batch N publishes before batch N+1 is processed
  -> final overlap result exposes the queued rebalance session result and final
     topology version
```

Focused tests cover both the official `RunRebalanceAsync()` contour and a
delayed-consumer overlap contour where two batches are accepted before drain.
The first batch publishes a topology move from version 0 to version 1, and the
second queued-ahead batch processes against version 1 rather than the topology
that existed when it was enqueued.

Latest verification after milestone 010 slice 7:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingRebalanceSessionTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
5 passed, 0 failed, 0 skipped for overlap runner/topology focused coverage.
19 passed, 0 failed, 0 skipped for overlap runner plus queued/direct rebalance coverage.
691 passed, 3 skipped for the full test project.
```

Milestone 010 slice 8 overlap telemetry and allocation attribution is
implemented in the current working tree. New accounting surfaces:

```text
RadarProcessingArchiveOverlapTelemetrySummary
  -> reports retention strategy, elapsed time, producer active time, consumer
     active time, overlap elapsed time, measured allocation delta, queue
     telemetry, and retained payload telemetry
  -> exposes queue depth and retained-byte high-water marks, provider blocked
     time from enqueue wait, consumer idle time from dequeue wait, retained
     batch/event/payload counters, retention allocation, provider-to-processing
     latency, release counts, and unattributed allocation

RadarProcessingArchiveQueuedOverlapResult.OverlapTelemetry
RadarProcessingArchiveQueuedOverlapResult.Telemetry
  -> attach the overlap attribution summary to every overlap run while keeping
     the existing QueueTelemetry surface intact

RadarProcessingProviderQueueTelemetrySummary.OwnedSnapshotEventCount
RadarProcessingProviderQueueTelemetrySummary.TotalDequeueWaitTime
  -> preserve retained event count independently of bounded recent details and
     expose time the consumer spent waiting on the provider queue
```

`RadarProcessingArchiveQueuedOverlapRunner.RunAsync()` now captures an
allocation snapshot around the overlapped producer/consumer run and builds the
overlap summary from producer timing, consumer timing, queue telemetry, and the
current snapshot-copy retention path. Queue/session/benchmark telemetry
reconstruction preserves the new retained event and dequeue wait fields, and
archive benchmark queue aggregation sums them across iterations. The current
runtime still uses `SnapshotCopy` through
`ArchiveOwnedRadarEventBatchQueueingPublisher`; pooled-copy strategy selection,
benchmark result output, and CLI flags remain for later milestone 010 slices.

Latest verification after milestone 010 slice 8:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests|FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
35 passed, 0 failed, 0 skipped for overlap telemetry plus queue telemetry coverage.
691 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 010 slice 9 optimized queued validation is implemented in the
current working tree. The queued provider validator now covers the overlapped
retained path, not only the original queued-owned structure:

```text
RadarProcessingQueuedProviderValidationContext
  -> names the semantic surface being compared, the provider overlap mode, the
     retention strategy, retained payload telemetry, and overlap elapsed time

RadarProcessingQueuedProviderValidationSurface
  -> processing-only or rebalance comparison surface

RadarProcessingQueuedProviderOverlapMode
  -> none or producer-consumer overlap

RadarProcessingQueuedProviderMetrics
  -> now includes payload value count, failed migration count, and inferred
     semantic surface in addition to checksum, moves, skipped decisions, failed
     batches, and worker failures

RadarProcessingQueuedProviderReference
  -> can now carry borrowed-reference payload value count, failed migration
     count, and semantic surface
```

Validation behavior added in this slice:

```text
accepted provider sequences must be contiguous, not only monotonic
processed provider sequences reject out-of-order commits and gaps
queue retained snapshot telemetry must match accepted queued batches
benchmark reference comparison includes payload value count and failed
  migration count
optional semantic-surface comparison prevents accidentally mixing processing
  only and rebalance references
optimized validation context checks retention telemetry completeness, retained
  batch/event/payload/allocation parity, release completion, and positive
  producer-consumer overlap telemetry for completed overlap runs
validation results surface semantic surface, overlap mode, and retention
  strategy for diagnostics
```

This slice is still validation-only. It does not wire archive benchmark CLI
flags for selecting `pooled-copy`, `builder-transfer`, provider overlap mode,
or retained-byte controls; those remain for slice 10.

Latest verification after milestone 010 slice 9:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingQueuedProviderValidatorTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
19 passed, 0 failed, 0 skipped for optimized queued provider validation coverage.
527 passed, 0 failed, 0 skipped for Processing-focused coverage.
700 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 010 slice 10 archive benchmark and CLI integration is implemented in
the current working tree. The archive benchmark can now run the queued-owned
provider with explicit retention and overlap controls instead of only the
milestone 009 snapshot-copy compatibility path:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile/MeasureCache
  -> accept provider overlap mode, retained payload strategy, and retained-byte
     queue budget

RadarProcessingArchiveRebalanceBenchmarkResult
RadarProcessingArchiveRebalanceCacheBenchmarkResult
  -> report provider overlap mode, retention strategy, retained-byte budget,
     queue telemetry, retention telemetry, and overlap telemetry

ArchiveOwnedRadarEventBatchQueueingPublisher
  -> retains archive batches through RadarProcessingRetainedPayloadFactory,
     tracks retained resources by provider sequence, and releases resources
     when the consumer completes a queued batch

RadarProcessingArchiveQueuedOverlapRunner
  -> accepts consumers that can release publisher-owned retained resources and
     refreshes provider telemetry after the consumer has released pending
     resources
```

CLI controls added in this slice:

```text
--provider-overlap none|producer-consumer
--retention-strategy snapshot-copy|pooled-copy|builder-transfer
--queue-retained-bytes <bytes>
--overlap-telemetry none|summary|recent
--overlap-consumer-delay-ms <ms> for controlled queue-ahead proof only
```

CLI behavior:

```text
blocking-borrowed remains the default provider mode
provider overlap requires --provider queued-owned
retention strategy selection requires --provider queued-owned
retained-byte budget requires --provider queued-owned
builder-transfer is parsed but guarded as unsupported
output separates provider mode, queue capacity, overlap mode, retention
  strategy, retained-byte budget, queue telemetry, retention lifecycle counters,
  and overlap allocation/timing attribution
```

Latest focused verification after milestone 010 slice 10:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
40 passed, 0 failed, 0 skipped for archive benchmark, CLI, and overlap runner focused coverage.
702 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 010 initial performance gate is captured in
`docs/milestones/010-owned-provider-overlap-cost-reduction-performance-gate.md`.
The gate result is deliberately mixed:

```text
pooled-copy reduced full-cache retained allocation from 9_947_507_832 bytes to
  102_811_264 bytes on the non-overlapped queued-owned contour
pooled-copy released 198 retained batches with 0 failed releases
borrowed-reference parity held across all measured contours
producer/consumer task lifetimes overlapped, but queue depth high watermark
  remained 1 and queued-ahead overlap stayed absent
queued-owned remains opt-in; blocking-borrowed remains the default
```

Milestone 010 now has a new slice 11 in the implementation plan: cache-level
producer pipeline. The goal is to run one producer/consumer overlap session
across the selected cache file set, so producer work can enqueue ahead of the
consumer under bounded queue and retained-byte pressure while preserving
provider sequence order, topology ordering, borrowed-reference parity, and
retained resource cleanup.

Milestone 010 slice 11 cache-level producer pipeline is implemented in the
current working tree. `MeasureCache` now uses one shared
`RadarProcessingArchiveQueuedOverlapRunner` invocation for
`queued-owned + producer-consumer` cache runs instead of invoking overlap once
per file. The producer selects the cache file set with the existing date,
radar, `max-files`, skipped-file, and Archive Two base-data rules, then
publishes every selected base-data file into one retained provider queue. The
consumer drains that shared queue through the existing ordered processing
callback, and the result aggregates cache totals, queue telemetry, retention
telemetry, and overlap telemetry for the whole run.

Latest focused verification after milestone 010 slice 11:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
19 passed, 0 failed, 0 skipped for archive publisher and archive benchmark focused coverage.
41 passed, 0 failed, 0 skipped for archive benchmark, CLI, and overlap runner focused coverage.
703 passed, 0 failed, 3 skipped for the full test project.
```

Milestone 008 slice 1 is implemented in the current working tree. RadarPulse
now has the first async execution option contracts:
`RadarProcessingExecutionMode.AsyncShardTransport`,
`RadarProcessingAsyncExecutionOptions`, `RadarProcessingWorkerAffinity`, and
`RadarProcessingWorkerTimeoutPolicy`. `RadarProcessingCoreOptions` now carries
async execution settings while preserving sequential defaults and existing
synchronous behavior. Async shard transport is recognized as a mode but
intentionally throws `NotSupportedException` until the retained worker runtime
is implemented in later slices. Focused tests cover stable enum values,
conservative borrowed-batch defaults, invalid worker/queue/timeout values,
composition through core options, and the explicit not-yet-implemented runtime
guard.

Latest verification after milestone 008 slice 1:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingContractTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
16 passed for focused processing contract coverage.
333 passed for processing-focused coverage.
488 passed, 3 skipped for the full test project.
```

Milestone 008 slice 2 is implemented in the current working tree. RadarPulse
now has worker lifecycle contracts without starting any worker threads:
`RadarProcessingWorkerGroupState`, `RadarProcessingWorkerHealth`,
`RadarProcessingWorkerLifecycleError`, `RadarProcessingWorkerId`,
`RadarProcessingWorkerGroupStatus`,
`RadarProcessingWorkerLifecycleResult`, and
`RadarProcessingWorkerGroupLifecycle`. The lifecycle state machine records
not-started, running, stopping, stopped, faulted, and disposed states, exposes
health/status snapshots, validates dispatch eligibility, makes dispose
idempotent, keeps benign invalid calls such as duplicate start from corrupting
a running group, and rejects dispatch before start, while stopping, after
fault, and after dispose. Focused tests cover stable enum values, worker id
validation, valid transitions, invalid transitions, dispatch eligibility,
fault health, idempotent dispose, immutable status snapshots, and invalid
status/result shapes.

Latest verification after milestone 008 slice 2:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingWorkerLifecycleContractTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
11 passed for focused worker lifecycle coverage.
344 passed for processing-focused coverage.
499 passed, 3 skipped for the full test project.
```

Milestone 008 slice 3 is implemented in the current working tree. RadarPulse
now has borrowed async batch scope and completion contracts without worker
mailboxes or runtime threads: `RadarProcessingAsyncBatchScope`,
`RadarProcessingAsyncWorkItem`, `RadarProcessingAsyncWorkCompletion`,
`RadarProcessingAsyncBatchCompletion`,
`RadarProcessingAsyncBatchScopeResult`,
`RadarProcessingAsyncWorkStatus`, and
`RadarProcessingAsyncBatchCompletionError`. The scope records one batch
sequence, one topology version, and an expected work-item count; creates
topology-scoped work items with copied ordered partition ids; accepts
completion records; rejects wrong batch sequence, wrong topology version,
out-of-range work item ids, duplicates, and completions after close; reports
missing completion, failed work, and canceled work through explicit result
errors; and emits immutable aggregate completion snapshots with timing and
processed-count summaries.

Latest verification after milestone 008 slice 3:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingAsyncBatchScopeContractTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
14 passed for focused async batch scope coverage.
358 passed for processing-focused coverage.
513 passed, 3 skipped for the full test project.
```

Milestone 008 slice 4 is implemented in the current working tree. RadarPulse
now has the bounded in-process worker mailbox foundation under
`RadarPulse.Infrastructure.Processing`: `RadarProcessingWorkerMailbox<TWork>`,
`RadarProcessingWorkerMailboxOptions`,
`RadarProcessingWorkerMailboxEnqueueStatus`,
`RadarProcessingWorkerMailboxDequeueStatus`,
`RadarProcessingWorkerMailboxEnqueueResult`, and
`RadarProcessingWorkerMailboxDequeueResult<TWork>`. The mailbox enforces fixed
capacity, returns deterministic enqueue/dequeue status values, preserves FIFO
order, rejects enqueue after close or dispose, lets closed mailboxes drain
accepted items, supports cancellation while waiting for dequeue, releases
waiting dequeue calls on dispose, clears pending accepted items on dispose, and
tracks pending count so drained work is not retained by the mailbox.

Latest verification after milestone 008 slice 4:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingWorkerMailboxTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
10 passed for focused worker mailbox coverage.
368 passed for processing-focused coverage.
523 passed, 3 skipped for the full test project.
```

Milestone 008 slice 5 is implemented in the current working tree. RadarPulse
now has the first retained async worker group runtime under
`RadarPulse.Infrastructure.Processing`: `RadarProcessingAsyncWorkerGroup`,
`RadarProcessingAsyncWorkerGroupOptions`,
`RadarProcessingAsyncWorkerGroupResult`,
`RadarProcessingAsyncWorkerGroupError`,
`RadarProcessingAsyncWorkExecutor`, and internal retained worker/request/batch
state types. The runtime starts one retained task per worker, dispatches
topology-scoped work items through bounded mailboxes, enforces one in-flight
borrowed batch per worker group, records every accepted work item into the
batch scope completion barrier, turns worker delegate exceptions into failed
work completions, rejects dispatch before start/while stopping/after dispose,
and drains accepted borrowed work before stop or dispose releases worker
resources.

Latest verification after milestone 008 slice 5:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingAsyncWorkerGroupTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
10 passed for focused async worker group coverage.
378 passed for processing-focused coverage.
533 passed, 3 skipped for the full test project.
```

Milestone 008 slice 6 is implemented in the current working tree. The retained
worker group now makes the borrowed batch lifetime guardrails explicit through
`RadarProcessingAsyncWorkerGroupDrainResult`, running/pending/outstanding work
counts, closed-scope dispatch rejection, and timeout diagnostics. Successful
dispatch returns only after the accepted borrowed work is drained and reports
zero outstanding work. Failed or rejected dispatch paths report numeric drain
diagnostics so accepted work cannot disappear untracked. A completed
`RadarProcessingAsyncBatchScope` is rejected before any worker delegate is run.
Timeouts mark the worker group faulted and can request cooperative
cancellation according to policy, but dispatch still waits for the completion
barrier before returning, so timeout is not treated as permission to release
borrowed payload while a worker may still read it.

Latest verification after milestone 008 slice 6:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingAsyncWorkerGroupTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
13 passed for focused async worker group and borrowed lifetime coverage.
381 passed for processing-focused coverage.
536 passed, 3 skipped for the full test project.
```

Milestone 008 slice 7 is implemented in the current working tree. RadarPulse
now has an async batch dispatcher under `RadarPulse.Infrastructure.Processing`:
`RadarProcessingAsyncBatchDispatcher`,
`RadarProcessingAsyncDispatchPlan`,
`RadarProcessingAsyncDispatchResult`, and
`RadarProcessingAsyncDispatchExecutor`. The dispatcher captures one topology
snapshot through a provider, routes one `RadarEventBatch` against that
snapshot, builds one shard-scoped `RadarProcessingAsyncWorkItem` per shard,
maps shard work to retained worker ids, and submits the plan through
`RadarProcessingAsyncWorkerGroup`. The borrowed batch is passed to the
dispatcher executor only inside the awaited dispatch path; the baseline path
does not copy payload storage, and dispatch returns only after the worker group
completion barrier drains. Focused tests cover one captured topology version,
route/topology mismatch rejection, one work item per shard, borrowed batch and
route object flow into the executor, completion-before-return behavior, and
worker timing/completion status projection through the dispatch result.

Latest verification after milestone 008 slice 7:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingAsyncBatchDispatcherTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
dotnet build RadarPulse.sln --no-restore
```

Result:

```text
7 passed for focused async batch dispatcher coverage.
388 passed for processing-focused coverage.
543 passed, 3 skipped for the full test project.
```

Milestone 008 slice 7 compile follow-up is implemented in the current working
tree. `RadarProcessingAsyncWorkerGroup.DisposeAsync()` now follows the standard
`IAsyncDisposable` pattern and returns `ValueTask`, so `await using` works in
tests and future callers. The lifecycle-result disposal path is now exposed as
`DisposeWithResultAsync()` for tests that need to assert the disposal state.

Latest verification after the slice 7 compile follow-up:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingAsyncBatchDispatcherTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingAsyncWorkerGroupTests
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
```

Result:

```text
7 passed for focused async batch dispatcher coverage.
13 passed for focused async worker group coverage.
Solution build succeeded with 0 warnings and 0 errors.
388 passed for processing-focused coverage.
```

Milestone 008 slice 8 is implemented in the current working tree. RadarPulse
now has deterministic async completion aggregation under
`RadarPulse.Infrastructure.Processing`: `RadarProcessingAsyncCompletionAggregator`,
`RadarProcessingAsyncAggregationResult`, and
`RadarProcessingAsyncAggregationError`. The aggregator consumes an async
dispatch result, validates completion scope/count/status, orders worker
completions by work item id instead of completion arrival order, checks each
work item against its captured shard route metrics, checks aggregate processed
event and payload-value counts against the captured route, and projects
successful async dispatch into `RadarProcessingTelemetry` with
`RadarProcessingExecutionMode.AsyncShardTransport`. Failed, canceled, missing,
duplicate, rejected, or metric-mismatched completions do not produce successful
telemetry. `RadarProcessingResult` and `RadarProcessingOutputValidator` now
accept telemetry for async shard transport as well as the synchronous
partitioned barrier path, so async results can be compared against the
synchronous oracle once the async executor has updated processing state.

Latest verification after milestone 008 slice 8:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingAsyncCompletionAggregatorTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingAsyncBatchDispatcherTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
8 passed for focused async completion aggregation coverage.
7 passed for focused async batch dispatcher coverage.
396 passed for processing-focused coverage.
551 passed, 3 skipped for the full test project.
Solution build succeeded with 0 warnings and 0 errors.
```

Milestone 008 slice 9 is implemented in the current working tree. RadarPulse
now has explicit async failure, cancellation, timeout, and health-transition
contracts in `RadarPulse.Domain.Processing`: `RadarProcessingAsyncFailureKind`,
`RadarProcessingAsyncCancellationKind`, `RadarProcessingAsyncTimeoutResult`,
and `RadarProcessingWorkerGroupHealthTransition`. Async work completions now
carry a failure kind for failed work and a cancellation kind for canceled work.
The retained worker group projects those contracts into
`RadarProcessingAsyncWorkerGroupResult`, including batch-level failure kind,
cancellation kind, timeout details, and the health transition recorded when a
timeout marks the group faulted.

Runtime behavior now distinguishes cancellation before dispatch, cancellation
while queued, cancellation while running, and timeout-requested cooperative
cancellation. Cancellation before dispatch returns a canceled batch result
without enqueuing borrowed work. Cancellation while queued records a canceled
work item without invoking the executor. Cancellation while running is observed
only at executor-safe cancellation points. Worker exceptions record
`WorkerException` on the failed completion and fail the batch without faulting
the worker loop. Timeout remains a borrowed-payload health diagnostic: the
worker group marks itself faulted, optionally requests cooperative
cancellation, waits for the accepted borrowed work to drain, and only then
returns a timed-out rejected dispatch result.

Latest verification after milestone 008 slice 9:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingAsyncWorkerGroupTests|FullyQualifiedName~RadarProcessingAsyncBatchScopeContractTests|FullyQualifiedName~RadarProcessingWorkerLifecycleContractTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingAsyncCompletionAggregatorTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
43 passed for focused async failure/cancellation/timeout/health coverage.
9 passed for focused async completion aggregation coverage.
402 passed for processing-focused coverage.
Solution build succeeded with 0 warnings and 0 errors.
557 passed, 3 skipped for the full test project.
```

Milestone 008 slice 10 is implemented in the current working tree. RadarPulse
now has bounded async worker telemetry contracts in
`RadarPulse.Domain.Processing`: `RadarProcessingWorkerTelemetrySummary`,
`RadarProcessingWorkerTelemetryCounters`, `RadarProcessingRecentWorkerBatch`,
`RadarProcessingRecentWorkerFailure`, and
`RadarProcessingWorkerRetentionStats`. `RadarProcessingTelemetryRetentionOptions`
now also carries worker-specific retention limits for recent worker batches and
recent worker failures while preserving the existing retention mode discipline.

The worker telemetry recorder is implemented under
`RadarPulse.Infrastructure.Processing` as `RadarProcessingWorkerTelemetryRecorder`.
It records async dispatch results into aggregate batch/work-item counters,
latest worker count and queue capacity, total dispatch/queue/execution/
aggregation/barrier timing, bounded recent batch samples, bounded recent
failure/cancellation/timeout samples, and dropped-detail counters. Counters-only
retention keeps aggregate values while dropping all worker detail. Failure
samples retain compact enum codes such as `WorkerException`, `TimedOut`, and
`BeforeDispatch`; they do not retain formatted exception text.

Latest verification after milestone 008 slice 10:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingWorkerTelemetry|FullyQualifiedName~RadarProcessingRebalanceHardeningOptionsTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
21 passed for focused worker telemetry and retention-option coverage.
415 passed for processing-focused coverage.
Solution build succeeded with 0 warnings and 0 errors.
570 passed, 3 skipped for the full test project.
```

Milestone 008 slice 11 is implemented in the current working tree. RadarPulse
now exposes async shard transport through an explicit disposable processing
session in `RadarPulse.Infrastructure.Processing`:
`RadarProcessingAsyncCoreSession`. The session composes a
`RadarProcessingCore`, retained `RadarProcessingAsyncWorkerGroup`,
`RadarProcessingAsyncBatchDispatcher`, `RadarProcessingAsyncCompletionAggregator`,
and `RadarProcessingWorkerTelemetryRecorder`. It starts an owned worker group
on first use, processes borrowed batches through shard work items, aggregates
completion deterministically, attaches worker telemetry to the processing
result, and disposes owned worker resources when the session is disposed.

`RadarProcessingCore.Process(...)` remains synchronous and does not hide a
blocking async transport call. When the core is configured for
`AsyncShardTransport`, callers must use `RadarProcessingAsyncCoreSession`
instead. The core now exposes internal async shard work-item application to
infrastructure without adding a Domain -> Infrastructure dependency. Async
state updates preserve the same topology snapshot contract as the dispatcher,
and `RadarSourceProcessingStateStore` now uses atomic active-source counting so
parallel shard updates cannot lose first-activation counts. Custom processing
handlers are conservatively serialized during async shard application because
handler instances may not be thread-safe.

Async processing results now carry optional `RadarProcessingWorkerTelemetrySummary`
on `RadarProcessingResult`. Deterministic async workloads match synchronous
partitioned metrics and source snapshots, async output validates through
`RadarProcessingOutputValidator`, capacity failures reject without state
mutation while exposing worker telemetry, source-order violations return
invalid processing results without counting the batch complete, and owned
worker resources are disposed by the async session.

Latest verification after milestone 008 slice 11:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingAsyncCoreSessionTests|FullyQualifiedName~RadarProcessingContractTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingAsyncCoreSessionTests
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
22 passed for focused async core session and processing contract coverage.
6 passed for focused async core session coverage after handler/state guardrail.
Solution build succeeded with 0 warnings and 0 errors.
421 passed for processing-focused coverage.
576 passed, 3 skipped for the full test project.
```

Milestone 008 slice 12 is implemented in the current working tree. RadarPulse
now composes async processing with the milestone 007 rebalance control plane
through `RadarProcessingAsyncRebalanceSession` in
`RadarPulse.Infrastructure.Processing`. The async session owns or reuses a
`RadarProcessingAsyncCoreSession`, awaits completed async shard processing,
then passes the completed `RadarProcessingResult` into the same domain
rebalance path used by synchronous sessions.

`RadarProcessingRebalanceSession` now accepts `AsyncShardTransport` cores for
that shared control-plane path, but its public `Process(...)` remains
synchronous-only and throws for async cores with an explicit
`RadarProcessingAsyncRebalanceSession.ProcessAsync` message. This preserves the
no-hidden-blocking rule while keeping rebalance policy, pressure windows,
quarantine lifecycle, migration publication, hardening telemetry, and
validation in Domain. `RadarProcessingRebalanceSessionResult.WorkerTelemetry`
now exposes worker telemetry when the underlying processing result carries it.

Async rebalance tests cover successful one-batch processing against a single
topology snapshot, accepted migration publication only after worker completion,
failed async dispatch skipping rebalance planning/publication, result-level
worker telemetry plus existing hardening telemetry, and deterministic sync
versus async state/topology parity where expected.

Latest verification after milestone 008 slice 12:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingAsyncRebalanceSessionTests|FullyQualifiedName~RadarProcessingRebalanceSessionTests|FullyQualifiedName~RadarProcessingAsyncCoreSessionTests"
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
19 passed for focused async rebalance/session coverage.
Solution build succeeded with 0 warnings and 0 errors.
426 passed for processing-focused coverage.
581 passed, 3 skipped for the full test project.
```

Milestone 008 slice 13 is implemented in the current working tree. RadarPulse
now has explicit async validation contracts in Domain:
`RadarProcessingAsyncValidationError`,
`RadarProcessingAsyncValidationResult`, and
`RadarProcessingAsyncValidator`. The validator covers async processing result
invariants, rebalance result invariants, route/work-item/completion transport
diagnostics, worker telemetry retention bounds, and benchmark-profile
synchronous-versus-async checksum comparison markers.

Essential async validation is now wired into the runtime boundary:
`RadarProcessingAsyncCoreSession` validates every returned async processing
result before handing it back, and `RadarProcessingAsyncRebalanceSession`
validates that failed async processing does not publish rebalance artifacts.
The essential profile allows pre-dispatch invalid batches without worker
telemetry, but requires worker failure/cancellation/rejection propagation once
async dispatch has produced worker telemetry.

Diagnostic validation catches missing partition work, duplicate partition
assignment, shard ownership mistakes, completion scope mismatches, aggregation
metric mismatches, and processing telemetry parity issues. Benchmark validation
compares synchronous reference metrics/snapshots against async results and
returns the comparison checksums in the validation result.

Latest verification after milestone 008 slice 13:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingAsyncValidatorTests|FullyQualifiedName~RadarProcessingAsyncCoreSessionTests|FullyQualifiedName~RadarProcessingAsyncRebalanceSessionTests"
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
17 passed for focused async validation/runtime coverage.
Solution build succeeded with 0 warnings and 0 errors.
432 passed for processing-focused coverage.
587 passed, 3 skipped for the full test project.
```

Milestone 008 slice 14 is implemented in the current working tree. The
processing-only synthetic benchmark now supports
`RadarProcessingExecutionMode.AsyncShardTransport` through
`RadarProcessingAsyncCoreSession` while keeping the existing synchronous
`Measure(...)` API as a compatibility wrapper over an async-aware measurement
path. Async benchmark runs use retained workers, reset worker telemetry after
warmup, and report worker telemetry for measured iterations only.

`RadarProcessingBenchmarkResult` now carries the benchmark validation profile,
optional `RadarProcessingWorkerTelemetrySummary`, and optional
`RadarProcessingAsyncValidationResult`. Async synthetic benchmark validation
uses the new benchmark-profile async validator to compare a synchronous
partitioned reference run against the async run, including deterministic
processing checksums and source snapshots.

The CLI synthetic processing benchmark now accepts `--mode async` plus
`--workers` and `--queue-capacity` for the processing-only synthetic surface.
Output includes validation profile, worker counts, worker item/batch counters,
worker timing totals, async validation status, and sync/async comparison
checksums when async validation is present. Worker options are rejected for
non-async modes and must be positive.

Latest verification after milestone 008 slice 14:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingSyntheticBenchmarkTests|FullyQualifiedName~RadarPulseCliProcessingBenchmarkTests"
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
9 passed for focused synthetic benchmark and CLI async coverage.
Solution build succeeded with 0 warnings and 0 errors.
436 passed for processing-focused coverage.
591 passed, 3 skipped for the full test project.
```

Milestone 008 slice 15 is implemented in the current working tree. The
synthetic rebalance benchmark now exposes
`RadarProcessingExecutionMode.AsyncShardTransport` for all three benchmark
modes: static processing, pressure sampling only, and full rebalance session.
The existing synchronous `Measure(...)` entry points remain compatibility
wrappers over the async-aware measurement path.

Async synthetic rebalance runs use one retained
`RadarProcessingAsyncWorkerGroup` for the whole benchmark run while still
creating a fresh `RadarProcessingCore` or `RadarProcessingRebalanceSession` per
iteration. That preserves the behavioral benchmark contract where every
iteration sees the same starting topology and can publish the same deterministic
accepted/skipped rebalance decisions, without paying worker startup repeatedly
after warmup.

`RadarProcessingSyntheticRebalanceBenchmarkResult` now carries the execution
mode and optional `RadarProcessingWorkerTelemetrySummary`. Worker telemetry is
recorded for measured iterations only, validated against the configured
retention options, and reported alongside the existing rebalance counters,
validation checksum, retention mode, quarantine lifecycle settings, and
allocation attribution. `RadarProcessingSyntheticRebalanceWorkload` can now
create core options and rebalance sessions for either partitioned sync or async
transport.

The CLI `processing benchmark rebalance-synthetic` command now accepts
`--execution sync|async`, `--workers`, and `--queue-capacity`. Output prints the
actual execution mode and async worker telemetry when present. Worker options
are positive-only and rejected unless `--execution async` is selected.

Latest verification after milestone 008 slice 15:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmarkTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
29 passed for focused synthetic rebalance benchmark and CLI async coverage.
Solution build succeeded with 0 warnings and 0 errors.
439 passed for processing-focused coverage.
596 passed, 3 skipped for the full test project.
```

Milestone 008 slice 16 is implemented in the current working tree. The archive
rebalance benchmark now exposes
`RadarProcessingExecutionMode.AsyncShardTransport` for both `--file` and
`--cache` inputs across static processing, pressure sampling, and full
rebalance-session modes. The benchmark keeps the archive publisher callback
synchronous at the contract boundary: async processing dispatch blocks until
the worker completion barrier finishes, so leased `RadarEventBatch` values are
not retained after callback exit.

`RadarProcessingArchiveRebalanceBenchmarkResult` and
`RadarProcessingArchiveRebalanceCacheBenchmarkResult` now carry execution mode
and optional `RadarProcessingWorkerTelemetrySummary`. Async archive runs use one
retained `RadarProcessingAsyncWorkerGroup` for the benchmark run, while each
archive iteration owns its own processing/rebalance session. Worker telemetry is
recorded for measured iterations only and validated against the configured
retention options before result construction.

The CLI `processing benchmark rebalance-archive` command now accepts
`--execution sync|async`, `--workers`, and `--queue-capacity` for both file and
cache inputs. Output prints the actual execution mode and async worker
telemetry when present, while preserving separate archive end-to-end timing,
processing callback timing, callback allocation, and replay/batch-construction
allocation fields.

Latest verification after milestone 008 slice 16:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
32 passed for focused archive async benchmark, CLI, and allocation coverage.
Solution build succeeded with 0 warnings and 0 errors.
439 passed for processing-focused coverage.
600 passed, 3 skipped for the full test project.
```

Local corpus smoke after slice 16 used
`data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06` and cache
selection `data\nexrad --date 2026-05-04 --radar KTLX --max-files 1`, both in
rebalance-session mode with one iteration and no warmup. Single-file sync and
async produced the same topology/rebalance result: 1 batch, 32,400 stream
events, 38,759,040 payload values, 1 accepted direct-hot-relief move, validation
checksum `3_750_039_633_875_006_276`. The single-file callback was 169.57 ms
sync versus 190.10 ms async; callback allocation was 1,329,384 bytes sync
versus 1,371,224 bytes async. Cache `max-files 1` had the same deterministic
processing result; callback was 166.59 ms sync versus 181.06 ms async, and
callback allocation was 1,322,952 bytes sync versus 1,364,624 bytes async.
Async worker telemetry reported 4 workers, queue capacity 1, 1 dispatched and
completed batch, 4 submitted and completed work items, and 0 failed items for
both file and cache smoke runs.

Milestone 008 performance guardrail pass is captured in the current working
tree. The measured local cache contour is the full available KTLX base-data
selection for `2026-05-04`: `data\nexrad --date 2026-05-04 --radar KTLX
--max-files 220 --parallelism 24 --iterations 1 --warmup-iterations 0`. The
cache selection examined 220 files, skipped 22 non-published files, and
published 198 Archive Two base-data files per run.

Commands:

```powershell
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --execution sync --iterations 1 --warmup-iterations 0 --parallelism 24
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --execution async --workers 4 --queue-capacity 1 --iterations 1 --warmup-iterations 0 --parallelism 24
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode sampling --execution sync --iterations 1 --warmup-iterations 0 --parallelism 24
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode sampling --execution async --workers 4 --queue-capacity 1 --iterations 1 --warmup-iterations 0 --parallelism 24
```

Archive rebalance full-cache comparison:

```text
sync:  end-to-end 78,916.46 ms, callback 27,427.74 ms,
       callback allocation 260,599,080 bytes,
       processing payload values/s 279,311,694.78,
       checksum 7_480_064_646_096_449_000,
       accepted moves 2, skipped decisions 392, failed migrations 0.

async: end-to-end 78,334.34 ms, callback 27,428.21 ms,
       callback allocation 262,952,952 bytes,
       processing payload values/s 279,306,922.85,
       checksum 7_480_064_646_096_449_000,
       accepted moves 2, skipped decisions 392, failed migrations 0.
```

Rebalance interpretation: async preserved deterministic correctness and
rebalance behavior exactly. Callback elapsed time was effectively parity
(`+0.47 ms`, less than 0.01%). Callback allocation increased by 2,353,872 bytes
or about 0.90%. Async worker telemetry reported 4 workers, queue capacity 1,
198 dispatched/completed batches, 792 submitted/completed/succeeded work items,
0 failed work items, 26,752.31 ms total dispatch time, 70.14 ms queue wait,
1,368.89 ms worker execution, 5.10 ms aggregation, and 585.32 ms barrier wait.

Archive pressure-sampling full-cache comparison:

```text
sync:  end-to-end 78,196.31 ms, callback 27,512.23 ms,
       callback allocation 258,245,568 bytes,
       processing payload values/s 278,453,962.53,
       checksum 2_540_507_904_059_963_540,
       rebalance evaluations 198, accepted moves 0.

async: end-to-end 78,316.08 ms, callback 27,477.16 ms,
       callback allocation 260,567,328 bytes,
       processing payload values/s 278,809,294.52,
       checksum 2_540_507_904_059_963_540,
       rebalance evaluations 198, accepted moves 0.
```

Sampling interpretation: async preserved checksum and evaluation counts.
Callback elapsed time was also parity (`-35.07 ms`, about 0.13% faster in this
single run). Callback allocation increased by 2,321,760 bytes or about 0.90%.
Async worker telemetry reported 4 workers, queue capacity 1, 198
dispatched/completed batches, 792 submitted/completed/succeeded work items, 0
failed work items, 26,843.82 ms dispatch time, 52.30 ms queue wait, 1,154.00 ms
worker execution, 4.90 ms aggregation, and 772.19 ms barrier wait.

Guardrail conclusion: async archive processing is correct on the full local
KTLX cache contour and does not introduce meaningful callback latency
regression at 4 workers and queue capacity 1. The measurable cost is a stable
~0.9% processing callback allocation increase from async dispatch/telemetry
machinery. The end-to-end contour remains dominated by archive replay and batch
construction, so processing callback metrics should stay the primary comparison
surface for milestone 008.

Milestone 008 decision trace is written:
`docs/milestones/008-retained-async-shard-transport-decision-trace.md`. It
records the retained-workers-before-owned-payloads decision, callback completion
barrier semantics, one-in-flight borrowed-batch scope, replay/worker separation,
synchronous reference oracle, deterministic topology snapshot and aggregation,
bounded mailboxes, failure/cancellation/timeout semantics, rebalance after
async completion, worker telemetry, benchmark interpretation, CLI surface, and
deferred owned-payload/provider-queue work.

Milestone 008 closeout is written:
`docs/milestones/008-retained-async-shard-transport-closeout.md`. It records
implemented slices, verification results, processing-only sync versus async
comparison, synthetic rebalance sync versus async comparison, single-file
archive smoke, full-cache archive guardrail, worker telemetry, allocation
interpretation, deferred work, and the next milestone input. The closeout keeps
the important performance distinction explicit: small synthetic contours show
visible async scheduler overhead, while the production-shaped full-cache archive
callback contour shows sync/async latency parity and about `0.90%` additional
callback allocation.

Milestone 007 slice 1 is implemented in the current working tree. RadarPulse
now has the first hardening option/profile contracts:
`RadarProcessingRebalanceHardeningOptions`,
`RadarProcessingTelemetryRetentionOptions`,
`RadarProcessingQuarantineLifecycleOptions`,
`RadarProcessingValidationProfile`, and
`RadarProcessingDiagnosticRetentionMode`. The contracts define deterministic
defaults for bounded recent retention, quarantine TTL/cooling/material-pressure
change thresholds, and diagnostic validation profile selection. Focused tests
cover defaults, stable enum values, invalid values, counters-only zero detail
retention, and independence between retention mode and validation profile.

Milestone 007 slice 2 is implemented in the current working tree. RadarPulse
now has bounded rebalance telemetry contracts:
`RadarProcessingRebalanceTelemetrySummary`,
`RadarProcessingRebalanceTelemetryCounters`,
`RadarProcessingRebalanceSkippedReasonCounter`,
`RadarProcessingRebalanceRecentDecision`,
`RadarProcessingRebalanceRecentAcceptedMove`,
`RadarProcessingRebalanceRecentValidationFailure`, and
`RadarProcessingRebalanceRetentionStats`. The contracts preserve compact
numeric detail, stable enum/code fields, defensive collection copies, dropped
detail counters, and projection helpers from existing rebalance decisions and
validation results. Focused tests cover invalid shapes, immutable/copy-safe
summary behavior, recent accepted move projection, recent validation failure
projection, retention stats, and empty summary behavior.

Milestone 007 slice 3 is implemented in the current working tree. RadarPulse
now has a bounded telemetry recorder foundation:
`RadarProcessingBoundedTelemetryWindow<T>` and
`RadarProcessingRebalanceTelemetryRecorder`. The recorder consumes existing
rebalance decisions and validation results, updates aggregate counters, stores
capped recent decisions/accepted moves/validation failures, counts dropped
detail, supports counters-only retention, tracks quarantine lifecycle counters,
and emits immutable `RadarProcessingRebalanceTelemetrySummary` snapshots.
Focused tests cover bounded-window overflow, zero-capacity counters-only
behavior, decision aggregation, accepted move aggregation, skipped-reason
aggregation, validation failure aggregation, snapshot stability, reset, and
invalid input.

Milestone 007 slice 4 is implemented in the current working tree. RadarPulse
now has quarantine lifecycle state and transition contracts:
`RadarProcessingQuarantineEffectiveClassification`,
`RadarProcessingQuarantineTransitionReason`,
`RadarProcessingQuarantineEvidence`,
`RadarProcessingQuarantineTransition`, and
`RadarProcessingQuarantineLifecycleState`. The contracts retain compact
numeric evidence only: partition/shard ids, topology version, logical
evaluation sequence, baseline/latest pressure, pressure band, sustained cooling
sample count, and effective classification. The lifecycle state supports
entering quarantine, recording cooled or hot samples, marking retry eligibility,
clearing quarantine, and re-entering quarantine with fresh evidence. Focused
tests cover stable enum values, evidence validation, quarantine baseline
recording, cooling/hot sample behavior, retry eligibility, clearing, re-entry,
transition telemetry, mismatched evidence, and out-of-order evidence.

Milestone 007 slice 5 is implemented in the current working tree. RadarPulse
now has a deterministic quarantine lifecycle evaluator:
`RadarProcessingQuarantineLifecycleEvaluator` and
`RadarProcessingQuarantineLifecycleEvaluationResult`. The evaluator advances
compact partition evidence before planning, records current non-quarantine
classification, enters quarantine, clears after sustained cooling, marks stale
quarantine evidence retry-eligible after TTL expiry or material pressure
change, and supports retry re-entry with fresh baseline pressure. Focused tests
cover default options, active quarantine entry, insufficient cooling, sustained
cooling clear, hot-sample cooling reset, TTL retry, material pressure increase
and drop retry, immaterial pressure changes, retry re-entry, clearing to
observed effective classification, transition result validation, and invalid
inputs.

Latest verification after milestone 007 slice 5:

```powershell
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingQuarantineLifecycle"
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
31 passed for the focused quarantine lifecycle suite.
428 passed, 3 skipped for the full solution suite.
```

Milestone 007 slice 6 is implemented in the current working tree. RadarPulse
now has planner integration for lifecycle-effective classification:
`RadarProcessingQuarantineLifecycleTracker` owns per-partition lifecycle state
and feeds compact pressure evidence through the evaluator. Direct hot relief can
consume the tracker alongside the existing hot-partition classifier: active
quarantine blocks direct movement with an explicit `PartitionQuarantined`
skipped reason, retry-eligible partitions can be reconsidered under normal
policy gates, and retry failure re-enters quarantine with fresh evidence. Cold
evacuation accepts the same tracker for compatible fallback planning while
remaining available when direct hot relief is blocked or unsafe. Focused tests
cover active quarantine blocking, retry-eligible reconsideration, retry
re-entry, stale quarantine clear without stale skipped reasons, lifecycle-aware
cold evacuation fallback, tracker state updates, and invalid tracker inputs.

Latest verification after milestone 007 slice 6:

```powershell
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingQuarantineLifecycle|FullyQualifiedName~RadarProcessingDirectHotReliefPlanner|FullyQualifiedName~RadarProcessingColdEvacuationPlanner"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing"
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
57 passed for focused lifecycle/direct/cold planner coverage.
289 passed for processing-focused coverage.
438 passed, 3 skipped for the full solution suite.
```

Milestone 007 slice 7 is implemented in the current working tree. RadarPulse
now advances quarantine lifecycle inside `RadarProcessingRebalanceSession`
before direct hot relief and cold evacuation planning. The session owns a
`RadarProcessingQuarantineLifecycleTracker`, passes it to lifecycle-aware
planners, drains per-evaluation lifecycle transitions, and exposes those
transitions through `RadarProcessingRebalanceSessionResult.QuarantineTransitions`.
The tracker now has explicit transition drain behavior so session results can
report current-evaluation transitions without retaining old detail. Focused
tests cover active quarantine blocking before planning, TTL retry becoming
eligible in the same evaluation, retry re-entry when no safe target exists,
invalid processing results leaving lifecycle untouched, and transition drain
semantics.

Latest verification after milestone 007 slice 7:

```powershell
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRebalanceSession|FullyQualifiedName~RadarProcessingQuarantineLifecycleTracker"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing"
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
11 passed for focused session/tracker coverage.
292 passed for processing-focused coverage.
441 passed, 3 skipped for the full solution suite.
```

Milestone 007 slice 7 telemetry wiring follow-up is implemented in the current
working tree. `RadarProcessingRebalanceSession` now owns a
`RadarProcessingRebalanceTelemetryRecorder`, records direct and cold rebalance
decisions, records drained quarantine lifecycle transitions, and exposes the
immutable session snapshot through
`RadarProcessingRebalanceSessionResult.TelemetrySummary`. Lifecycle transition
detail is retained through the bounded
`RadarProcessingRebalanceRecentLifecycleTransition` contract, while
`RadarProcessingBoundedTelemetryWindow<T>.CanRetain` and `Drop()` let
counters-only or zero-retention paths count dropped detail without constructing
unneeded recent-detail objects. At this point validation-failure recording was
left for validation profile integration so that validation cost and summary
snapshot timing would stay explicit.

Latest verification after milestone 007 slice 7 telemetry wiring:

```powershell
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRebalanceTelemetry|FullyQualifiedName~RadarProcessingRebalanceSession"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing"
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
41 passed for focused telemetry/session coverage.
296 passed for processing-focused coverage.
445 passed, 3 skipped for the full solution suite.
```

Milestone 007 slice 8 is implemented in the current working tree. RadarPulse now
has profile-aware rebalance session validation. `RadarProcessingRebalanceSession`
uses `RadarProcessingRebalanceHardeningOptions`, exposes the active
`ValidationProfile`, validates session results through that profile, records
validation failures into the bounded telemetry recorder before the final
`TelemetrySummary` snapshot, and passes the precomputed validation result into
`RadarProcessingRebalanceSessionResult` so result construction does not need a
second validation pass. `RadarProcessingRebalanceValidator` now supports
`Off`, `Essential`, `Diagnostic`, and `Benchmark` session validation paths:
`Off` skips read-side diagnostics, `Essential` checks migration and handoff
failures without full pressure/telemetry diagnostics, and `Diagnostic` plus
`Benchmark` preserve the existing milestone 006 read-side behavior. The
telemetry recorder now avoids constructing recent validation-failure detail when
the retention window cannot keep it.

Latest verification after milestone 007 slice 8:

```powershell
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRebalanceValidator|FullyQualifiedName~RadarProcessingRebalanceSession|FullyQualifiedName~RadarProcessingRebalanceTelemetryRecorder"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing"
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
32 passed for focused validator/session/recorder coverage.
301 passed for processing-focused coverage.
450 passed, 3 skipped for the full solution suite.
```

Milestone 007 slice 9 is implemented in the current working tree. RadarPulse now
has allocation attribution contracts and benchmark result surfaces:
`RadarProcessingBenchmarkAllocationSnapshot` and
`RadarProcessingRebalanceAllocationSummary`. Synthetic rebalance benchmark
results report validation profile, telemetry retention mode, and processing-only
allocation summary. Archive rebalance benchmark results keep the existing
end-to-end `AllocatedBytes` field for compatibility while also exposing
processing callback allocation and replay/batch-construction allocation
separately. CLI output prints validation profile, retention mode, allocation
scope, and callback/replay allocation fields so static, sampling, and rebalance
contours can be compared without confusing archive replay cost with processing
callback cost.

Latest verification after milestone 007 slice 9:

```powershell
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRebalanceAllocationSummary|FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmark|FullyQualifiedName~RadarPulseCliRebalanceBenchmark"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing"
dotnet test RadarPulse.sln --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Result:

```text
24 passed for focused allocation/benchmark/CLI coverage.
311 passed for processing-focused coverage.
460 passed, 3 skipped for the full solution suite.
Release build succeeded with 0 warnings and 0 errors.
```

Latest Release cache performance smoke after milestone 007 slice 9:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- processing benchmark rebalance-archive --cache data/nexrad --radar KTLX --max-files 20 --mode all --partitions 24 --shards 4 --iterations 1 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Result on the local KTLX cache slice:

```text
20 examined files, 18 published base-data files, 2 skipped files.
Static:    451.46M end-to-end payload values/s, 3.13B processing payload values/s, 0.03 callback allocated bytes/payload.
Sampling:  454.95M end-to-end payload values/s, 3.34B processing payload values/s, 0.03 callback allocated bytes/payload.
Rebalance: 462.49M end-to-end payload values/s, 3.43B processing payload values/s, 0.03 callback allocated bytes/payload.
```

Exploratory full-cache logical-source histogram after milestone 007 slice 9:

```text
244 examined files, 220 published base-data files, 24 skipped files.
23_040 possible logical sources.
6_480 active logical sources.
16_560 inactive logical sources.
7_114_560 stream events.
10_599_423_360 payload values.
Average stream events per active logical source: 1_097.926.
Min/median/max stream events per active logical source: 660 / 1_100 / 1_540.
```

Interpretation: the current pipeline keeps logical-source processing compact.
`SourceId` is `radarOrdinal/elevationSlot/azimuthBucket/rangeBand`; `momentId`
is not part of `SourceId`. The full local cache therefore carries 10.6B payload
values through 7.1M stream events over 6,480 active logical sources rather than
materializing every gate value as a separate logical source event.

Milestone 007 slice 10 is implemented in the current working tree. RadarPulse
now has a first allocation-reduction pass over the rebalance control plane and
benchmark aggregation paths. Synthetic and archive rebalance benchmark telemetry
now creates skipped-reason and accepted-move pressure lists only when those
details exist, so static/sampling and no-move archive callback paths avoid
empty `List<T>` churn. Rebalance policy evaluation now avoids allocating a
rejection list for allowed moves. Empty bounded-window, telemetry-summary,
session-result, decision, and policy-result snapshots return shared empty
arrays while non-empty public snapshots remain immutable or defensively copied.
Skipped-reason counter snapshot creation now avoids the previous LINQ ordering
path. The accepted-move benchmark aggregation regression guardrail is tightened
from 400 MB to 250 MB for the 3,000-iteration sample path and now uses one
warmup iteration so the Debug unit test tracks normal benchmark command
semantics without becoming full-suite-order sensitive.

Latest verification after milestone 007 slice 10:

```powershell
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~AcceptedMovePressureAggregationDoesNotCopyPreviousIterations|FullyQualifiedName~RadarProcessingRebalancePolicy|FullyQualifiedName~RadarProcessingRebalanceTelemetry|FullyQualifiedName~RadarProcessingRebalanceDecision|FullyQualifiedName~RadarProcessingRebalanceSession"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing"
dotnet test RadarPulse.sln --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Result:

```text
65 passed for focused allocation/policy/telemetry/decision/session coverage.
311 passed for processing-focused coverage.
460 passed, 3 skipped for the full solution suite.
Release build succeeded with 0 warnings and 0 errors.
```

Latest Release cache performance smoke after milestone 007 slice 10:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- processing benchmark rebalance-archive --cache data/nexrad --radar KTLX --max-files 20 --mode all --partitions 24 --shards 4 --iterations 1 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Result on the local KTLX cache slice:

```text
20 examined files, 18 published base-data files, 2 skipped files.
Static:    461.83M end-to-end payload values/s, 3.20B processing payload values/s, 0.03 callback allocated bytes/payload.
Sampling:  471.18M end-to-end payload values/s, 3.34B processing payload values/s, 0.03 callback allocated bytes/payload.
Rebalance: 464.80M end-to-end payload values/s, 3.33B processing payload values/s, 0.03 callback allocated bytes/payload.
```

Additional synthetic allocation smoke after milestone 007 slice 10:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- processing benchmark rebalance-synthetic --workload hot-shard --mode all --iterations 1000 --warmup-iterations 100
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- processing benchmark rebalance-synthetic --workload hot-shard --mode rebalance --iterations 3000 --warmup-iterations 0
```

Result:

```text
1,000-iteration hot-shard rebalance-session allocation moved from 25,324,232 bytes in the pre-domain-pass smoke to 23,700,232 bytes after the allowed-policy/empty-snapshot reductions.
3,000-iteration hot-shard rebalance-session allocated 71,210,472 bytes in Release smoke, below the Debug unit-test guardrail and well below the old 400 MB bound.
```

Milestone 007 slice 11 is implemented in the current working tree. RadarPulse
now has deterministic synthetic quarantine lifecycle workloads over the full
rebalance session path:
`QuarantineTtlRetry`, `QuarantineSustainedCoolingClear`,
`QuarantinePressureChangeRetry`, `QuarantineRetryReentry`, and
`QuarantineSuccessfulReliefClear`. The workload catalog can now carry
workload-specific hardening options so lifecycle TTL, sustained-cooling, and
material-pressure-change thresholds are deterministic per scenario. Synthetic
workload results expose final quarantine lifecycle states, final telemetry
summary, quarantine lifecycle counters, and transition counting helpers. The
synthetic benchmark honors workload-default hardening options when callers do
not override them, and the CLI accepts the new workload names through
`processing benchmark rebalance-synthetic`.

Latest verification after milestone 007 slice 11:

```powershell
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingSyntheticRebalanceWorkloadTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmark|FullyQualifiedName~RadarPulseCliRebalanceBenchmark"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing"
dotnet test RadarPulse.sln --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Result:

```text
18 passed for focused synthetic workload and CLI coverage.
19 passed for focused synthetic benchmark and CLI coverage.
316 passed for processing-focused coverage.
466 passed, 3 skipped for the full solution suite.
Release build succeeded with 0 warnings and 0 errors.
```

Latest Release lifecycle workload smoke after milestone 007 slice 11:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- processing benchmark rebalance-synthetic --workload quarantine-successful-relief-clear --mode rebalance --iterations 3 --warmup-iterations 1
```

Result:

```text
Validation succeeded.
3 iterations, 2 batches per iteration, 6 rebalance evaluations.
3 accepted direct hot relief moves, 0 cold evacuation moves, 0 failed migrations.
Skipped reasons: partition-quarantined, cold-evacuation-insufficient-benefit.
Allocated bytes: 87,552; allocation includes CLI formatting: no.
```

Milestone 007 slice 12 is implemented in the current working tree. RadarPulse
now has deterministic synthetic retention stress workloads:
`LongNoHotShard`, `LongCooldownRejection`, `LongUnsafeTargetRejection`,
`LongMixedSkippedReasons`, and `CountersOnlyRetention`. The workloads run
longer repeated rebalance-session contours that stress retained decision detail,
skipped-reason counters, cooldown rejections, unsafe target rejection, mixed
skip aggregation, and counters-only retention. Workload results now expose
retention stats and skipped-reason counting helpers so tests can assert that
detail remains bounded while aggregate counters remain correct.

Milestone 007 archive retention benchmark gate is implemented in the current
working tree. `processing benchmark rebalance-archive` now accepts
`--retention-mode counters|recent|diagnostic`,
`--max-retained-decisions`, `--max-retained-transitions`,
`--max-retained-accepted-moves`, and
`--max-retained-validation-failures`. Archive benchmark results now expose
retention limits plus retained/dropped detail counters, and CLI output prints
those fields for both single-file and cache-wide archive contours. The new
result fields are explicit properties so IDE design-time analysis does not
depend on primary-constructor record member synthesis.

Latest verification after milestone 007 slice 12 and archive retention gate:

```powershell
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingSyntheticRebalanceWorkloadTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmark|FullyQualifiedName~RadarPulseCliRebalanceBenchmark"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing|FullyQualifiedName~Presentation"
dotnet test RadarPulse.sln --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Result:

```text
25 passed for focused synthetic workload and CLI coverage.
21 passed for focused synthetic benchmark and CLI coverage.
330 passed for processing/presentation-focused coverage.
473 passed, 3 skipped for the full solution suite.
Release build succeeded with 0 warnings and 0 errors.
```

Latest Release full-cache retention comparison after milestone 007 slice 12:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --partitions 24 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse --retention-mode recent
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --partitions 24 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse --retention-mode counters
```

Result on the local full cache:

```text
244 examined files, 220 published base-data files, 24 skipped files.
8,513,587,200 payload values, 7,114,560 stream events.
Recent:   validation succeeded, 128 retained decisions, 310 dropped decision details, 2 accepted moves, 0 failed migrations, 3.23B processing payload values/s, 0.03 callback allocated bytes/payload.
Counters: validation succeeded, 0 retained decisions, 438 dropped decision details, 0 retained accepted moves, 2 dropped accepted move details, 2 accepted moves, 0 failed migrations, 3.28B processing payload values/s, 0.03 callback allocated bytes/payload.
Both modes produced the same validation checksum and accepted only two cautious direct-hot-relief moves on the full cache.
```

Milestone 007 slice 13 is implemented in the current working tree. Archive
rebalance benchmark results now expose counted skipped-reason telemetry through
`SkippedReasonCounters` for both file and cache runs, and CLI output prints
those counters alongside the existing distinct skipped-reason set. The archive
aggregation path counts skipped reasons directly from decisions, so the
diagnostic survives counters-only retention and does not depend on retained
decision detail.

Latest verification after milestone 007 slice 13:

```powershell
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing|FullyQualifiedName~Presentation"
dotnet test RadarPulse.sln --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Result:

```text
15 passed for focused archive allocation/result and CLI coverage.
330 passed for processing/presentation-focused coverage.
473 passed, 3 skipped for the full solution suite.
Release build succeeded with 0 warnings and 0 errors.
```

Latest Release full-cache skipped-reason counter smoke after milestone 007
slice 13:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --partitions 24 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse --retention-mode counters
```

Result on the local full cache:

```text
244 examined files, 220 published base-data files, 24 skipped files.
220 rebalance evaluations, 2 accepted direct-hot-relief moves, 436 skipped decisions, 0 failed migrations.
Skipped reason counters: no-hot-shard=420, no-cold-target-shard=4, source-shard-move-budget-exhausted=12, global-move-budget-exhausted=12.
Validation succeeded with the same checksum as the slice 12 recent/counters comparison.
Processing callback throughput was 2.68B payload values/s with 0.03 callback allocated bytes/payload.
```

Milestone 007 slice 14 is implemented in the current working tree. RadarPulse
now has a benchmark-only pressure skew overlay for archive rebalance runs:
`RadarProcessingPressureSkewProfile`, `RadarProcessingPressureSkewOptions`,
and `RadarProcessingPressureSkewTransformer`. The overlay keeps archive payload
and observed processing telemetry unchanged, while feeding an effective
synthetic pressure sample into pressure windows and rebalance planning. This
lets real archive replay exercise more active rebalance contours without
rewriting cached files or confusing observed telemetry validation. Supported
profiles are `none`, `hot-shard`, `rotating-hot-shard`, `hot-partition`,
`target-starvation`, and `budget-storm`.

`processing benchmark rebalance-archive` now accepts:

```text
--skew-profile none|hot-shard|rotating-hot-shard|hot-partition|target-starvation|budget-storm
--skew-factor n
--skew-period n
```

Archive benchmark results and CLI output mark whether the synthetic pressure
overlay is active and print the active profile, factor, and period.

Latest verification after milestone 007 slice 14:

```powershell
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingPressureSkewTransformerTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing|FullyQualifiedName~Presentation"
dotnet test RadarPulse.sln --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Result:

```text
21 passed for focused skew/CLI/allocation coverage.
336 passed for processing/presentation-focused coverage.
479 passed, 3 skipped for the full solution suite.
Release build succeeded with 0 warnings and 0 errors.
```

Latest Release full-cache pressure-skew smoke after milestone 007 slice 14:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --partitions 96 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse --retention-mode counters --skew-profile hot-shard
```

Result on the local full cache:

```text
244 examined files, 220 published base-data files, 24 skipped files.
8,513,587,200 payload values, 7,114,560 stream events.
Synthetic pressure overlay: yes; profile: hot-shard; factor: 1.00; period: 8.
220 rebalance evaluations, 20 accepted direct-hot-relief moves, 400 skipped decisions, 0 failed migrations.
Skipped reason counters: no-hot-shard=128, source-shard-move-budget-exhausted=272, target-shard-receive-budget-exhausted=272, global-move-budget-exhausted=272.
Validation succeeded. Processing callback throughput was 3.26B payload values/s with 0.04 callback allocated bytes/payload.
```

Milestone 007 slice 15 is implemented in the current working tree. The
rebalance benchmark CLI now exposes validation profile selection for both
synthetic and archive benchmark contours:

```text
processing benchmark rebalance-synthetic --validation-profile off|essential|diagnostic|benchmark
processing benchmark rebalance-archive --validation-profile off|essential|diagnostic|benchmark
```

The default remains `diagnostic`, so existing benchmark commands keep their
previous behavior. For synthetic workloads, the CLI override only changes the
validation profile and preserves each workload's own hardening defaults,
including retention stress and quarantine lifecycle options. This protects the
slice 11-12 workload guarantees while making validation cost directly
measurable from the CLI. Archive benchmark hardening now carries the selected
validation profile alongside existing telemetry retention options.

Latest verification after milestone 007 slice 15:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing|FullyQualifiedName~Presentation"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
dotnet build RadarPulse.sln --configuration Release --no-restore
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload counters-only-retention --mode rebalance --validation-profile off --iterations 1 --warmup-iterations 0
```

Result:

```text
29 passed for focused CLI/synthetic/allocation coverage.
337 passed for processing/presentation-focused coverage.
480 passed, 3 skipped for the full test suite.
Release build succeeded with 0 warnings and 0 errors.
Release CLI smoke printed Validation profile: off and Telemetry retention mode: counters.
```

Milestone 007 slice 16 is implemented in the current working tree. The
rebalance benchmark CLI now exposes quarantine lifecycle tuning for synthetic
and archive benchmark contours:

```text
--quarantine-ttl-evaluations n
--quarantine-sustained-cooling-samples n
--quarantine-material-pressure-change n
```

The flags are additive and optional. For `rebalance-synthetic`, partial
overrides are merged into the selected workload's existing hardening options,
so workload-specific retention and quarantine lifecycle defaults are preserved
unless a particular lifecycle value is explicitly overridden. For
`rebalance-archive`, the overrides are merged into the default quarantine
lifecycle options alongside the selected validation profile and telemetry
retention settings. Synthetic and archive benchmark result contracts, plus CLI
output, now print the effective quarantine TTL, sustained-cooling sample count,
and material pressure-change threshold.

Latest verification after milestone 007 slice 16:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing|FullyQualifiedName~Presentation"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
dotnet build RadarPulse.sln --configuration Release --no-restore
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload quarantine-ttl-retry --mode sampling --quarantine-sustained-cooling-samples 7 --iterations 1 --warmup-iterations 0
```

Result:

```text
30 passed for focused CLI/synthetic/allocation coverage.
338 passed for processing/presentation-focused coverage.
481 passed, 3 skipped for the full test suite.
Release build succeeded with 0 warnings and 0 errors.
Release CLI smoke printed Quarantine TTL evaluations: 1, Quarantine sustained cooling samples: 7,
and Quarantine material pressure change: 1.00.
```

Milestone 007 slice 17 is implemented in the current working tree. The policy
default audit is written in
`docs/milestones/007-rebalance-production-hardening-policy-default-audit.md`,
and the implementation plan now links to it from the policy-default audit
section.

Audit decision:

```text
No code default changes are required before closeout.
Current defaults are accepted as conservative, bounded, and observable.
Validation remains diagnostic by default for tests and closeout benchmarks.
Telemetry retention remains recent-detail with bounded caps.
Quarantine lifecycle remains 64 TTL evaluations, 3 sustained cooling samples,
and 0.25 material pressure-change threshold.
Archive pressure skew remains disabled unless explicitly requested.
Release comparison commands should keep passing explicit topology, parallelism,
retention, validation, and skew settings.
```

Latest verification after milestone 007 slice 17:

```powershell
git diff --check
```

Result:

```text
No whitespace errors. Tests were not rerun because this slice is documentation-only.
```

Milestone 007 slice 18 is implemented in the current working tree. The decision
trace is written in
`docs/milestones/007-rebalance-production-hardening-decision-trace.md`, and the
implementation plan now records that decision trace is complete while closeout
remains pending.

Decision trace coverage:

```text
why hardening preceded async worker transport
temporary evidence-based quarantine lifecycle
bounded telemetry retention
validation profile split
allocation attribution and allocation reduction decisions
synthetic workload interpretation
archive pressure skew as benchmark-only overlay
policy defaults remaining conservative after audit
additive CLI hardening surface
final performance gate contour separation
remaining risks and deferred async worker transport
```

Latest verification after milestone 007 slice 18:

```powershell
git diff --check
```

Result:

```text
No whitespace errors. Tests were not rerun because this slice is documentation-only.
```

## Milestone Status

Done:

- `001` historical archive loader is complete.
- `002` NEXRAD archive inspection/decoder foundation is complete.
- `003` historical replay publisher foundation is complete.
- `004` processing-core input contract architecture is scoped.
- `004` processing-core input contract implementation plan is complete.
- `004` slice 1 contract types and version constants are implemented.
- `004` slice 2 append-only dense identity catalog is implemented.
- `004` slice 3 dictionary version snapshots and deltas are implemented.
- `004` slice 4 canonicalization and error policy is implemented.
- `004` slice 5 source universe definition is implemented.
- `004` slice 6 identity normalizer is implemented.
- `004` slice 7 batch builder and payload storage is implemented.
- `004` slice 8 single-file sequential replay integration is implemented.
- `004` slice 9 batch validation and checksum metrics are implemented.
- `004` ordered-parallel batch replay parity is implemented.
- `004` normalized batch stream CLI smoke command is implemented.
- `004` normalized batch stream benchmark command is implemented.
- `004` first parallel stream buffer-churn reduction pass is implemented.
- `004` stream identity-cache and batch pre-sizing optimization pass is
  implemented.
- `004` no-copy batch finalization and cached payload counters are implemented
  for builder-owned normalized stream batches.
- `004` normalized batch stream cache benchmark command is implemented and
  verified against the full local cache.
- `004` reusable normalized batch publish session is implemented for stream
  benchmarks.
- `004` leased hot-path batch delivery is implemented for reusable stream
  sessions, with explicit owned snapshot conversion for retained batches.
- `004` normalized stream throughput now exceeds the milestone 003 count-only
  replay-publish baseline on the comparable payload-value metric.
- `004` processing-core input contract milestone is closed.
- `005` processing-core architecture is complete.
- `005` processing-core implementation plan is complete.
- `005` processing-core contracts are implemented and tested.
- `005` static partition topology is implemented and tested.
- `005` dense source-local state store is implemented and tested.
- `005` processing payload reader helpers are implemented and tested.
- `005` sequential processing core baseline is implemented and tested.
- `005` sequential lifetime and parity guardrails are tested.
- `005` partitioned batch routing substrate is implemented and tested.
- `005` first synchronous `PartitionedBarrier` execution path is implemented
  and tested.
- `005` partitioned telemetry and route-summary validation are implemented and
  tested.
- `005` processing-output validation helpers are implemented and tested.
- `005` source-local handler slot model is implemented and tested.
- `005` synthetic processing-only benchmark harness is implemented and tested.
- `005` synthetic processing CLI benchmark command is implemented and
  smoke-tested.
- `005` Release processing-only benchmark baseline is captured for sequential
  and partitioned synthetic modes, with and without the counter/checksum
  handler workload.
- `005` processing-core decision trace and closeout are written.
- `006` partition-level shard rebalance architecture is complete.
- `006` partition-level shard rebalance implementation plan is complete.
- `006` slice 1 versioned topology contracts and publication boundary are
  implemented and tested.
- `006` slice 2 route topology-version integration is implemented and tested.
- `006` slice 3 pressure sample and pressure score contracts are implemented
  and tested.
- `006` slice 4 pressure window and hysteresis tracking is implemented and
  tested.
- `006` slice 5 anti-churn policy state is implemented and tested.
- `006` slice 6 rebalance decision and skipped-reason telemetry contracts are
  implemented and tested.
- `006` slice 7 direct hot relief planner is implemented and tested.
- `006` slice 8 intrinsic hot partition classification is implemented and
  tested.
- `006` slice 9 cold evacuation planner is implemented and tested.
- `006` slice 10 migration lifecycle and coordinator is implemented and
  tested.
- `006` slice 11 state handoff validation is implemented and tested.
- `006` slice 12 rebalance-aware processing loop is implemented and tested.
- `006` slice 13 rebalance validation helpers are implemented and tested.
- `006` slice 14 synthetic rebalance workloads are implemented and tested.
- `006` slice 15 synthetic rebalance benchmarks are implemented and tested.
- `006` slice 16 synthetic rebalance CLI benchmark command is implemented and
  smoke-tested.
- `006` Release synthetic rebalance benchmark numbers are captured and compared
  against same-run static baselines and the milestone 005 processing-only
  baseline.
- `006` real-data rebalance archive smoke benchmark command is implemented and
  verified against a local KTLX Archive Two file.
- `006` real-data rebalance archive cache-wide benchmark is captured and
  compared with milestone 005 processing-only throughput.
- `006` partition-level shard rebalance decision trace is written.
- `006` partition-level shard rebalance closeout is written.
- `007` rebalance production hardening architecture is complete.
- `007` rebalance production hardening implementation plan is complete.
- `007` slice 1 hardening options and validation/retention profile contracts
  are implemented and tested.
- `007` slice 2 bounded rebalance telemetry contracts are implemented and
  tested.
- `007` slice 3 telemetry recorder and bounded retention windows are
  implemented and tested.
- `007` slice 4 quarantine lifecycle state and transition contracts are
  implemented and tested.
- `007` slice 5 quarantine lifecycle evaluator is implemented and tested.
- `007` slice 6 planner integration for lifecycle-effective classification is
  implemented and tested.
- `007` slice 7 session lifecycle integration and transition result surfaces
  are implemented and tested.
- `007` slice 7 session-level hardening telemetry summary wiring is implemented
  and tested.
- `007` slice 8 validation profiles are implemented and tested.
- `007` slice 9 allocation attribution baseline is implemented, tested, and
  smoke-checked against the local KTLX cache.
- `007` slice 10 allocation reduction pass is implemented, tested, and
  smoke-checked against synthetic hot-shard and local KTLX cache contours.
- `007` slice 11 synthetic quarantine lifecycle workloads are implemented,
  tested, and smoke-checked through the CLI benchmark path.
- `007` slice 12 synthetic retention stress workloads are implemented and
  tested.
- `007` archive rebalance benchmark retention options, retention result
  fields, CLI output, and full-cache recent/counters comparison gate are
  implemented and tested.
- `007` slice 13 archive skipped-reason counters are implemented, tested, and
  smoke-checked against the full local cache.
- `007` slice 14 archive benchmark pressure skew overlay is implemented,
  tested, and smoke-checked against the full local cache.
- `007` slice 15 validation profile CLI options are implemented and tested for
  synthetic and archive rebalance benchmark commands.
- `007` slice 16 quarantine lifecycle CLI options are implemented and tested
  for synthetic and archive rebalance benchmark commands.
- `007` slice 17 policy-default audit is written and linked from the milestone
  007 implementation plan.
- `007` slice 18 decision trace is written and linked from the milestone 007
  implementation plan.
- `007` closeout is written, the final comprehensive performance comparison is
  captured and interpreted, the implementation plan completion criteria are
  checked off, and this handoff now records the closed milestone 007 baseline.
- `008` retained async shard transport milestone is complete.
- `009` owned payload provider decoupling milestone is complete.
- `010` owned provider overlap and cost reduction milestone is complete.
- `011` queued-owned default-readiness architecture is drafted.
- `011` queued-owned default-readiness implementation plan is drafted.
- `011` slice 1 baseline readiness audit and candidate contour freeze is
  complete.
- `011` slice 2 retained-resource pressure contracts are implemented and
  tested.
- `011` slice 3 provider queue telemetry compatibility extensions are
  implemented and tested.
- `011` slice 4 active consumer retained-resource lifecycle integration is
  implemented and tested.
- `011` slice 5 overlap telemetry and benchmark result propagation is
  implemented and tested.
- `011` slice 6 candidate configuration surface is implemented and tested.
- `011` slice 7 readiness validation and gate contracts are implemented and
  tested.
- `011` slice 8 failure, cancellation, and cleanup gate coverage is
  implemented and tested.
- `011` slice 9 CLI and operator telemetry output is implemented and tested.
- `011` slice 10 natural Release gate matrix is captured, interpreted, and
  documented.
- `011` slice 11 retained payload allocation optimization is implemented,
  measured, and documented.
- `011` slice 12 controlled proof separation hardening is implemented and
  tested.
- `011` slice 13 decision trace, closeout, and final handoff are complete.
- `archive list` supports one radar and explicit `--all-radars`.
- Manifest summary output and JSON write/read are implemented.
- `archive download` supports live AWS listing and saved manifests.
- Saved manifest download can be filtered with `--radar`, `--max-files`, and
  `--max-bytes`.
- Download concurrency, retry/backoff, Ctrl+C cancellation, temp-file writes,
  deterministic cache paths, metadata sidecars, skip/redownload behavior, and
  free-space preflight are implemented.
- Standard unit tests and opt-in live AWS integration tests covered the loader
  milestone at handoff time.

Closed milestone 008 baseline:

- `docs/milestones/008-retained-async-shard-transport.md`,
  `docs/milestones/008-retained-async-shard-transport-plan.md`,
  `docs/milestones/008-retained-async-shard-transport-decision-trace.md`, and
  `docs/milestones/008-retained-async-shard-transport-closeout.md` are the
  closed retained async shard transport reference.
- The first retained async worker runtime is implemented as a conservative
  one-in-flight borrowed-batch runtime. Retained workers and bounded queues are
  allowed; retained borrowed `RadarEventBatch` payload is not allowed.
- Workers remain replay-independent by dependency. Workers see processing
  inputs, topology snapshot, shard/partition assignment, cancellation, and
  completion contracts; they do not know about Archive Two, NEXRAD cache paths,
  decompression, historical replay, or future live ingestion.
- The callback lifetime boundary is preserved. The provider callback may block
  on worker completion for borrowed batches; that wait is the backpressure
  boundary that keeps borrowed payload valid.
- Slow workers are backpressure, failed workers fail the batch and suppress
  rebalance publication, and hung/non-cooperative workers are unhealthy runtime
  conditions. Timeout is a detection and health signal, not permission to return
  the callback while a worker may still read borrowed payload.
- Retained workers, coarse work items, no baseline payload copying, per-worker
  local metrics, deterministic post-completion aggregation, and same-run sync
  versus async benchmark comparison are the accepted shape.
- Synchronous `PartitionedBarrier` processing remains the correctness oracle and
  default execution mode. Async execution is selectable and benchmarked
  explicitly.
- Full-cache archive sync/async comparison is the accepted milestone 008
  production-shaped guardrail: async preserved deterministic results and
  callback latency parity, with about `0.90%` additional callback allocation.
- Defer owned `RadarEventBatch` snapshots, durable provider decoupling,
  multi-batch pipeline scheduling, physical worker-local state transfer, live
  ingestion, source-level migration, and partition splitting to later
  milestones.
- Use pressure skew only as an explicit benchmark contour. Baseline real-data
  performance and correctness captures must keep `--skew-profile none`; skewed
  runs should be reported as "real archive with synthetic pressure overlay."
- Preserve the slice 10 allocation guardrails, slice 11 workload-default
  hardening behavior, and slice 12 retention stress guarantees: no-move/no-detail
  paths should keep avoiding empty collection churn, allowed policy evaluation
  should remain allocation-light, retained detail must stay bounded, and
  benchmark allocation fields should stay comparable across static, sampling,
  and rebalance modes.
- Treat the final milestone 007 closeout performance table as the accepted
  baseline for future async-worker comparisons. The primary no-skew real-data
  row is cache-wide rebalance at `3.36B` callback payload values/s and `0.03`
  callback allocated bytes/payload.
- Carry forward the captured benchmark caveat: the milestone 006 rebalance
  catalog uses tiny deterministic behavioral workloads, so same-run static
  ratios are the meaningful overhead signal and milestone 005 throughput ratios
  are diagnostic only.
- Carry forward the milestone 007 benchmark caveat: lifecycle and retention
  synthetic workloads are behavioral microscopes, not production throughput
  shapes. Archive processing callback timing is the production-shaped rebalance
  performance signal.
- Preserve migration lifecycle semantics: no partial ownership changes after
  failed validation.
- Keep cold evacuation as a pressure-relief fallback, not general load
  shuffling, when tuning or extending the controller.
- Preserve the follow-up requirement that quarantined hot partitions must decay
  or clear by logical evaluation state after sustained cooling; quarantine must
  not become an eternal ban.
- Keep skipped rebalance decisions visible through telemetry so "no move" can
  be explained by policy gates rather than ambiguity.
- Bound skipped-decision detail with aggregate counters plus capped recent
  windows; long-running milestone 007 sessions must not become unbounded
  in-memory decision logs.
- Preserve synchronous `PartitionedBarrier` processing as the first rebalance
  correctness boundary: process one batch against one topology snapshot, then
  evaluate and apply rebalance before the next batch.
- Preserve the `SourceId -> PartitionId -> ShardId` ownership model when
  extending partition movement and source-state transfer. The
  source-to-partition mapping remains stable; only partition-to-shard ownership
  moves.
- Preserve rebalance as cautious pressure relief, not mechanical equalization.
  The controller requires sustained pressure, projected benefit,
  headroom on the target shard, cooldown, minimum residency, and move budgets.
- Preserve direct hot-partition relief first, then cold-partition evacuation
  from a hot shard when the hot partition cannot move safely.
- Treat the current `archive benchmark stream` numbers as replay construction
  throughput, not as the future processing-core throughput over
  already-built `RadarEventBatch` values.
- Preserve the leased batch lifetime rule: hot-path consumers may inspect a
  leased `RadarEventBatch` only during the synchronous publish callback; any
  retained batch must be converted with `ToOwnedSnapshot()`.
- Preserve the slice 1 cache-conscious stream event constraint:
  `RadarStreamEvent` is a 64-byte unmanaged value type with no reference
  fields.
- Preserve the ordered parallel projection rule in any future replay work:
  workers may decompress/project records concurrently, but emission must be
  merged by original source order, not worker completion order.
- Keep the order-sensitive chronology checksum as the validation gate for
  sequential/parallel equivalence.
- Keep processing benchmark commands explicit that measured time excludes
  decompression, Archive Two scanning, identity normalization, and
  `RadarEventBatch` construction.
- Consider the remaining cache-wide allocation sources only if they block the
  next milestone goal: compressed-record descriptor storage, ordered task
  scheduling, file enumeration/order materialization, and scanner/decompression
  buffer churn.
- Avoid implementing processing algorithms, live ingestion, durable broker
  integration, visualization, or a general storage subsystem as retroactive
  milestone 004 work.

Completed in milestone 004 planning:

- `docs/milestones/004-processing-core-input-contract.md`.
- `docs/milestones/004-processing-core-input-contract-plan.md`.
- `docs/milestones/004-processing-core-input-contract-decision-trace.md`.
- `docs/milestones/004-processing-core-input-contract-closeout.md`.
- Milestone 004 scope narrowed to the normalized processing-core input contract,
  not downstream processing algorithms or distribution.
- Dense identity catalogs are specified as persistent append-only catalogs with
  external versioned visibility.
- `RadarEventBatch` / `RadarStreamEvent` contract shape is specified with
  `StreamSchemaVersion`, `DictionaryVersion`, and `SourceUniverseVersion`.
- Identity normalization is specified as a mandatory boundary between decoded
  radar structures and batch construction, with no per-gate text lookup.
- Payload rules are specified: raw radar values are canonical, payload storage
  is associated with the visible batch lifetime, and event payload references
  are explicit.

Completed in milestone 005 planning:

- `docs/milestones/005-processing-core-architecture.md`.
- `docs/milestones/005-processing-core-architecture-plan.md`.
- `docs/milestones/005-processing-core-architecture-decision-trace.md`.
- `docs/milestones/005-processing-core-architecture-closeout.md`.
- Milestone 005 scope is the first static partitioned processing core over
  `RadarEventBatch`, not live shard rebalance or complex radar algorithms.
- The expected result is an accepted processing-core boundary over
  `RadarEventBatch`, static source-based partition/shard ownership, dense
  source-local state, explicit leased/retained payload lifetime rules,
  source-local handler slots, processing-only telemetry, validation, and
  benchmark contracts.
- The implementation plan is broken into processing contracts, static
  topology, dense state, handler slots, payload readers, sequential baseline,
  partitioned completion-barrier mode, lifetime guardrails, telemetry,
  validation, benchmarks, CLI smoke commands, and closeout/handoff.
- Milestone 006 is identified as the next milestone for partition-level shard
  rebalance after the static processing core baseline is measured.

Completed in milestone 006 documentation and planning:

- `docs/milestones/006-partition-level-shard-rebalance.md`.
- `docs/milestones/006-partition-level-shard-rebalance-plan.md`.
- `docs/milestones/006-partition-level-shard-rebalance-decision-trace.md`.
- `docs/milestones/006-partition-level-shard-rebalance-closeout.md`.
- Milestone 006 scope is cautious partition-level shard rebalance over the
  synchronous milestone 005 `PartitionedBarrier` baseline, not retained async
  processing, live ingestion, source-level migration, partition splitting, or
  complex radar algorithms.
- The accepted architecture preserves stable `SourceId -> PartitionId` mapping
  and makes only `PartitionId -> ShardId` movable through versioned topology
  snapshots.
- Rebalance is defined as pressure relief, not mechanical equalization:
  decisions require sustained shard pressure, projected benefit, target
  headroom, cooldown, minimum residency, and move-budget gates.
- The implementation plan is broken into versioned topology contracts,
  topology publication, route topology-version integration, pressure samples,
  pressure windows, anti-churn state, decision/skipped-reason telemetry, direct
  hot relief, intrinsic hot partition classification, cold evacuation,
  migration lifecycle, state handoff validation, rebalance validation,
  synthetic workloads, benchmarks, CLI smoke/benchmark command, and
  closeout/handoff.
- The first implementation slice added versioned topology contracts while
  preserving the existing contiguous source-range partition mapping.

Completed in milestone 007 documentation and planning:

- `docs/milestones/007-rebalance-production-hardening.md`.
- `docs/milestones/007-rebalance-production-hardening-plan.md`.
- Milestone 007 scope is production hardening of the synchronous rebalance
  control plane before retained async worker transport.
- The architecture preserves the milestone 006 synchronous correctness
  boundary while adding automatic quarantine lifecycle, bounded telemetry
  retention, validation profiles, allocation attribution, real-data contour
  expansion, and a final performance regression gate.
- The implementation plan is broken into hardening options, telemetry
  contracts, telemetry recorder, quarantine lifecycle state, lifecycle
  evaluator, planner integration, session result surfaces, validation profiles,
  allocation attribution, allocation reduction, lifecycle workloads, retention
  stress workloads, benchmark harness extensions, CLI updates, policy-default
  audit, documentation, and final comprehensive performance comparison.

Completed in milestone 008 documentation, planning, and implementation:

- `docs/milestones/008-retained-async-shard-transport.md`.
- `docs/milestones/008-retained-async-shard-transport-plan.md`.
- `docs/milestones/008-retained-async-shard-transport-decision-trace.md`.
- `docs/milestones/008-retained-async-shard-transport-closeout.md`.
- Milestone 008 scope is the first retained async shard worker transport over
  the closed milestone 007 synchronous rebalance baseline, not retained payload
  snapshots, live ingestion, durable broker integration, physical worker-local
  state transfer, source-level migration, partition splitting, or complex radar
  algorithms.
- The architecture preserves the borrowed batch lifetime rule: retained
  workers and queues may live across callbacks, but work items that reference a
  leased `RadarEventBatch` must complete before the provider callback returns.
- The first implementation target is conservative one-in-flight borrowed batch
  per worker group, with bounded queues, coarse shard or partition-group work
  items, explicit worker lifecycle, failure, cancellation, timeout, health, and
  bounded worker telemetry semantics.
- The implementation plan is broken into execution options, worker lifecycle,
  batch scope/work completion contracts, bounded mailboxes, retained worker
  group runtime, borrowed batch lifetime guardrails, async dispatch,
  deterministic aggregation, failure/cancellation/timeout/health semantics,
  worker telemetry, processing core integration, rebalance session integration,
  async validation, synthetic and archive benchmark extensions, CLI execution
  surface, performance guardrails, documentation, and final comprehensive
  performance comparison.
- Milestone 008 is complete. Async execution is available through the processing
  core, rebalance session, synthetic benchmark, synthetic rebalance benchmark,
  archive rebalance benchmark, and CLI benchmark surfaces.
- The final full-cache archive guardrail accepted sync/async callback latency
  parity and recorded the remaining async cost as about `0.90%` additional
  processing callback allocation.

Completed in milestone 006 implementation:

- `RadarProcessingTopologyVersion`.
- `RadarProcessingTopologyMoveError`.
- `RadarProcessingTopologyMoveRequest`.
- `RadarProcessingTopologyMoveResult`.
- `RadarProcessingTopologyManager`.
- `RadarProcessingTopology` now exposes `Version`, starting at
  `RadarProcessingTopologyVersion.Initial`.
- `RadarProcessingTopology.Partitions` now exposes an immutable read-only view
  over the partition assignments.
- A valid partition owner move creates a new topology snapshot with version
  `N+1`.
- Old topology snapshots remain unchanged after a partition owner move.
- The source-to-partition mapping is preserved across partition owner moves.
- Only the requested `PartitionId -> ShardId` owner changes during a move.
- Topology move publication rejects stale topology versions, out-of-range
  partition ids, out-of-range source/target shard ids, no-op moves, and source
  shard ownership mismatches.
- `RadarProcessingTopologyVersioningTests` cover initial versioning, valid
  publication, old snapshot immutability, stable source-to-partition mapping,
  single-partition owner changes, stale requests, invalid requests, and negative
  topology version guardrails.
- Verification after milestone 006 slice 1:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 106 tests passed.
- Verification after milestone 006 slice 1:
  `dotnet test RadarPulse.sln --no-restore` passed with 249 tests passed and 3
  skipped.
- Verification after milestone 006 slice 1:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingBatchRoute` now exposes `TopologyVersion`, captured from the
  `RadarProcessingTopology` used by `RadarProcessingBatchRouter.Route`.
- `RadarProcessingTelemetry` now exposes `TopologyVersion`, copied from the
  route used to build partitioned telemetry.
- `RadarProcessingResult` now exposes `TopologyVersion`. When telemetry is
  supplied, the result validates that telemetry topology version matches result
  topology version.
- `RadarProcessingCore` now returns valid and invalid results with the core
  topology version.
- Router tests verify that routes capture topology version and that a route
  built from an old topology snapshot remains explainable after the manager
  publishes a newer snapshot.
- Telemetry tests verify that partitioned result topology version and telemetry
  topology version match.
- Contract tests verify the default initial result topology version and reject
  telemetry/result topology-version mismatch.
- Verification after milestone 006 slice 2:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 108 tests passed.
- Verification after milestone 006 slice 2:
  `dotnet test RadarPulse.sln --no-restore` passed with 251 tests passed and 3
  skipped.
- Verification after milestone 006 slice 2:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingPressureBand`.
- `RadarProcessingPressureScore`.
- `RadarProcessingPressureOptions`.
- `RadarProcessingShardPressureSample`.
- `RadarProcessingPartitionPressureSample`.
- `RadarProcessingPressureSample`.
- `RadarProcessingPressureSample.FromTelemetry` projects partitioned telemetry
  into immutable shard and partition pressure samples.
- Pressure samples preserve topology version, batch metrics, per-shard metrics,
  per-partition metrics, event counts, payload value counts, and raw-value
  checksums.
- Pressure scoring is deterministic and currently uses configurable event,
  payload-value, and raw-checksum weights.
- Pressure band classification supports `Cold`, `Normal`, `Warm`, `Hot`, and
  `SuperHot`.
- Pressure numeric options reject negative, NaN, infinity, and non-monotonic
  threshold values.
- Pressure sample tests cover empty telemetry, topology version and metric
  projection, score growth by event and payload count, deterministic band
  classification, leased payload lifetime safety, stability after later
  processing, and numeric guardrails.
- Verification after milestone 006 slice 3:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 116 tests passed.
- Verification after milestone 006 slice 3:
  `dotnet test RadarPulse.sln --no-restore` passed with 259 tests passed and 3
  skipped.
- Verification after milestone 006 slice 3:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingPressureWindowOptions`.
- `RadarProcessingShardPressureState`.
- `RadarProcessingPartitionPressureState`.
- `RadarProcessingPressureWindow`.
- Pressure windows retain the last configured number of
  `RadarProcessingPressureSample` values and expose current shard and partition
  pressure state.
- Pressure windows expose `IsRebalanceEligible` only after the configured
  minimum sample count is reached.
- Pressure windows preserve the latest observed topology version.
- Shard pressure state tracks sample count, latest partition counts, total
  route metrics across the window, average score, band, hot flag, and super-hot
  flag.
- Partition pressure state tracks partition id, latest owner shard, sample
  count, total route metrics across the window, average score, band, hot flag,
  and super-hot flag.
- Window hysteresis uses explicit warm/hot/super-hot enter and exit thresholds.
- Pressure window tests cover minimum sample eligibility, sustained hot
  detection, preserving hot band between enter/exit thresholds, leaving hot
  below the exit threshold, cold empty samples, partition pressure ownership,
  latest topology version, mismatched sample shape rejection, and option
  guardrails.
- Verification after milestone 006 slice 4:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 125 tests passed.
- Verification after milestone 006 slice 4:
  `dotnet test RadarPulse.sln --no-restore` passed with 268 tests passed and 3
  skipped.
- Verification after milestone 006 slice 4:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingRebalanceOptions`.
- `RadarProcessingRebalanceBudget`.
- `RadarProcessingPartitionResidency`.
- `RadarProcessingPartitionCooldown`.
- `RadarProcessingShardCooldown`.
- `RadarProcessingRebalanceMovePolicyInput`.
- `RadarProcessingRebalancePolicyRejection`.
- `RadarProcessingRebalancePolicyResult`.
- `RadarProcessingRebalancePolicyState`.
- Rebalance options define deterministic anti-churn gates: budget window,
  global move budget, source-shard move budget, target-shard receive budget,
  minimum partition residency, partition move cooldown, source-shard move
  cooldown, target-shard receive cooldown, minimum projected benefit, and
  target headroom threshold.
- Rebalance policy state is driven by logical `EvaluationSequence`, not wall
  clock time.
- `EvaluateMove` validates a candidate move and returns all active rejection
  reasons without mutating policy state.
- `RecordAcceptedMove` applies budget, cooldown, and residency state only when
  the candidate passes every policy gate.
- Policy gates cover minimum partition residency, partition cooldown,
  source-shard cooldown, target-shard cooldown, global move budget,
  source-shard move budget, target-shard receive budget, projected benefit, and
  target headroom.
- Policy state exposes read-side accessors for partition residency, partition
  cooldown, source/target shard cooldowns, and source/target shard budgets.
- Rebalance policy tests cover residency, cooldown expiry, source/target shard
  cooldowns, global/source/target budgets, projected-benefit rejection, target
  headroom rejection, non-mutating evaluation, rejected-record non-mutation,
  deterministic evaluation advancement, accessors, option guardrails, and move
  input guardrails.
- Verification after milestone 006 slice 5:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 139 tests passed.
- Verification after milestone 006 slice 5:
  `dotnet test RadarPulse.sln --no-restore` passed with 282 tests passed and 3
  skipped.
- Verification after milestone 006 slice 5:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingRebalanceDecisionKind`.
- `RadarProcessingRebalanceMoveKind`.
- `RadarProcessingRebalanceSkippedReason`.
- `RadarProcessingProjectedPressure`.
- `RadarProcessingRebalanceCandidate`.
- `RadarProcessingRebalanceDecision`.
- Rebalance decisions now represent three stable outcomes: `NoAction`,
  `AcceptedMove`, and `RejectedCandidate`.
- Rebalance move kinds now classify direct hot relief, cold evacuation, and a
  reserved room-making move kind.
- Skipped reasons are explicit telemetry values for no sustained pressure, no
  hot shard, no cold target shard, unsafe direct hot relief, insufficient
  projected benefit, target pressure risk, residency/cooldown/budget gates,
  intrinsic hot partition classification, cold evacuation benefit failure, and
  migration validation failure.
- Rebalance candidates carry move kind, partition id, source shard id, target
  shard id, projected source/target pressure before and after, and expected
  relief.
- Rebalance candidates can be converted into policy inputs and topology move
  requests without the future planner duplicating move shape logic.
- Rejected-candidate decisions can be created from
  `RadarProcessingRebalancePolicyResult`; policy rejections are copied into
  decision telemetry and mapped to skipped reasons.
- Decision contracts copy and de-duplicate reason collections so later caller
  mutation does not affect recorded telemetry.
- Rebalance decision tests cover no-pressure no-action, hot shard with no
  target, rejected candidate with multiple policy gates, accepted move
  telemetry, deterministic construction, collection copying, decision
  guardrails, and candidate guardrails.
- Verification after milestone 006 slice 6:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 147 tests passed.
- Verification after milestone 006 slice 6:
  `dotnet test RadarPulse.sln --no-restore` passed with 290 tests passed and 3
  skipped.
- Verification after milestone 006 slice 6:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingDirectHotReliefPlanner`.
- Direct hot relief planning reads pressure windows and policy state without
  mutating topology or consuming anti-churn budgets/cooldowns.
- The planner returns `NoAction` when the pressure window has not reached its
  minimum sample count, when no shard is hot, or when no cold target shard is
  available.
- The planner ranks direct hot relief candidates deterministically by projected
  max-pressure relief, partition pressure, partition id, and target shard id.
- Candidate projection records source shard pressure before/after, target shard
  pressure before/after, and expected relief.
- Candidate target projection rejects direct moves that would make the target
  shard warm, hot, or super-hot before policy gates are evaluated.
- Anti-churn gates are applied through `RadarProcessingRebalancePolicyState`
  evaluation before an accepted move decision is returned.
- Direct hot relief tests cover sustained hot shard candidate creation,
  deterministic largest useful partition selection, target-hot rejection,
  insufficient projected benefit rejection, cooldown rejection, accepted move
  projected max-pressure relief, ineligible window no-action, and eligible
  no-hot-shard no-action.
- Verification after milestone 006 slice 7:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 155 tests passed.
- Verification after milestone 006 slice 7:
  `dotnet test RadarPulse.sln --no-restore` passed with 298 tests passed and 3
  skipped.
- Verification after milestone 006 slice 7:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingHotPartitionClassification`.
- `RadarProcessingHotPartitionState`.
- `RadarProcessingHotPartitionClassifier`.
- `RadarProcessingRebalanceSkippedReason.PartitionQuarantined`.
- Hot partition classification tracks `None`, `MovableHot`, `IntrinsicHot`,
  and `Quarantined` states per partition.
- Intrinsic and quarantined hot partition states block direct hot relief
  selection for that partition.
- The direct hot relief planner can receive optional hot-partition
  classification state. It records intrinsic hot classification when the
  selected direct-hot candidate has no safe target, skips previously intrinsic
  or quarantined partitions, and returns diagnostic skipped reasons when all
  hot partitions are classification-blocked.
- Hot partition move outcomes can quarantine a partition after repeated
  ineffective movement attempts, using configurable ineffective-move count and
  minimum effective relief ratio.
- Quarantine is intentionally conservative in slice 8, but the milestone plan
  now requires later controller lifecycle handling so quarantined partitions can
  decay, clear, or downgrade after sustained cooling on logical evaluations.
- Hot partition classifier tests cover initial unclassified state, intrinsic
  blocking, movable-hot non-blocking, ineffective-move quarantine, effective
  outcome reset, clearing classification, and guardrails.
- Direct hot relief tests now cover intrinsic classification recording,
  skipping an intrinsic partition in favor of another direct candidate, and
  diagnostic no-action when every hot partition is classification-blocked.
- Verification after milestone 006 slice 8:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 164 tests passed.
- Verification after milestone 006 slice 8:
  `dotnet test RadarPulse.sln --no-restore` passed with 307 tests passed and 3
  skipped.
- Verification after milestone 006 slice 8:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingColdEvacuationPlanner`.
- Cold evacuation planning reads pressure windows and policy state without
  mutating topology or consuming anti-churn budgets/cooldowns.
- The planner returns `NoAction` when the pressure window has not reached its
  minimum sample count, when no shard is hot, when no cold target shard is
  available, or when no useful non-hot partition can be evacuated.
- The planner selects low-pressure non-hot partitions currently owned by hot or
  super-hot source shards and emits `ColdEvacuation` candidates.
- Cold evacuation candidate projection records source shard pressure
  before/after, target shard pressure before/after, and expected relief.
- Target projection rejects cold evacuation moves that would make the target
  shard warm, hot, or super-hot before policy gates are evaluated.
- Anti-churn gates are applied through `RadarProcessingRebalancePolicyState`
  evaluation before an accepted cold evacuation decision is returned.
- Cold evacuation tests cover direct-hot unsafe fallback, moving a cold
  partition off a hot shard, deterministic smallest useful cold-partition
  selection, target headroom, insufficient projected relief, target-warm
  rejection, and source-shard move budget rejection.
- Verification after milestone 006 slice 9:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 170 tests passed.
- Verification after milestone 006 slice 9:
  `dotnet test RadarPulse.sln --no-restore` passed with 313 tests passed and 3
  skipped.
- Verification after milestone 006 slice 9:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingPartitionMigrationState`.
- `RadarProcessingMigrationValidationError`.
- `RadarProcessingPartitionMigration`.
- `RadarProcessingMigrationValidationResult`.
- `RadarProcessingMigrationResult`.
- `RadarProcessingMigrationCoordinator`.
- Migration coordination accepts only accepted rebalance decisions and rejects
  no-action or rejected-candidate decisions before topology validation.
- Migration validation checks current topology version, partition id range,
  source shard id range, target shard id range, no-op moves, and current source
  shard ownership before publishing a topology move.
- Valid migrations are applied through `RadarProcessingTopologyManager`, so
  published moves still reuse the monotonic topology snapshot boundary.
- Migration results record lifecycle state, validation result, migration
  request, previous topology version, current topology version, and topology
  move error when one is surfaced.
- Failed migration validation leaves the current topology snapshot unchanged
  and does not publish a partial ownership change.
- Migration coordinator tests cover accepted decision publication to topology
  `N+1`, stale decision rejection, wrong old-owner rejection, invalid target
  rejection, non-accepted decision rejection, validation without publication,
  lifecycle state/version fields, and migration contract guardrails.
- Verification after milestone 006 slice 10:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 177 tests passed.
- Verification after milestone 006 slice 10:
  `dotnet test RadarPulse.sln --no-restore` passed with 320 tests passed and 3
  skipped.
- Verification after milestone 006 slice 10:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingPartitionStateSnapshot`.
- `RadarProcessingPartitionStateChecksum`.
- `RadarProcessingStateHandoffValidationError`.
- `RadarProcessingStateHandoffValidationResult`.
- `RadarProcessingStateHandoffValidator`.
- Partition state snapshots capture the partition id, current owner shard,
  source range, active source count, processed event count, processed payload
  value count, raw value checksum, aggregate processing checksum, order-sensitive
  last message timestamp checksum, and handler snapshot checksum.
- State handoff validation intentionally allows owner shard id changes, because
  moving ownership is the successful handoff path.
- State handoff validation rejects partition id, source range, active source
  count, processed event count, processed payload value count, raw value
  checksum, processing checksum, last timestamp checksum, and handler snapshot
  checksum mismatches.
- Handler snapshot checksums include active source id, snapshot field names,
  field types, and field values so configured processing handlers participate
  in handoff validation.
- Empty partition source ranges with no active source state produce empty
  handoff checksums and validate across owner shard changes.
- State handoff validator tests cover owner-only shard changes, active source
  count mismatch, processed event count mismatch, processed payload value count
  mismatch, raw checksum mismatch, processing checksum mismatch, last timestamp
  checksum mismatch, handler checksum mismatch, and empty partition handoff.
- Verification after milestone 006 slice 11:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 186 tests passed.
- Verification after milestone 006 slice 11:
  `dotnet test RadarPulse.sln --no-restore` passed with 329 tests passed and 3
  skipped.
- Verification after milestone 006 slice 11:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingCore` now owns a `RadarProcessingTopologyManager` and routes
  each `PartitionedBarrier` batch from the current immutable topology snapshot.
- `RadarProcessingCore.Topology` now exposes the manager's current topology, so
  a published migration affects only later processing calls, not the already
  routed batch.
- `RadarProcessingCore.CapturePartitionState` captures partition-owned state
  summaries for state handoff validation without exposing the dense state store.
- `RadarProcessingRebalanceSession`.
- `RadarProcessingRebalanceSessionResult`.
- Rebalance sessions require `PartitionedBarrier` execution mode; sequential
  cores are rejected with a clear unsupported shape.
- Rebalance session processing converts valid partitioned telemetry into a
  pressure sample, appends it to the rolling pressure window, advances logical
  policy evaluation, evaluates direct hot relief first, then evaluates cold
  evacuation when direct relief does not produce an accepted move.
- Accepted session moves validate projected source-state handoff before
  publication, publish through `RadarProcessingMigrationCoordinator`, validate
  the post-publication partition state snapshot, then record the move in
  anti-churn policy state.
- Invalid processing results do not update pressure windows, advance policy
  evaluation, evaluate planners, or attempt migration.
- Rebalance session tests cover first-batch initial topology routing, accepted
  direct relief publishing topology `N+1`, next-batch routing on the new
  topology version, direct-unsafe fallback to cold evacuation, handoff
  validation participation, invalid processing no-op behavior, and sequential
  core rejection.
- Verification after milestone 006 slice 12:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 190 tests passed.
- Verification after milestone 006 slice 12:
  `dotnet test RadarPulse.sln --no-restore` passed with 333 tests passed and 3
  skipped.
- Verification after milestone 006 slice 12:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingRebalanceValidationError`.
- `RadarProcessingRebalanceValidationResult`.
- `RadarProcessingRebalanceValidator`.
- Rebalance validation checks monotonic topology sequences, stable topology
  shape, stable `SourceId -> PartitionId` source ranges, in-range partition
  ownership, accepted move source/target ownership, and the rule that accepted
  moves change only the selected partition owner shard.
- Route validation checks route, telemetry, and topology shapes; route topology
  version against telemetry topology version; partition ownership in route and
  telemetry against the topology snapshot; routed event partition/shard ownership
  against `SourceId -> PartitionId -> ShardId`; and route/telemetry topology
  version against the topology snapshot.
- Pressure validation checks that a pressure sample still matches the telemetry
  it was derived from, including topology version, batch metrics, partition
  metrics, shard metrics, partition counts, and active partition counts.
- Session validation treats invalid processing results with no rebalance
  artifacts as valid no-op session output, rejects unexpected artifacts on
  invalid processing, checks decision topology against the processed pressure
  sample, checks migration result topology against the accepted decision and
  current topology, and reports failed state handoff with the underlying
  handoff validation error.
- `RadarProcessingRebalanceSessionResult` now carries
  `RadarProcessingRebalanceValidationResult` when constructed with the current
  topology; the session passes the current topology for every result it returns.
- Rebalance validator tests cover valid topology sequence, non-monotonic
  topology sequence rejection, mixed route/telemetry topology version rejection,
  route partition owner mismatch rejection, invalid accepted move ownership,
  and invalid state handoff diagnostics.
- Rebalance session tests now assert valid session-level rebalance validation
  for direct relief, cold evacuation fallback, next-batch topology routing, and
  invalid-processing no-op behavior.
- Verification after milestone 006 slice 13:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 196 tests passed.
- Verification after milestone 006 slice 13:
  `dotnet test RadarPulse.sln --no-restore` passed with 339 tests passed and 3
  skipped.
- Verification after milestone 006 slice 13:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingSyntheticRebalanceWorkloadKind`.
- `RadarProcessingSyntheticRebalanceWorkload`.
- `RadarProcessingSyntheticRebalanceWorkloadResult`.
- `RadarProcessingSyntheticRebalanceWorkloadRunner`.
- Synthetic rebalance workloads are implemented in
  `RadarPulse.Infrastructure.Processing`, alongside the existing synthetic
  processing workload harness, because they construct prebuilt
  `RadarEventBatch` values for tests and future benchmarks.
- The balanced workload distributes pressure across shards and produces no
  accepted moves.
- The sustained-hot workload produces direct hot relief and topology version
  movement.
- The intrinsic-hot workload seeds a blocked small partition so direct movement
  rejects the intrinsically hot partition and cold evacuation can move a useful
  cold partition instead.
- The oscillating workload uses a longer pressure window so short spikes do not
  trigger churn.
- The cooldown-storm workload accepts one initial move, then records cooldown
  and budget skipped reasons without publishing a second move.
- Workload results expose batch count, accepted move counts, direct/cold move
  counts, initial/final topology versions, topology version count, aggregate
  skipped reasons, and aggregate session validation success.
- Synthetic rebalance workload tests cover balanced no-move, sustained direct
  relief, intrinsic hot fallback to cold evacuation, oscillating no-churn,
  cooldown skipped reasons, and unknown workload guardrails.
- Verification after milestone 006 slice 14:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 202 tests passed.
- Verification after milestone 006 slice 14:
  `dotnet test RadarPulse.sln --no-restore` passed with 345 tests passed and 3
  skipped.
- Verification after milestone 006 slice 14:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingSyntheticRebalanceBenchmarkMode`.
- `RadarProcessingSyntheticRebalanceMovePressure`.
- `RadarProcessingSyntheticRebalanceBenchmarkResult`.
- `RadarProcessingSyntheticRebalanceBenchmark`.
- The rebalance benchmark supports three measured modes:
  static no-rebalance baseline, pressure-sampling-only, and full
  `RadarProcessingRebalanceSession` evaluation.
- Benchmark workloads reuse prebuilt `RadarEventBatch` values from the synthetic
  rebalance workload catalog, excluding archive replay and batch construction
  from measured iterations.
- Benchmark results report workload kind, mode, iteration and warmup counts,
  source/partition/shard shape, per-iteration batch/event/payload totals,
  topology version count, rebalance evaluation count, accepted move count,
  skipped decision count, direct hot relief count, cold evacuation count, failed
  migration count, validation status, deterministic validation checksum,
  aggregate skipped reasons, accepted-move pressure projections, elapsed time,
  allocation totals, throughput, and allocation ratios.
- Static no-rebalance mode processes through `RadarProcessingCore` only and
  records zero rebalance evaluations.
- Pressure-sampling-only mode processes through `RadarProcessingCore`, derives
  pressure samples from telemetry, updates a pressure window, and records zero
  moves.
- Full session mode runs fresh session state per measured iteration so topology
  moves, cooldowns, and budgets remain deterministic across iterations.
- Rebalance benchmark tests cover warmup exclusion, static zero-evaluation
  baseline, sampling-only zero moves, accepted direct relief accounting,
  accepted cold evacuation accounting, deterministic totals/checksum for the
  same workload, and invalid input guardrails.
- Verification after milestone 006 slice 15:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 209 tests passed.
- Verification after milestone 006 slice 15:
  `dotnet test RadarPulse.sln --no-restore` passed with 352 tests passed and 3
  skipped.
- Verification after milestone 006 slice 15:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `processing benchmark rebalance-synthetic`.
- `processing benchmark rebalance` is available as a short alias for the same
  command.
- The command accepts
  `--workload balanced|hot-shard|intrinsic-hot|oscillating|cooldown-storm|all`,
  `--mode static|sampling|rebalance|all`, `--iterations`, and
  `--warmup-iterations`.
- The command runs the processing-only synthetic rebalance benchmark over
  prebuilt `RadarEventBatch` values and keeps replay construction, identity
  normalization, batch construction, and CLI formatting out of the measured
  loop.
- CLI output reports workload kind, benchmark mode, partition and shard shape,
  iteration counts, per-iteration batch/event/payload totals, topology version
  count, rebalance evaluations, accepted moves, skipped decisions, direct/cold
  move counts, failed migrations, validation status/checksum, unique skipped
  reasons, elapsed time, throughput, allocation ratios, and accepted-move
  pressure projections.
- CLI smoke tests cover option parsing, rejection of `--mode sequential`, and a
  real small-process run that emits topology and rebalance counters.
- Verification after milestone 006 slice 16:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests`
  passed with 3 tests passed.
- Verification after milestone 006 slice 16:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- Verification after milestone 006 slice 16:
  `dotnet test RadarPulse.sln --no-restore` passed with 355 tests passed and 3
  skipped.
- Verification after milestone 006 slice 16:
  `dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload hot-shard --mode all --iterations 1 --warmup-iterations 0`
  passed and emitted static, sampling, and rebalance-session benchmark blocks.
- Synthetic rebalance benchmark aggregation no longer copies previously
  accepted move pressure samples on every measured iteration. This keeps the
  benchmark allocation signal from being dominated by O(n^2) result aggregation
  when accepted moves are present.
- CLI accepted-move pressure output is capped to the first 8 samples, followed
  by an omitted-sample count, so large Release benchmark captures remain
  readable while the benchmark result still counts all accepted moves.
- Rebalance benchmark tests now include a bounded-allocation regression for
  accepted move pressure aggregation over 3_000 iterations.
- Verification after milestone 006 Release benchmark capture:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 210 tests passed.
- Verification after milestone 006 Release benchmark capture:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- Release benchmark capture command:
  `dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload all --mode all --iterations 10000 --warmup-iterations 1000`.
- Captured milestone 006 Release benchmark results:

  ```text
  workload        mode       topo versions  accepted moves  skipped decisions  payload values/s  alloc bytes/event  vs static  vs 005 baseline
  balanced        static     1              0               0                  1_086_338.08      667.00             100.0%     0.0414%
  balanced        sampling   1              0               0                    852_609.41    1_008.00              78.5%     0.0325%
  balanced        rebalance  1              0              40_000                634_847.07    1_624.06              58.4%     0.0242%
  hot-shard       static     1              0               0                  1_899_984.80      443.33             100.0%     0.0724%
  hot-shard       sampling   1              0               0                  1_166_343.98      670.67              61.4%     0.0445%
  hot-shard       rebalance  2             10_000          20_000                788_605.70    1_322.52              41.5%     0.0301%
  intrinsic-hot   static     1              0               0                  2_642_682.85      352.01             100.0%     0.1008%
  intrinsic-hot   sampling   1              0               0                  2_201_177.87      512.00              83.3%     0.0839%
  intrinsic-hot   rebalance  2             10_000          10_000                675_789.32    1_642.48              25.6%     0.0258%
  oscillating     static     1              0               0                  2_797_429.72      382.80             100.0%     0.1067%
  oscillating     sampling   1              0               0                  2_375_452.08      583.60              84.9%     0.0906%
  oscillating     rebalance  1              0              40_000              2_733_173.90      796.83              97.7%     0.1042%
  cooldown-storm  static     1              0               0                  5_077_044.14      445.33             100.0%     0.1936%
  cooldown-storm  sampling   1              0               0                  5_660_404.06      672.67             111.5%     0.2158%
  cooldown-storm  rebalance  2             10_000          20_000                823_981.71    1_907.32              16.2%     0.0314%
  ```

- Captured benchmark interpretation: same-run static ratios are the useful
  overhead signal for milestone 006. The milestone 005 ratio is diagnostic only
  because the 006 catalog uses 8-20 payload values per iteration while the
  milestone 005 throughput baseline used 38_750_400 payload values per
  iteration.
- All captured Release rows reported successful validation and zero failed
  migrations.
- `RadarProcessingArchiveRebalanceBenchmark`.
- `RadarProcessingArchiveRebalanceBenchmarkResult`.
- `processing benchmark rebalance-archive`.
- The real-data command supports `--file` input or cache input through
  `--cache`, `--date`, `--radar`, and `--max-files`. It also accepts
  `--mode static|sampling|rebalance|all`, `--partitions`, `--shards`,
  `--iterations`, `--warmup-iterations`, `--parallelism`, and `--decompressor`.
- The archive rebalance benchmark uses
  `NexradArchiveRadarEventBatchPublishSession` to stream real Archive Two data
  into leased `RadarEventBatch` callbacks. It processes the batch during the
  callback and does not retain leased batch data.
- Archive benchmark output separates end-to-end archive replay/batch
  construction timing from processing callback timing, and reports topology,
  rebalance decisions, validation, skipped reasons, accepted move pressure,
  throughput, and allocation counters.
- CLI archive rebalance option tests cover file/mode/topology parsing and
  required-file/compatible-topology guardrails.
- Verification after milestone 006 real-data rebalance smoke command:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests`
  passed with 5 tests passed.
- Verification after milestone 006 real-data rebalance smoke command:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- Real-data Release smoke command:
  `dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode all --partitions 24 --shards 4 --iterations 3 --warmup-iterations 1 --parallelism 1`.
- Captured real-data smoke shape:
  `55` compressed records, `50_741_824` decompressed bytes, `1` batch,
  `32_400` stream events, and `38_759_040` payload values per iteration.
- Captured real-data smoke results:

  ```text
  mode       topology versions  evaluations  accepted moves  skipped decisions  callback payload values/s  end-to-end payload values/s  alloc bytes/payload value
  static     1                  0            0               0                  2_589_754_314.69           92_333_354.54              0.06
  sampling   1                  3            0               0                  2_990_889_752.58           92_347_294.79              0.06
  rebalance  2                  3            3               0                  3_061_858_015.59           92_350_954.71              0.06
  ```

- Captured real-data rebalance pressure projection:
  `direct-hot-relief source 51_868.80->42_837.12, target 0.00->9_031.68, relief 9_031.68`.
- All captured real-data rows reported successful validation and zero failed
  migrations.
- Important comparison note: the `92M` end-to-end real-data smoke value above
  used archive `--parallelism 1`, while the earlier milestone 004 `~500M`
  normalized-stream baseline used archive `--parallelism 24`.
- Comparable real-data rerun on `KTLX20260504_002334_V06` with archive
  `--parallelism 24`:

  ```text
  command/result                                      end-to-end payload values/s
  archive benchmark stream                            430_859_940.37
  processing benchmark rebalance-archive sampling     458_420_311.03
  processing benchmark rebalance-archive rebalance    449_250_477.25
  ```

- The comparable parallel real-data rebalance smoke remains in the same order
  of magnitude as the milestone 004 stream baseline and still reports
  successful validation, accepted direct hot relief, and zero failed
  migrations. The earlier `92M` result is therefore a single-thread replay
  smoke number, not evidence of a rebalance regression.
- Cache-wide real-data rebalance benchmark command:
  `dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode all --partitions 24 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse`.
- Captured cache-wide real-data shape:
  `244` examined files, `24` skipped files, `220` published Archive Two
  base-data files, `1_330_634_309` compressed bytes, `11_145_331_584`
  decompressed bytes, `220` batches, `7_114_560` stream events, and
  `8_513_587_200` payload values.
- Captured cache-wide real-data results:

  ```text
  mode       topology versions  evaluations  accepted moves  skipped decisions  callback payload values/s  end-to-end payload values/s  alloc bytes/payload value
  static     1                  0            0               0                  2_796_597_485.46           355_001_379.25             0.24
  sampling   1                  220          0               0                  2_735_817_941.09           385_154_964.58             0.23
  rebalance  2                  220          2               436                2_680_685_752.29           380_667_655.66             0.23
  ```

- Cache-wide accepted pressure projections:
  `direct-hot-relief source 51_868.80->42_837.12, target 0.00->9_031.68, relief 9_031.68`;
  `direct-hot-relief source 43_966.08->35_038.08, target 0.00->8_928.00, relief 8_928.00`.
- Cache-wide skipped reasons were `global-move-budget-exhausted`,
  `source-shard-move-budget-exhausted`, `no-cold-target-shard`, and
  `no-hot-shard`.
- All cache-wide rows reported successful validation and zero failed
  migrations.
- Milestone 006 benchmark assessment:
  the milestone is successful as a correctness, cautious-rebalance, and
  real-data validation milestone. Synthetic, single-file real-data, parallel
  real-data, and cache-wide real-data runs all validated successfully with zero
  failed migrations.
- Cautious behavior is visible in the cache-wide result: rebalance accepted only
  `2` direct-hot-relief moves across `220` real batches, then policy gates
  (`global-move-budget-exhausted`, `source-shard-move-budget-exhausted`,
  target availability, and no-hot-shard cases) prevented churn.
- Pressure relief is visible in accepted real-data moves:
  source pressure dropped from `51_868.80` to `42_837.12` and from `43_966.08`
  to `35_038.08`, while target pressure rose from `0` to `9_031.68` and
  `8_928.00` respectively.
- Comparison with milestone 005: cache-wide rebalance processing callback
  throughput was `2_680_685_752.29` payload values/s, about `102.2%` of the
  milestone 005 `partitioned 24/24 none` processing-only baseline
  (`2_622_669_443.85` payload values/s). End-to-end archive numbers are replay
  dominated and are not directly comparable to milestone 005 processing-only
  results.
- Known performance cost: cache-wide real-data allocation is about `0.23`
  bytes/payload value versus `0.03` in the milestone 005 processing-only
  synthetic baseline. Treat this as production-hardening input, not a blocker
  for milestone 006 closeout.
- Closeout judgement: milestone 006 is closed as a correctness and cautious
  real-data rebalance milestone. Follow-up work should focus on allocation
  profile, repeated cache-wide runs, policy tuning, and longer multi-radar
  scenarios rather than expanding controller behavior further inside 006.

Completed in milestone 005 implementation:

- `RadarProcessingExecutionMode`.
- `RadarProcessingCoreOptions`.
- `RadarProcessingMetrics`.
- `RadarProcessingValidationError`.
- `RadarProcessingValidationResult`.
- `RadarProcessingResult`.
- Processing contracts are isolated under `RadarPulse.Domain.Processing`.
- Initial execution modes are explicit: `Sequential` and
  `PartitionedBarrier`.
- Core options validate execution mode, partition count, shard count, and the
  initial `PartitionCount >= ShardCount` topology constraint.
- Processing results carry execution mode, topology shape, deterministic
  metrics, and validation state.
- Processing results can optionally carry partitioned telemetry. Telemetry is
  accepted only for `PartitionedBarrier` results and must match the result's
  execution mode, partition count, and shard count.
- `RadarProcessingPartitionAssignment`.
- `RadarProcessingTopology`.
- Static topology maps `SourceId -> PartitionId -> ShardId` with contiguous
  source blocks.
- Topology construction validates against `RadarSourceUniverse.SourceCount`.
- Partition assignments expose partition id, shard id, source range start,
  source range end, source count, and source containment checks.
- Source ids and partition ids are range-checked at lookup boundaries.
- `RadarSourceProcessingSnapshot`.
- `RadarSourceProcessingStateStore`.
- Dense source-local state arrays are sized by
  `RadarSourceUniverse.SourceCount`.
- State updates are direct by `SourceId` and track processed event count,
  processed payload value count, raw value checksum, last message timestamp,
  active source marker, and deterministic source checksum.
- Source-local timestamp regression is rejected during state updates.
- State snapshots are read-side projections and aggregate
  `RadarProcessingMetrics` can be produced from active source state.
- `RadarProcessingPayloadMetrics`.
- `RadarProcessingPayloadReader`.
- Processing payload reader helpers compute event-level and batch-level payload
  value counts and raw value checksums.
- Payload readers support 8-bit values and 16-bit big-endian values, matching
  the existing `RadarEventBatchMetrics` raw-value contract.
- Payload reader guardrails reject null batches, unsupported word sizes,
  payload length mismatches, and out-of-range payload references.
- Payload reader signatures pass `RadarStreamEvent` by value to avoid
  ref-safety/tooling ambiguity around returned spans.
- `RadarProcessingCore`.
- The first core baseline supports `Sequential` execution mode.
- `RadarProcessingCore.Process` consumes `RadarEventBatch`, honors
  `CancellationToken`, validates stream schema and source-universe version, and
  returns `RadarProcessingResult`.
- Sequential processing iterates `RadarEventBatch.Events` in canonical order,
  reads event payload metrics through `RadarProcessingPayloadReader`, updates
  `RadarSourceProcessingStateStore`, and exposes source snapshots.
- Sequential metrics accumulate across processed batches and are available
  through both the processing result and the core state.
- Sequential baseline returns invalid processing results for unsupported stream
  schema, source-universe mismatch, source id outside universe, source
  ownership mismatch, and source-local timestamp regression.
- Sequential lifetime/parity guardrails are covered without additional
  production-code changes.
- Guardrails verify owned and leased-equivalent batch parity, result/snapshot
  stability after leased builder buffer reuse, result counters matching
  `RadarEventBatchMetrics`, invalid processing not incrementing processed batch
  count, and invalid source validation not mutating state.
- `RadarProcessingBatchRouter`.
- `RadarProcessingBatchRoute`.
- `RadarProcessingPartitionBatchRoute`.
- `RadarProcessingShardBatchRoute`.
- `RadarProcessingRoutedEvent`.
- `RadarProcessingRouteMetrics`.
- `RadarProcessingPartitionTelemetry`.
- `RadarProcessingShardTelemetry`.
- `RadarProcessingTelemetry`.
- `RadarProcessingOutputValidator`.
- `RadarSourceProcessingChecksum`.
- `IRadarSourceProcessingHandler`.
- `RadarSourceProcessingHandlerContext`.
- `RadarSourceProcessingState`.
- `RadarSourceProcessingHandlerDescriptor`.
- `RadarSourceProcessingHandlerSlotAssignment`.
- `RadarSourceProcessingHandlerSlotLayout`.
- `RadarSourceProcessingSnapshotFieldDescriptor`.
- `RadarSourceProcessingSnapshotFieldType`.
- `RadarSourceProcessingSnapshotValue`.
- `RadarSourceProcessingHandlerSnapshot`.
- `RadarProcessingBenchmarkHandlerSet`.
- `RadarProcessingBenchmarkResult`.
- `RadarProcessingBenchmarkShardDistribution`.
- `RadarProcessingSyntheticWorkloadOptions`.
- `RadarProcessingSyntheticWorkload`.
- `RadarProcessingSyntheticBenchmark`.
- Partitioned routing maps each batch event index to `PartitionId` and
  `ShardId` through `RadarProcessingTopology`.
- Routing stores event indexes and per-partition/per-shard counters without
  copying payload bytes or starting worker execution.
- Route metrics track event count, payload value count, and raw value checksum
  and aggregate consistently through partitions and shards.
- Source ids outside the topology are rejected before a route is returned.
- Partitioned telemetry is a read-side snapshot over the route: batch metrics,
  per-partition metrics, per-shard metrics, shard partition counts, active
  partition counts, deterministic hot partition id, and deterministic hot shard
  id.
- Telemetry does not expose event indexes, payload spans, or references to
  leased batch storage.
- Telemetry construction validates that partition and shard metric totals match
  the batch route metrics.
- `RadarProcessingCore` now supports both `Sequential` and
  `PartitionedBarrier` execution modes.
- The first `PartitionedBarrier` path uses `RadarProcessingBatchRouter`, then
  synchronously iterates shard event indexes and returns only after all shard
  loops finish.
- `RadarProcessingRoutedEvent` carries precomputed payload metrics so the
  partitioned path does not read payload bytes a second time after routing.
- Valid `PartitionedBarrier` results now carry partitioned telemetry; sequential
  and invalid results do not carry partitioned telemetry.
- Processing-output validation is available as a read-side helper outside the
  hot path. It validates a processed batch against `RadarProcessingResult`,
  before/after source snapshots, and optional previous metrics.
- Output validation detects missing work, duplicate work, source-local order
  violations, result/snapshot metric mismatches, and missing partitioned
  telemetry for valid `PartitionedBarrier` results.
- Processing checksum construction is shared between the runtime state store and
  the output validator so both compare the same source/event checksum contract.
- Source-local handler slots are available as a small extension platform for
  future source-local algorithms. Options can carry configured handler
  instances; the state store precomputes handler slot layout and allocates dense
  `long`/`double` slots per `SourceId`.
- Handlers receive event metadata, payload span, and precomputed payload metrics
  through `RadarSourceProcessingHandlerContext`, then mutate only their
  source-local `RadarSourceProcessingState` view.
- Handler snapshot projection is read-side. Snapshot fields are declared by
  descriptors, duplicate field names are rejected across handlers, and the hot
  path does not do per-event field-name lookup.
- `RadarProcessingCore` exposes `GetSourceHandlerSnapshot` and
  `CreateSourceHandlerSnapshots`. The no-handler path remains valid and does
  not require payload span materialization for handlers.
- Synthetic processing-only benchmarks are available through
  `RadarProcessingSyntheticBenchmark` over prebuilt deterministic
  `RadarEventBatch` workloads.
- Benchmark setup constructs the synthetic workload before the measured loop;
  warmup iterations also run before stopwatch and allocation snapshots.
- Benchmark results report execution mode, topology, handler set, iterations,
  warmup iterations, per-iteration and total batch/event/payload counters,
  raw checksum, active source count, validation checksum, shard distribution,
  elapsed time, throughput, and allocated bytes per event/value.
- Benchmark handler sets currently support `None` and `CounterChecksum`, giving
  a stable no-handler baseline and a simple source-local handler workload.
- Latest Release processing-only benchmark used a synthetic prebuilt
  `RadarEventBatch` workload shaped close to the milestone 004 single-file
  normalized stream benchmark: 32_400 sources, 1 batch, 32_400 stream events per
  iteration, 1_196 payload values per stream event, 38_750_400 payload values
  per iteration, 20 measured iterations, and 3 warmup iterations.
- Latest Release processing-only benchmark results:

  ```text
  mode              handlers          payload values/s   stream events/s   allocated bytes / payload value
  sequential        none              2_559_218_888.23   2_139_815.12      0.00
  partitioned 24/24 none              2_622_669_443.85   2_192_867.43      0.03
  sequential        counter-checksum  1_630_968_124.27   1_363_685.72      0.03
  partitioned 24/24 counter-checksum  1_745_635_000.27   1_459_561.04      0.06
  ```

- Compared with milestone 004 normalized stream single-file throughput
  (`553_123_110.90` payload values/s), the latest processing-only baseline is
  roughly `4.63x` faster for sequential/no-handler, `4.74x` faster for
  partitioned/no-handler, `2.95x` faster for sequential/counter-checksum, and
  `3.16x` faster for partitioned/counter-checksum.
- Compared with milestone 004 cache-wide normalized stream throughput
  (`509_716_417.97` payload values/s), the same processing-only results are
  roughly `5.02x`, `5.15x`, `3.20x`, and `3.42x` faster respectively.
- The processing-only result is not directly measuring multi-core scaling:
  current `PartitionedBarrier` execution is still synchronous and measures the
  static routing/barrier/shard-loop contour. Worker execution and live
  partition-level rebalance remain future milestones.
- The visible remaining performance risk inside milestone 005 is routing-buffer
  allocation in the partitioned path: the measured allocation ratios are
  `40.33` bytes per stream event without handlers and `72.33` bytes per stream
  event with the counter/checksum handler workload.
- `processing benchmark synthetic` is available as a manual CLI command over
  the synthetic processing benchmark harness.
- The CLI command accepts execution mode, source count, batch shape, topology,
  handler set, iteration count, and warmup iteration options.
- CLI output names the measured contour explicitly and states that measured
  time excludes decompression, Archive Two scanning, identity normalization,
  and `RadarEventBatch` construction.
- Source validation runs before execution for both modes, so invalid source ids
  and ownership mismatches do not mutate state.
- Partitioned and sequential results/snapshots are verified for parity on the
  current deterministic workload.
- Focused contract tests cover default options, invalid execution modes,
  invalid topology counts, validation result invariants, result shape, and empty
  result construction.
- Focused topology tests cover contiguous source blocks, complete source-id
  coverage, stable assignments, shard range mapping, null inputs, too many
  partitions, invalid source/partition ids, and invalid partition ranges.
- Focused state-store tests cover source-universe sizing, single-source update
  isolation, unique active-source counting, snapshot projection, aggregate
  metrics, empty metrics, invalid inputs, and timestamp regression.
- Focused payload-reader tests cover 8-bit payload reads, 16-bit big-endian
  payload reads, batch-level parity with `RadarEventBatchMetrics`, empty batch
  metrics, metric addition, null batch rejection, length mismatch rejection,
  out-of-range payload rejection, and unsupported word size rejection.
- Focused sequential-core tests cover constructor validation, null batch,
  cancellation before processing, unsupported stream schema, source-universe
  version mismatch, empty batch result, multi-source metrics/snapshots,
  cumulative processing across batches, source id outside universe, source
  ownership mismatch, and source-local timestamp regression.
- Focused guardrail tests cover owned/leased parity, leased-buffer reuse,
  sequential counter parity with `RadarEventBatchMetrics`, invalid batch
  accounting, and invalid-source state isolation.
- Focused batch-router tests cover empty routes, every event routed exactly
  once, topology assignment parity, same-source order preservation inside
  partition/shard routes, metrics parity with `RadarEventBatchMetrics`,
  payload-mutation stability after routing, null/mismatched input rejection,
  source id outside topology, and route lookup bounds.
- Focused partitioned-barrier tests cover sequential metric/snapshot parity,
  owned/leased parity, same-source ordering, unsupported stream schema,
  source-universe mismatch, invalid source id before mutation, source ownership
  mismatch before mutation, and source-local timestamp regression.
- Focused telemetry tests cover partitioned result telemetry, empty-batch
  telemetry, deterministic hot partition/shard ids, leased-buffer stability,
  and sequential results staying free of partitioned telemetry.
- Focused output-validator tests cover valid sequential output, valid
  partitioned output with telemetry, missing processed event detection,
  duplicate processed event detection, source-local order violation detection,
  and missing partitioned telemetry detection.
- Focused handler-slot tests cover non-overlapping slot offsets, duplicate
  snapshot field rejection, source-local handler state isolation, core handler
  invocation and snapshot projection, payload-span delivery, payload-aware
  apply requirements when handlers are configured, and no-handler base-path
  behavior.
- Focused synthetic benchmark tests cover stable sequential iteration totals,
  partitioned shard distribution, warmup exclusion from measured totals,
  sequential/partitioned validation-checksum parity, and invalid input
  guardrails.
- Verification after slice 1:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore`
  passed with 151 tests passed and 3 skipped.
- Verification after slice 2:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore`
  passed with 160 tests passed and 3 skipped.
- Verification after slice 3:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore`
  passed with 168 tests passed and 3 skipped.
- Verification after slice 4:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore`
  passed with 177 tests passed and 3 skipped.
- Verification after slice 5:
  `dotnet test --no-restore` passed with 188 tests passed and 3 skipped.
- Verification after slice 6:
  `dotnet test --no-restore` passed with 193 tests passed and 3 skipped.
- Verification after slice 7:
  `dotnet test --no-restore` passed with 202 tests passed and 3 skipped.
- Verification after slice 8:
  `dotnet test --no-restore` passed with 211 tests passed and 3 skipped.
- Verification after slice 9:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulse.Tests.Processing`
  passed with 73 tests passed.
- Verification after slice 9:
  `dotnet test --no-restore` passed with 216 tests passed and 3 skipped.
- Verification after slice 9:
  `git diff --check` passed. Scoped formatting verification for the changed
  processing files passed with the scoped
  `dotnet format --verify-no-changes --no-restore --include ...` command. The
  repo-wide formatting command still reports pre-existing whitespace in
  `tests\RadarPulse.Tests\Archive\NexradArchiveReplayPublisherTests.cs`.
- Verification after slice 10:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulse.Tests.Processing`
  passed with 79 tests passed.
- Verification after slice 10:
  `dotnet test --no-restore` passed with 222 tests passed and 3 skipped.
- Verification after slice 10:
  `git diff --check` passed. Scoped formatting verification for the changed
  processing files passed with
  `dotnet format --verify-no-changes --no-restore --include ...`.
- Verification after slice 11:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulse.Tests.Processing`
  passed with 86 tests passed.
- Verification after slice 11:
  `dotnet test --no-restore` passed with 229 tests passed and 3 skipped.
- Verification after slice 11:
  `git diff --check` passed. Scoped formatting verification for the changed
  processing files passed with
  `dotnet format --verify-no-changes --no-restore --include ...`.
- Verification after slice 12:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulse.Tests.Processing`
  passed with 91 tests passed.
- Verification after slice 12:
  `dotnet test --no-restore` passed with 234 tests passed and 3 skipped.
- Verification after slice 12:
  `git diff --check` passed. Scoped formatting verification for the changed
  processing files passed with
  `dotnet format --verify-no-changes --no-restore --include ...`.
- Verification after the CLI benchmark command:
  `dotnet run --no-restore --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode partitioned --sources 4 --batches 1 --events-per-batch 8 --payload-values 2 --partitions 4 --shards 2 --handlers counter-checksum --iterations 1 --warmup-iterations 0`
  passed.
- Verification after the CLI benchmark command:
  `dotnet test --no-restore` passed with 234 tests passed and 3 skipped.
- Verification after the CLI benchmark command:
  `git diff --check` passed with only Git line-ending warnings for touched
  files.
- Latest Release processing-only benchmark verification:

  ```powershell
  dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore
  dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode sequential --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 1 --shards 1 --handlers none --iterations 20 --warmup-iterations 3
  dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode partitioned --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 24 --shards 24 --handlers none --iterations 20 --warmup-iterations 3
  dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode sequential --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 1 --shards 1 --handlers counter-checksum --iterations 20 --warmup-iterations 3
  dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode partitioned --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 24 --shards 24 --handlers counter-checksum --iterations 20 --warmup-iterations 3
  ```

  Result: Release build passed; measured payload values/s were
  `2_559_218_888.23`, `2_622_669_443.85`, `1_630_968_124.27`, and
  `1_745_635_000.27` for the four commands above.
- Verification at milestone 005 closeout:
  `dotnet test --no-restore` passed with 234 tests passed and 3 skipped.
- Verification at milestone 005 closeout:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- Milestone 005 committed checkpoints before closeout:
  `d9106b0 Add processing core contracts`;
  `4639ec0 Add static processing topology`;
  `33c437a Add dense source processing state`;
  `3a3ce88 Add processing payload reader`;
  `e04265d Avoid ref parameter in payload reader`;
  `981afa1 Add sequential processing core`;
  `38296b6 Add processing core guardrail tests`;
  `5b65852 Add partitioned batch routing substrate`;
  `e900c54 Add partitioned barrier processing path`;
  `5b573d1 Add partitioned processing telemetry`;
  `c6cdd94 Add processing output validation`;
  `eb22723 Add source processing handler slots`;
  `e40aece Add processing synthetic benchmark`.

Completed in milestone 004 implementation:

- `RadarEventBatch`.
- `RadarStreamEvent`.
- `StreamSchemaVersion`.
- `DictionaryVersion`.
- `SourceUniverseVersion`.
- `RadarStreamWordSize`.
- `RadarStreamStatusModel`.
- `RadarStreamEvent` is explicitly sized at 64 bytes and contains no reference
  fields.
- `RadarEventBatch` carries stream schema, dictionary, and source-universe
  versions, event memory, payload memory, and explicit owned/leased lifetime.
- `RadarEventBatch` validates event payload references against batch payload
  storage and rejects mismatched gate-count/word-size payload lengths.
- Focused streaming contract tests cover event layout, version metadata,
  payload range validation, and version value validation.
- `DenseIdentityCatalog` implements append-only dense text-to-id mappings for
  small stream identity dimensions.
- `DenseIdentityCatalog` exposes `string`, `ReadOnlySpan<char>`, and
  `ReadOnlySpan<byte>` lookup views over the same canonical entries.
- Existing catalog ids remain stable; new valid unknown identities append under
  a serialized registration gate.
- Reverse lookup is backed by a dense id-indexed array, so assigned ids satisfy
  `0 <= id < Count`.
- Invalid identity text is not registered. The initial canonical policy accepts
  only non-empty `A-Z`, `0-9`, and underscore text within the configured maximum
  length.
- Focused dense-catalog tests cover lookup-view equivalence, dense append-only
  ids, reverse lookup, invalid identity rejection, concurrent duplicate
  registration, concurrent distinct registration, and partial-entry visibility.
- `DenseIdentityCatalog` now exposes `CurrentVersion`, immutable
  `DenseIdentityCatalogSnapshot` views, and append-only
  `DenseIdentityCatalogDelta` views.
- The empty catalog starts at `DictionaryVersion.Initial`; each new identity
  append advances the catalog version, while duplicate registration keeps the
  current version unchanged.
- A snapshot for version `N` exposes only entries visible at `N`. Later appends
  do not mutate existing snapshots.
- A delta from version `N` to a later version contains only the dense appended
  entries needed to reconstruct the later snapshot.
- Focused versioning tests cover version-scoped snapshot visibility, delta
  reconstruction, published forward/reverse lookup, duplicate registration
  version stability, immutable old snapshots, empty deltas, and rejection of
  versions that are not yet visible.
- `DenseIdentityCanonicalizationPolicy` makes identity validation explicit
  instead of hardcoding one rule inside the catalog.
- Built-in policies now cover radar codes and moment names separately. Radar
  codes require exactly four uppercase ASCII letters or digits; moment names
  allow compact uppercase ASCII letters, digits, and underscores up to eight
  characters.
- Canonicalization intentionally does not trim input and does not fold case.
  Lowercase, padding, trailing spaces, unsupported characters, and non-ASCII
  byte values are invalid rather than silently normalized.
- `DenseIdentityValidationResult` exposes validation error, input kind, length,
  invalid position, and invalid value for diagnostics without registering
  invalid identities.
- Focused canonicalization tests cover radar and moment policy differences,
  no-trim/no-case-fold behavior, validation diagnostics, UTF-8 byte validation,
  and exception messages containing catalog, dimension, and reason.
- `RadarSourceKey` defines the dense source tuple:
  `RadarOrdinal x ElevationSlot x AzimuthBucket x RangeBand`.
- `RadarSourceUniverse` defines source-universe metadata and arithmetic:
  version, dimension counts, per-dimension source strides, source count,
  `RadarSourceKey -> SourceId`, and `SourceId -> RadarSourceKey`.
- `SourceId` values are dense in `0 <= SourceId < SourceCount`, and every radar
  ordinal owns a contiguous source-id block.
- Adding a new radar ordinal with the same per-radar dimensions keeps existing
  radar-zero source IDs stable and starts the new radar at the next contiguous
  block.
- Source-universe layout compatibility is explicit: the same
  `SourceUniverseVersion` can be reused only for the same source layout.
- Focused source-universe tests cover count/stride calculation, dense id space,
  tuple/id round-trip, radar block boundaries, stable existing blocks when a
  radar is added, invalid dimension rejection, and version/layout compatibility.
- `RadarStreamIdentityNormalizer` now resolves radar code, moment name,
  elevation slot, azimuth bucket, and range band into dense `RadarOrdinal`,
  `MomentId`, and `SourceId` values.
- The normalizer owns radar and moment dense catalogs with the radar-code and
  moment-name canonicalization policies.
- Known identities use a read-mostly path. Unknown valid identities append
  through a serialized cold registration path; invalid text and invalid source
  tuples do not mutate dictionaries.
- The normalizer publishes an aggregate `DictionaryVersion` across radar and
  moment catalogs, plus `RadarStreamDictionarySnapshot` views for a requested
  aggregate version.
- Resolved identities carry `DictionaryVersion` and `SourceUniverseVersion`,
  so a future `RadarEventBatch` can use the same version metadata.
- Focused normalizer tests cover stable identity resolution, repeated lookup
  version stability, unknown valid appends, dictionary snapshots for resolution
  versions, invalid radar/moment rejection without mutation, source out-of-range
  rejection without mutation, one-radar source-universe capacity limits, UTF-8
  input equivalence, and throwing normalization failure behavior.
- `RadarEventBatchBuilder` builds normalized `RadarEventBatch` values from
  `RadarStreamIdentity`, event metadata, and raw payload bytes.
- The builder owns event and payload buffers internally. `Build()` returns owned
  snapshot arrays; leased hot-path publication borrows the current buffers only
  during the synchronous callback.
- `PayloadOffset` and `PayloadLength` are assigned by the builder. Payload bytes
  are copied at append time so later mutation of parser/external buffers cannot
  change the batch payload.
- The builder validates payload length against `GateCount * WordSize`, rejects
  invalid identity versions, and requires all events in one batch to share the
  same `SourceUniverseVersion`.
- The batch dictionary version is the highest dictionary version visible among
  appended identities. Empty batches use initial dictionary and source-universe
  versions.
- Focused builder tests cover payload copying and offsets, multi-event payload
  accumulation, append-order preservation across different source IDs, owned
  build snapshots, empty build behavior, invalid payload rejection without
  mutation, source-universe version mismatch rejection, and invalid identity
  version rejection.
- `IArchiveRadarEventBatchPublisher` defines the first batch-stream publisher
  boundary without replacing the milestone 003 semantic replay publisher.
- `NexradArchiveRadarEventBatchPublisher` implements sequential single-file
  Archive Two replay into normalized `RadarEventBatch` output.
- `ArchiveTwoRadarEventBatchProjector` parses Type 31 moment blocks directly
  into gate-run stream events, preserving raw 8-bit and 16-bit moment payload
  bytes, word size, scale, offset, source order, and lifetime-scoped payload
  storage.
- The first replay integration publishes one batch per file. It uses the
  identity normalizer and batch builder, and emits numeric `RadarOrdinal`,
  `MomentId`, and `SourceId` values instead of text.
- The default batch replay source universe is a one-radar demonstration layout:
  32 elevation slots x 720 azimuth buckets x 1 range band = 23,040 logical
  sources.
- `ArchiveRadarEventBatchCountingPublisher` records batch count, stream-event
  count, payload bytes, payload value count, raw-value checksum, and visible
  stream/dictionary/source-universe versions.
- Focused replay-integration tests cover sequential single-file batch replay,
  dictionary snapshot visibility, raw payload bytes, 16-bit raw-value checksum,
  and splitting one moment block into range-band gate-run events.
- `RadarEventBatchMetrics` computes deterministic per-batch event count,
  payload byte count, payload value count, raw-value checksum, and structural
  metadata checksum over batch headers and event metadata.
- `RadarStreamDictionarySnapshotMetrics` computes stable externally visible
  dictionary snapshot counts and mapping checksum for versioned radar/moment
  catalog snapshots.
- `RadarEventBatchValidator` validates the processing-core input contract
  outside the hot path: supported stream schema version, source-universe version
  match, optional dictionary snapshot version match, non-decreasing batch
  chronology, contiguous payload references, no unreferenced payload tail,
  source-id/source-dimension agreement, dictionary-id visibility, and optional
  expected metrics/checksum match.
- `ArchiveRadarEventBatchCountingPublisher` now uses `RadarEventBatchMetrics`
  for payload value counts and raw-value checksum, keeping replay counters and
  validation checksums on one shared interpretation.
- Focused validator tests cover valid metrics, out-of-order chronology,
  source-id range errors, source-id/source-dimension mismatch, non-contiguous
  payload references, unreferenced payload tails, dictionary version mismatch,
  invisible dictionary IDs, expected metrics mismatch, and stable dictionary
  snapshot mapping checksums.
- `ArchiveRadarEventBatchPublishOptions` now carries `DegreeOfParallelism` for
  normalized batch replay while preserving the existing one-radar default.
- `NexradArchiveRadarEventBatchPublisher` supports ordered-parallel single-file
  batch replay. Workers read and decompress compressed records concurrently
  into a bounded in-flight window, but the ordered drain feeds decompressed
  records into one `ArchiveTwoRadarEventBatchProjector`.
- Dynamic dictionary registration therefore happens only during ordered
  emission, so worker completion order cannot change `RadarOrdinal`,
  `MomentId`, `DictionaryVersion`, payload bytes, or event metadata.
- Focused parity tests compare sequential and parallel batch replay for the
  same synthetic file, including event sequence, payload bytes, batch metrics,
  dictionary snapshot checksum, validator result, and moment-id registration
  order under an intentionally delayed first record.
- `archive stream --file ... [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]` is implemented as the
  first manual CLI smoke command for the normalized `RadarEventBatch` stream.
- The stream command prints stream schema, dictionary version,
  source-universe version, logical source count, compressed/decompressed byte
  counts, batch count, event count, payload bytes, payload value count,
  raw-value checksum, radar/moment dictionary entry counts, and dictionary
  mapping checksum.
- `ArchiveRadarEventBatchStreamBenchmarkResult` and
  `NexradArchiveRadarEventBatchStreamBenchmark` add a repeatable benchmark for
  the normalized batch stream.
- `archive benchmark stream --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]` reports per-iteration
  stream counters, total throughput, `Stream events/s`, `Payload values/s`,
  allocation totals, and allocation ratios per stream event and payload value.
- The benchmark verifies that every measured iteration produces the same stream
  versions, counts, raw-value checksum, and dictionary mapping checksum.
- Focused benchmark tests cover stable iteration totals over a synthetic Archive
  Two file.
- The first benchmark result is intentionally conservative: it measures Archive
  Two replay into the normalized batch stream, including BZip2 decompression,
  message scanning, Type 31 parsing, identity normalization, source-id
  calculation, batch event construction, lifetime-scoped payload copying,
  counting, and checksum work.
- The gap between earlier decoder-level throughput and the normalized stream
  benchmark is currently attributed to payload copying, ordered-drain
  serialization after parallel decompression, extra decompressed-record
  buffering in the parallel path, and high allocation pressure.
- The first allocation/buffer-churn pass changed the parallel batch replay path
  so decompressed payload storage belongs to the worker and is reused across
  records. The worker is returned to the available pool only after ordered scan
  consumes its decompressed payload, preserving deterministic dictionary
  registration while avoiding a separate decompressed-record buffer owner per
  compressed record.
- This pass improved parallel stream benchmark throughput materially, but did
  not remove the main allocation sources. The next optimization targets are
  reusable stream publish sessions, batch-builder payload/event buffer reuse,
  and reducing string allocations in Type 31 moment-name extraction.
- The next stream optimization pass removed per-block Type 31 moment-name
  string allocation by reading the 3-byte moment name as a byte span, caches
  radar/moment dimensions per moment code after the first ordered dictionary
  normalization, uses source-universe stride arithmetic in the cached path, and
  pre-sizes archive stream event/payload buffers from compressed file size and
  compressed record count. This keeps deterministic dictionary registration on
  the ordered scan path while avoiding most builder resize churn.
- The follow-up stream optimization pass added a one-shot
  `RadarEventBatchBuilder.BuildAndReset()` path used by Archive Two projection.
  This transfers builder-owned event and payload buffers into the final
  `RadarEventBatch` instead of copying them, while preserving the existing
  snapshot-copy semantics of `Build()`. Builder-created batches also carry
  cached payload value count and raw-value checksum, allowing the archive batch
  counting publisher to avoid a second full payload scan.
- `archive benchmark stream` now supports cache-wide benchmark selection with
  `--cache`, optional `--date`, optional `--radar`, and `--max-files`. It
  reports examined/skipped/published file counts, aggregate stream totals,
  throughput, and allocation ratios for the normalized `RadarEventBatch`
  construction path.
- The latest projector hot-path pass keeps current dictionary version,
  source-universe version, source-universe strides, and volume timestamp ticks
  as projector-local fields. The cached identity path no longer reads the
  normalizer's volatile dictionary version for every stream event.
- `NexradArchiveRadarEventBatchPublishSession` now gives stream benchmarks a
  reusable file-publish context. It reuses decompression workers, worker-owned
  buffers, the available-worker stack, and the in-flight task dictionary across
  repeated single-file and cache-wide benchmark runs.
- `RadarEventBatch` now exposes an explicit `Lifetime`: `Owned` batches may be
  retained, while `Leased` batches are valid only during the synchronous publish
  callback. Consumers that need to keep a leased batch must call
  `ToOwnedSnapshot()`.
- `RadarEventBatchBuilder.ConsumeLeased()` gives the hot path a borrowed batch
  view and then resets counters while retaining event and payload buffer
  capacity for reuse.
- `NexradArchiveRadarEventBatchPublishSession` now reuses the
  `ArchiveTwoRadarEventBatchProjector` and its builder buffers across file
  publishes. The non-session `NexradArchiveRadarEventBatchPublisher` keeps the
  owned batch behavior for safer external capture tests and one-off publishing.

Completed in milestone 003:

- `docs/milestones/003-historical-replay-publisher-plan.md`.
- `docs/milestones/003-historical-replay-publisher.md`.
- `docs/handoff.md` updated to point at milestone 003.
- `IArchiveReplayEventPublisher`.
- `ArchiveReplayPublishOptions`.
- `ArchiveReplayPublishResult`.
- `ArchiveReplayCachePublishResult`.
- `ArchiveReplayPublishCacheBenchmarkResult`.
- `ArchiveReplayCountingPublisher`.
- `NexradArchiveReplayPublisher` sequential single-file replay path.
- `NexradArchiveReplayPublisher` ordered parallel replay path.
- `NexradArchiveReplayPublishSession` reusable count-only replay runner.
- `archive replay --file ... [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`.
- `archive replay --cache ... [--date yyyy-MM-dd] [--radar KTLX]
  [--max-files n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`.
- `archive benchmark replay-publish --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`.
- `archive benchmark replay-publish --cache ... [--date yyyy-MM-dd]
  [--radar KTLX] [--max-files n] [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`.
- Focused unit tests for source-order publication, counters/checksums,
  sequential/parallel equivalence, custom publisher ordered drain, non-Archive
  Two diagnostics, invalid parallelism, cancellation, and replay-publish
  benchmark iteration consistency, repeated reusable-session parity, and
  reusable-session disposal behavior, plus cache replay selection/skip
  aggregation and cache replay-publish benchmark iteration consistency.

Completed in milestone 002:

- `archive inspect --file`.
- Archive Two base-data classification for files starting with `AR2V`.
- MDM/compressed-stream classification for `_MDM` and early `BZh` non-`AR2V`
  files.
- Unknown binary classification.
- 24-byte Archive Two volume header parsing.
- Archive Two compressed record boundary parsing from 4-byte signed big-endian
  control words.
- Per-record BZip2 signature detection.
- Per-record BZip2 decompression byte counting through the shared BZip2
  decompressor abstraction.
- `archive benchmark decompress --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`.
- Benchmark path pools compressed-payload and output buffers to avoid measuring
  avoidable local buffer churn.
- Parallel benchmark mode scans compressed record boundaries in file order,
  decompresses independent BZip2 records concurrently, and aggregates results by
  original record index so worker completion order does not mix records.
- `radarpulse` is the default BZip2 backend after adding a reusable-workspace
  decoder to remove per-record managed BZip2 workspace allocations.
  SharpZipLib and SharpCompress remain selectable for comparison.
- BZip2 decompression sessions now expose a streaming/chunk callback so future
  parsing can consume decompressed bytes without materializing full records.
- `archive validate decompress` compares the default `radarpulse` backend
  against SharpZipLib record-by-record with streaming hashes.
- Decompressed Archive Two bytes are now scanned through the streaming callback
  for RDA/RPG message headers.
- Minimal Message Type 31 parsing reports radial counts and gate-count totals
  for generic moment data blocks.
- `archive inspect --file` reports message counts by type, Type 31 radial
  counts, estimated gate-moment events, and moment gate/radial totals.
- `archive inspect --file` reports Type 31 `VOL`/`ELV`/`RAD` constant block
  counts and sweep summaries from radial status, elevation number, cut sector,
  elevation angle, moment membership, and source order.
- Type 31 sweep summaries carry explicit source order as compressed record,
  message-in-record, and Type 31 radial sequence positions for future ordered
  replay publishing.
- Type 31 generic moment descriptors now summarize per-moment gate count range,
  word size, first-gate range, gate spacing, scale, and offset. CLI output
  documents the calibration formula `value=(raw-offset)/scale`.
- `archive benchmark parse` supports `--decode-calibrated-moments`. This mode
  reads raw 8/16-bit moment values, preserves Message Type 31 sentinel/status
  semantics, applies per-block scale/offset only to valid samples, and reports
  calibrated value counts, min/max, and a scaled checksum.
- `archive benchmark parse --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress] [--decode-moments]
  [--decode-calibrated-moments]`
  measures decompress+message-scan+minimal-Type31 throughput in estimated
  gate-moment events/s, and optionally reads actual 8/16-bit moment gate
  values or calibrated moment values with checksums.
- A first reusable Type 31 gate-moment event shape is implemented with radar id,
  volume timestamp, sweep/elevation/radial/gate identity, range, moment name,
  raw value, decoded status, optional calibrated value, and explicit source
  order.
- `archive benchmark replay-shape --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`
  projects ordered Type 31 gate-moment events and measures the cost of creating
  the replay-facing event shape before a downstream publisher exists.
- Replay-shape projection supports parallel compressed-record decoding. The
  parallel path first builds per-record starting projector states from Type 31
  radial transitions, then projects records concurrently and aggregates record
  results in original Archive Two record order.
- Replay-shape benchmark output includes an order-sensitive chronology checksum
  on every run, so parallel runs can be compared against sequential runs for
  event-order preservation, not just commutative totals.
- `archive validate replay-shape (--file path | --cache data/nexrad [--radar
  KTLX] [--max-files n]) [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]` compares sequential
  ordered projection against parallel replay-shape projection and reports
  calibrated-data unevenness by compressed record, sweep, radial, and minute.
- `archive inspect (--file path | --cache data/nexrad [--date yyyy-MM-dd]
  [--radar KTLX] [--max-files n])` can inspect a single file or aggregate a
  selected cache slice without failing on MDM/unknown files.
- Archive Two volume/framing helpers are centralized in `ArchiveTwoFileReader`
  instead of duplicated across inspector, benchmarks, and validators.
- The inspection path also uses the shared decompressor abstraction and pooled
  compressed-payload/output buffers.
- CLI output for size, kind, archive filename, version, extension number, radar
  id, volume timestamp, compressed record totals, and decompressed byte totals.
- Unit tests with small synthetic fixtures.

## Current Achievement Summary

The handoff state is a completed milestone 003 publisher-facing replay
foundation on top of the completed milestone 002 NEXRAD Archive Two decoder
foundation. Milestone 003 supports sequential single-file replay publishing,
ordered parallel replay publishing, cache-selection replay, and a reusable
steady-state count-only replay session used by the internal benchmarks. The
internal replay-publish benchmark also supports cache-wide measurement.

Achieved:

- RadarPulse recognizes cached Archive Two base-data files that start with
  `AR2V`.
- The reader parses the 24-byte volume header and reports archive filename,
  version, extension number, radar id, and volume timestamp.
- The reader parses Archive Two compressed record boundaries from 4-byte signed
  big-endian control words.
- Each internal BZip2 payload is decompressed per record. The file is correctly
  treated as an Archive Two container, not as one continuous BZip2 stream.
- `_MDM` and early `BZh` non-`AR2V` files are classified separately so they are
  not accidentally parsed as base-data volumes.
- Parallel decompression is implemented for independent compressed records.
  The implementation preserves order by scanning records in file order and
  writing worker results back by original record index.
- Benchmarking now compares the reusable-workspace `radarpulse` BZip2 backend
  with SharpZipLib and SharpCompress on the same Archive Two framing path.
- The `radarpulse` backend is currently the default because it preserves the
  measured byte counts while reducing measured per-record allocation by roughly
  three orders of magnitude on the current KTLX file.
- A local differential validation gate compares `radarpulse` against
  SharpZipLib across selected cached Archive Two files before parser work.
- The inspection path and benchmark path both use the shared BZip2 decompressor
  abstraction and pooled compressed-payload/output buffers.
- The message scanner now validates the RDA/RPG header enough to avoid
  byte-shift false positives in real KTLX records.
- The current KTLX smoke file reports 6_496 messages, including 6_480 Type 31
  radials, and 38_759_040 estimated gate-moment events.
- The current KTLX smoke file reports 12 Type 31 sweeps, 6_480 `VOL`,
  6_480 `ELV`, and 6_480 `RAD` constant blocks, with sweep source ranges
  ordered by compressed record/message/radial position.
- The current KTLX smoke file reports stable descriptor metadata such as
  `REF scale=2 offset=66`, `VEL scale=2 offset=129`, `ZDR scale=32 offset=418`,
  and 0.25 km gate spacing for the observed moments.
- Calibrated decoding on the current KTLX smoke file reports 5_523_459 valid
  calibrated values per volume, 27_316_941 below-threshold values, 1_355
  range-folded values, 5_794_484 CFP filter-not-applied values, 65_871 CFP
  point-clutter-filter values, 56_930 CFP dual-pol-filtered values, no reserved
  or unsupported values, and a calibrated range of `-31.5..359.649`.
- Replay-shape projection on the current KTLX smoke file generates 38_759_040
  ordered gate-moment events per volume, with the same raw checksum
  `1_063_626_011`, calibrated checksum `70_028_121_122`, valid/status counts,
  calibrated range `-31.5..359.649`, range span `2.125..459.875` km, and
  chronology checksum `5_257_350_734_454_804_390`. Sequential and parallel runs
  produced the same chronology checksum.
- Cache-wide KTLX replay-shape validation examined 244 files, skipped 24
  non-base-data files, compared 220 Archive Two base-data files, found zero
  sequential/parallel mismatches, and reported 8_513_587_200 replay-shaped
  events with 1_369_194_138 valid calibrated events.
- The cache-wide unevenness report found the largest compressed-record valid
  share spread in `KTLX20260504_032003_V06`: record 51 had 8.592% valid events
  while record 13 had 50.437%. The largest sweep spread was also in
  `KTLX20260504_032003_V06`: sweep 11 had 9.187% valid events while sweep 2 had
  44.909%.
- Replay-shape validation also reports radial and minute-bucket valid-share
  spreads using message timestamps from the RDA/RPG message header.
- Cache inspection can aggregate selected local cache files and report file-kind,
  compressed-record, decompressed-byte, message, Type 31 radial, and estimated
  gate-moment totals.
- Cache-selection replay can publish a selected cache slice with
  `archive replay --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX]
  [--max-files n]`, reusing one replay publish session across files, skipping
  non-Archive Two files, and aggregating status totals and checksums in selected
  cache order.
- Full local KTLX cache replay for `2026-05-04` examined 244 files, skipped 24
  non-base-data files, published 220 Archive Two files, and reported
  8_513_587_200 published events with 1_369_194_138 valid events.
- Full local KTLX cache replay-publish benchmark for `2026-05-04` validated two
  full cache iterations with the same chronology checksum and measured
  310_665_492.15 published events/s with 0.06 allocated bytes/event.
- The parse benchmark now gives a first measured answer against the 20M
  events/s target for decompression plus minimal parsing.
- With `--decode-moments`, the same KTLX file decodes all 38_759_040 raw
  gate-moment values per iteration and measures above the 20M values/s target.
- The latest Release performance rerun measured 910.77 decompressed MB/s,
  501_164_693 minimal-parse estimated events/s, 670_226_077 calibrated-parse
  decoded values/s, and 230_347_912 replay-shaped events/s on
  `KTLX20260504_000245_V06` with `radarpulse` and `--parallelism 24`.
- Calibrated parse is faster than replay-shape because it reads/classifies
  values and updates counters/checksums, while replay-shape also builds the
  publisher-facing event shape, carries source-order/time identity, computes
  order-sensitive chronology, and pays for the parallel projector prepass plus
  ordered aggregation. The slower replay-shape path still remains roughly 11.5x
  above the 20M events/s target on this file.

Deferred beyond milestone 003:

- No downstream event publisher is implemented yet.
- The parser/replay benchmarks still do not publish downstream engine events.
- The existing replay-shape benchmark/validator still have their own parallel
  projection loops instead of reusing the new production replay publisher path;
  this is not required for milestone 003 closure.

## Documentation

- `docs/milestones/001-historical-loader-plan.md`
- `docs/milestones/001-historical-loader.md`
- `docs/milestones/001-historical-loader-decision-trace.md`
- `docs/milestones/001-historical-loader-closeout.md`
- `docs/milestones/002-nexrad-archive-inspection-plan.md`
- `docs/milestones/002-nexrad-archive-inspection.md`
- `docs/milestones/002-nexrad-archive-inspection-decision-trace.md`
- `docs/milestones/002-nexrad-archive-inspection-closeout.md`
- `docs/milestones/003-historical-replay-publisher-plan.md`
- `docs/milestones/003-historical-replay-publisher.md`
- `docs/milestones/003-historical-replay-publisher-decision-trace.md`
- `docs/milestones/003-historical-replay-publisher-closeout.md`
- `docs/milestones/004-processing-core-input-contract.md`
- `docs/milestones/004-processing-core-input-contract-plan.md`
- `docs/milestones/004-processing-core-input-contract-decision-trace.md`
- `docs/milestones/004-processing-core-input-contract-closeout.md`
- `docs/milestones/005-processing-core-architecture.md`
- `docs/milestones/005-processing-core-architecture-plan.md`
- `docs/milestones/005-processing-core-architecture-decision-trace.md`
- `docs/milestones/005-processing-core-architecture-closeout.md`
- `docs/handoff.md`

## Verification

Latest milestone 004 normalized stream benchmark verification:

```powershell
dotnet test RadarPulse.sln --no-restore
dotnet build src\Presentation\RadarPulse.Cli.csproj --no-restore
dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore
dotnet run --no-build --project src\Presentation\RadarPulse.Cli.csproj -- archive stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --parallelism 1 --decompressor radarpulse
dotnet run --no-build --project src\Presentation\RadarPulse.Cli.csproj -- archive stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --parallelism 4 --decompressor radarpulse
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- archive benchmark stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --iterations 3 --warmup-iterations 1 --parallelism 1 --decompressor radarpulse
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- archive benchmark stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --iterations 3 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- archive benchmark stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --iterations 5 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- archive benchmark stream --cache data\nexrad --max-files 1000000 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse
```

Result:

```text
tests: 142 passed, 3 skipped
debug build: 0 warnings, 0 errors
release build: 0 warnings, 0 errors

parallelism 1 and 4 both produced:
Stream schema version: 1
Dictionary version: 9
Source-universe version: 1
Logical sources: 23_040
Compressed records: 55
Compressed bytes: 5_500_904
Decompressed bytes: 50_741_824
Batches: 1
Events: 32_400
Payload bytes: 48_257_280
Payload values: 38_759_040
Raw value checksum: 1_091_828_328
Radar dictionary entries: 1
Moment dictionary entries: 7
Dictionary mapping checksum: 15_566_013_436_132_944_234

Release benchmark stream after no-copy batch finalization and cached counters, parallelism 1:
Stream events per iteration: 32_400
Payload values per iteration: 38_759_040
Elapsed ms: 1_319.05
Stream events/s: 73_689.31
Payload values/s: 88_152_063.19
Allocated bytes / payload value: 1.61

Release benchmark stream after no-copy batch finalization and cached counters, parallelism 24:
Stream events per iteration: 32_400
Payload values per iteration: 38_759_040
Elapsed ms: 357.59
Stream events/s: 271_822.42
Payload values/s: 325_172_098.27
Allocated bytes / payload value: 4.51

Release benchmark stream after leased hot-path delivery and reusable projector buffers, parallelism 24, longer 5-iteration check:
Stream events per iteration: 32_400
Payload values per iteration: 38_759_040
Elapsed ms: 350.37
Stream events/s: 462_374.42
Payload values/s: 553_123_110.90
Allocated bytes: 180_080
Allocated bytes / payload value: 0.00

Release benchmark stream cache-wide after leased hot-path delivery and reusable projector buffers, parallelism 24:
Examined files per iteration: 244
Skipped files per iteration: 24
Published files per iteration: 220
Compressed records per iteration: 12_087
Decompressed bytes per iteration: 11_145_331_584
Stream events per iteration: 7_114_560
Payload values per iteration: 8_513_587_200
Elapsed ms: 16_702.60
Stream events/s: 425_955.35
Payload values/s: 509_716_417.97
Allocated bytes: 1_710_792_384
Allocated bytes / payload value: 0.20
```

Milestone 004 throughput achievement versus the milestone 003 count-only
`replay-publish` baseline:

```text
Comparable metric:
  milestone 003 Published events/s == milestone 004 Payload values/s
  milestone 004 Stream events/s is not comparable because one stream event
  references a payload range that can contain many raw values.

single file, parallelism 24:
  milestone 003 replay-publish: 362_695_693.02 published events/s
  milestone 004 normalized stream: 553_123_110.90 payload values/s
  delta: +190_427_417.88 values/s, +52.5%

cache-wide KTLX corpus, parallelism 24:
  milestone 003 replay-publish: 310_665_492.15 published events/s
  milestone 004 normalized stream: 509_716_417.97 payload values/s
  delta: +199_050_925.82 values/s, +64.1%

allocation tradeoff:
  milestone 003 single-file: 0.07 allocated bytes/event
  milestone 004 single-file: 0.00 allocated bytes/payload value
  milestone 003 cache-wide: 0.06 allocated bytes/event
  milestone 004 cache-wide: 0.20 allocated bytes/payload value
```

Assessment: milestone 004 has recovered and exceeded the earlier 300M+
throughput level while doing more structural work: normalized batch creation,
dense identity normalization, source-universe mapping, version visibility, and
explicit payload lifetime. The leased hot-path delivery pass removed the main
single-file batch buffer allocation cost and reduced cache-wide allocation from
`1.86` to `0.20` allocated bytes/payload value. Remaining performance work
should focus on cache-wide replay overhead outside the normalized batch buffers.

The skipped tests are the opt-in live AWS integration tests and opt-in local
corpus validation test.

Latest milestone 003 implementation verification:

```powershell
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
66 passed, 3 skipped
```

The skipped tests are the opt-in live AWS integration tests and opt-in local
corpus validation test.

Latest milestone 003 publisher smoke commands:

```powershell
$ReplayOutput = $null
$elapsed = Measure-Command {
    $script:ReplayOutput = & dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive replay --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --parallelism 1 --decompressor radarpulse
}
$ParallelReplayOutput = $null
$parallelElapsed = Measure-Command {
    $script:ParallelReplayOutput = & dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive replay --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --parallelism 24 --decompressor radarpulse
}
```

Result:

```text
parallelism 1:
Published events: 38_759_040
Valid events: 5_523_459
Raw value checksum: 1_063_626_011
Calibrated value scaled checksum: 70_028_121_122
Chronology checksum: 5_257_350_734_454_804_390
Measured elapsed ms: 1_222.83
Measured published events/s: 31_696_112.78

parallelism 24:
Published events: 38_759_040
Valid events: 5_523_459
Raw value checksum: 1_063_626_011
Calibrated value scaled checksum: 70_028_121_122
Chronology checksum: 5_257_350_734_454_804_390
Measured elapsed ms: 592.17
Measured published events/s: 65_453_053.24
```

This is an external CLI smoke measurement after a Release build, so it includes
process startup overhead.

Latest milestone 003 cache replay smoke command:

```powershell
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive replay --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 2 --parallelism 24 --decompressor radarpulse
```

Result:

```text
Examined files: 2
Skipped files: 0
Published files: 2
Compressed records: 110
Compressed bytes: 10_848_033
Decompressed bytes: 101_483_648
Published events: 77_518_080
Valid events: 11_076_025
Raw value checksum: 2_135_395_556
Calibrated value scaled checksum: 140_796_164_125
Chronology checksum: 10_768_380_537_427_882_607
Chronology verification: required
```

The exact throughput is not measured by this smoke command; use
`archive benchmark replay-publish` for single-file steady-state performance.

Latest milestone 003 full cache replay smoke command:

```powershell
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive replay --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 1000000 --parallelism 24 --decompressor radarpulse
```

Result:

```text
Examined files: 244
Skipped files: 24
Published files: 220
File size bytes: 1_330_687_937
Compressed records: 12_087
Compressed bytes: 1_330_634_309
Decompressed bytes: 11_145_331_584
Published events: 8_513_587_200
Valid events: 1_369_194_138
Valid event share: 16.082%
Below-threshold events: 5_841_331_993
Range-folded events: 842_331
CFP filter-not-applied events: 1_277_128_201
CFP point-clutter-filter events: 14_296_674
CFP dual-pol-filtered events: 10_793_863
Reserved events: 0
Unsupported events: 0
Raw value checksum: 266_648_133_947
Calibrated value scaled checksum: 21_398_534_126_880
Chronology checksum: 9_060_754_844_693_896_318
```

Latest milestone 003 replay-shape comparison commands:

```powershell
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive benchmark replay-shape --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --iterations 5 --warmup-iterations 1 --parallelism 1 --decompressor radarpulse
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive benchmark replay-shape --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --iterations 5 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Result:

```text
parallelism 1:  50_671_150.52 replay-shaped events/s
parallelism 24: 248_026_584.81 replay-shaped events/s
chronology checksum per iteration: 5_257_350_734_454_804_390
```

Latest milestone 003 internal publisher benchmark commands:

```powershell
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive benchmark replay-publish --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --iterations 5 --warmup-iterations 1 --parallelism 1 --decompressor radarpulse
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive benchmark replay-publish --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --iterations 5 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive benchmark replay-publish --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 1000000 --iterations 2 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse
```

Result:

```text
parallelism 1:
Published events per iteration: 38_759_040
Published events/s: 51_754_463.69
Allocated bytes / event: 0.06

parallelism 24:
Published events per iteration: 38_759_040
Published events/s: 362_695_693.02
Allocated bytes / event: 0.07
Chronology checksum per iteration: 5_257_350_734_454_804_390

cache KTLX 2026-05-04, parallelism 24:
Iterations: 2
Examined files per iteration: 244
Skipped files per iteration: 24
Published files per iteration: 220
Published events per iteration: 8_513_587_200
Valid events per iteration: 1_369_194_138
Chronology checksum per iteration: 9_060_754_844_693_896_318
Published events/s: 310_665_492.15
Valid events/s: 49_962_649.20
Allocated bytes / event: 0.06
```

This benchmark now uses `NexradArchiveReplayPublishSession` inside the timed
loop. It removes per-command process startup and also reuses replay workers,
decompressor sessions, projectors, accumulators, and compressed/output buffers
across warmup and measured iterations. The older `replay-shape` benchmark keeps
its own benchmark workers outside its timed iteration window, so allocation and
throughput numbers are not one-to-one.

Current milestone 003 performance assessment:

- Sequential publisher throughput is acceptable for the milestone because
  `51_754_463.69` published events/s is above the initial 20M events/s target
  through the publisher path.
- Parallel publisher throughput is strong for the milestone because
  `362_695_693.02` published events/s confirms the ordered merge can preserve
  chronology while exceeding the target by a wide margin on the current KTLX
  smoke file.
- Worker/decompressor-session setup allocation is no longer visible as a major
  benchmark cost. Parallel allocation pressure is now about `0.07` bytes/event
  on the current smoke file; remaining allocation work should be driven by
  cache-wide replay profiling.

Likely remaining allocation contributors in the current publisher benchmark:

```text
per-file record descriptor and metadata arrays
per-record metadata radial arrays
Task/Parallel scheduling infrastructure
per-record event buffers in the custom publisher path
```

Potential later performance slice before treating replay as long-running
production profile:

```text
reuse or pool record descriptor and metadata-radial storage where practical
compare cache benchmark allocation before/after metadata storage reuse
profile whether Parallel/ConcurrentStack scheduling is visible after metadata allocation is reduced
```

Earlier milestone 003 planning slice changed documentation only.

Last verified normal command after the current milestone 002 slice:

```powershell
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
55 passed, 3 skipped
```

Manual CLI smoke tests:

```powershell
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_005834_V06_MDM
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --cache data/nexrad --date 2026-05-04 --radar KTLX --max-files 2
```

The first command classified the file as `Archive Two base data` and parsed
`AR2V0006.266`, version `06`, extension `266`, radar `KTLX`, and volume time
`2026-05-04T00:02:45.042Z`. It also found 55 compressed records, 5_406_610
compressed bytes, 55 records with BZip2 signatures, 55 decompressed records,
50_741_824 decompressed bytes, zero decompression diagnostics, 6_496 messages,
6_480 Type 31 radials, 38_759_040 estimated gate-moment events, 6_480 each of
`VOL`/`ELV`/`RAD` constant blocks, 12 sweep summaries, and descriptor metadata
for all observed moments. The second command classified the `_MDM` file as
`MDM or compressed stream`. The cache inspect smoke examined 2 KTLX files,
classified both as Archive Two base data, and aggregated 110 compressed records,
101_483_648 decompressed bytes, 12_992 messages, 12_960 Type 31 radials, and
77_518_080 estimated gate-moment events.

Last verified decompression validation command:

```powershell
dotnet run --no-restore --project src/Presentation/RadarPulse.Cli.csproj -- archive validate decompress --cache data/nexrad --radar KTLX --max-files 20
```

Result:

```text
Candidate decompressor: radarpulse
Reference decompressor: sharpziplib
Examined files: 22
Skipped files: 2
Compared files: 20
Failed files: 0
Compressed records: 1_100
Compressed bytes: 112_494_786
Decompressed bytes: 1_014_836_480
```

Last verified opt-in corpus command:

```powershell
$env:RADARPULSE_RUN_CORPUS_TESTS='true'; $env:RADARPULSE_NEXRAD_CORPUS='data/nexrad'; $env:RADARPULSE_NEXRAD_CORPUS_RADAR='KTLX'; $env:RADARPULSE_NEXRAD_CORPUS_MAX_FILES='20'; dotnet test RadarPulse.sln --no-restore --filter NexradArchiveDecompressionValidatorCorpusTests
```

Result:

```text
1 passed, 0 skipped
```

Last verified decompression benchmark command:

```powershell
dotnet run --no-restore -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark decompress --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 10 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Result on the current development machine:

```text
Decompressor: radarpulse
Iterations: 10
Warmup iterations: 1
Parallelism: 24
Compressed records per iteration: 55
Compressed bytes per iteration: 5_406_610
Decompressed bytes per iteration: 50_741_824
Elapsed ms: 467.16
Compressed MB/s: 115.73
Decompressed MB/s: 1_086.18
Records/s: 1_177.33
Allocated bytes: 1_243_568
Allocated bytes / decompressed MB: 2_450.78
Allocated bytes / record: 2_261.03
```

Historical SharpCompress baseline before the decoder comparison:

```text
Elapsed ms: 1_606.65
Compressed MB/s: 10.10
Decompressed MB/s: 94.75
Records/s: 102.70
Allocated bytes: 907_268_368
```

After adding the reusable-workspace `radarpulse` decoder, the Release comparison
on the same machine and file produced:

```text
iterations: 10
warmup iterations: 1

decompressor  parallelism  elapsed ms  decompressed MB/s  records/s  allocated bytes  allocated bytes / record
radarpulse    1            3_800.97    133.50             144.70     43_920           79.85
radarpulse    24           467.16      1_086.18           1_177.33   1_243_568        2_261.03
sharpziplib   24           643.11      789.01             855.22     2_511_390_704    4_566_164.92
```

Parallel decompression improves byte throughput substantially on the current
machine. Future parser/replay work must preserve file/message order when
publishing data: worker completion order is not a valid stream order.

Last verified parse benchmark command:

```powershell
dotnet run --no-restore -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 20 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse
```

Result on the current development machine:

```text
Decompressor: radarpulse
Iterations: 20
Warmup iterations: 2
Parallelism: 24
Messages per iteration: 6_496
Type 31 radials per iteration: 6_480
Estimated gate-moment events per iteration: 38_759_040
Elapsed ms: 1_035.20
Compressed MB/s: 104.46
Decompressed MB/s: 980.33
Messages/s: 125_502.63
Type 31 radials/s: 125_193.51
Estimated gate-moment events/s: 748_824_137.31
Allocated bytes: 25_297_992
Allocated bytes / estimated event: 0.03
```

The sequential Release parse benchmark on the same file and backend measured
about 90_930_375 estimated gate-moment events/s with `--parallelism 1`.

Last verified decoded moment benchmark command:

```powershell
dotnet run --no-restore -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 20 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse --decode-moments
```

Result on the current development machine:

```text
Decode moment values: True
Messages per iteration: 6_496
Type 31 radials per iteration: 6_480
Estimated gate-moment events per iteration: 38_759_040
Decoded gate-moment values per iteration: 38_759_040
Decoded gate-moment value checksum per iteration: 1_063_626_011
Elapsed ms: 1_174.67
Decompressed MB/s: 863.93
Estimated gate-moment events/s: 659_912_891.38
Decoded gate-moment values/s: 659_912_891.38
Allocated bytes: 25_314_800
Allocated bytes / decoded value: 0.03
```

The sequential Release decoded benchmark on the same file and backend measured
about 96_122_482 decoded gate-moment values/s with `--parallelism 1`.

Last verified calibrated moment benchmark command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 10 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse --decode-calibrated-moments
```

Result on the current development machine:

```text
Decode calibrated moment values: True
Decoded gate-moment values per iteration: 38_759_040
Calibrated gate-moment values per iteration: 5_523_459
Below-threshold gate-moment values per iteration: 27_316_941
Range-folded gate-moment values per iteration: 1_355
CFP filter-not-applied values per iteration: 5_794_484
CFP point-clutter-filter values per iteration: 65_871
CFP dual-pol-filtered values per iteration: 56_930
Reserved gate-moment values per iteration: 0
Unsupported calibrated gate-moment values per iteration: 0
Calibrated gate-moment value scaled checksum per iteration: 70_028_121_122
Calibrated value range per iteration: -31.5..359.649
Elapsed ms: 578.30
Estimated gate-moment events/s: 670_226_077.21
Decoded gate-moment values/s: 670_226_077.21
Calibrated gate-moment values/s: 95_512_331.01
Allocated bytes / calibrated value: 0.66
```

The sequential Release calibrated benchmark on the same file and backend
measured about 10_851_453 valid calibrated values/s, while still reading all
raw gate-moment values at about 76_146_475 decoded values/s.

Last verified replay-shape benchmark command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark replay-shape --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 5 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Result on the current development machine:

```text
Replay-shaped events per iteration: 38_759_040
Valid events per iteration: 5_523_459
Below-threshold events per iteration: 27_316_941
Range-folded events per iteration: 1_355
CFP filter-not-applied events per iteration: 5_794_484
CFP point-clutter-filter events per iteration: 65_871
CFP dual-pol-filtered events per iteration: 56_930
Reserved events per iteration: 0
Unsupported events per iteration: 0
Raw value checksum per iteration: 1_063_626_011
Calibrated value scaled checksum per iteration: 70_028_121_122
Chronology checksum per iteration: 5_257_350_734_454_804_390
Calibrated value range per iteration: -31.5..359.649
Range km per iteration: 2.125..459.875
Replay-shaped events/s: 230_347_912.41
Valid events/s: 32_826_335.48
Allocated bytes / event: 0.07
```

The calibrated parse benchmark is intentionally cheaper than replay-shape:
calibrated parse reads/classifies values and updates aggregate counters, while
replay-shape constructs full ordered event records and computes chronology.
The current replay-shape result is still roughly 11.5x above the 20M events/s
target.

Last verified sequential chronology smoke command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark replay-shape --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 1 --warmup-iterations 0 --parallelism 1 --decompressor radarpulse
```

Result:

```text
Chronology checksum per iteration: 5_257_350_734_454_804_390
Replay-shaped events per iteration: 38_759_040
Raw value checksum per iteration: 1_063_626_011
Calibrated value scaled checksum per iteration: 70_028_121_122
```

Latest replay-shape validation smoke command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive validate replay-shape --cache data/nexrad --radar KTLX --max-files 1 --parallelism 24 --decompressor radarpulse
```

Result:

```text
Compared files: 1
Failed files: 0
Record valid-share spread: 21.858%
Sweep valid-share spread: 18.336%
Radial valid-share spread: 27.012%
Minute valid-share spread: 14.757%
```

Previously verified full cache-wide replay-shape validation command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive validate replay-shape --cache data/nexrad --radar KTLX --parallelism 24 --decompressor radarpulse
```

Result on the current cache:

```text
Examined files: 244
Skipped files: 24
Compared files: 220
Failed files: 0
Replay-shaped events: 8_513_587_200
Valid events: 1_369_194_138
Valid event share: 16.082%
Reserved events: 0
Unsupported events: 0
Record valid-share spread top file: KTLX20260504_032003_V06, record 51 8.592% -> record 13 50.437%
Sweep valid-share spread top file: KTLX20260504_032003_V06, sweep 11 9.187% -> sweep 2 44.909%
```

Last verified normal command for milestone 001:

```powershell
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
25 passed, 2 skipped
```

The skipped tests are opt-in live AWS integration tests.

Last verified full opt-in command for milestone 001:

```powershell
$env:RADARPULSE_RUN_INTEGRATION_TESTS='true'; dotnet test RadarPulse.sln --no-restore
```

Result:

```text
27 passed, 0 skipped
```

Manual CLI smoke test used by the user:

```powershell
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive download --date 2026-05-04 --radar KTLX --output data/nexrad
```

The files downloaded successfully. Re-running the same command skipped existing
valid files.

## Cache Layout

Downloaded files are stored deterministically:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

Example for the manual smoke test:

```text
data/nexrad/level2/2026/05/04/KTLX/{fileName}
```

## Decoder Observations

Local inspection of the cached KTLX files showed:

```text
KTLX20260504_000245_V06 starts with AR2V0006...KTLX and later contains BZh9
KTLX20260504_005834_V06_MDM does not start with AR2V and contains BZh9 early
```

This supports the milestone 002 plan: first classify files, then parse Archive
II volume structure and its internal compressed records.

Additional documentation search found:

```text
ROC ICD 2620010J Archive II/User, Build 23.0:
  https://www.roc.noaa.gov/public-documents/icds/2620010J.pdf
ROC ICD 2620002Y RDA/RPG, Build 23.0:
  https://www.roc.noaa.gov/public-documents/icds/2620002Y.pdf
ROC ICD index:
  https://www.roc.noaa.gov/interface-control-documents.php
NCEI NEXRAD archive overview:
  https://www.ncei.noaa.gov/products/radar/next-generation-weather-radar
NCEI decoding utilities:
  https://www.ncei.noaa.gov/products/radar/decoding-utilities-examples
```

The expected base-data record shape is:

```text
24-byte Archive Two volume header
repeated records:
  4-byte big-endian signed control word
  abs(control word) bytes of bzip2-compressed Archive Two messages
```

The first compressed record contains metadata messages. Later records contain
radial messages, primarily Message Type 31, and may include Message Type 2 RDA
status messages. Message Type 31 represents one radial and contains pointers to
constant and moment data blocks.

## Constraints

- Live AWS tests remain opt-in because they require network access and public AWS
  availability.
- Do not use the deprecated `noaa-nexrad-level2` bucket for loader work.
- Large downloaded data and generated manifests under `data/` stay outside
  source control.
- Do not commit large real NEXRAD archive binary fixtures unless a deliberate fixture
  strategy is agreed first.
- Milestone 002 should avoid promising visualization, event processing,
  partitioning, production replay benchmarking, or live ingestion.
- The decompression throughput check should guide parser design toward the
  eventual 20M events/s replay target.
- Parallel decompression is allowed only behind an ordered merge or another
  explicit ordering contract; historical replay must not accidentally publish
  messages/events in worker completion order.
- Milestone 008 must preserve the borrowed batch lifetime rule: retained
  workers may live across callbacks, but work items that reference leased
  `RadarEventBatch` payload must complete before the provider callback returns.
- Milestone 008 should not use per-batch `Task.Run` as the transport model.
  The target is retained workers, bounded queues, coarse work items, and
  explicit completion barriers.
- Milestone 008 timeout handling is a health diagnostic, not permission to
  release borrowed payload while a worker may still read it.
- Milestone 008 async execution must remain comparable against the synchronous
  `PartitionedBarrier` correctness oracle and should stay selectable until
  benchmark evidence justifies any default change.

## Important Files

- `docs/milestones/004-processing-core-input-contract.md`
- `docs/milestones/004-processing-core-input-contract-plan.md`
- `docs/milestones/004-processing-core-input-contract-decision-trace.md`
- `docs/milestones/004-processing-core-input-contract-closeout.md`
- `docs/milestones/005-processing-core-architecture.md`
- `docs/milestones/005-processing-core-architecture-plan.md`
- `docs/milestones/005-processing-core-architecture-decision-trace.md`
- `docs/milestones/005-processing-core-architecture-closeout.md`
- `docs/milestones/006-partition-level-shard-rebalance.md`
- `docs/milestones/006-partition-level-shard-rebalance-plan.md`
- `docs/milestones/006-partition-level-shard-rebalance-decision-trace.md`
- `docs/milestones/006-partition-level-shard-rebalance-closeout.md`
- `docs/milestones/007-rebalance-production-hardening.md`
- `docs/milestones/007-rebalance-production-hardening-plan.md`
- `docs/milestones/007-rebalance-production-hardening-decision-trace.md`
- `docs/milestones/007-rebalance-production-hardening-closeout.md`
- `docs/milestones/008-retained-async-shard-transport.md`
- `docs/milestones/008-retained-async-shard-transport-plan.md`
- `docs/milestones/008-retained-async-shard-transport-decision-trace.md`
- `docs/milestones/008-retained-async-shard-transport-closeout.md`
- `docs/milestones/009-owned-payload-provider-decoupling.md`
- `docs/milestones/009-owned-payload-provider-decoupling-plan.md`
- `docs/milestones/009-owned-payload-provider-decoupling-performance-gate.md`
- `docs/milestones/009-owned-payload-provider-decoupling-decision-trace.md`
- `docs/milestones/009-owned-payload-provider-decoupling-closeout.md`
- `docs/milestones/010-owned-provider-overlap-cost-reduction.md`
- `docs/milestones/010-owned-provider-overlap-cost-reduction-plan.md`
- `docs/milestones/010-owned-provider-overlap-cost-reduction-performance-gate.md`
- `docs/milestones/010-owned-provider-overlap-cost-reduction-decision-trace.md`
- `docs/milestones/010-owned-provider-overlap-cost-reduction-closeout.md`
- `docs/milestones/011-queued-owned-default-readiness.md`
- `docs/milestones/011-queued-owned-default-readiness-plan.md`
- `src/Domain/Processing/RadarProcessingRetainedResourcePressureSnapshot.cs`
- `src/Domain/Processing/RadarProcessingRetainedResourcePressureSummary.cs`
- `src/Domain/Processing/RadarProcessingRetainedResourcePressureRecorder.cs`
- `src/Domain/Processing/RadarProcessingProviderQueueTelemetrySummary.cs`
- `src/Infrastructure/Processing/RadarProcessingOwnedBatchQueue.cs`
- `src/Infrastructure/Processing/RadarProcessingQueuedProcessingSession.cs`
- `src/Infrastructure/Processing/RadarProcessingQueuedRebalanceSession.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingRetainedResourcePressureContractTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingProviderQueueContractTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingProviderQueueTelemetryRecorderTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingOwnedBatchQueueTests.cs`
- `src/Domain/Processing/RadarProcessingAsyncExecutionOptions.cs`
- `src/Domain/Processing/RadarProcessingWorkerAffinity.cs`
- `src/Domain/Processing/RadarProcessingWorkerTimeoutPolicy.cs`
- `src/Domain/Processing/RadarProcessingWorkerGroupState.cs`
- `src/Domain/Processing/RadarProcessingWorkerHealth.cs`
- `src/Domain/Processing/RadarProcessingWorkerLifecycleError.cs`
- `src/Domain/Processing/RadarProcessingWorkerId.cs`
- `src/Domain/Processing/RadarProcessingWorkerGroupStatus.cs`
- `src/Domain/Processing/RadarProcessingWorkerLifecycleResult.cs`
- `src/Domain/Processing/RadarProcessingWorkerGroupLifecycle.cs`
- `src/Domain/Processing/RadarProcessingAsyncBatchScope.cs`
- `src/Domain/Processing/RadarProcessingAsyncWorkItem.cs`
- `src/Domain/Processing/RadarProcessingAsyncWorkCompletion.cs`
- `src/Domain/Processing/RadarProcessingAsyncBatchCompletion.cs`
- `src/Domain/Processing/RadarProcessingAsyncBatchScopeResult.cs`
- `src/Domain/Processing/RadarProcessingAsyncWorkStatus.cs`
- `src/Domain/Processing/RadarProcessingAsyncBatchCompletionError.cs`
- `src/Infrastructure/Processing/RadarProcessingWorkerMailbox.cs`
- `src/Infrastructure/Processing/RadarProcessingWorkerMailboxOptions.cs`
- `src/Infrastructure/Processing/RadarProcessingWorkerMailboxEnqueueStatus.cs`
- `src/Infrastructure/Processing/RadarProcessingWorkerMailboxDequeueStatus.cs`
- `src/Infrastructure/Processing/RadarProcessingWorkerMailboxEnqueueResult.cs`
- `src/Infrastructure/Processing/RadarProcessingWorkerMailboxDequeueResult.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncWorkExecutor.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncWorkerGroup.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncWorkerGroupOptions.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncWorkerGroupResult.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncWorkerGroupDrainResult.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncWorkerGroupError.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncDispatchExecutor.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncBatchDispatcher.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncDispatchPlan.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncDispatchResult.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncCompletionAggregator.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncAggregationResult.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncAggregationError.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncWorker.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncWorkerRequest.cs`
- `src/Infrastructure/Processing/RadarProcessingAsyncWorkerGroupBatchState.cs`
- `src/Domain/Processing/RadarProcessingExecutionMode.cs`
- `src/Domain/Processing/RadarProcessingCoreOptions.cs`
- `src/Domain/Processing/RadarProcessingRebalanceHardeningOptions.cs`
- `src/Domain/Processing/RadarProcessingTelemetryRetentionOptions.cs`
- `src/Domain/Processing/RadarProcessingQuarantineLifecycleOptions.cs`
- `src/Domain/Processing/RadarProcessingValidationProfile.cs`
- `src/Domain/Processing/RadarProcessingDiagnosticRetentionMode.cs`
- `src/Domain/Processing/RadarProcessingBenchmarkAllocationSnapshot.cs`
- `src/Domain/Processing/RadarProcessingRebalanceAllocationSummary.cs`
- `src/Domain/Processing/RadarProcessingRebalanceTelemetrySummary.cs`
- `src/Domain/Processing/RadarProcessingRebalanceTelemetryCounters.cs`
- `src/Domain/Processing/RadarProcessingRebalanceSkippedReasonCounter.cs`
- `src/Domain/Processing/RadarProcessingRebalanceRecentDecision.cs`
- `src/Domain/Processing/RadarProcessingRebalanceRecentAcceptedMove.cs`
- `src/Domain/Processing/RadarProcessingRebalanceRecentValidationFailure.cs`
- `src/Domain/Processing/RadarProcessingRebalanceRecentLifecycleTransition.cs`
- `src/Domain/Processing/RadarProcessingRebalanceRetentionStats.cs`
- `src/Domain/Processing/RadarProcessingBoundedTelemetryWindow.cs`
- `src/Domain/Processing/RadarProcessingRebalanceTelemetryRecorder.cs`
- `src/Domain/Processing/RadarProcessingQuarantineEffectiveClassification.cs`
- `src/Domain/Processing/RadarProcessingQuarantineTransitionReason.cs`
- `src/Domain/Processing/RadarProcessingQuarantineEvidence.cs`
- `src/Domain/Processing/RadarProcessingQuarantineTransition.cs`
- `src/Domain/Processing/RadarProcessingQuarantineLifecycleState.cs`
- `src/Domain/Processing/RadarProcessingQuarantineLifecycleEvaluationResult.cs`
- `src/Domain/Processing/RadarProcessingQuarantineLifecycleEvaluator.cs`
- `src/Domain/Processing/RadarProcessingQuarantineLifecycleTracker.cs`
- `src/Domain/Processing/RadarProcessingTopologyVersion.cs`
- `src/Domain/Processing/RadarProcessingTopologyManager.cs`
- `src/Domain/Processing/RadarProcessingTopologyMoveRequest.cs`
- `src/Domain/Processing/RadarProcessingTopologyMoveResult.cs`
- `src/Domain/Processing/RadarProcessingTopologyMoveError.cs`
- `src/Domain/Processing/RadarProcessingBatchRoute.cs`
- `src/Domain/Processing/RadarProcessingBatchRouter.cs`
- `src/Domain/Processing/RadarProcessingTelemetry.cs`
- `src/Domain/Processing/RadarProcessingResult.cs`
- `src/Domain/Processing/RadarProcessingPressureBand.cs`
- `src/Domain/Processing/RadarProcessingPressureScore.cs`
- `src/Domain/Processing/RadarProcessingPressureOptions.cs`
- `src/Domain/Processing/RadarProcessingPressureSample.cs`
- `src/Domain/Processing/RadarProcessingShardPressureSample.cs`
- `src/Domain/Processing/RadarProcessingPartitionPressureSample.cs`
- `src/Domain/Processing/RadarProcessingPressureWindowOptions.cs`
- `src/Domain/Processing/RadarProcessingPressureWindow.cs`
- `src/Domain/Processing/RadarProcessingShardPressureState.cs`
- `src/Domain/Processing/RadarProcessingPartitionPressureState.cs`
- `src/Domain/Processing/RadarProcessingRebalanceOptions.cs`
- `src/Domain/Processing/RadarProcessingRebalanceBudget.cs`
- `src/Domain/Processing/RadarProcessingPartitionResidency.cs`
- `src/Domain/Processing/RadarProcessingPartitionCooldown.cs`
- `src/Domain/Processing/RadarProcessingShardCooldown.cs`
- `src/Domain/Processing/RadarProcessingRebalanceMovePolicyInput.cs`
- `src/Domain/Processing/RadarProcessingRebalancePolicyRejection.cs`
- `src/Domain/Processing/RadarProcessingRebalancePolicyResult.cs`
- `src/Domain/Processing/RadarProcessingRebalancePolicyState.cs`
- `src/Domain/Processing/RadarProcessingRebalanceDecisionKind.cs`
- `src/Domain/Processing/RadarProcessingRebalanceMoveKind.cs`
- `src/Domain/Processing/RadarProcessingRebalanceSkippedReason.cs`
- `src/Domain/Processing/RadarProcessingProjectedPressure.cs`
- `src/Domain/Processing/RadarProcessingRebalanceCandidate.cs`
- `src/Domain/Processing/RadarProcessingRebalanceDecision.cs`
- `src/Domain/Processing/RadarProcessingDirectHotReliefPlanner.cs`
- `src/Domain/Processing/RadarProcessingHotPartitionClassification.cs`
- `src/Domain/Processing/RadarProcessingHotPartitionState.cs`
- `src/Domain/Processing/RadarProcessingHotPartitionClassifier.cs`
- `src/Domain/Processing/RadarProcessingColdEvacuationPlanner.cs`
- `src/Domain/Processing/RadarProcessingPartitionMigrationState.cs`
- `src/Domain/Processing/RadarProcessingMigrationValidationError.cs`
- `src/Domain/Processing/RadarProcessingPartitionMigration.cs`
- `src/Domain/Processing/RadarProcessingMigrationValidationResult.cs`
- `src/Domain/Processing/RadarProcessingMigrationResult.cs`
- `src/Domain/Processing/RadarProcessingMigrationCoordinator.cs`
- `src/Domain/Processing/RadarProcessingPartitionStateSnapshot.cs`
- `src/Domain/Processing/RadarProcessingPartitionStateChecksum.cs`
- `src/Domain/Processing/RadarProcessingStateHandoffValidationError.cs`
- `src/Domain/Processing/RadarProcessingStateHandoffValidationResult.cs`
- `src/Domain/Processing/RadarProcessingStateHandoffValidator.cs`
- `src/Domain/Processing/RadarProcessingRebalanceSession.cs`
- `src/Domain/Processing/RadarProcessingRebalanceSessionResult.cs`
- `src/Domain/Processing/RadarProcessingRebalanceValidationError.cs`
- `src/Domain/Processing/RadarProcessingRebalanceValidationResult.cs`
- `src/Domain/Processing/RadarProcessingRebalanceValidator.cs`
- `src/Infrastructure/Processing/RadarProcessingSyntheticRebalanceWorkloadKind.cs`
- `src/Infrastructure/Processing/RadarProcessingSyntheticRebalanceWorkload.cs`
- `src/Infrastructure/Processing/RadarProcessingSyntheticRebalanceWorkloadResult.cs`
- `src/Infrastructure/Processing/RadarProcessingSyntheticRebalanceWorkloadRunner.cs`
- `src/Infrastructure/Processing/RadarProcessingSyntheticRebalanceBenchmarkMode.cs`
- `src/Infrastructure/Processing/RadarProcessingSyntheticRebalanceMovePressure.cs`
- `src/Infrastructure/Processing/RadarProcessingSyntheticRebalanceBenchmarkResult.cs`
- `src/Infrastructure/Processing/RadarProcessingSyntheticRebalanceBenchmark.cs`
- `src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmarkResult.cs`
- `src/Infrastructure/Processing/RadarProcessingArchiveRebalanceCacheBenchmarkResult.cs`
- `src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingTopologyVersioningTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingBatchRouterTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingTelemetryTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingContractTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingPressureSampleTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingPressureWindowTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceHardeningOptionsTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceTelemetryContractTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceTelemetryRecorderTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingQuarantineLifecycleStateTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingQuarantineLifecycleEvaluatorTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingQuarantineLifecycleTrackerTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingRebalancePolicyStateTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceDecisionTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingDirectHotReliefPlannerTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingHotPartitionClassifierTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingColdEvacuationPlannerTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingMigrationCoordinatorTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingStateHandoffValidatorTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceSessionTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceValidatorTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceAllocationSummaryTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingSyntheticRebalanceWorkloadTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingSyntheticRebalanceBenchmarkTests.cs`
- `tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs`
- `src/Domain/Processing/*`
- `tests/RadarPulse.Tests/Processing/*`
- `src/Domain/Streaming/DenseIdentityAllowedCharacters.cs`
- `src/Domain/Streaming/DenseIdentityCanonicalizationPolicy.cs`
- `src/Domain/Streaming/DenseIdentityCatalog.cs`
- `src/Domain/Streaming/DenseIdentityCatalogDelta.cs`
- `src/Domain/Streaming/DenseIdentityCatalogEntry.cs`
- `src/Domain/Streaming/DenseIdentityCatalogSnapshot.cs`
- `src/Domain/Streaming/DenseIdentityValidationError.cs`
- `src/Domain/Streaming/DenseIdentityValidationInputKind.cs`
- `src/Domain/Streaming/DenseIdentityValidationResult.cs`
- `src/Domain/Streaming/RadarEventBatch.cs`
- `src/Domain/Streaming/RadarEventBatchBuilder.cs`
- `src/Domain/Streaming/RadarEventBatchLifetime.cs`
- `src/Domain/Streaming/RadarEventBatchMetrics.cs`
- `src/Domain/Streaming/RadarEventBatchValidationError.cs`
- `src/Domain/Streaming/RadarEventBatchValidationResult.cs`
- `src/Domain/Streaming/RadarEventBatchValidator.cs`
- `src/Domain/Streaming/RadarSourceKey.cs`
- `src/Domain/Streaming/RadarSourceUniverse.cs`
- `src/Domain/Streaming/RadarStreamChecksum.cs`
- `src/Domain/Streaming/RadarStreamDictionarySnapshot.cs`
- `src/Domain/Streaming/RadarStreamDictionarySnapshotMetrics.cs`
- `src/Domain/Streaming/RadarStreamEvent.cs`
- `src/Domain/Streaming/RadarStreamIdentity.cs`
- `src/Domain/Streaming/RadarStreamIdentityNormalizationError.cs`
- `src/Domain/Streaming/RadarStreamIdentityNormalizationResult.cs`
- `src/Domain/Streaming/RadarStreamIdentityNormalizer.cs`
- `src/Domain/Streaming/StreamSchemaVersion.cs`
- `src/Domain/Streaming/DictionaryVersion.cs`
- `src/Domain/Streaming/SourceUniverseVersion.cs`
- `src/Domain/Streaming/RadarStreamWordSize.cs`
- `src/Domain/Streaming/RadarStreamStatusModel.cs`
- `src/Application/Archive/ArchiveRadarEventBatchPublishOptions.cs`
- `src/Application/Archive/IArchiveRadarEventBatchPublisher.cs`
- `src/Domain/Archive/ArchiveRadarEventBatchPublishResult.cs`
- `src/Domain/Archive/ArchiveRadarEventBatchStreamCacheBenchmarkResult.cs`
- `src/Domain/Archive/ArchiveRadarEventBatchStreamBenchmarkResult.cs`
- `src/Infrastructure/Archive/ArchiveRadarEventBatchCountingPublisher.cs`
- `src/Infrastructure/Archive/ArchiveTwoRadarEventBatchProjector.cs`
- `src/Infrastructure/Archive/NexradArchiveRadarEventBatchPublishSession.cs`
- `src/Infrastructure/Archive/NexradArchiveRadarEventBatchPublisher.cs`
- `src/Infrastructure/Archive/NexradArchiveRadarEventBatchStreamBenchmark.cs`
- `tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs`
- `tests/RadarPulse.Tests/Streaming/RadarEventBatchBuilderTests.cs`
- `tests/RadarPulse.Tests/Streaming/RadarEventBatchValidatorTests.cs`
- `tests/RadarPulse.Tests/Streaming/DenseIdentityCanonicalizationPolicyTests.cs`
- `tests/RadarPulse.Tests/Streaming/DenseIdentityCatalogTests.cs`
- `tests/RadarPulse.Tests/Streaming/DenseIdentityCatalogVersioningTests.cs`
- `tests/RadarPulse.Tests/Streaming/RadarStreamIdentityNormalizerTests.cs`
- `tests/RadarPulse.Tests/Streaming/RadarSourceUniverseTests.cs`
- `tests/RadarPulse.Tests/Streaming/RadarStreamContractTests.cs`
- `src/Presentation/Program.cs`
- `src/Application/Archive/IHistoricalArchiveClient.cs`
- `src/Application/Archive/HistoricalArchiveManifestSelector.cs`
- `src/Infrastructure/Archive/AwsNexradArchiveClient.cs`
- `src/Infrastructure/Archive/ArchiveBZip2Decompressors.cs`
- `src/Infrastructure/Archive/HistoricalArchiveDownloader.cs`
- `src/Infrastructure/Archive/IArchiveBZip2Decompressor.cs`
- `src/Infrastructure/Archive/ArchiveTwoFileReader.cs`
- `src/Infrastructure/Archive/ArchiveTwoMessageStreamScanner.cs`
- `src/Infrastructure/Archive/ArchiveTwoMessageSummaryBuilder.cs`
- `src/Domain/Archive/ArchiveTwoGateMomentEvent.cs`
- `src/Domain/Archive/NexradArchiveCacheInspection.cs`
- `src/Domain/Archive/ArchiveTwoReplayShapeBenchmarkResult.cs`
- `src/Domain/Archive/ArchiveTwoReplayShapeValidationResult.cs`
- `src/Domain/Archive/ArchiveReplayPublishResult.cs`
- `src/Domain/Archive/ArchiveReplayCachePublishResult.cs`
- `src/Domain/Archive/ArchiveReplayPublishBenchmarkResult.cs`
- `src/Domain/Archive/ArchiveReplayPublishCacheBenchmarkResult.cs`
- `src/Infrastructure/Archive/IArchiveTwoMessageConsumer.cs`
- `src/Application/Archive/IArchiveReplayEventPublisher.cs`
- `src/Application/Archive/ArchiveReplayPublishOptions.cs`
- `src/Infrastructure/Archive/ArchiveReplayEventAccumulator.cs`
- `src/Infrastructure/Archive/ArchiveReplayCountingPublisher.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayPublisher.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayPublishSession.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayPublishBenchmark.cs`
- `src/Infrastructure/Archive/ArchiveTwoGateMomentChronologyChecksum.cs`
- `src/Infrastructure/Archive/ArchiveTwoGateMomentEventProjector.cs`
- `src/Infrastructure/Archive/NexradArchiveDecompressionBenchmark.cs`
- `src/Infrastructure/Archive/NexradArchiveParseBenchmark.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayShapeBenchmark.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayShapeValidator.cs`
- `src/Infrastructure/Archive/NexradArchiveCacheInspector.cs`
- `src/Infrastructure/Archive/NexradArchiveDecompressionValidator.cs`
- `src/Infrastructure/Archive/NexradArchiveFileInspector.cs`
- `src/Infrastructure/Archive/ReusableArchiveBZip2Decompressor.cs`
- `src/Infrastructure/Archive/NexradCachePathMapper.cs`
- `src/Infrastructure/Archive/SharpCompressArchiveBZip2Decompressor.cs`
- `src/Infrastructure/Archive/SharpZipLibArchiveBZip2Decompressor.cs`
- `tests/RadarPulse.Tests/Archive/*`

## Milestone 003 Done Criteria

Milestone 003 is complete:

- RadarPulse exposes an explicit replay publisher API for
  `ArchiveTwoGateMomentEvent`. (Implemented.)
- One cached Archive Two file can publish ordered events through that API.
  (Implemented for sequential and parallel replay.)
- A counting/checksum publisher can verify status totals, raw checksum,
  calibrated checksum, and chronology checksum. (Implemented.)
- The production-facing parallel replay path publishes through an ordered merge
  rather than worker completion order. (Implemented.)
- Sequential and parallel replay over the same file produce identical counts
  and chronology checksums. (Implemented.)
- The CLI can smoke-test the publisher path. (Implemented for
  `--file`, `--cache`, and `--parallelism n`.)
- The CLI can benchmark cache-wide replay-publish throughput and allocations.
  (Implemented for `archive benchmark replay-publish --cache`.)
- Focused tests cover ordering, totals, diagnostics, and cancellation.
  (Implemented for sequential, parallel, custom-publisher, benchmark, and
  reusable-session/cache-selection paths.)

