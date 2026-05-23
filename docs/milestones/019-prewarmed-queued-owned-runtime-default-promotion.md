# Milestone 019: Prewarmed Queued-Owned Runtime Default Promotion Architecture

Status: complete.

RadarPulse milestone 019 starts from the closed milestone 018 runtime/live
ingestion readiness decision.

Milestone 018 closed with this answer, which milestone 019 is explicitly
designed to supersede for the scoped queued-overlap provider path:

```text
explicit opt-in only, queued-owned is runtime-safe when selected explicitly
for scoped in-process runtime/archive replay surfaces with startup prewarm and
existing guardrails, but it is not accepted as the omitted runtime/live
ingestion default
```

The important correction for milestone 019 is that the queued-owned contour is
not being re-proven from zero. Milestones 016 and 017 already accepted the
direct archive benchmark default-equivalent contour with retained payload
prewarm for broader cache-level, file-level, and small-file workloads.
Milestone 018 then proved that the same contour is runtime-safe when selected
explicitly for scoped in-process runtime/archive replay surfaces.

Milestone 019 closes the remaining default-selection integration gap:

```text
make the benchmark-proven startup-prewarmed queued-owned contour the omitted
default for the scoped in-process runtime/archive queued-overlap path, and
gate that the default path actually uses the same contour without hiding
startup prewarm cost or weakening lifecycle guardrails
```

If accepted, this default becomes the baseline rail for remaining
runtime/archive work. Future milestones should integrate this default into
new surfaces and prove only the new surface boundary, not reopen whether
prewarmed queued-owned is the default contour.

This document is intentionally not an implementation plan. It records the
milestone 019 concept, default-promotion boundary, runtime startup prewarm
lifecycle, scoped default surface, fallback posture, gate requirements, and
expected decision before task breakdown is written.

## Milestone Goal

Milestone 019 should promote the accepted benchmark/runtime explicit contour
into the scoped runtime omitted-default path.

The target default contour is:

```text
surface:
  RadarProcessingArchiveQueuedOverlapRunner with omitted options
  scoped in-process runtime/archive replay and rebalance-overlap paths that
    already use ArchiveOwnedRadarEventBatchQueueingPublisher,
    RadarProcessingOwnedBatchQueue, and queued processing/rebalance sessions

effective contour:
  provider mode: queued-owned by construction of the queued-overlap runner
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  provider queue capacity: 8
  retained-byte budget: 536870912
  overlap consumer delay: 0
  startup retained payload prewarm: enabled and explicit
  startup retained payload prewarm sizing:
    event count: 65_536
    payload bytes: 67_108_864
    retained batch count: 1
```

Processing execution remains owned by the supplied processing core or
rebalance session. Milestone 019 promotes the queued-overlap provider,
retention, retained-byte, and prewarm defaults. It does not rewrite an already
constructed `RadarProcessingRebalanceSession` into async shard transport. When
a caller supplies the rollout async core contour, the queued-overlap runtime
default composes with it.

The implementation should make the omitted runtime queued-overlap options use
the rollout queue/retention/prewarm contour. Broader live network ingestion,
durable queues, brokers, cross-process workers, or ordered concurrent
rebalance remain future implementation surfaces that should inherit the
accepted baseline unless their boundary proves a concrete incompatibility.

## Architecture Boundary

Safe in milestone 019:

```text
change the scoped runtime queued-overlap omitted options from generic
  queue/snapshot-copy defaults to the rollout queued-owned runtime default
add explicit startup prewarm lifecycle wiring inside the queued-overlap runner
surface retained payload prewarm result on queued-overlap runtime results
preserve explicit caller-provided options as opt-out/override behavior
preserve BlockingBorrowed benchmark/reference surfaces as explicit oracles
preserve fail-closed queued-owned behavior
preserve processing-completeness requirements
preserve retained cleanup and release health through success, cancellation,
  failure, drain, and dispose paths
add focused tests that omitted runtime options use the promoted contour
add a small runtime gate over deterministic archive-shaped input if local
  corpus is available
```

Not safe in milestone 019 unless explicitly reprioritized:

