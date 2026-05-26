# Milestone 024 Decision Trace

Date: 2026-05-26

Decision: accept custom handler output contract and BFF readiness for
deterministic archive-shaped MVP workloads with named scoped warnings.

This decision accepts milestone 024's stable handler output contract,
committed snapshot export posture, processing read models, BFF read-model
query surface, and MVP runtime wrapper on top of the milestone 020
runtime/archive baseline, the milestone 021 ordered processing foundation,
the milestone 022 ordered rebalance/topology foundation, and the milestone
023 durable runtime contract.

The accepted scope is MVP-facing output readiness: RadarPulse can expose
committed custom handler outputs, handler metadata, processing run state,
batch details, source outputs, readiness diagnostics, retained pressure,
release health, provider sequence, checksums, and first blocking reason
through stable application read models that a future frontend can consume.

The accepted handler posture is conservative by design. Handler-free
processing can continue to use the ordered concurrent runtime surfaces that
were accepted in earlier milestones. Stateful custom handlers are exported
from committed snapshots and use an explicit sequential fallback in the MVP
runtime surface until a handler delta/merge contract exists.

The decision does not accept ordered concurrent execution for arbitrary
stateful handlers, handler delta/merge, high-volume custom analytics
performance readiness, a concrete frontend, a production HTTP BFF host,
persistent durable adapters, true live network ingestion, production
deployment/operations readiness, or exactly-once production delivery.

The milestone is an MVP output contract acceptance, not a production
analytics throughput claim. It proves that results are stable and consumable;
it intentionally leaves the fast stateful handler parallelism contract as the
next milestone input.

## Decision Matrix

```text
custom handler output contract:
  accepted with scoped warnings

handler output descriptor metadata:
  accepted; handler name, contract id, field name, field type, slot, and
  presentation-safe label are explicit

committed snapshot export posture:
  accepted; stateful handler output is exported from committed deterministic
  state, not from speculative concurrent handler mutation

stateful handler runtime posture:
  accepted with warning; stateful handlers use explicit sequential fallback
  until a handler delta/merge contract exists

handler-free ordered concurrent posture:
  preserved; handler-free cores may keep using the previously accepted
  ordered concurrent processing and ordered rebalance surfaces

processing run read models:
  accepted; run, batch, source, handler output, diagnostics, readiness, and
  warning shapes are stable enough for MVP frontend work

BFF read-model query surface:
  accepted; latest run, run detail, batch list, batch detail, source output,
  handler catalog, handler output, and diagnostics queries are available
  through an application-level store

MVP runtime wrapper:
  accepted; runtime result provenance reports whether the plan is
  handler-free ordered concurrent or stateful sequential fallback

archive-shaped MVP gate:
  accepted; deterministic archive-shaped workload evidence proves that MVP
  outputs can be produced and served with diagnostics visible

optional full-cache performance matrix:
  accepted as regression evidence only; no full-cache regression was observed,
  but the matrix does not prove future handler delta/merge throughput

high-volume custom analytics readiness:
  not accepted; fast stateful custom analytics on large volumes needs a
  handler delta/merge milestone with performance gates

handler delta/merge:
  not implemented; recommended as the next milestone input because MVP
  analytics needs fast custom handler processing on large data volumes

frontend application:
  not implemented; the accepted surface is backend read-model readiness for a
  future frontend

production HTTP BFF host:
  not implemented; the accepted BFF boundary is an application read-model
  query surface, not a deployed API host

persistent durable adapter:
  not implemented; milestone 023 adapter follow-up remains future reliability
  work after the immediate MVP analytics slice

true live network ingestion:
  not implemented; archive-shaped deterministic workloads remain the gate
  input

production deployment and operations:
  not implemented; deployment, rollback, autoscaling, alerts, and runbooks
  remain future work

exactly-once production delivery:
  not claimed; future adapter/storage/downstream idempotency gates are still
  required
```

## Decision Explanations

### Accept Handler Output Contract

Decision: accept the handler output contract as the stable MVP export shape.

