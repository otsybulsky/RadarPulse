# Milestone 026: Persistent Durable Adapter Readiness

Status: planned.

Milestone 026 starts from the closed milestone 025 handler delta/merge
contract for fast custom analytics milestone.

Milestone 025 closed with this answer:

```text
accepted with scoped warnings for handler delta/merge contract and fast
custom analytics over deterministic archive-shaped MVP workloads
```

The important remaining warning is:

```text
persistent durable adapter readiness remains future reliability work
```

The important shift is:

```text
from:
  durable/cross-process runtime semantics are proven by a broker-neutral
  in-process harness and handler delta replay/idempotency is proven by
  in-process versioned contracts

to:
  one concrete persistent adapter shape preserves durable envelope state,
  restart recovery, ordered commit, handler delta replay, retained ownership
  cleanup, and operator diagnostics across adapter/session recreation
```

This document records the milestone 026 concept, architecture boundary,
persistent adapter model, schema/versioning posture, recovery and lease
semantics, handler delta composition, diagnostics shape, and expected gate
question. The detailed implementation sequence will be recorded in
`026-persistent-durable-adapter-readiness-plan.md`.

## Milestone Goal

Milestone 026 should validate one concrete persistent or broker-like adapter
against the milestone 023 durable envelope contract, while preserving the
milestone 025 handler delta identity, idempotency, replay, and ordered merge
semantics.

The milestone should prove these concrete outcomes:

```text
durable envelope state can be serialized with an explicit schema version
accepted envelopes can be recovered after adapter/session recreation
duplicate accept remains idempotent after restart
claim, complete, fail, abandon, retry, poison, commit, release, cancel, and
  cleanup transitions persist across adapter/session recreation
provider-sequence ordered commit remains deterministic after restart
claimed envelopes do not become implicitly retryable without a recovery policy
abandoned attempts can be retried or poisoned by bounded policy
completed but uncommitted envelopes remain ordered commit inputs after restart
released envelopes remain terminal and do not reappear as pending work
handler delta identity and replay behavior survive adapter-backed execution
operator summaries expose adapter state, first blocking envelope, and first
  blocking reason
focused Release gates prove the adapter contract without claiming production
  exactly-once delivery
```

This is a reliability milestone. It is not a true live ingestion, production
HTTP BFF, frontend, deployment, autoscaling, runbook, or exactly-once
production delivery milestone.

## Architecture Boundary

Safe in milestone 026:

```text
add a storage-neutral durable adapter contract beside the existing in-process
  durable queue contract
add a concrete deterministic local persistent adapter that can be exercised by
  the test suite without external services
persist durable envelope snapshots and state transitions with explicit schema
  and compatibility validation
support adapter/session recreation as the restart recovery proof
recover pending, claimed, completed, failed, poison, abandoned, canceled, and
  released states without changing their semantics
compose adapter-backed state with ordered processing commit and handler
  delta replay gates
add lease or abandoned-attempt recovery policy hooks where claimed envelopes
  need operator-owned recovery
add operator-readable adapter summaries and first blocking state diagnostics
add focused tests and Release gates over adapter-backed durable workloads
```

Not safe in milestone 026 unless explicitly reprioritized:

```text
adding Kafka, RabbitMQ, a cloud queue, or a database server as a mandatory
  dependency for the local test gate
claiming production broker durability from a local deterministic adapter
claiming exactly-once delivery without downstream idempotency and production
  storage evidence
making claimed envelopes silently retryable after restart
allowing worker completion order to become externally visible commit order
weakening milestone 021 processing ordered commit invariants
weakening milestone 022 rebalance/topology ordered commit invariants
weakening milestone 023 durable envelope state semantics
weakening milestone 024 BFF read-model compatibility
weakening milestone 025 handler delta idempotency and ordered merge semantics
introducing silent borrowed-provider fallback after adapter failure
```

Out of scope for this milestone:

```text
Kafka, RabbitMQ, cloud queue, or database-backed production adapter
production HTTP BFF host
frontend application
true live network ingestion
production deployment, rollback, autoscaling, alerting, and runbooks
cross-machine throughput certification
exactly-once end-to-end production delivery claims
changing RadarProcessingCoreOptions.Default
reopening the accepted queued-owned provider/execution baseline
```

## Adapter Shape

Milestone 026 should introduce a narrow adapter boundary around the existing
durable envelope operations.

The accepted operation surface remains:

```text
accept:
  persist a new owned batch envelope or return the existing envelope for an
  idempotent duplicate accept

claim:
  claim the next pending envelope in provider sequence order and record worker
  identity and attempt metadata

complete/fail/abandon:
  persist worker attempt outcomes without treating completion as ordered commit

retry/poison:
  apply explicit bounded recovery policy while preserving batch id and
  provider sequence

commit/release:
  persist externally visible ordered commit and terminal retained cleanup

cancel/cleanup:
  make cancellation and release cleanup explicit and observable

summary:
  report accepted counts, state counts, retry counts, oldest uncommitted
  sequence, first blocking envelope, and first blocking reason
```

The selected adapter for milestone 026 is deterministic local file-backed
persistence. The milestone stops at this file-based adapter.

This is a deliberate scope decision:

```text
accepted for milestone 026:
  file-based durable envelope adapter
  restart recovery and serialization proof
  adapter-backed ordered commit and handler delta replay proof

not pulled into milestone 026:
  Kafka adapter
  RabbitMQ adapter
  cloud queue adapter
  database-backed adapter
  production broker operations or retention certification
```

