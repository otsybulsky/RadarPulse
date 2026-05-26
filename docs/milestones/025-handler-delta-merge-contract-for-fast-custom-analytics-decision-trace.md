# Milestone 025 Decision Trace

Date: 2026-05-26

Decision: accept the handler delta/merge contract for fast custom analytics
over deterministic archive-shaped MVP workloads with named scoped warnings.

This decision accepts milestone 025's explicit handler execution
classification, per-batch handler delta contract, deterministic
provider-sequence handler delta merge coordinator, MVP runtime integration,
fallback policy, BFF/read-model compatibility, handler-heavy performance
gate, and optimized full-cache handler matrix on top of the milestone 020
runtime/archive baseline, the milestone 021 ordered processing foundation,
the milestone 022 ordered rebalance/topology foundation, the milestone 023
durable runtime contract, and the milestone 024 custom handler output/BFF
surface.

The accepted scope is the scoped in-process MVP fast path for explicitly
mergeable stateful custom handlers. Mergeable handlers may compute per-batch
handler deltas concurrently, complete out of order, and merge only in
provider sequence before exposing committed output through the milestone 024
read models.

The decision preserves the conservative stateful handler posture. Handler-free
processing keeps the previously accepted ordered concurrent runtime surfaces.
Snapshot-only stateful handlers keep explicit sequential fallback. Unsupported
handler sets fail closed with readiness diagnostics. Arbitrary stateful
handlers are not made concurrent unless they opt into deterministic
handler-owned merge semantics.

The decision does not accept production persistent durable adapters,
production HTTP BFF hosting, a frontend application, true live network
ingestion, production deployment/operations readiness, exactly-once production
delivery, or cross-machine throughput certification. It also does not claim
allocation parity between active=4 handler delta/merge and active=1
handler-aware sequential drain; the remaining active=4 allocation overhead is
accepted as a scoped warning.

## Decision Matrix

```text
handler delta/merge contract for fast custom analytics:
  accepted with scoped warnings

handler execution classification:
  accepted; handler-free, mergeable, snapshot-only, and unsupported postures
  are explicit and fail closed where needed

mergeable handler fast path:
  accepted; only handlers that opt into deterministic mergeable semantics may
  use ordered concurrent handler delta compute

snapshot-only handler posture:
  accepted; committed snapshot export and explicit sequential fallback remain
  the safe default for existing stateful handlers

unsupported handler posture:
  accepted; unsupported handler sets block readiness with diagnostics instead
  of entering an ambiguous runtime mode

per-batch handler delta contract:
  accepted; handler identity, contract version, provider sequence, durable
  batch id, counters, checksums, deterministic delta id, versioning, and
  serializable values are explicit

deterministic ordered merge coordinator:
  accepted; worker completion order cannot become externally visible handler
  output order, and duplicate delta replay does not double-count state

MVP runtime integration:
  accepted; ordered handler delta/merge is selected only for all-mergeable
  handler sets, while handler-free and fallback paths preserve prior posture

BFF/read-model compatibility:
  accepted; merged handler output is projected through the milestone 024 read
  models without exposing merge internals as product contracts

handler-heavy deterministic performance gate:
  accepted; focused Release evidence proves parity, cleanup, elapsed, and
  allocation behavior for handler-heavy deterministic workloads

optimized full-cache handler matrix:
  accepted as local full-cache evidence; active=4 handler delta/merge is
  correct and no longer an elapsed-time blocker in the captured matrix

active=4 allocation overhead:
  accepted as scoped warning; allocation remains above active=1 and parity is
  not claimed

arbitrary stateful handler concurrency:
  not accepted; handlers must explicitly opt into mergeable semantics before
  using ordered concurrent handler delta compute

delta serialization:
  accepted as an in-process/versioned contract gate, not as a production
  persistent storage or broker adapter proof

production persistent durable adapter:
  not implemented; future reliability milestone required

production HTTP BFF host:
  not implemented; the accepted surface remains application read models

frontend application:
  not implemented; future product milestone required

true live network ingestion:
  not implemented; deterministic archive-shaped workloads remain the gate
  input

production deployment and operations:
  not implemented; deployment, rollback, autoscaling, alerts, and runbooks
  remain future work

exactly-once production delivery:
  not claimed; future adapter, storage, and downstream idempotency gates are
  required

known full-suite residual risk:
  accepted as unrelated allocation-sensitive synthetic benchmark caveat; the
  isolated rerun passed and the failure is outside handler delta/merge
  correctness
```

