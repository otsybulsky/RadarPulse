# Milestone 023: Durable/Cross-Process Runtime Readiness Architecture Decision

Status: accepted for implementation.

Milestone 022 accepted ordered rebalance/topology commit for the scoped
in-process runtime/archive queued-overlap path. That result is necessary but
not sufficient for durable or cross-process runtime readiness because process
boundaries change ownership, retry, recovery, and operator visibility.

## Decision

Durable/cross-process runtime readiness should be built as a broker-neutral
envelope contract with a deterministic in-process durable harness.

The first milestone 023 implementation should use this pipeline:

```text
durable accept stage:
  accept owned runtime/archive batches into durable envelopes
  assign stable batch ids and provider sequences
  record accepted/pending retained pressure and startup prewarm attribution
  fail closed on validation or retained ownership failure

worker claim stage:
  claim pending envelopes explicitly
  record worker identity and attempt number
  process claimed envelopes through the accepted ordered runtime/archive
    processing or rebalance path
  allow worker completion to happen out of provider order

ordered commit stage:
  publish and mutate processing/rebalance state only by provider sequence
  hold later completed envelopes behind earlier incomplete or failed
    envelopes
  keep stale topology recompute behavior for ordered rebalance where needed
  mark committed envelopes separately from completed worker attempts

recovery and cleanup stage:
  expose retryable, abandoned, poison, failed, canceled, committed, and
    released states
  release retained resources exactly once from the runtime perspective
  report release failures, terminal retained pressure, and first blocking
    envelope state as readiness evidence
```

The in-process durable harness is the milestone gate adapter. It should obey
the same contract a future broker adapter must obey, but it does not claim
production broker durability.

## Why This Direction

This is preferred over the alternatives:

```text
external broker first:
  too much infrastructure for the current repo gate; broker operations,
  deployment, credentials, retention, and failure injection would dominate the
  milestone before the RadarPulse ownership contract is proven

reuse process-local owned batch queue as durable contract:
  unsafe; object identity, in-memory lifecycle, and queue drain semantics do
  not define restart, retry, abandoned attempt, poison, or operator recovery

worker completion equals commit:
  unsafe; it would violate milestone 021 and 022 provider-sequence ordered
  commit guarantees

silent borrowed fallback after durable failure:
  rejected by prior readiness decisions and would hide queued-owned runtime
  failures behind a different ownership path

exactly-once production claim:
  too broad without a real broker adapter, durable storage contract, and
  idempotent downstream integration evidence
```

The chosen approach proves the RadarPulse-owned contract first. A production
broker adapter can later be judged against the same envelope states and
readiness gates.

## Required Invariants

Implementation must preserve:

```text
provider sequence is the only externally visible commit order
batch id stays stable across retry attempts
attempt number increases on retry
worker completion is not ordered commit
failed or poison earlier sequence blocks later publication
queued-owned failures fail closed
no automatic borrowed fallback is introduced
retained payload ownership and release are explicit
release failures remain readiness blockers
terminal retained pressure returns to zero on successful cleanup
operator-visible state identifies the first blocking envelope
```

## Contract Shape

The durable layer should introduce narrowly scoped contract types around these
concepts:

```text
durable envelope identity:
  batch id, provider sequence, attempt, and optional worker id

durable envelope state:
  pending, claimed, completed, committed, failed, poison, abandoned, canceled,
  released

durable queue operations:
  accept, claim next, complete attempt, fail attempt, abandon attempt,
  mark committed, mark released, summarize state

runtime result:
  accepted counts, commit counts, retry counts, failure counts, release
  failures, terminal retained pressure, oldest uncommitted sequence, first
  blocking envelope
```

The contract should be storage-neutral. The in-process implementation may use
memory collections for tests and gates, but public semantics should avoid
requiring reference equality or thread-affine state.

## Recovery Policy

Milestone 023 should implement explicit recovery policy hooks even if the
default policy is conservative.

Default recovery posture:

```text
pending envelopes are claimable
claimed envelopes remain owned by the attempt until abandoned, failed, or
  completed
abandoned claimed attempts can be retried if policy permits
completed but uncommitted envelopes are ordered commit inputs
failed non-retryable envelopes block publication and readiness
poison envelopes block publication and readiness
canceled envelopes require release cleanup before terminal readiness
```

Retry policy should be deterministic and bounded. Unbounded retry is not a
readiness answer.

## Performance And Gate Constraints

Implementation should preserve the accepted performance posture:

```text
do not add per-event durable records
durable envelope granularity is per accepted RadarEventBatch
avoid copying payload bytes when the in-process harness can retain an owned
  payload reference
keep startup prewarm attribution separate from steady measured allocation
keep active batch capacity separate from provider, worker, and durable queue
  capacities
collect focused Release gate evidence before writing the decision trace
```

The milestone should include correctness gates before any performance gate.
Performance evidence is meaningful only after ordered commit, retry/recovery,
and cleanup semantics pass focused tests.

## Unsupported State

Milestone 023 does not implement:

```text
production broker adapters
true live network ingestion
production deployment, rollback, autoscaling, or runbooks
handler-state delta/merge
cross-machine performance certification
exactly-once production delivery claims
```

## Implementation Direction

The implementation should add the minimum explicit path:

```text
durable envelope/state contract types
deterministic in-process durable queue harness
runtime adapter that composes durable envelopes with the accepted ordered
  runtime/archive processing and rebalance paths
operator-visible summary/result types
focused tests for idempotent accept, out-of-order completion with ordered
  commit, retry, poison/failure blocking, cancellation cleanup, retained
  release failure visibility, and terminal retained pressure
Release gate document and temporary output before decision trace
```

Existing in-process queued-overlap APIs must remain unchanged unless a caller
opts into the durable/cross-process runtime path.
