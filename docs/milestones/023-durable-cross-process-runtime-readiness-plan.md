# Milestone 023: Durable/Cross-Process Runtime Readiness Implementation Plan

Status: active.

This plan implements the milestone 023 architecture defined in
`023-durable-cross-process-runtime-readiness.md` and the accepted
implementation direction in
`023-durable-cross-process-runtime-readiness-architecture-decision.md`.

The plan is scoped to the broker-neutral durable/cross-process contract and a
deterministic in-process durable harness. It does not introduce an external
broker dependency and does not claim production deployment readiness.

## Goal

Implement durable/cross-process runtime readiness over the accepted
runtime/archive baseline.

The milestone must prove these concrete outcomes:

```text
there is an explicit durable envelope contract
accepted envelopes have stable batch ids and provider sequences
worker claim, completion, failure, abandon, retry, poison, commit, and release
  states are visible
worker completion order does not become external commit order
ordered processing commit remains by provider sequence
ordered rebalance/topology commit remains by provider sequence
retry policy is explicit and bounded
failed or poison earlier sequences block later publication
retained payload ownership and release are accounted for across the durable
  boundary
terminal retained pressure returns to zero after success, failure,
  cancellation, and cleanup
release failures remain readiness blockers
no automatic borrowed fallback is introduced
operator-visible summary identifies the first blocking envelope and reason
```

Scope note:

```text
milestone 023 proves RadarPulse durable/cross-process contracts before adding
a production broker adapter. The in-process durable harness is a gate adapter,
not a production durability claim.
```

## Slice 1: Durable Envelope Contract And Queue Harness

Status: pending.

Implementation:

```text
add durable envelope identity, state, attempt, and failure/status types
add a deterministic in-process durable queue harness
support accept, claim, complete, fail, abandon, retry, mark committed, mark
  released, and summarize operations
preserve provider-sequence assignment and idempotent accept behavior
keep the contract broker-neutral and avoid process-local object identity in
  public semantics
```

Tests:

```text
accept assigns stable provider sequences and batch ids
duplicate accept by batch id is idempotent
claim transitions pending to claimed with attempt and worker identity
complete/fail/abandon transitions reject invalid state changes
retry preserves batch id and provider sequence while incrementing attempt
summary reports pending, claimed, completed, failed, poison, abandoned, and
  released counts
```

Exit criteria:

```text
durable state transitions are explicit and tested before runtime/archive
processing is composed with them
```

## Slice 2: Durable Ordered Processing Runtime

Status: pending.

Implementation:

```text
add an explicit durable ordered-processing runtime adapter
consume durable envelopes through worker claims
process completed attempts through the accepted ordered processing commit
  model
allow attempts to complete out of provider order while committing only the
  next provider sequence
hold later completed attempts behind earlier pending, claimed, failed, or
  poison envelopes
record committed and released envelope states separately from worker
  completion
```

Tests:

```text
out-of-order worker completion commits in provider sequence
later completed envelope waits behind earlier incomplete envelope
earlier failure blocks later publication and reports first blocking envelope
processing checksum and completion counters match the ordered in-process
  reference for the same batches
retained resources release after ordered commit
```

Exit criteria:

```text
durable processing proves that worker completion and ordered commit are
separate contracts
```

## Slice 3: Retry, Recovery, Cancellation, And Cleanup

Status: pending.

Implementation:

```text
add bounded retry policy for retryable failed or abandoned attempts
add conservative recovery behavior for claimed envelopes
add poison state when retry policy is exhausted or failure is non-retryable
add cancellation cleanup that releases retained resources and stops further
  publication
surface release failures and terminal retained pressure in durable runtime
  results
```

Tests:

```text
retry increments attempt while preserving batch id and provider sequence
retry success can unblock ordered publication
retry exhaustion marks poison and blocks later publication
abandoned claimed attempt can be retried only when policy permits
cancellation leaves retained pressure at zero after cleanup
release failure remains visible and readiness-blocking
no borrowed fallback is used after durable or queued-owned failure
```

Exit criteria:

```text
failure and recovery behavior is deterministic, bounded, and
operator-visible
```

## Slice 4: Durable Ordered Rebalance Runtime

Status: pending.

Implementation:

```text
compose durable envelopes with the accepted RunOrderedRebalanceAsync path or
  queued rebalance ordered-concurrent session
preserve provider-sequence ordered processing, pressure, policy, quarantine,
  decision, validation, and topology mutation
preserve stale topology recompute behavior when earlier ordered commits
  migrate topology
mark durable envelopes committed only after rebalance/topology commit
```

Tests:

```text
durable ordered rebalance commits accepted moves in provider sequence
final topology and accepted move evidence match sequential or in-process
  ordered reference
stale topology recompute remains safe across durable envelope completion
failure blocks later rebalance publication and reports first blocking envelope
retained pressure cleanup remains clean after success and failure
```

Exit criteria:

```text
durable processing and durable rebalance share the same envelope/recovery
contract without weakening milestone 022 topology safety
```

## Slice 5: Operator Summary And Gate Evidence

Status: pending.

Implementation:

```text
add operator-visible durable runtime summary/result fields
add focused benchmark or harness output for durable/cross-process workloads
capture accepted, pending, claimed, completed, committed, failed, poison,
  abandoned, retry, release failure, terminal pressure, oldest uncommitted
  sequence, and first blocking envelope evidence
write milestone 023 gate document after implementation verification
update handoff and project-progress with gate evidence before decision trace
```

Tests:

```text
summary reports first blocking envelope and reason
summary reports oldest uncommitted sequence
release failures and terminal retained pressure are visible
CLI or harness result shape preserves provenance for durable readiness
```

Exit criteria:

```text
the milestone has focused Release evidence and operator-readable state before
decision-trace discussion
```

## Slice 6: Pre-Decision Trace Review Point

Status: pending.

Implementation:

```text
record focused test commands and results
record full Release test result and any known caveats
record durable/cross-process gate output
stop before writing decision trace
```

Exit criteria:

```text
decision trace is not written until the milestone result and warnings are
reviewed
```

## Verification Targets

Expected verification before decision-trace discussion:

```text
Release build succeeds with 0 warnings and 0 errors
focused durable queue contract tests pass
focused durable ordered processing tests pass
focused retry/recovery/cancellation cleanup tests pass
focused durable ordered rebalance tests pass
operator summary/gate shape tests pass
full Release test project is run, with any known allocation-sensitive caveat
  explicitly recorded
durable/cross-process gate document is captured or a concrete blocker is
  recorded
```

## Stop Conditions

Stop for discussion only for a blocker that changes the architecture decision
or creates a material product/runtime policy choice. Examples:

```text
durable envelopes require a real external broker to prove the contract
provider-sequence ordered commit cannot be preserved across durable recovery
retained payload ownership cannot be made explicit across the boundary
retry policy needs unbounded retry to pass correctness gates
rebalance/topology state requires a new snapshot/merge contract before durable
  ordered commit is safe
operator-visible recovery state cannot identify the first blocking envelope
```

Do not stop for routine file creation, localized test failures with a clear
fix, mechanical handoff updates, or per-slice documentation.
