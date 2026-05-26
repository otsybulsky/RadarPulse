# Milestone 024: Custom Handler Output Contract And BFF Readiness Architecture

Status: complete.

Milestone 024 starts from the closed milestone 023 durable/cross-process
runtime readiness milestone.

Milestone 023 closed with this answer:

```text
accepted with scoped warnings for durable/cross-process runtime readiness over
the broker-neutral durable envelope contract and deterministic in-process
durable harness
```

Milestone 023 recommended persistent durable adapter readiness as the next
input. Post-closeout MVP planning changes the order: RadarPulse should first
make the processing output useful to a future product surface, then return to
persistent adapter reliability work.

The important shift is:

```text
from:
  prove that accepted runtime/archive batches can move through durable
  envelopes with ordered commit and operator-visible recovery state

to:
  expose custom handler outputs and processing diagnostics through stable
  MVP-facing read models that a future frontend can consume
```

This document records the milestone 024 concept, architecture boundary,
handler-output posture, BFF boundary, readiness question, and expected gate
shape. The detailed implementation sequence is recorded in
`024-custom-handler-output-contract-and-bff-readiness-plan.md`.

## Milestone Goal

Milestone 024 should define and implement the first MVP-facing processing
output surface.

The milestone should prove these concrete outcomes:

```text
custom handler outputs have a stable export contract
handler descriptor metadata is visible to frontend-facing consumers
handler state is projected into deterministic source and batch summaries
stateful handler execution has an explicit safe posture under ordered runtime
  paths
BFF read models can serve latest run, batch list, batch detail, source
  summaries, handler outputs, diagnostics, and readiness state
processing completeness, checksums, provider sequence, retained pressure,
  release health, and first blocking reason remain visible
the accepted runtime/archive default baseline remains the inherited execution
  contour unless a concrete surface incompatibility is proven
```

The milestone is MVP-facing, not production-operations-facing. It should make
RadarPulse's computed results consumable by a future UI before adding a
persistent durable adapter.

## Architecture Boundary

Safe in milestone 024:

```text
define stable handler output and BFF DTO contracts
project existing handler snapshots into product-readable output shapes
add handler output summary/read-model builders in application or
  infrastructure code
add a BFF-oriented application surface or lightweight presentation surface
  without implementing the frontend itself
use deterministic archive-shaped workloads for MVP evidence
preserve the accepted queued-owned runtime/archive baseline and ordered
  commit invariants
make stateful handler execution posture explicit for concurrent runtime paths
add focused tests for output projection, DTO stability, deterministic source
  summaries, and BFF responses
record Release gate evidence over an archive-shaped MVP workload
```

Not safe in milestone 024 unless explicitly reprioritized:

```text
claiming production deployment readiness from BFF DTOs
building the frontend application
introducing a production broker or persistent durable adapter as a hidden
  dependency
claiming live network ingestion readiness
allowing stateful handlers through ordered concurrent delta compute without a
  handler-state delta/merge contract
hiding runtime readiness blockers, retained pressure, or release failures
behind frontend-friendly summaries
changing the milestone 020 provider/execution baseline
weakening milestone 021, 022, or 023 ordered commit invariants
```

Out of scope for this milestone:

```text
persistent durable adapter implementation
external broker/cloud queue/database runtime adapter; these adapters are not
  planned for this project
true live network ingestion
frontend UI implementation
production deployment, rollback, autoscaling, alerting, and runbooks
exactly-once production delivery claims
cross-machine performance certification
```

## Handler Output Model

RadarPulse already has handler descriptors, source-local handler state, and
snapshot projection. Milestone 024 should turn those internal shapes into a
stable result contract.

The output model should represent:

```text
run identity:
  stable identifier for the processing run or archive-shaped MVP request

batch identity:
  provider sequence, optional durable batch id where present, event count,
  payload bytes, payload value count, raw checksum, and processing status

handler catalog:
  handler name, version or contract id if introduced, field names, field
  types, slot mapping, and presentation-safe labels where needed

source output:
  source id, radar/source metadata available in the current source universe,
  processed event counts, timestamp bounds, checksums, and handler field
  values

diagnostics:
  processing completeness, validation status, retained pressure, release
  health, worker telemetry, readiness status, first blocking reason, and
  relevant warnings
```

The first MVP output contract can be intentionally narrow. It should be
stable enough for a future frontend to build against, but it should not claim
that every custom handler side effect is serializable or mergeable.

## Handler State And Ordered Runtime Posture

Milestones 021 and 022 deliberately kept ordered concurrent processing
handler-free because arbitrary custom handlers can mutate state and produce
side effects. Milestone 024 must make the safe handler posture explicit.

Acceptable MVP postures:

```text
snapshot/export posture:
  existing stateful handlers run only on paths that commit handler state
  synchronously and deterministically; the BFF exports committed snapshots

restricted mergeable posture:
  only handlers with an explicit delta/merge and serialization contract can
  participate in ordered concurrent compute

sequential fallback posture:
  when handlers are configured and no delta/merge contract exists, the MVP
  runtime surface selects a documented non-concurrent processing path rather
  than silently using unsafe ordered concurrent handler deltas
```

The milestone should choose and document the accepted posture before exposing
handler output through BFF contracts. The conservative default is
snapshot/export plus sequential fallback for stateful handlers.

## BFF Boundary

The BFF is the backend-for-frontend boundary for future UI work. It should
avoid leaking low-level runtime internals while preserving enough diagnostics
for operator and developer clarity.

The first BFF surface should be able to answer:

```text
what is the latest processing run?
which batches were accepted, processed, failed, blocked, or released?
which sources have output?
what handler fields are available?
what are the handler output values for a source or batch?
is the runtime output ready for display?
if not ready, what is the first blocking envelope, batch, or diagnostic?
```

The BFF can start as application-level read services and DTOs. A concrete HTTP
host is useful only if it materially helps the frontend-readiness gate.

## Readiness Question

Milestone 024 should close by answering:

```text
is RadarPulse ready to expose MVP processing results through stable custom
handler output and BFF read models for a future frontend?
```

The expected accepted answer is:

```text
yes with scoped warnings, for deterministic archive-shaped MVP workloads and
the accepted handler-state posture; persistent adapter, true live ingestion,
production operations, and frontend implementation remain future work
```

## Expected Evidence

Primary evidence should include:

```text
handler output contract tests
handler descriptor/read-model projection tests
processing run summary and batch detail DTO tests
BFF latest-run/source-output/diagnostics tests
stateful handler posture tests that prove sequential fallback or explicit
  mergeable handler behavior
archive-shaped MVP workload gate
Release build
focused Release gate suite
full test project with known residual caveats called out separately
```