## Decision Explanations

### Accept Explicit Handler Classification

Decision: accept explicit handler execution classification as the runtime
eligibility boundary.

Why chosen: milestone 024 made stateful handler output visible but kept it on
a conservative sequential fallback. Milestone 025 needs a safe way to decide
which handlers can enter ordered concurrent delta compute. Classification
makes that decision visible: handler-free work can keep earlier concurrency,
mergeable handlers can use delta/merge, snapshot-only handlers fallback, and
unsupported handlers fail closed.

Alternatives: infer safety from handler shape, allow all stateful handlers
through the concurrent path, or keep all stateful handlers sequential.

Rejected because: inferred safety is not a contract; arbitrary stateful
concurrency can corrupt output; and keeping all stateful handlers sequential
would block the fast custom analytics goal.

Trade-offs/debt: plugin authors must now make a deliberate classification
choice and own deterministic merge semantics when selecting mergeable.

Review explanation: "Fast handler execution starts only after the handler
explicitly says how it can be merged."

### Accept Per-Batch Handler Delta Contract

Decision: accept per-batch handler deltas as the replayable unit of
concurrent stateful handler work.

Why chosen: ordered concurrent processing already separates batch-local
compute from provider-sequence commit. Stateful handler output needs the same
shape. The delta contract carries handler identity, contract version, provider
sequence, optional durable batch id, event count, source count, payload value
count, input checksum, deterministic delta id, schema version, and values.

Alternatives: merge mutable handler instances directly, export only final
snapshots, or keep process-local object identity as the idempotency key.

Rejected because: mutable instance merge is not deterministic enough;
snapshots alone do not support ordered concurrent stateful work; and
process-local identity cannot support retry, replay, or durable adapter
composition.

Trade-offs/debt: the delta format is accepted as an in-process/versioned
contract gate. Persistent storage schema, wire compatibility, and production
adapter serialization still need their own proof.

Review explanation: "The unit of replay is a handler delta, not a worker
object."

### Accept Provider-Sequence Ordered Merge

Decision: accept provider-sequence ordered handler delta merge as the only
state mutation path for mergeable handler output.

Why chosen: workers may complete out of order, especially once concurrency or
durable dispatch is involved. The merge coordinator accepts out-of-order
completion but applies only the next provider sequence, preserving the
ordered commit invariant established in milestones 021 and 022.

Alternatives: commit handler output as soon as workers complete, force worker
completion order, or let the BFF sort outputs after the fact.

Rejected because: completion-order commit would make output nondeterministic;
in-order worker completion removes useful concurrency without solving retry;
and BFF sorting cannot repair state that was merged in the wrong order.

Trade-offs/debt: later completed deltas can wait behind an earlier missing or
invalid sequence. The first blocking sequence and reason must stay visible.

Review explanation: "Completion order is allowed to vary; committed handler
state is not."

### Accept Duplicate-Replay Idempotency

Decision: accept duplicate delta detection as part of the merge contract.

Why chosen: retry, replay, durable recovery, and worker resubmission can all
produce an equivalent handler delta for the same provider sequence. The
coordinator must ignore equivalent duplicates and reject conflicting
duplicates rather than double-counting handler state.

Alternatives: treat all duplicates as fatal, merge duplicates again, or rely
on external queues to prevent duplicates.

Rejected because: fatal duplicates make recovery brittle; merging duplicates
breaks correctness; and external queues cannot be the only idempotency
boundary for RadarPulse-owned state.

Trade-offs/debt: deterministic delta identity is now part of the handler
contract. Future durable adapters must preserve that identity.

Review explanation: "Replay can happen; double-counting cannot."

### Accept MVP Runtime Integration And Fallback

Decision: accept the MVP runtime integration that selects ordered
delta/merge only for all-mergeable handler sets.

Why chosen: the runtime must be useful without weakening safety. Handler-free
work keeps the accepted ordered concurrent posture. Mergeable handler sets
use ordered handler delta/merge. Snapshot-only handler sets fallback
sequentially. Unsupported sets fail closed with diagnostics.