```text
changing direct benchmark accepted contour
removing explicit BlockingBorrowed oracle/fallback support
adding automatic silent borrowed fallback after queued-owned failure
hiding startup retained payload prewarm cost inside steady measured allocation
claiming true live network ingestion readiness
implementing durable queues or brokers
implementing cross-process provider or worker transport
changing ordered concurrent rebalance semantics
implementing builder-transfer retained payload execution
changing topology publication ordering or source-universe semantics except for
  direct bug fixes needed to preserve existing completeness guardrails
```

## Runtime Default Model

The runtime default path in this milestone is intentionally narrow:

```text
omitted RadarProcessingArchiveQueuedOverlapOptions means:
  use rollout provider queue capacity
  use rollout retained-byte budget
  use pooled-copy retained payload ownership
  create a startup-owned retained payload factory
  prewarm that factory before steady measurement begins
  pass that factory into ArchiveOwnedRadarEventBatchQueueingPublisher
  report the prewarm result separately from steady overlap telemetry
```

Explicit options remain explicit. A caller that constructs
`RadarProcessingArchiveQueuedOverlapOptions` can still choose a smaller queue,
snapshot-copy retention, no prewarm, custom retained payload factory, a
different retained-byte budget, or shutdown behavior for a named diagnostic or
test. The default promotion only changes the omitted runtime path.

## Prewarm Cost Attribution

Startup retained payload prewarm is accepted as a default lifecycle cost only
for the scoped runtime default. It must be visible:

```text
prewarm applied: true on omitted runtime default rows
prewarm event count, payload bytes, retained batch count: reported
prewarm elapsed, allocated bytes, and retained bytes: reported
steady overlap measured allocation: does not include startup prewarm
runtime result: carries both steady overlap telemetry and startup prewarm
  telemetry
```

This preserves the milestone 017/018 interpretation: prewarm is not a hidden
allocation optimization and not a silent first-use side effect.

## Fallback And Failure Posture

The promoted default remains fail-closed:

```text
queued-owned failure faults or cancels the runtime row
no automatic borrowed retry follows queued-owned failure
provider enqueue success is not processing completion
processing validation failures remain readiness blockers
worker failed batches/items remain readiness blockers
release failures remain readiness blockers
terminal retained pressure must return to zero on success, cancellation,
failure, and drain paths
```

BlockingBorrowed remains available where benchmark or explicit reference
surfaces support it. It is not the omitted runtime default for the scoped
queued-overlap path after this milestone.

## Expected Outcome

At the end of milestone 019, RadarPulse should have a clear answer to this
question:

```text
Is the benchmark-proven startup-prewarmed queued-owned contour accepted as the
omitted default for scoped in-process runtime/archive queued-overlap surfaces?
```

Acceptable outcomes:

```text
accepted:
  omitted runtime queued-overlap options use the prewarmed queued-owned
  rollout contour, focused tests and gates pass, startup prewarm is visible,
  and existing guardrails are preserved

accepted with scoped warnings:
  the default is promoted for the named scoped runtime surface, but one or
  more warnings remain assigned to startup cost, local corpus coverage,
  repeatability, or non-live adapter coverage

deferred:
  implementation or gate evidence shows the default path cannot be promoted
  without a larger design change

rejected:
  omitted runtime queued-overlap default promotion breaks correctness,
  cleanup, release, processing completeness, or fail-closed behavior
```

The milestone should stop before decision trace for review of the implemented
default path, test evidence, gate evidence, and any accepted warnings.

## Starting Implementation Surface

Primary runtime integration files:

```text
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapOptions.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapRunner.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapResult.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceRolloutDefaults.cs
src/Infrastructure/Processing/RadarProcessingRetainedPayloadFactory.cs
src/Infrastructure/Processing/RadarProcessingRetainedPayloadPrewarmResult.cs
src/Infrastructure/Archive/ArchiveOwnedRadarEventBatchQueueingPublisher.cs
```

Primary test surfaces:

```text
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Archive/ArchiveOwnedRadarEventBatchQueueingPublisherTests.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedPayloadFactoryTests.cs
```

Primary benchmark/default evidence carried forward:

```text
milestone 017:
  accepted direct MeasureFile()/MeasureCache() prewarmed queued-owned
  default-equivalent contour

milestone 018:
  accepted startup-prewarmed queued-owned as runtime-safe for scoped
  in-process runtime/archive replay surfaces when selected explicitly
```
