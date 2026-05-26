# Milestone 025: Handler Delta/Merge Contract For Fast Custom Analytics

Status: planned.

Milestone 025 starts from the closed milestone 024 custom handler output
contract and BFF readiness milestone.

Milestone 024 closed with this answer:

```text
accepted with scoped warnings for custom handler output contract and BFF
readiness over deterministic archive-shaped MVP workloads
```

The important remaining warning is:

```text
stateful custom handlers are exported from committed snapshots and use
explicit sequential fallback until a handler delta/merge contract exists
```

The important shift is:

```text
from:
  expose deterministic stateful handler output safely through committed
  snapshots and BFF-ready read models

to:
  allow eligible stateful handler analytics to compute per-batch deltas
  concurrently, then merge those deltas deterministically in provider
  sequence without weakening accepted ordered commit or BFF output contracts
```

This document records the milestone 025 concept, architecture boundary,
handler classification model, delta/merge contract, retry and idempotency
posture, diagnostics shape, BFF compatibility requirement, and expected gate
shape. The detailed implementation sequence is recorded in
`025-handler-delta-merge-contract-for-fast-custom-analytics-plan.md`.

## Milestone Goal

Milestone 025 should make stateful custom analytics fast on large archive
volumes without making unordered state mutation externally visible.

The milestone should prove these concrete outcomes:

```text
handlers can declare whether they are mergeable, snapshot-only, or unsupported
mergeable handlers can produce immutable per-batch deltas
handler deltas can be merged only in deterministic provider-sequence order
the merged handler output matches the existing sequential fallback result
non-mergeable handlers keep the explicit sequential fallback posture from
  milestone 024
handler delta serialization and versioning boundaries are explicit
retry, replay, and duplicate delta application are idempotent from the
  runtime perspective
handler delta failures produce stable diagnostics and first blocking reason
the milestone 024 BFF output/read-model contracts remain compatible
handler-heavy large-volume gates show the fast path is ready for scoped MVP
  custom analytics workloads
```

The milestone is an analytics-runtime contract milestone. It is not a
frontend, production HTTP API, live ingestion, persistent durable adapter, or
operations milestone.

## Architecture Boundary

Safe in milestone 025:

```text
add handler delta/merge contract types and handler classification metadata
add a mergeable handler interface or adapter model beside the existing
  handler output contract
compute immutable per-batch handler deltas on ordered concurrent runtime
  paths for explicitly mergeable handlers only
merge handler deltas through the existing provider-sequence ordered commit
  boundary
preserve sequential fallback for snapshot-only or non-mergeable handlers
project merged handler output through the milestone 024 read models without
  changing BFF query semantics
add parity gates comparing merged output with sequential fallback output
add handler-heavy performance gates that exercise handler work rather than
  only archive producer throughput
add failure diagnostics for unsupported handler mix, delta validation,
  serialization/version mismatch, retry exhaustion, and merge failure
```

Not safe in milestone 025 unless explicitly reprioritized:

```text
running arbitrary stateful handlers through ordered concurrent runtime paths
  without an explicit mergeable classification
allowing handler deltas to mutate shared handler state during batch compute
committing handler output in worker completion order instead of provider
  sequence order
changing the milestone 020 runtime/archive construction baseline
weakening milestone 021 processing delta ordered commit invariants
weakening milestone 022 ordered rebalance/topology commit invariants
weakening milestone 023 durable retry/recovery semantics
breaking the milestone 024 BFF output/read-model shape
claiming production exactly-once delivery from in-process idempotency tests
```

Out of scope for this milestone:

```text
frontend application implementation
production HTTP BFF host
persistent durable adapter implementation
external broker/cloud queue/database runtime adapter; these adapters are not
  planned for this project
true live network ingestion
production deployment, rollback, autoscaling, alerting, and runbooks
exactly-once production delivery claims
cross-machine performance certification
```

## Handler Classification

Milestone 025 should introduce an explicit handler execution classification.
The classification must be visible before runtime selection chooses an
ordered concurrent handler path.

Expected classifications:

```text
mergeable:
  the handler can compute an immutable per-batch delta from batch-local input
  and can merge that delta later in provider sequence into a deterministic
  aggregate state

snapshot-only:
  the handler can expose committed snapshot output but does not provide a
  delta/merge contract; it keeps milestone 024 sequential fallback behavior

unsupported:
  the handler cannot safely participate in the MVP runtime surface and should
  fail closed with diagnostics rather than silently selecting an unsafe path
```

The default classification for existing stateful handlers should remain
snapshot-only unless they explicitly opt into mergeable semantics. This
preserves milestone 024's conservative safety posture.

Mixed handler sets should use the safest compatible posture:

```text
all handlers mergeable:
  eligible for ordered concurrent handler delta compute and ordered merge

any handler snapshot-only:
  route the MVP runtime through explicit sequential fallback unless the caller
  asks for fail-closed mergeable-only behavior

any handler unsupported:
  fail closed with an unsupported handler diagnostic and first blocking reason
```

## Delta Contract

A handler delta represents batch-local analytic work. It must be immutable
after creation and must not rely on process-local object identity.

The contract should carry:

```text
handler identity:
  stable handler name and contract version

batch identity:
  provider sequence, optional durable batch id where present, event count,
  source count, payload value count, raw checksum, and input checksum if
  available

delta identity:
  deterministic delta id derived from handler identity and batch identity, or
  an equivalent idempotency key

payload:
  handler-owned delta data with explicit serialization and versioning
  boundaries

validation:
  field names, source identities, counters, checksums, and warnings required
  to reject ambiguous or incompatible deltas before merge
```

Deltas should be per accepted `RadarEventBatch`, not per event. The contract
should avoid copying payload bytes and should not introduce retained payload
ownership outside the accepted runtime/archive ownership boundaries.

## Merge Contract

The merge stage is the only place where handler aggregate state changes for
the ordered concurrent path.

Required merge invariants:

```text
merge order is provider sequence order
worker completion order is not merge order
the same delta id cannot be applied twice to the same handler aggregate
missing earlier provider sequences block later merge and publication
failed or invalid earlier deltas block later merge and publication
merged output must be deterministic and match sequential fallback output for
  the same handler inputs
merge failures must preserve enough state for retry or diagnostic reporting
```

Merge state should be separate from batch-local delta compute state. The
runtime can keep multiple batch deltas active, but committed handler output
must advance through the provider-sequence ordered boundary already accepted
by milestones 021 and 022.

## Serialization And Versioning

Milestone 025 does not need a production storage adapter, but it must define
the serialization boundary the accepted local persistent adapter path will
preserve.

The milestone should define:

```text
stable handler contract id or version
stable delta schema version
serialization-compatible delta payload shape for tests and the accepted local
  persistent adapter path
deserialization failure diagnostics
version mismatch diagnostics
forward-compatible rejection behavior for unknown required fields
idempotency key preservation across serialization roundtrip
```

The first implementation may use in-memory serialization roundtrip gates
instead of choosing an external format as a production commitment. The
important contract is that deltas are value-like, versioned, and replayable.

## Retry, Replay, And Idempotency

Handler delta behavior must not contradict the milestone 023 durable runtime
contract.

Required posture:

```text
retrying batch-local delta compute for the same handler and batch produces
  the same idempotency key and equivalent delta payload
replaying an already applied delta is detected and does not double-count
handler state
retrying a failed merge is allowed only when the merge state can prove the
  previous attempt did not partially commit, or when the handler implements a
  deterministic idempotent merge result
invalid, incompatible, or poison deltas block later provider sequences and
  surface a first blocking reason
cancellation cleanup must leave no retained handler-delta pressure in
  terminal successful or failed runs
```

Milestone 025 should claim runtime idempotency for the scoped in-process
delta/merge path only. It must not claim exactly-once production delivery.

## BFF Compatibility

Milestone 024 accepted the BFF shape for run detail, batch detail, source
output, handler output, handler catalog, diagnostics, readiness status,
warnings, retained pressure, release health, checksums, and first blocking
reason.

Milestone 025 must preserve that shape:

```text
merged handler output projects through the same handler output read models
handler catalog metadata exposes mergeable versus snapshot-only posture
diagnostics report whether output came from sequential fallback or ordered
delta/merge
unsupported or blocked handler delta work appears as readiness diagnostics,
  not as missing or silently partial output
```

The BFF contract should not expose runtime queue internals or mutable
handler-delta implementation details.

## Readiness Question

Milestone 025 should close by answering:

```text
is RadarPulse ready to run scoped stateful custom analytics through an
ordered concurrent handler delta/merge path and expose the merged output
through the existing MVP BFF read models?
```

The expected accepted answer is:

```text
yes with scoped warnings, for explicitly mergeable handlers over
deterministic archive-shaped workloads; snapshot-only handlers keep explicit
sequential fallback, and production adapters, live ingestion, frontend,
operations, and exactly-once delivery remain future work
```

## Expected Evidence

Primary evidence should include:

```text
handler classification tests
delta contract validation tests
deterministic provider-sequence merge tests
sequential fallback parity tests
serialization and versioning roundtrip/rejection tests
retry, replay, duplicate delta, and idempotency tests
failure diagnostic and first blocking reason tests
BFF compatibility tests for merged output and handler posture metadata
handler-heavy large-volume performance gate
Release build and focused Release milestone gate
```

Optional evidence, if affordable before decision trace:

```text
full Release test project
full-cache regression matrix to confirm no runtime/archive baseline
  regression outside handler-heavy workloads
```

## Unsupported State

Milestone 025 does not implement:

```text
merge contracts for arbitrary existing stateful handlers unless they opt in
production persistent durable adapter serialization
production HTTP BFF host
frontend application
true live network ingestion
production deployment or operations runbooks
exactly-once production delivery
cross-machine performance certification
```

## Implementation Direction

The first implementation should add the minimum explicit path:

```text
handler execution classification contract
mergeable handler delta contract and validation
in-memory value-like delta serialization/versioning gate
ordered handler delta coordinator that accepts out-of-order completed deltas
  and merges by provider sequence
MVP runtime selection that uses ordered delta/merge only when every handler
  is mergeable
fallback and fail-closed diagnostics for snapshot-only and unsupported
  handler sets
BFF/read-model projection updates for handler posture and delta/merge
  provenance
focused correctness, parity, retry/idempotency, and performance tests
```

Existing handler-free ordered concurrent runtime APIs and milestone 024 BFF
query methods should remain compatible unless a concrete contract conflict is
found and documented before implementation continues.