Alternatives: always use the fast path, always use sequential fallback, or
make runtime selection implicit.

Rejected because: always-fast is unsafe for unmergeable handlers;
always-sequential loses the milestone benefit; and implicit selection would
hide a major performance and correctness posture from operators and product
consumers.

Trade-offs/debt: the runtime now has multiple explicit postures. Diagnostics
and readiness reporting must remain clear when a run falls back or blocks.

Review explanation: "The runtime is fast only when the handler contract makes
fast safe."

### Accept BFF Compatibility

Decision: accept merged handler output through the existing milestone 024
read models.

Why chosen: milestone 024 established the product-facing output shape. The
delta/merge implementation should improve the runtime without forcing a
future frontend to learn merge coordinator internals. Merged output appears
through the same handler catalog, handler output, run detail, source output,
diagnostics, and readiness shapes.

Alternatives: expose merge coordinator state directly to BFF consumers, add a
parallel handler-delta product API, or delay BFF compatibility until a
frontend exists.

Rejected because: coordinator internals are not product contracts; a
parallel API would fragment the output surface; and delaying compatibility
would make the fast path unusable for MVP analytics.

Trade-offs/debt: product read models remain in-process application contracts.
A production HTTP host, transport DTO versioning, auth, and persistence
remain separate milestones.

Review explanation: "The product surface sees committed handler output, not
the machinery that merged it."

### Accept Handler-Heavy Performance Gate

Decision: accept the handler-heavy deterministic Release gate as sufficient
for milestone 025 fast-path readiness.

Why chosen: full-cache archive runs can be producer dominated and may hide
handler compute. The focused handler-heavy gate exercises parity, retained
cleanup, elapsed time, allocation, worker health, checksums, and first
blocking reason in a deterministic shape that makes handler work visible.

Alternatives: rely only on unit tests, rely only on full-cache archive runs,
or require cross-machine production throughput testing now.

Rejected because: unit tests do not prove performance shape; full-cache runs
alone can hide handler bottlenecks; and cross-machine certification belongs
to a later production milestone.

Trade-offs/debt: the gate proves deterministic in-process behavior, not
cloud, broker, multi-machine, or production traffic throughput.

Review explanation: "The gate is designed to see handler cost, not just
archive replay cost."

### Accept Optimized Full-Cache Handler Evidence

Decision: accept the optimized full-cache handler matrix as local
full-cache evidence with an allocation warning.

Why chosen: the requested matrix covered the whole local cache with the
standard handler and the standard-plus-heavy handler set. The optimized
active=4 rows completed correctly, kept terminal retained pressure clean,
and no longer showed elapsed-time regression versus active=1.

Alternatives: block milestone 025 until active=4 allocation reaches active=1
parity, accept the pre-optimization matrix, or ignore full-cache evidence
because the focused gate already passed.

Rejected because: allocation parity is desirable but not required for this
MVP fast-path decision; the pre-optimization matrix exposed a real merge
state cost that was worth fixing before decision; and ignoring full-cache
evidence would weaken confidence in the local large-data contour.

Trade-offs/debt: active=4 allocation remains higher than active=1. Further
sparse-state and applied-value materialization reduction should be targeted
only if allocation parity becomes a readiness requirement.

Review explanation: "The full-cache fast path is correct and no longer slow
on elapsed time, but it still spends more memory than the sequential row."

### Keep Production Adapter, Live, Frontend, And Operations Deferred

Decision: keep production persistent adapters, true live ingestion, frontend,
HTTP BFF hosting, and production operations outside milestone 025.

Why chosen: milestone 025 resolves the stateful custom analytics concurrency
contract. Pulling in persistence, deployment, live network behavior, product
UI, and exactly-once claims would mix several independent readiness surfaces
into one decision and make the result harder to review.

Alternatives: combine handler delta/merge with persistent durable adapter
work, build the frontend immediately, or claim production delivery based on
the in-process gates.

Rejected because: each surface needs its own failure model, diagnostics,
tests, and acceptance criteria. In-process deterministic gates cannot prove
production adapter durability, live ingestion behavior, or operational
readiness.

Trade-offs/debt: the next reliability step is still required before
RadarPulse can claim production-shaped recoverable execution outside one
process.

