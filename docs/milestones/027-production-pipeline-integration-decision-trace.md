# Milestone 027 Decision Trace

Date: 2026-05-26

Decision: accept production pipeline integration for deterministic
archive-shaped backend workloads with named scoped warnings.

This decision accepts milestone 027's production pipeline profile and
configuration resolution, operator summary/readiness contract,
archive-shaped pipeline runner, durable file-backed restart/recovery
pipeline gate, rollback/fallback control diagnostics, handler output/BFF
compatibility, capacity evidence helper, focused Release gate, and Release
build on top of the milestone 020 runtime/archive baseline, the milestone
021 ordered processing foundation, the milestone 022 ordered
rebalance/topology foundation, the milestone 023 durable envelope contract,
the milestone 024 custom handler output/BFF surface, the milestone 025
handler delta/merge contract, and the milestone 026 file-based persistent
durable adapter.

The accepted scope is a deterministic archive-shaped operational backend
pipeline. RadarPulse can resolve production-shaped defaults, process
archive-shaped `RadarEventBatch` input through the accepted runtime path,
publish BFF read models, expose operator readiness and first blocking
reasons, validate file durable adapter restart/recovery posture, make
rollback/fallback actions explicit, and capture local representative capacity
evidence.

The decision deliberately does not claim true live network ingestion,
production HTTP hosting, frontend implementation, Kafka/RabbitMQ/cloud
queue/database adapter readiness, deployment platform automation,
cross-machine throughput certification, or exactly-once production delivery.
The focused capacity evidence is accepted for the archive-shaped runtime
pipeline. Durable adapter evidence is accepted as restart/recovery-focused;
it is not accepted as a durable-backed broker throughput row.

## Decision Matrix

```text
production pipeline integration:
  accepted with scoped warnings

production pipeline profile:
  accepted; a named profile resolves accepted runtime/archive defaults with
  explicit value provenance

configuration validation:
  accepted; invalid capacities, unsupported durable adapter kinds, borrowed
  provider fallback requests, and unsafe combinations fail closed with first
  invalid option diagnostics

archive-shaped pipeline runner:
  accepted; deterministic RadarEventBatch input runs through the accepted
  MVP/queued-overlap runtime path and publishes BFF read models

handler-free runtime posture:
  accepted; handler-free work uses ordered concurrent processing defaults

mergeable handler posture:
  accepted; mergeable handlers use ordered handler delta/merge posture

snapshot-only handler posture:
  accepted; snapshot-only stateful handlers use explicit sequential fallback
  and preserve BFF diagnostics

unsupported handler posture:
  accepted; unsupported handlers block with a handler-specific reason and a
  ResolveHandlerPosture recommendation

operator summary:
  accepted; configuration, durable readiness, adapter compatibility,
  retained pressure, processing completeness, handler posture, first blocker,
  and fallback recommendation are visible

durable restart/recovery gate:
  accepted; file-backed durable state can be reopened by the pipeline
  recovery runner, completed envelopes can commit, and claimed/failed/poison
  states remain operator-visible blockers

rollback/fallback controls:
  accepted; stop-accepting, drain-accepted, cancel-open/release, and
  reject-unsafe-fallback postures are explicit

BFF compatibility:
  accepted; successful runs publish latest run, batch list, source output,
  diagnostics, and handler output through the milestone 024 read-model store

capacity evidence:
  accepted with warning; local representative evidence captures elapsed
  time, allocation, batch counts, handler mode, durable adapter kind,
  terminal retained pressure, processing completeness, readiness, first
  blocking reason, and configuration contour

durable-backed throughput row:
  not accepted; durable evidence is restart/recovery-focused, while the
  capacity row measures the archive-shaped runtime pipeline

focused Release gate:
  accepted; 29 focused Release tests passed with no failures or skips

Release build:
  accepted; Release build succeeded with zero warnings and zero errors

true live network ingestion:
  not implemented; deterministic archive-shaped workloads remain the gate
  input

Kafka/RabbitMQ/cloud queue/database adapter:
  not implemented; future adapter milestones require separate decisions

production HTTP BFF host:
  not implemented; milestone 024 read models remain application contracts

frontend application:
  not implemented; future product milestone required

deployment and operations:
  not implemented; deployment automation, rollback runbooks, autoscaling,
  alert routing, and operator procedures remain future work

exactly-once production delivery:
  not claimed; future storage, adapter, and downstream idempotency gates are
  required
```

## Decision Explanations

### Accept Production Pipeline Profile

Decision: accept `RadarProcessingProductionPipelineProfile` as the
production-shaped backend configuration boundary for milestone 027.

Why chosen: previous milestones accepted lower-level defaults, but operators
still needed one production-shaped profile that resolves provider, overlap,
retention, execution, worker, queue, ordered capacity, durable adapter, and
handler posture values with provenance.