Why chosen: previous milestones proved runtime ordering, rebalance ordering,
and durable envelope semantics, but the computed result was still not shaped
for product consumption. Milestone 024 introduces explicit handler output
descriptors and fields so a frontend-facing consumer can discover what
handler values exist without depending on processing internals.

Alternatives: expose internal handler state directly, defer output contracts
until a frontend exists, or jump immediately to a production API.

Rejected because: internal handler state is not a stable contract; waiting
for frontend work would delay the MVP data boundary; and production API work
would mix deployment concerns with the read-model decision.

Trade-offs/debt: the contract is intentionally narrow. It is stable for MVP
read models, but it is not a serialization, replay, merge, or plugin
compatibility contract for arbitrary future handler implementations.

Review explanation: "The frontend gets a stable output shape without owning
processing internals."

### Accept Committed Snapshot Export And Sequential Fallback

Decision: accept committed snapshot export plus explicit sequential fallback
as the safe stateful handler posture.

Why chosen: milestones 021 and 022 only accepted ordered concurrent compute
for handler-free processing deltas. Stateful custom handlers can mutate local
state and may have side effects. Without a delta/merge contract, running
them through ordered concurrent compute would make correctness depend on
implicit merge behavior that does not exist.

Alternatives: allow all handlers through ordered concurrent processing,
block the MVP output milestone until delta/merge exists, or hide the fallback
inside the runtime.

Rejected because: allowing arbitrary stateful handlers would be unsafe;
blocking would delay usable MVP output; and hidden fallback would mislead
operators and future frontend consumers about runtime posture.

Trade-offs/debt: stateful handler output is safe and deterministic, but not
yet fast enough for high-volume custom analytics where handler work is the
dominant cost.

Review explanation: "Stateful handlers can produce MVP output now, but the
runtime is honest when it falls back to sequential execution."

### Preserve Handler-Free Ordered Concurrent Runtime

Decision: preserve the earlier ordered concurrent handler-free runtime
boundary.

Why chosen: milestone 024 should not weaken accepted runtime/archive
foundations. Handler-free cores already have a proven non-mutating delta plus
provider-sequence ordered commit model. The new handler output posture adds
a safe stateful path without changing that accepted handler-free contour.

Alternatives: force all MVP processing through sequential fallback, or
reopen the milestone 021 and 022 ordered concurrent decisions.

Rejected because: handler-free ordered concurrency remains valid; forcing all
work through sequential fallback would throw away accepted runtime headroom;
and reopening closed runtime decisions would expand the milestone without a
new correctness reason.

Trade-offs/debt: the runtime now has an explicit split: handler-free can be
ordered concurrent, while stateful handler work waits for a merge contract
before it can parallelize safely.

Review explanation: "The new handler output path does not erase the
handler-free concurrency already accepted."

### Accept Processing Read Models

Decision: accept the processing run, batch, source, handler output, and
diagnostic read models as the MVP result shape.

Why chosen: a future frontend needs stable result objects rather than queue,
session, core, or durable envelope internals. The read models expose provider
sequence, status, event counts, payload bytes, payload values, checksums,
timestamp bounds, source output values, handler catalog entries, warnings,
readiness, release health, retained pressure, and first blocking reason.

Alternatives: have BFF consumers assemble output from lower-level runtime
objects, or wait until HTTP endpoints exist before defining DTOs.

Rejected because: low-level assembly leaks runtime internals; waiting for
HTTP endpoints would leave the application boundary undefined.

Trade-offs/debt: the read models are in-process application contracts. A
future API can map them to transport DTOs, but transport versioning and auth
remain separate work.

Review explanation: "The product surface can read processing results without
knowing how the runtime queues and commits them."

### Accept BFF Read-Model Query Surface

Decision: accept the application-level BFF read-model store as the first
backend-for-frontend surface.

Why chosen: the frontend needs predictable queries: latest run, run detail,
batch list, batch detail, source output, handler catalog, handler output, and
diagnostics. Milestone 024 provides those query shapes without adding a
production web host or persistence dependency.

Alternatives: build the frontend first, add a deployed HTTP API now, or
store only the latest raw runtime result and leave queries to callers.