Review explanation: "The handler merge contract is now accepted; production
durability still needs its own gate."

## Included Surface

Included:

```text
handler execution classification
mergeable handler opt-in contract
snapshot-only sequential fallback
unsupported handler fail-closed diagnostics
per-batch handler delta identity, payload, validation, versioning, and
  serialization roundtrip
deterministic provider-sequence handler delta merge
duplicate replay idempotency and conflicting duplicate rejection
MVP runtime plan and provenance for ordered delta/merge versus fallback
BFF/read-model compatibility for merged handler output and diagnostics
handler-heavy deterministic Release performance gate
optimized full-cache handler performance matrix over data\nexrad
Release build and focused Release test evidence
known residual full-suite allocation-sensitive caveat attribution
```

Excluded:

```text
arbitrary stateful handler concurrency without mergeable opt-in
production persistent durable adapter implementation
Kafka, RabbitMQ, cloud queue, or database-backed runtime adapter
production HTTP BFF host
frontend application
true live network ingestion
production deployment, rollback, autoscaling, alerts, or runbooks
exactly-once production delivery
cross-machine performance certification
active=4 allocation parity with active=1
changing RadarProcessingCoreOptions.Default
changing the milestone 020 provider/execution baseline decision
changing the milestone 021 processing delta architecture decision
changing the milestone 022 ordered rebalance/topology decision
changing the milestone 023 durable runtime contract decision
changing the milestone 024 custom handler output/BFF decision
```

## Evidence

Primary source documents:

```text
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-plan.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-gate.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-full-cache-performance-matrix.md
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
  deltas, including stale topology recompute before provider-sequence
  topology mutation

milestone 023:
  broker-neutral durable envelope contract and deterministic in-process
  durable harness accepted as runtime readiness contract evidence

milestone 024:
  committed custom handler output, BFF read models, runtime posture, and
  diagnostics accepted for deterministic archive-shaped MVP workloads
```

Implementation evidence:

```text
RadarProcessingHandlerDeltaClassification:
  makes handler-free, mergeable, snapshot-only, and unsupported posture
  explicit

RadarProcessingHandlerDelta:
  carries deterministic identity, provider sequence, durable batch id,
  counters, checksums, schema version, and handler output values

RadarProcessingHandlerDeltaSerializer:
  proves in-memory versioned serialization roundtrip and version mismatch
  failure behavior

RadarProcessingHandlerDeltaMergeCoordinator:
  accepts completed deltas out of order, merges in provider sequence, detects
  duplicates, blocks on invalid earlier deltas, and exposes summaries

IRadarProcessingHandlerDeltaAccumulator:
  allows handler-owned incremental merge state for optimized commit paths

RadarProcessingQueuedProcessingSession:
  composes ordered concurrent processing with handler delta compute, sparse
  touched-source handler state, ordered merge, grouped handler value commit,
  and fallback diagnostics

RadarProcessingMvpRuntimePlan:
  selects ordered delta/merge only for all-mergeable handler sets and keeps
  explicit fallback/fail-closed posture for other handler sets

RadarProcessingRunReadModelBuilder and BFF read models:
  preserve milestone 024 query shape while exposing merged output and
  handler delta provenance

RadarProcessingBenchmarkHandlers:
  provides standard and heavy mergeable benchmark handlers with accumulator
  support for deterministic performance evidence
```

Verification:

```text
slice 1 classification suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingHandlerDeltaClassificationTests|FullyQualifiedName~RadarProcessingHandlerOutputContractTests"
  result: 11 passed, 0 failed, 0 skipped

slice 2 delta contract suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingHandlerDeltaContractTests"
  result: 6 passed, 0 failed, 0 skipped

slice 3 merge coordinator suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests"
  result: 6 passed, 0 failed, 0 skipped

slice 4 runtime integration suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingMvpRuntimePlanTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests"
  result: 7 passed, 0 failed, 0 skipped

slice 5 BFF compatibility suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingBffReadModelStoreTests|FullyQualifiedName~RadarProcessingRunReadModelTests|FullyQualifiedName~RadarProcessingHandlerDeltaBffCompatibilityTests"
  result: 11 passed, 0 failed, 0 skipped

focused milestone 025 Release gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarProcessingHandlerDeltaClassificationTests|FullyQualifiedName~RadarProcessingHandlerDeltaContractTests|FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests|FullyQualifiedName~RadarProcessingHandlerDeltaBffCompatibilityTests|FullyQualifiedName~RadarProcessingHandlerDeltaPerformanceGateTests"
  result: 26 passed, 0 failed, 0 skipped

Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

merge-state optimization focused Release suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-build
    --filter "FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests|FullyQualifiedName~RadarProcessingSyntheticBenchmarkTests|FullyQualifiedName~RadarProcessingHandlerDeltaPerformanceGateTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
  result: 53 passed, 0 failed, 0 skipped

full Release test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
  result: 890 passed, 1 failed, 3 skipped

known-caveat isolated rerun:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
    --filter "FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed, 0 skipped
```