Alternatives: let callers continue composing lower-level options manually, or
change the global processing defaults.

Rejected because: manual composition leaves the production posture implicit;
changing global defaults would reopen earlier milestone decisions and affect
caller-owned cores and sessions.

Trade-offs/debt: the profile is conservative and file-adapter scoped. Future
adapter or deployment profiles require separate validation.

Review explanation: "The production pipeline profile names the accepted
backend contour without rewriting the lower-level defaults."

### Accept Fail-Closed Configuration Validation

Decision: accept fail-closed configuration validation and first-invalid
option diagnostics.

Why chosen: production-shaped orchestration must not normalize unsafe
configuration into safe-looking defaults. Invalid capacities, unsupported
adapter kinds, non-accepted provider modes, and silent borrowed fallback
requests need to block readiness visibly.

Alternatives: clamp invalid values, silently fall back to accepted defaults,
or allow unsupported options with warnings only.

Rejected because: clamping and silent fallback hide operator intent; warnings
alone would let unsupported runtime contours enter the production pipeline.

Trade-offs/debt: some future experiments must explicitly opt into validated
overrides instead of relying on permissive defaults.

Review explanation: "Bad configuration blocks; it does not become a
different configuration."

### Accept Archive-Shaped Runtime Pipeline Runner

Decision: accept the archive-shaped production pipeline runner as the
application-level backend run surface.

Why chosen: RadarPulse needed one runner that composes deterministic
archive-shaped input, accepted runtime construction, ordered processing,
handler posture, BFF read-model publication, readiness diagnostics, and
capacity evidence.

Alternatives: keep only benchmark/runtime helpers, or build a production HTTP
host before stabilizing the application runner.

Rejected because: helper-only composition does not provide an operational
surface; HTTP hosting would add transport, auth, deployment, and product API
concerns before the backend pipeline contract was accepted.

Trade-offs/debt: the runner is still archive-shaped and in-process. It is
not true live network ingestion and not an HTTP API host.

Review explanation: "The runner gives the backend one production-shaped path
without claiming a deployed service."

### Accept Explicit Handler Posture In The Pipeline

Decision: accept explicit handler-free, mergeable, snapshot-only, and
unsupported handler posture through the pipeline.

Why chosen: milestone 025 made handler posture part of correctness. The
production pipeline must preserve that: handler-free work stays ordered
concurrent, mergeable handlers use delta/merge, snapshot-only handlers fall
back sequentially, and unsupported handlers fail closed with a
handler-specific reason.

Alternatives: hide fallback in the pipeline, let unsupported handlers run
sequentially, or expose only generic blocked pipeline state.

Rejected because: hidden fallback obscures performance posture; unsupported
handlers may be unsupported for correctness reasons; generic blocked state
does not tell operators what to fix.

Trade-offs/debt: product surfaces need to present handler posture clearly
when a future frontend or API host is added.

Review explanation: "Handler posture remains a visible contract, not a
runtime accident."

### Accept Operator Summary And Fallback Recommendations

Decision: accept the operator summary as the readiness and first-blocker
contract for the milestone 027 pipeline.

Why chosen: a production-shaped pipeline needs a single place to answer
whether it is ready, why it is blocked, which batch/state is first, whether
retained pressure remains, and what explicit fallback action is recommended.

Alternatives: rely on separate queue, adapter, BFF, and runtime summaries; or
defer operator posture to deployment/runbook work.

Rejected because: separate summaries force operators to infer readiness
across layers; deferring this would leave the pipeline integrated but not
operable.

Trade-offs/debt: the summary is still an application/backend contract. Alert
rules, dashboards, HTTP DTOs, and runbooks remain future work.

Review explanation: "One summary tells the operator what is blocking and
what kind of action is safe."

### Accept File Durable Restart/Recovery Pipeline Gate

Decision: accept the pipeline recovery runner over the milestone 026
file-based durable adapter as restart/recovery evidence.

Why chosen: milestone 026 proved the file adapter contract. Milestone 027
needed to prove that production pipeline recovery can consume that durable
state, preserve blockers, commit completed envelopes where safe, and expose
adapter compatibility through the operator summary.

Alternatives: integrate file durability directly into the normal throughput
runner, or leave durable recovery only in milestone 026 tests.

Rejected because: direct durable-backed throughput would broaden this
milestone into a different execution path and capacity claim; leaving
recovery only in milestone 026 would not prove pipeline-level diagnostics.

Trade-offs/debt: durable evidence is restart/recovery-focused. A future
milestone is required if durable-backed throughput, broker throughput, or
cross-process delivery becomes the question.

Review explanation: "The production pipeline can recover durable state; this
does not certify broker throughput."

### Accept Rollback And Fallback Controls

