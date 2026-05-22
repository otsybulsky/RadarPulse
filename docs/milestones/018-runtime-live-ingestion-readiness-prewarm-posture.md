# Milestone 018: Runtime Prewarm Posture

Status: complete.

This document records the slice 4 runtime prewarm lifecycle decision for
milestone 018. It is a gate posture, not a production default migration.

## Decision

Use startup-owned retained payload prewarm as the explicit queued-owned runtime
gate candidate.

Do not migrate runtime defaults yet. Natural first-use rows remain the control
surface, and explicit borrowed/reference rows remain unprewarmed.

The decision is intentionally gradual:

```text
queued-owned runtime-shaped gates:
  may use startup prewarm as an explicit candidate contour

runtime omitted/default posture:
  unchanged and undecided until the milestone 018 decision trace

natural first-use rows:
  retained as control evidence

BlockingBorrowed/reference rows:
  remain unprewarmed and visibly separate

caller-supplied retained payload factory:
  remains explicit caller behavior, not automatic runtime default behavior
```

## Candidate Contour

The initial runtime gate candidate reuses the milestone 017 direct benchmark
prewarm sizing:

```text
retained payload prewarm:
  enabled for explicit queued-owned runtime gate candidate only

event count:
  65_536

payload bytes:
  67_108_864

retained batch count:
  1

retained-byte budget:
  536_870_912 unless a later contour explicitly changes it
```

This sizing is accepted only as an initial candidate for milestone 018 gate
capture. It does not prove that the same values are the right product runtime
defaults.

## Ownership Model

The runtime gate harness owns the prewarmed retained payload factory.

Expected gate setup:

```text
1. create RadarProcessingRetainedPayloadFactory
2. call Prewarm(eventCount, payloadBytes, retainedBatchCount)
3. record RadarProcessingRetainedPayloadPrewarmResult
4. pass the factory through
   RadarProcessingArchiveQueuedOverlapOptions.RetainedPayloadFactory
5. start runtime-shaped archive intake only after prewarm has completed
```

The factory and prewarm result must be associated with the scenario that used
them. A prewarmed factory must not leak into borrowed/reference rows or natural
first-use control rows.

## Cost Attribution

Prewarm cost is lifecycle cost. It must be recorded separately from
steady-state processing cost.

Required attribution fields for prewarmed rows:

```text
prewarm applied:
  true

prewarm source:
  runtime gate startup candidate

event count:
  recorded

payload bytes:
  recorded

retained batch count:
  recorded

elapsed time:
  recorded

allocated bytes:
  recorded

retained bytes:
  recorded

steady-state measured allocation:
  excludes prewarm allocation
```

Natural first-use rows must explicitly report that startup prewarm did not
apply. Borrowed/reference rows must explicitly report that no retained payload
prewarm applied.

## Failure Policy

Startup prewarm is fail-closed for the explicit candidate row.

If prewarm fails:

```text
intake:
  must not start

scenario status:
  failed before intake

fallback:
  no hidden borrowed/reference retry

operator visibility:
  prewarm failure, attempted sizing, and failure detail must be visible in the
  gate output
```

The gate may still run a separate borrowed/reference row, but that row is
separate evidence and must not overwrite the failed queued-owned candidate row.

## Cleanup And Pressure

Prewarm retains arrays in the retained payload pools, not pending queued
batches. Retained queue pressure therefore remains a terminal-state cleanup
invariant for runtime sessions:

```text
pending retained batches/bytes:
  return to 0 after success, cancellation, fault, drain, stop, and dispose

active retained batches/bytes:
  return to 0 after success, cancellation, fault, drain, stop, and dispose

combined retained batches/bytes:
  return to 0 after success, cancellation, fault, drain, stop, and dispose

prewarm retained bytes:
  recorded as retained payload pool lifecycle cost

release failures:
  remain readiness blockers
```

The runtime gate must not treat prewarmed pool capacity as a reason to ignore
pending, active, combined, or release-health cleanup failures.

## Separation Rules

The following separation rules are required for slice 5 and gate capture:

```text
startup-prewarmed queued-owned candidate:
  uses the harness-owned prewarmed factory

natural first-use queued-owned control:
  uses a non-prewarmed factory

BlockingBorrowed/reference:
  remains unprewarmed

explicit caller-supplied retained payload factory:
  reported as explicit caller behavior, not omitted runtime default behavior

queued-owned failure:
  remains fail-closed and is not replaced by borrowed success
```

## Guardrails Carried To Slice 5

Slice 5 should add or verify focused guardrails for:

```text
no silent fallback after queued-owned prewarm or processing failure
retained cleanup after success, cancellation, fault, drain, stop, and dispose
cancellation with a prewarmed retained payload factory
fault handling with a prewarmed retained payload factory
borrowed/reference rows staying unprewarmed
natural first-use rows staying separate from startup-prewarmed rows
prewarm failure blocking intake for the candidate row
operator-visible prewarm attribution in temporary gate output
```

The existing `ShutdownMode.CancelQueued` gap remains a slice 5 guardrail item:
it must be implemented/tested or explicitly carried as a blocker or coverage
gap before runtime readiness is interpreted.

## Deferred Decisions

The following decisions remain deferred until gate evidence and the milestone
018 decision trace:

```text
whether queued-owned should become an omitted runtime default
whether startup prewarm should become a runtime default lifecycle step
whether startup prewarm should remain explicit opt-in only
whether the initial 65_536 event / 67_108_864 byte / 1 batch sizing is
  acceptable beyond local milestone 018 gates
whether true live ingestion coverage is sufficient or remains a named gap
```

## Outcome

Slice 4 is complete with documentation-only changes.

The milestone can proceed to slice 5 backpressure, failure, cancellation, and
cleanup guardrails using startup prewarm as an explicit queued-owned candidate
and natural first-use plus borrowed/reference rows as separate controls.
