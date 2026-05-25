# Milestone 023: Durable/Cross-Process Runtime Readiness Architecture

Status: complete.

The milestone closeout is recorded in
`023-durable-cross-process-runtime-readiness-closeout.md`.

RadarPulse milestone 023 starts from the closed milestone 022 ordered
rebalance/topology commit foundation.

Milestone 022 closed with this answer:

```text
accepted with scoped warnings, the scoped in-process runtime/archive
rebalance path is ready to keep multiple accepted batches active for
handler-free processing-delta compute while committing processing,
rebalance decisions, validation, and topology mutation deterministically in
provider sequence
```

Milestone 023 should not reopen the milestone 020 provider/execution default
decision, the milestone 021 processing-delta decision, or the milestone 022
ordered rebalance/topology decision. It designs the first durable and
cross-process runtime boundary on top of those accepted in-process contracts.

The important shift is:

```text
from:
  producer, retained payload ownership, active batch processing, ordered
  commit, release, cleanup, and telemetry are all coordinated inside one
  process

to:
  accepted batches can cross a durable or process boundary as explicit
  envelopes, workers can claim and complete those envelopes, and ordered
  commit/recovery semantics remain deterministic and operator-visible
```

This document records the milestone 023 concept, architecture boundary,
durable queue model, cross-process ownership model, recovery invariants,
operator-visible readiness posture, and expected gate question. It is
intentionally not the detailed implementation plan.

## Milestone Goal

Milestone 023 should implement a broker-neutral durable/cross-process runtime
readiness layer for the scoped runtime/archive path.

The inherited runtime/archive contour remains:

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
ordered active batch capacity: 4
ordered processing commit: accepted
ordered rebalance/topology commit: accepted
```

The milestone 023 readiness target is:

```text
durable/cross-process runtime surface:
  accepts owned runtime/archive batches into a durable envelope stream
  gives each accepted envelope a stable provider sequence and batch id
  exposes explicit worker claim, completion, failure, retry, and abandon
    outcomes
  preserves ordered processing and ordered rebalance/topology commit
  preserves retained payload ownership and release accounting across the
    boundary
  preserves fail-closed queued-owned behavior and no silent borrowed fallback
  reports operator-visible recovery state instead of hiding it in benchmark
    counters
```

## Architecture Boundary

Safe in milestone 023:

```text
add explicit durable runtime contracts for accepted envelopes, queue state,
  worker claims, completion, failure, retry, and cleanup
add a deterministic in-process durable harness that exercises the same
  contracts without requiring an external broker service
add broker-neutral result and telemetry types that can later be backed by a
  production broker adapter
compose durable envelope consumption with the accepted ordered processing and
  ordered rebalance runtime/archive paths
record recovery semantics for pending, claimed, completed, failed, canceled,
  and released envelopes
add focused tests for idempotent accept, ordered commit after out-of-order
  worker completion, retry/fail-closed behavior, cancellation cleanup,
  release failure visibility, and retained pressure returning to zero
add Release gates over deterministic durable/cross-process workloads
```

Not safe in milestone 023 unless explicitly reprioritized:

```text
adding a mandatory external broker dependency to the test suite
claiming production deployment readiness from an in-process durable harness
claiming exactly-once broker semantics without a real broker adapter and
  durable storage guarantee
allowing worker completion order to become externally visible commit order
silently retrying through a borrowed provider path after queued-owned failure
hiding retained payload prewarm or durable recovery allocation in steady-state
  readiness claims
weakening milestone 021 or milestone 022 ordered commit invariants
```

Out of scope for this milestone:

```text
Kafka, RabbitMQ, cloud queue, or database-backed adapter implementation
production deployment, rollback, autoscaling, and operator runbooks
true live network ingestion
product-facing radar workflows
full handler-state delta/merge support
cross-machine performance certification
exactly-once end-to-end production claim
```

## Durable Envelope Model

Milestone 023 introduces a durable envelope boundary rather than passing
process-local queue items as the durable contract.

The envelope should represent:

```text
batch id:
  stable identity for idempotent accept, retry, recovery, and diagnostics