Rejected because: frontend-first work would lack a backend contract; HTTP
deployment would be premature for this milestone; and raw-result callers
would duplicate filtering and diagnostic logic.

Trade-offs/debt: this is not a production BFF service. It is the application
contract that a future BFF host should expose or adapt.

Review explanation: "The BFF contract exists before the BFF deployment."

### Accept MVP Runtime Wrapper

Decision: accept the MVP runtime wrapper as the explicit posture selector for
handler output work.

Why chosen: MVP output needs to route workloads according to handler safety.
The runtime plan and result make the selected posture visible: handler-free
ordered concurrent where safe, stateful sequential fallback where no
delta/merge contract exists, and clear diagnostics for unsupported shapes.

Alternatives: let callers choose runtime surfaces manually, or infer posture
only through logs.

Rejected because: manual selection would repeat safety decisions at each
call site; logs are not a stable contract for frontend-facing readiness.

Trade-offs/debt: the wrapper is intentionally conservative. It does not yet
schedule mergeable handlers concurrently because the merge contract has not
been defined.

Review explanation: "The MVP runtime makes the safety posture part of the
result, not an implementation accident."

### Accept Archive-Shaped MVP Gate

Decision: accept the deterministic archive-shaped MVP gate as sufficient for
this output contract milestone.

Why chosen: the milestone's readiness question is whether RadarPulse can
produce and serve MVP processing results for a future frontend. A
deterministic archive-shaped workload can prove stable output projection,
provider-sequence ordering, checksums, handler values, diagnostics,
readiness, release health, retained pressure cleanup, and first blocking
reason without adding live ingestion uncertainty.

Alternatives: require live network ingestion, a deployed frontend, or a
production adapter before accepting the output contract.

Rejected because: those are separate readiness surfaces. The output contract
should be proven before live, frontend, and production adapter work depend on
it.

Trade-offs/debt: archive-shaped evidence is strong for deterministic output
and regression checks, but it is not a live operations or cross-machine
performance proof.

Review explanation: "Archive-shaped input is enough to accept the output
contract; live input still needs its own gate."

### Accept Optional Full-Cache Regression Evidence

Decision: accept the optional full-cache matrix as regression evidence for
milestone 024.

Why chosen: the matrix covered the full file cache after the output/BFF
slice work. Default queued-owned remained faster than explicit
BlockingBorrowed in rebalance-archive static, sampling, and
rebalance-session rows. Ordered active=4 stayed close to active=1 on
allocation and slightly faster on elapsed time in this run. Correctness,
checksum parity, accepted move parity, worker health, release health, and
terminal retained pressure cleanup passed.

Alternatives: skip the matrix because milestone 024 is mostly read-model
work, or treat the matrix as proof that high-volume custom analytics is
already ready.

Rejected because: the matrix is useful regression evidence; however, it does
not exercise future handler delta/merge parallelism and should not be
overstated.

Trade-offs/debt: callback attribution remains heavier under queued-owned and
the workload remains archive-producer dominated. The next analytics
milestone needs its own handler-heavy performance gate.

Review explanation: "The full-cache run says milestone 024 did not regress
the accepted contour; it does not say handler delta/merge is solved."

### Promote Handler Delta/Merge As The Next Milestone Input

Decision: make handler delta/merge for fast custom analytics the recommended
next milestone input.

Why chosen: MVP needs fast custom analytics on large volumes. Milestone 024
made custom handler outputs safe and visible, but stateful handler work still
uses sequential fallback. To make large-volume custom analytics fast, the
runtime needs a contract for classifying mergeable handlers, producing
per-batch deltas, merging those deltas deterministically by provider
sequence, preserving retry/idempotency semantics, and proving parity against
the sequential fallback.

Alternatives: move next to persistent durable adapter readiness, keep
stateful handler work sequential until after frontend work, or parallelize
handlers without a formal merge contract.

Rejected because: persistent adapter work remains important but does not
solve the immediate MVP analytics throughput need; frontend work would be
built on slow stateful processing; and parallelizing handlers without a
merge contract would weaken correctness.