Decision: accept explicit stop-accepting, drain-accepted,
cancel-open/release, and reject-unsafe-fallback controls.

Why chosen: milestone 027 needed rollback/fallback posture, but not hidden
runtime behavior. The controls make state preservation, drain, cancellation,
release cleanup, and unsafe fallback rejection visible.

Alternatives: silently fall back to borrowed provider, cancel everything on
stop, or defer rollback posture to runbooks.

Rejected because: silent borrowed fallback would violate accepted default
posture; cancel-everything can lose accepted durable work; runbooks need
machine-readable backend state first.

Trade-offs/debt: the controls are backend primitives. Human runbooks and
deployment orchestration still need their own milestone or closeout work.

Review explanation: "Fallback is an operator-visible action, not hidden
runtime magic."

### Accept Local Capacity Evidence

Decision: accept local representative capacity evidence for the integrated
archive-shaped runtime pipeline.

Why chosen: milestone 027 needed evidence beyond unit-level contracts:
elapsed time, allocation, accepted/processed/committed counts, handler
posture, durable adapter kind, terminal retained pressure, completeness,
readiness, first blocker, and configuration contour.

Alternatives: require full-cache or cross-machine throughput certification,
or skip capacity until deployment work.

Rejected because: cross-machine and broker throughput are outside this
milestone; skipping capacity would leave no integrated performance contour
for the accepted pipeline surface.

Trade-offs/debt: this is local representative evidence. It does not certify
production throughput, broker durability, or multi-machine scaling.

Review explanation: "The capacity row describes this backend pipeline shape;
it is not a production SLA."

### Keep Live, Broker, HTTP, Frontend, And Operations Deferred

Decision: keep true live network ingestion, broker/database adapters,
production HTTP BFF hosting, frontend implementation, deployment operations,
and exactly-once delivery outside milestone 027.

Why chosen: milestone 027 integrates the backend pipeline. Each deferred
surface has a separate failure model, acceptance gate, and operational
contract.

Alternatives: combine production pipeline integration with live network
ingestion, broker selection, HTTP hosting, frontend, deployment, or
exactly-once claims.

Rejected because: combining these would blur the accepted backend result and
make the decision too broad to validate rigorously.

Trade-offs/debt: the next milestone can now build product-facing or
deployment-facing surfaces on top of a stable backend pipeline.

Review explanation: "Milestone 027 makes the backend operationally shaped;
product and deployment surfaces still need their own gates."

## Included Surface

Included:

```text
RadarProcessingProductionPipelineProfile
RadarProcessingProductionPipelineOptions
resolved pipeline configuration with option provenance
configuration fail-closed validation and warnings
RadarProcessingProductionPipelineOperatorSummary
pipeline run state and fallback recommendation vocabulary
archive-shaped production pipeline runner
production pipeline run request/result contracts
BFF read-model publication for pipeline runs
handler-free, mergeable, snapshot-only, and unsupported handler posture
durable file-backed pipeline recovery runner
production pipeline recovery request/result contracts
stop-accepting, drain-accepted, cancel-open/release, and unsafe fallback
  rejection controls
capacity evidence record and configuration contour
focused Release gate and Release build evidence
handoff and gate documentation updates
```

Excluded:

```text
true live network ingestion
Kafka adapter
RabbitMQ adapter
cloud queue adapter
database-backed adapter
production broker operations and retention certification
durable-backed throughput certification
production HTTP BFF host
frontend application
deployment automation
autoscaling
alert routing
operator runbooks
cross-machine throughput certification
exactly-once end-to-end production delivery claim
changing RadarProcessingCoreOptions.Default
changing the milestone 020 runtime/archive baseline decision
changing the milestone 021 ordered processing decision
changing the milestone 022 ordered rebalance/topology decision
changing the milestone 023 durable envelope state decision
changing the milestone 024 custom handler output/BFF decision
changing the milestone 025 handler delta/merge decision
changing the milestone 026 file durable adapter boundary
```

## Evidence

Primary source documents:

```text
docs/milestones/027-production-pipeline-integration.md
docs/milestones/027-production-pipeline-integration-plan.md
docs/milestones/027-production-pipeline-integration-gate.md
```

Input evidence from earlier milestones:

```text
milestone 020:
  RadarProcessingRuntimeArchiveBaseline accepted as the named construction
  profile composing queued-owned provider defaults with async shard transport
  execution defaults

milestone 021:
  non-mutating per-batch processing delta plus provider-sequence ordered
  commit accepted as the safe architecture for overlapping processing-core
  batches

milestone 022:
  ordered rebalance/topology commit accepted for handler-free processing
  deltas

milestone 023:
  broker-neutral durable envelope contract and deterministic in-process
  durable harness accepted as durable/cross-process runtime readiness

milestone 024:
  committed custom handler output, processing read models, BFF query shapes,
  diagnostics, and MVP readiness outputs accepted

milestone 025:
  handler delta identity, duplicate replay idempotency, conflicting duplicate
  rejection, and provider-sequence ordered handler merge accepted for
  explicitly mergeable handlers

milestone 026:
  deterministic local file-based durable adapter accepted as the persistent
  restart/recovery baseline for deterministic archive-shaped MVP workloads
```