provider sequence:
  deterministic input order for ordered processing and rebalance commit

payload ownership reference:
  explicit owned payload handle or serialized payload reference; ownership
  must be released exactly once from the runtime perspective

attempt:
  monotonic processing attempt count for retry and poison/failure policy

state:
  pending, claimed, completed, failed, canceled, released, or abandoned

telemetry:
  accepted time, claimed time, completed time, release status, retry count,
  failure reason, retained pressure, and worker identity where available
```

The first implementation may keep the durable harness in process, but the
contract must not require process-local object identity for correctness.
Process-local references are allowed only as a test adapter detail.

## Cross-Process Ownership Model

The durable boundary must make ownership explicit.

Required posture:

```text
producer owns input until durable accept succeeds
durable queue owns accepted envelope state until worker claim succeeds
worker claim owns processing attempt, not global commit order
ordered commit owns externally visible processing and rebalance mutation
release owns retained resource cleanup and readiness-blocking release failures
operator-visible recovery owns stuck pending/claimed/completed-but-unreleased
  diagnostics
```

This prevents three unsafe shortcuts:

```text
assuming an accepted in-memory object is equivalent to a durable record
assuming worker completion is equivalent to ordered commit
assuming cleanup success when retained resource release failed or was skipped
```

## Ordered Commit And Recovery

The milestone keeps the accepted ordered commit architecture:

```text
processing commit happens by provider sequence
rebalance/topology commit happens by provider sequence
later completed worker results wait behind earlier incomplete or failed
  sequences
earlier failure boundaries stop later externally visible publication
topology stale-delta recompute remains available where ordered rebalance uses
  active compute
```

Durable recovery adds a new state question:

```text
when the runtime restarts or a worker disappears, which envelope states can be
replayed, retried, abandoned, or marked poison without violating ordered
commit?
```

The safe answer for milestone 023 is:

```text
pending envelopes can be claimed
claimed envelopes can be recovered only after an explicit recovery policy
  marks the previous attempt abandoned or retryable
completed but uncommitted envelopes remain ordered-commit inputs
failed envelopes stop ordered publication unless policy explicitly marks them
  retryable and the retry later completes successfully
released envelopes are terminal from retained-pressure accounting
release failures remain readiness blockers
```

## Retry And Poison Policy

Milestone 023 should define retry as an explicit policy decision, not as an
implicit fallback.

Default readiness posture:

```text
queued-owned validation failure fails closed
processing or rebalance failure fails the ordered drain unless a configured
  retry policy owns a new attempt
retry increments attempt and preserves batch id and provider sequence
retry does not change ordered publication order
max attempts or non-retryable failure marks the envelope poison/failed
poison/failed envelopes are operator-visible and readiness-blocking
borrowed fallback is never automatic
```

## Operator-Visible Runtime State

The durable/cross-process layer must report more than throughput.

Required operator-visible fields:

```text
accepted envelopes
pending envelopes
claimed envelopes
completed envelopes
committed envelopes
failed envelopes
poison envelopes
abandoned attempts
retry attempts
release failures
terminal retained pressure
oldest uncommitted provider sequence
first blocking envelope id and reason
```

These fields are readiness evidence. They are not yet a production operator
dashboard or runbook.

## Gate Question

The expected milestone 023 gate question is:

```text
Is the scoped runtime/archive path ready to move accepted queued-owned batches
through a durable/cross-process envelope boundary while preserving ordered
processing commit, ordered rebalance/topology commit, fail-closed behavior,
retained resource cleanup, retry/recovery visibility, and operator-readable
blocking state?
```

Expected decision posture if the milestone succeeds:

```text
accepted with scoped warnings for durable/cross-process runtime readiness over
the broker-neutral contract and deterministic in-process durable harness
```

Expected warnings to carry:

```text
production broker adapters are not implemented
true live network ingestion is still not implemented
production deployment, autoscaling, rollback, and runbooks are still not
  implemented
handler-state delta/merge is still not implemented
exactly-once production delivery is not claimed
broader default promotion still depends on workload breadth, recovery
variance, and real broker adapter evidence
```