Optimized full-cache handler matrix:

```text
cache:
  data\nexrad

rows:
  counter-checksum active=1:
    61_373.01 ms, 4_671_386_960 allocated bytes
  counter-checksum active=4:
    61_588.17 ms, 8_188_695_464 allocated bytes
  counter-checksum-heavy active=1:
    62_806.15 ms, 4_675_001_328 allocated bytes
  counter-checksum-heavy active=4:
    62_687.17 ms, 12_209_454_512 allocated bytes

active=4 optimization versus previous matrix:
  counter-checksum:
    elapsed 0.785x, allocation 0.243x
  counter-checksum-heavy:
    elapsed 0.756x, allocation 0.216x

active=4 versus same-handler active=1 after optimization:
  counter-checksum:
    elapsed 1.004x, allocation 1.753x
  counter-checksum-heavy:
    elapsed 0.998x, allocation 2.612x

correctness and lifecycle:
  4/4 rows completed
  processing completeness succeeded
  processing validation failed batches: 0
  final processed batches: 828
  final processed stream events: 27_254_760
  final processed payload values: 32_306_203_200
  final raw value checksum: 958_518_408_830
  final processing checksum: 2_294_439_733_285_583_699
  retained payload pool misses: 0
  terminal combined retained pressure: 0

interpretation:
  optimized active=4 handler delta/merge is correct and full-cache elapsed
  time is flat versus active=1 in this matrix
  active=4 allocation remains higher than active=1 and is accepted as a
  scoped warning
```

## Final Decision

Decision:

```text
accepted with scoped warnings for handler delta/merge contract and fast
custom analytics over deterministic archive-shaped MVP workloads
```

Accepted readiness answer:

```text
yes with scoped warnings, RadarPulse is ready to use ordered concurrent
handler delta compute for explicitly mergeable stateful custom handlers in
the scoped in-process MVP runtime, merge those deltas deterministically in
provider sequence, preserve snapshot-only sequential fallback and unsupported
fail-closed diagnostics, and expose committed merged output through the
existing milestone 024 read models
```

Named warnings:

```text
the accepted fast path applies only to explicitly mergeable handlers
mergeable handlers must provide deterministic handler-owned merge semantics
arbitrary stateful handlers are not made concurrent by default
snapshot-only handlers still use explicit sequential fallback
delta serialization is an in-process/versioned contract gate, not a
  production persistent adapter proof
the performance gate is deterministic in-process evidence, not cross-machine
  or production throughput certification
optimized full-cache active=4 elapsed time is flat versus active=1, but
  allocation remains higher than active=1
persistent durable adapter readiness remains future reliability work
true live network ingestion remains future work
production HTTP BFF host and frontend remain future work
production deployment, rollback, autoscaling, alerts, and runbooks remain
  future work
exactly-once production delivery is not claimed
the known allocation-sensitive synthetic benchmark caveat remains outside
  the focused handler delta/merge gate
```

Recommended next milestone input:

```text
persistent durable adapter readiness.

Use the accepted milestone 023 durable envelope contract together with the
milestone 025 handler delta identity, idempotency, replay, and ordered merge
semantics to validate one concrete persistent/broker-backed adapter shape.
The next milestone should prove storage or broker ownership, claim/retry,
recovery, poison handling, ordered commit after restart, handler delta replay,
release cleanup, operator diagnostics, and failure visibility without
claiming true live ingestion or exactly-once production delivery prematurely.
```