Trade-offs/debt: persistent durable adapter readiness remains future
reliability work. The immediate MVP acceleration path should solve handler
delta/merge first.

Review explanation: "Now that handler output is visible, the next problem is
making stateful custom analytics fast without lying about merge safety."

### Keep Persistent Adapter, Live, Frontend, And Operations Deferred

Decision: keep persistent durable adapter, true live ingestion, concrete
frontend implementation, production HTTP hosting, deployment, rollback,
autoscaling, alerts, runbooks, and exactly-once production delivery out of
milestone 024.

Why chosen: milestone 024 is the product-facing result contract. These
deferred surfaces each need separate gates: adapter storage and recovery,
network acquisition and feed health, UI workflows, deployed API concerns,
production operations, and end-to-end idempotency.

Alternatives: broaden milestone 024 into an MVP end-to-end product release,
or imply readiness because the read-model contract exists.

Rejected because: broadening would mix too many failure modes; implying
readiness would overstate what the gate proved.

Trade-offs/debt: the accepted BFF read models are ready for future product
work, but production deployment and live operations remain explicitly
unaccepted.

Review explanation: "The result contract is ready; production product
delivery still has separate gates."

### Accept Verification Posture

Decision: accept milestone 024 verification as sufficient for the scoped
decision.

Why chosen: the focused Debug slice suites passed, Release build succeeded
with `0` warnings and `0` errors, the focused milestone 024 Release gate
passed `17/17`, and the full Release test project passed `865` tests with
`0` failures and `3` skipped tests. The optional full-cache performance
matrix showed no contour regression.

Alternatives: block acceptance until handler delta/merge is implemented,
until frontend work exists, or until persistent adapter work exists.

Rejected because: those are future surfaces. The current evidence is enough
for handler output contract and BFF read-model readiness.

Trade-offs/debt: verification does not include handler-heavy parallel
analytics throughput, production API transport, persistence, live ingestion,
or deployment operations.

Review explanation: "The scoped output/BFF gate is clean; the next milestone
must test handler-heavy analytics throughput."

## Included Surface

Included application surfaces:

```text
RadarProcessingHandlerStatePosture
RadarProcessingHandlerOutputField
RadarProcessingHandlerOutputDescriptor
RadarProcessingHandlerOutputContract
RadarProcessingSourceIdentityReadModel
RadarProcessingHandlerOutputValueReadModel
RadarProcessingSourceOutputReadModel
RadarProcessingBatchReadModel
RadarProcessingRunDiagnosticsReadModel
RadarProcessingRunReadModel
RadarProcessingRunReadModelBuilder
RadarProcessingBffReadModelStore
```

Included infrastructure/runtime surfaces:

```text
RadarProcessingMvpRuntimePlan
RadarProcessingMvpRuntimeResult
RadarProcessingArchiveQueuedOverlapRunner.RunMvpProcessingAsync
```

Included MVP contour:

```text
deterministic archive-shaped workloads
committed snapshot export for stateful handler output
explicit sequential fallback for stateful handlers
handler-free ordered concurrent posture preserved
processing run, batch, source, handler output, diagnostics, and readiness
  read models
application-level BFF read-model query surface
retained pressure, release health, provider sequence, checksums, processing
  completeness, and first blocking reason remain visible
```

Included evidence shapes:

```text
handler output contract tests
processing read-model projection tests
BFF read-model store tests
MVP runtime posture tests
archive-shaped MVP gate test
Release build
focused milestone 024 Release gate suite
full Release test project
optional full-cache performance matrix
```

Excluded:

```text
ordered concurrent execution for arbitrary stateful handlers
handler delta/merge
high-volume custom analytics performance readiness
concrete frontend application
production HTTP BFF host
persistent durable adapter implementation
Kafka, RabbitMQ, cloud queue, or database-backed runtime adapter
true live network ingestion
production deployment, rollback, autoscaling, alerting, or runbooks
exactly-once production delivery
cross-machine performance certification
changing RadarProcessingCoreOptions.Default
changing the milestone 020 provider/execution baseline decision
changing the milestone 021 processing delta architecture decision
changing the milestone 022 ordered rebalance/topology decision
changing the milestone 023 durable runtime contract decision
```

