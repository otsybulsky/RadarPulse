# Milestone 019: Prewarmed Queued-Owned Runtime Default Promotion Implementation Plan

Status: complete.

This plan implements the milestone 019 architecture defined in
`019-prewarmed-queued-owned-runtime-default-promotion.md`.

The plan is intentionally narrow. It promotes the already accepted
startup-prewarmed queued-owned contour into the scoped runtime
queued-overlap omitted-default path. It does not re-open durable queues,
brokers, cross-process workers, ordered concurrent rebalance, builder-transfer,
or product-facing surfaces.

## Goal

Promote the benchmark-proven and runtime-explicit startup-prewarmed
queued-owned contour to the omitted default for scoped in-process
runtime/archive queued-overlap surfaces, and establish it as the default
baseline for remaining runtime/archive integration work.

The milestone must prove these concrete outcomes:

```text
omitted RadarProcessingArchiveQueuedOverlapOptions selects rollout queue and
  retained payload defaults
omitted runtime queued-overlap rows use pooled-copy retained ownership
startup retained payload prewarm runs before steady overlap measurement
prewarm result is visible on the runtime overlap result
steady overlap allocation remains separate from startup prewarm allocation
explicit caller-provided options remain explicit overrides
queued-owned failure remains fail-closed with no silent borrowed fallback
retained pressure returns to zero across success, cancellation, failure, and
  drain paths
```

Scope note:

```text
this plan promotes queued-overlap provider/retention/prewarm defaults.
Processing execution mode and async worker sizing remain supplied by the
processing core/rebalance session and are not automatically rewritten by
RadarProcessingArchiveQueuedOverlapOptions.Default.
Future runtime/archive surfaces should inherit this accepted baseline unless
they prove a concrete surface incompatibility.
```

## Slice 1: Runtime Default Contract

Status: complete.

Implementation:

```text
add a runtime prewarm options contract for queued-overlap defaults
change RadarProcessingArchiveQueuedOverlapOptions.Default to the rollout
  runtime default contour
preserve constructor behavior for explicit options unless the caller requests
  prewarm explicitly
add a helper/factory for default runtime queued-overlap options if useful
```

Tests:

```text
omitted/default queued-overlap options use provider queue capacity 8
omitted/default queued-overlap options use retained-byte budget 536870912
omitted/default queued-overlap options use pooled-copy retention
omitted/default queued-overlap options request rollout prewarm sizing
explicit constructed options can still use snapshot-copy/no-prewarm
```

Exit criteria:

```text
the default contract is reviewable without running archive gates
```

## Slice 2: Startup Prewarm Lifecycle

Status: complete.

Implementation:

```text
teach RadarProcessingArchiveQueuedOverlapRunner to create or reuse the retained
  payload factory for startup prewarm
run prewarm before steady overlap allocation capture
pass the prewarmed factory into ArchiveOwnedRadarEventBatchQueueingPublisher
surface RadarProcessingRetainedPayloadPrewarmResult on
  RadarProcessingArchiveQueuedOverlapResult
keep prewarm result None for explicit no-prewarm options
```

Tests:

```text
omitted runner options report retained payload prewarm applied
reported prewarm sizing matches rollout defaults
pooled-copy retention after omitted default run has fewer or no initial pool
  misses for the prewarmed size where the synthetic row fits the pool
explicit no-prewarm options report no retained payload prewarm
```

Exit criteria:

```text
startup prewarm is visible and not folded into steady overlap allocation
```

## Slice 3: Guardrail Regression Pass

Status: complete.

Implementation:

```text
adjust existing queued-overlap tests to distinguish omitted defaults from
  explicit diagnostic options
preserve producer failure cleanup
preserve cancellation cleanup
preserve CancelQueued cleanup
preserve validation failure faulting
preserve ordered rebalance drain
```

Tests:

```text
focused queued-overlap runner test class
focused archive owned queueing publisher test class if impacted
focused retained payload factory prewarm tests if impacted
```

Exit criteria:

```text
existing milestone 018 runtime guardrails still pass under explicit options,
and the omitted default path has its own prewarm/default assertions
```

## Slice 4: Runtime Default Gate

Status: complete.

Implementation:

```text
capture a small deterministic runtime/archive default gate using omitted
  queued-overlap options
use local archive input if available; otherwise use a deterministic synthetic
  archive-shaped row
compare with an explicit no-prewarm/snapshot-copy row only as diagnostic, not
  as a new benchmark proof
record output in a milestone 019 gate document
```

Gate requirements:

```text
omitted default path reports prewarm applied
default path uses pooled-copy retention
provider queue capacity and retained-byte budget match rollout defaults
result status completed
processing completeness passes
worker failed batches/items are zero when applicable
processing validation failed batches are zero
release failures are zero
terminal pending/active/combined retained pressure returns to zero
no automatic borrowed fallback occurs
```

Exit criteria:

```text
gate evidence is enough to review default promotion before decision trace
```

## Slice 5: Documentation Checkpoint Before Decision Trace

Status: complete.

Implementation:

```text
write milestone 019 runtime default gate evidence
update the implementation plan statuses
prepare a short review summary with implemented behavior, tests, gate results,
accepted warnings, and remaining risks
stop before writing decision trace
```

Exit criteria:

```text
decision-trace inputs are ready, but no decision trace is written until review
```

## Verification Plan

Focused verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests"
```

Build verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
```

Broader verification if focused tests pass:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Known caveat carried from milestone 018:

```text
one synthetic benchmark allocation-threshold test may be sensitive in full
suite execution but has passed in isolated rerun
```

## Stop Conditions

Stop and discuss before decision trace.

Stop earlier only if:

```text
the default promotion requires changing benchmark accepted contour
prewarm cannot be surfaced without hiding cost or changing result contracts
omitted default promotion breaks fail-closed behavior
cleanup/release pressure no longer returns to zero
processing completeness is no longer gateable
local tests show a real correctness regression
```