Implementation evidence:

```text
RadarProcessingProductionPipelineProfile:
  resolves accepted runtime defaults and validates unsupported production
  pipeline contours

RadarProcessingProductionPipelineResolvedConfiguration:
  records resolved values, provenance, invalid option, invalid reason, and
  warnings

RadarProcessingProductionPipelineOperatorSummary:
  composes configuration validity, durable readiness, adapter compatibility,
  retained pressure, processing completeness, handler posture, first blocker,
  and fallback recommendation

RadarProcessingProductionPipelineRunner:
  composes deterministic RadarEventBatch input with RunMvpProcessingAsync,
  accepted ordered concurrency, accepted queued-overlap options, BFF read
  model publication, and operator summary creation

RadarProcessingProductionPipelineRecoveryRunner:
  opens the file durable adapter, recovers completed envelopes, commits ready
  work, and exposes adapter summary/readiness through the pipeline result

RadarProcessingProductionPipelineControlCoordinator:
  exposes stop-accepting, drain-accepted, cancel-open/release, and
  reject-unsafe-fallback postures over durable state

RadarProcessingProductionPipelineCapacityEvidence:
  captures the local representative capacity contour for successful and
  blocked pipeline runs
```

Verification:

```text
slice 1 focused configuration suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingProductionPipelineConfigurationTests"
  result: 6 passed, 0 failed, 0 skipped

slice 2 focused operator summary suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingProductionPipelineSummaryTests"
  result: 7 passed, 0 failed, 0 skipped

slice 3 focused pipeline runner suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingProductionPipelineRunnerTests"
  result: 5 passed, 0 failed, 0 skipped

slice 4 focused durable recovery suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingProductionPipelineRecoveryTests"
  result: 4 passed, 0 failed, 0 skipped

slice 5 focused rollback/fallback suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingProductionPipelineFallbackTests"
  result: 5 passed, 0 failed, 0 skipped

slice 6 focused capacity evidence suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingProductionPipelineGateTests"
  result: 2 passed, 0 failed, 0 skipped

focused milestone 027 Release gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarProcessingProductionPipelineConfigurationTests|FullyQualifiedName~RadarProcessingProductionPipelineSummaryTests|FullyQualifiedName~RadarProcessingProductionPipelineRunnerTests|FullyQualifiedName~RadarProcessingProductionPipelineRecoveryTests|FullyQualifiedName~RadarProcessingProductionPipelineFallbackTests|FullyQualifiedName~RadarProcessingProductionPipelineGateTests"
  result: 29 passed, 0 failed, 0 skipped

Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors
```

## Final Decision

Decision:

```text
accepted with scoped warnings for production pipeline integration over
deterministic archive-shaped backend workloads
```

Accepted readiness answer:

```text
yes with scoped warnings, RadarPulse is ready to run the accepted backend
runtime as one production-shaped operational pipeline over deterministic
archive-shaped workloads, with named configuration defaults, explicit
override provenance, operator readiness diagnostics, handler posture,
file-durable restart/recovery validation, rollback/fallback controls, BFF
read-model output, and local representative capacity evidence
```

Named warnings:

```text
milestone 027 validates deterministic archive-shaped production pipeline
  integration, not true live network ingestion
the normal pipeline capacity row measures the archive-shaped runtime path,
  not durable-backed broker throughput
the file durable adapter remains the local restart/recovery baseline
Kafka, RabbitMQ, cloud queue, and database-backed adapters are not included
production broker durability, broker retention, and cross-machine delivery
  are not claimed
production HTTP BFF host and frontend remain future work
deployment platform automation, rollback runbooks, autoscaling, alert
  routing, and operator procedures remain future work
rollback/fallback posture is explicit and operator-visible; hidden borrowed
  provider fallback remains rejected
exactly-once production delivery is not claimed
```

Recommended next milestone input:

```text
product-facing completion.

Use the accepted production-shaped backend pipeline, BFF read models,
operator diagnostics, handler output posture, rollback/fallback vocabulary,
and capacity evidence to build the selected product-facing surface. The next
milestone should decide whether the immediate product slice is an HTTP/API
host, frontend application, operator console, product workflow, or another
explicit delivery surface. Do not silently expand that milestone into
Kafka/RabbitMQ/cloud queue/database adapter certification, true live network
ingestion, deployment automation, or exactly-once delivery unless those
decisions are explicitly selected.
```