The file-based adapter is not a production broker claim. It is the accepted
persistent contract adapter for this milestone. Later Kafka, RabbitMQ, cloud
queue, or database adapter work should require a new explicit milestone
decision instead of being treated as automatic continuation of milestone 026.

## Persistence Model

The adapter should persist per-envelope state at `RadarEventBatch` granularity,
not per event.

The persisted envelope schema should include:

```text
schema version
durable batch id
provider sequence
attempt
envelope state
worker id
message
accepted, claimed, completed, committed, and released timestamps
stream event count
payload byte count
payload value count
raw value checksum
owned batch payload reference or local serialized payload for the test adapter
optional handler delta identity evidence where adapter-backed handler tests
  need replay diagnostics
```

The adapter must validate schema version before loading persisted records.
Unknown future versions should fail closed with a compatibility diagnostic
rather than silently interpreting incompatible state.

For milestone 026, local serialized payload storage is accepted for the
file-based adapter because the purpose is recovery proof. Later production
adapters can replace this with broker payload references, object storage
references, or database rows only after a separate scope decision.

## Restart Recovery

Adapter/session recreation is the milestone restart proof.

Expected recovery behavior:

```text
pending:
  remains claimable

claimed:
  remains claimed and blocks ordered progress until explicit abandon, fail,
  complete, or recovery policy transition occurs

completed:
  remains an ordered commit input and can be committed after restart

failed:
  remains readiness-blocking until retry policy or poison policy acts

poison:
  remains readiness-blocking and operator-visible

abandoned:
  remains retryable or poisonable by bounded policy

canceled:
  remains cleanup-required until released

released:
  remains terminal and does not become pending or claimable again
```

The adapter must not infer success from missing process-local completion
objects. If a completed envelope needs commit data after restart, the milestone
must either persist enough deterministic completion material for the scoped
gate or define a safe recompute-on-commit path that preserves provider
sequence order and idempotency.

## Lease And Abandoned Attempt Policy

Milestone 026 should make the claimed-envelope recovery boundary explicit.

The default posture is conservative:

```text
claimed envelopes are owned by their recorded attempt
restart does not automatically retry claimed envelopes
operators or recovery policy must mark stale claims abandoned, failed, or
  poison before a new attempt can proceed
retry increments attempt and preserves batch id plus provider sequence
unbounded retry is not a readiness answer
```

The milestone can use deterministic lease timestamps or explicit abandon calls
for the gate. A full distributed lease protocol is not required, but the
adapter summary must expose stale or blocking claimed state clearly.

## Handler Delta Composition

Milestone 025 accepted handler delta identity, duplicate replay idempotency,
conflicting duplicate rejection, and provider-sequence ordered merge for
mergeable handlers.

Milestone 026 must preserve that contract when durable state is adapter-backed:

```text
handler delta id stays deterministic across retry/replay
equivalent duplicate handler deltas do not double-count output
conflicting duplicate handler deltas fail closed
out-of-order handler delta completion waits behind earlier provider sequence
merged output remains projected through milestone 024 read models
adapter-backed diagnostics identify handler delta replay or merge blockers
```

The milestone does not need to persist arbitrary production handler state for
all future plugin shapes. It should prove the accepted mergeable handler path
against the same deterministic benchmark handler surfaces used by milestone
025.

## Ordered Commit

The adapter must preserve the accepted ordered commit architecture:

```text
provider sequence is the only externally visible commit order
worker completion is not ordered commit
later completed envelopes wait behind missing, claimed, failed, abandoned,
  poison, or invalid earlier envelopes
processing commit, rebalance/topology commit, and handler merge commit cannot
  advance independently in incompatible orders
released envelopes are terminal after ordered commit and cleanup
```

Adapter persistence cannot weaken these invariants. If a restart loses
process-local commit material, the recovery path must recompute or reload the
material before ordered commit rather than skipping the envelope.

## Diagnostics And Readiness

Milestone 026 should keep operator diagnostics readable and storage-aware.

The adapter summary should report at least:

```text
adapter kind
schema version
storage path or adapter identity
accepted envelope count
pending, claimed, completed, committed, failed, poison, abandoned, canceled,
  and released counts
retry attempt count
oldest uncommitted provider sequence
first blocking batch id
first blocking provider sequence
first blocking state
first blocking reason
last compatibility or storage error where applicable
```

Readiness should fail closed when persisted state cannot be loaded, schema
compatibility fails, a blocking envelope exists, release cleanup fails, or a
handler delta replay conflict is detected.

## Gate Question

The milestone gate should answer:

```text
Is RadarPulse ready to treat the accepted durable envelope contract as
adapter-backed, recoverable state for deterministic archive-shaped MVP
workloads, with ordered commit, handler delta replay, retained cleanup, and
operator diagnostics preserved across adapter/session recreation?
```

The expected acceptable answer is likely:

```text
yes with scoped warnings for a deterministic local persistent adapter; no
production broker, true live ingestion, deployment readiness, cross-machine
throughput certification, or exactly-once production delivery is claimed
```

## Expected Evidence

The milestone should capture focused evidence for:

```text
schema compatibility and fail-closed version mismatch
idempotent duplicate accept after restart
pending/claimed/completed/failed/abandoned/poison/canceled/released restart
  recovery
explicit stale claimed attempt abandonment and retry
retry exhaustion to poison
ordered commit after restart with out-of-order completed envelopes
released terminal state after restart
handler delta replay idempotency through adapter-backed execution
operator summary first blocking envelope and first blocking reason
Release build and focused Release adapter suite
```

Full-cache performance certification is not required unless implementation
changes the accepted runtime/archive throughput contour. If a performance row
is added, it should be treated as local adapter overhead evidence, not a
production broker benchmark.