## Evidence

Primary source documents:

```text
docs/milestones/024-custom-handler-output-contract-and-bff-readiness.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-plan.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-gate.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-full-cache-performance-matrix.md
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
```

Implementation evidence:

```text
RadarProcessingHandlerOutputContract:
  defines handler descriptor metadata, stable output fields, field types,
  slots, labels, and handler posture
  rejects invalid or ambiguous field shapes

RadarProcessingRunReadModelBuilder:
  projects processing run, batch, source, handler output, diagnostics,
  readiness, warnings, and checksums into stable read models
  keeps runtime queue/session internals out of the BFF contract

RadarProcessingBffReadModelStore:
  serves latest run, run detail, batch list, batch detail, source output,
  handler output, handler catalog, and diagnostics queries

RadarProcessingMvpRuntimePlan:
  reports handler-free ordered concurrent eligibility
  reports explicit sequential fallback for stateful handlers without
  delta/merge support

RadarProcessingArchiveQueuedOverlapRunner.RunMvpProcessingAsync:
  exposes the MVP runtime path that produces BFF-ready processing output
  without adding a persistent adapter dependency
```

Verification:

```text
handler output contract focused suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingHandlerOutputContractTests"
  result: 5 passed, 0 failed, 0 skipped

processing read-model focused suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRunReadModelTests"
  result: 4 passed, 0 failed, 0 skipped

BFF read-model store focused suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingBffReadModelStoreTests"
  result: 4 passed, 0 failed, 0 skipped

MVP runtime posture focused suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingMvpRuntimePlanTests"
  result: 3 passed, 0 failed, 0 skipped

archive-shaped MVP gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingMvpArchiveGateTests"
  result: 1 passed, 0 failed, 0 skipped

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

Optional full-cache performance evidence:

```text
rebalance-archive default elapsed ratios versus explicit BlockingBorrowed:
  static: 0.812x
  sampling: 0.931x
  rebalance-session: 0.885x

rebalance-archive default allocation ratios versus explicit BlockingBorrowed:
  static: 1.002x
  sampling: 1.003x
  rebalance-session: 1.000x

ordered-archive-processing active=4 versus active=1:
  elapsed ratio: 0.982x
  steady allocation ratio: 1.007x

correctness and lifecycle:
  validation succeeded
  processing completeness succeeded
  checksum parity matched
  accepted move parity matched
  worker failed batches/items: 0/0
  retained payload pool misses: 0
  release failures: 0
  terminal combined retained pressure: 0

interpretation:
  no full-cache regression was observed after milestone 024 slice work
  the matrix is regression evidence, not handler delta/merge performance
  proof
```

## Final Decision

Decision:

```text
accepted with scoped warnings for custom handler output contract and BFF
readiness over deterministic archive-shaped MVP workloads
```

Accepted readiness answer:

```text
yes with scoped warnings, RadarPulse is ready to expose MVP processing
results through stable custom handler output contracts and application-level
BFF read models for a future frontend, using committed snapshot export and
explicit sequential fallback for stateful handlers while preserving the
accepted handler-free ordered concurrent runtime foundations
```

Named warnings:

```text
stateful handlers do not yet participate in ordered concurrent delta compute
handler delta/merge is not implemented
high-volume custom analytics performance readiness is not accepted yet
the BFF surface is an application read-model query surface, not a production
  HTTP API host
the frontend application is not implemented
persistent durable adapter readiness remains future reliability work
true live network ingestion is not implemented
production deployment, rollback, autoscaling, alerts, and runbooks are not
  implemented
exactly-once production delivery is not claimed
the optional full-cache matrix is regression evidence, not proof of future
  handler-heavy analytics throughput
```

Recommended next milestone input:

```text
handler delta/merge contract for fast custom analytics.

Define mergeable handler classification, per-batch handler deltas,
deterministic provider-sequence merge, serialization/versioning boundaries,
retry and idempotency behavior, failure diagnostics, sequential fallback
parity gates, BFF output compatibility, and a handler-heavy large-volume
performance gate.
```
