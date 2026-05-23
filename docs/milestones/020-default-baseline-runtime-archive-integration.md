# Milestone 020: Default-Baseline Runtime/Archive Integration Architecture

Status: decision trace written; awaiting closeout.

RadarPulse milestone 020 starts from the closed milestone 019 runtime default
baseline promotion.

Milestone 019 closed with this answer:

```text
accepted with scoped warnings, startup-prewarmed queued-owned is accepted as
the omitted default for the scoped in-process runtime/archive queued-overlap
provider path and as the default baseline for remaining runtime/archive work
```

Milestone 020 should not re-prove whether queued-owned is the runtime/archive
default contour. That decision is closed for the scoped baseline. This
milestone integrates the accepted contour into the remaining in-process
runtime/archive construction surfaces and proves only those integration
boundaries.

The important shift is:

```text
from:
  the queued-overlap provider path has an accepted omitted default baseline

to:
  runtime/archive construction surfaces that own their processing core or
  session can compose with that accepted baseline by default, with provenance,
  startup prewarm cost, pressure, cancellation, failure, release, and cleanup
  still visible
```

This document is intentionally not an implementation plan. It records the
milestone 020 concept, architecture boundary, accepted baseline contract,
integration surfaces, reporting posture, fallback policy, gate expectations,
and expected decision before task breakdown is written.

## Milestone Goal

Milestone 020 should integrate the accepted prewarmed queued-owned
runtime/archive baseline into the runtime/archive surfaces that remain on the
project path before durable or cross-process runtime work.

The accepted provider baseline is:

```text
provider path: queued-owned by construction of the queued-overlap runner
provider overlap: producer-consumer
retention strategy: pooled-copy
provider queue capacity: 8
retained-byte budget: 536870912
startup retained payload prewarm: enabled
prewarm event count: 65_536
prewarm payload bytes: 67_108_864
prewarm retained batch count: 1
```

Milestone 020 adds the processing-core construction side only where the
runtime/archive surface owns core/session construction. The accepted rollout
processing contour is:

```text
execution: async shard transport
worker count: 4
worker queue capacity: 8
```

The default integration target is therefore:

```text
runtime/archive integration surface:
  uses the accepted queued-overlap provider default
  uses startup retained payload prewarm when options are omitted
  creates async shard transport processing core/session defaults only when
    the surface owns that construction
  reports provider and execution provenance separately
  reports startup prewarm outside steady processing allocation
  preserves explicit diagnostic and reference options
```

## Architecture Boundary

Safe in milestone 020:

```text
add a named runtime/archive baseline factory or options profile that composes
  the accepted provider default with the accepted async processing contour
wire that profile into in-process runtime/archive surfaces that own
  processing core or session construction
add provenance/reporting that distinguishes provider defaulting from
  execution defaulting
add integration tests proving omitted runtime/archive construction selects
  the accepted provider and execution baseline
add live-adapter-shaped evidence over deterministic archive input if it can
  be implemented without durable transport
keep startup prewarm cost visible and separate from steady measured
  allocation
keep pressure, cancellation, failure, release, cleanup, and processing
  completeness gateable
```

Not safe in milestone 020 unless explicitly reprioritized:

```text
reopening the queued-owned provider default decision
changing the direct archive benchmark accepted contour
removing explicit BlockingBorrowed oracle or fallback support
adding automatic silent borrowed fallback after queued-owned failure
hiding startup retained payload prewarm cost inside steady measured allocation
claiming durable queue, broker, or cross-process readiness
implementing cross-process provider or worker transport
implementing ordered concurrent rebalance
changing topology publication ordering beyond direct integration bug fixes
turning temporary gate output into a production operator contract
```

Out of scope for this milestone:

```text
durable queues or brokers
cross-process providers/workers
ordered concurrent rebalance
builder-transfer retained payload execution
production deployment, rollback, and operator surfaces
product-facing radar workflows
automatic silent borrowed fallback
```

## Runtime/Archive Baseline Contract

The accepted runtime/archive baseline has two independent halves.

Provider half:

```text
RadarProcessingArchiveQueuedOverlapOptions.Default
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingOwnedBatchQueue
RadarProcessingRetainedPayloadFactory
startup retained payload prewarm result reported separately
```

Execution half:

```text
RadarProcessingCoreOptions uses AsyncShardTransport
RadarProcessingAsyncExecutionOptions uses worker count 4 and queue capacity 8
rebalance/session construction uses that core only where the integrating
  surface owns core construction
```

Milestone 020 should keep this split visible. Provider defaulting and
execution defaulting may be adopted by the same runtime/archive surface, but
they are still separate decisions in reporting and tests. This preserves the
milestone 019 warning that queued-overlap defaults do not rewrite an already
constructed processing core.

## Integration Surface Model

The milestone should prefer a small explicit integration layer over scattering
magic defaults through unrelated constructors.

The expected model is:

```text
runtime/archive integration options or factory:
  exposes an omitted/default profile for the accepted baseline
  can construct processing core options for runtime/archive integration
  can construct queued-overlap provider options for runtime/archive
    integration
  keeps explicit diagnostic/no-prewarm/reference options possible
  exposes provenance so tests and CLI/harness output can identify the source
```

Callers that already construct their own processing core keep owning that
choice. They may opt into the baseline profile, but milestone 020 should not
silently replace an existing supplied core or session.

## Live Adapter Evidence

Milestone 020 may add a live-adapter-shaped in-process surface if it can stay
within the current architecture:

```text
input:
  deterministic archive replay or synthetic archive-shaped batches

shape:
  runtime/live adapter contract that feeds the accepted runtime/archive
  baseline through the same lifecycle signals a live input adapter would use

evidence:
  startup prewarm visible
  provider queue pressure visible
  processing completion visible
  cancellation/failure cleanup visible
  no silent borrowed fallback
```

This evidence is integration evidence only. It does not claim true live
network ingestion, durable transport, broker semantics, cross-process worker
semantics, or production operator readiness.

## Reporting And Provenance

The integration surface should make the default posture reviewable:

```text
provider baseline:
  queued-owned, producer-consumer overlap, pooled-copy, queue capacity 8,
  retained-byte budget 536870912, startup prewarm enabled

execution baseline:
  async shard transport, worker count 4, worker queue capacity 8

cost attribution:
  startup prewarm elapsed, allocation, retained bytes, and retained batches
  remain separate from steady row allocation

health:
  processing completion, worker failures, validation failures, release
  failures, terminal retained pressure, cancellation, and cleanup remain
  observable
```

The milestone should not hide a failure by falling back to BlockingBorrowed.
BlockingBorrowed remains useful as an explicit benchmark/reference/oracle
where a surface already supports it.

## Expected Gate Shape

The milestone should collect focused evidence before decision trace:

```text
contract tests:
  default runtime/archive integration profile composes accepted provider and
  execution defaults

integration tests:
  omitted runtime/archive construction uses queued-owned pooled-copy startup
  prewarm and async shard transport worker sizing

lifecycle tests:
  cancellation, failure, release, cleanup, and terminal retained pressure
  remain healthy through the integrated default path

CLI or temporary harness evidence if applicable:
  output reports provider and execution provenance and separates startup
  prewarm from steady allocation
```

The full suite may continue to carry the known allocation-sensitive synthetic
benchmark caveat from milestones 018 and 019 unless a change touches that
surface directly.

## Expected Outcome

At the end of milestone 020, RadarPulse should have a clear answer to this
question:

```text
Are the remaining scoped in-process runtime/archive integration surfaces
ready to consume the accepted prewarmed queued-owned plus async execution
default baseline without reopening the provider default decision?
```

Acceptable outcomes:

```text
accepted:
  default runtime/archive integration profile is wired, focused gates pass,
  provenance is visible, startup prewarm cost remains separate, and lifecycle
  guardrails are preserved

accepted with scoped warnings:
  integration is accepted for named in-process runtime/archive surfaces, with
  warnings assigned to live-adapter coverage, local corpus breadth,
  repeatability, or remaining production reporting work

deferred:
  a construction-surface incompatibility requires a larger design change
  before default-baseline integration can be accepted

rejected:
  integrating the baseline breaks correctness, cleanup, release,
  cancellation, processing completeness, or fail-closed behavior
```

The milestone should stop before decision trace for review of implemented
surfaces, tests, gate evidence, and any remaining warnings.

## Starting Implementation Surface

Primary runtime/archive integration files:

```text
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapOptions.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapRunner.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapResult.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceRolloutDefaults.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Infrastructure/Processing/RadarProcessingQueuedProcessingSession.cs
src/Infrastructure/Processing/RadarProcessingQueuedRebalanceSession.cs
src/Domain/Processing/RadarProcessingCoreOptions.cs
src/Domain/Processing/RadarProcessingAsyncExecutionOptions.cs
src/Presentation/Program.cs
```

Primary test surfaces:

```text
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProcessingSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedRebalanceSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingSyntheticRebalanceBenchmarkTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Primary evidence carried forward:

```text
milestone 017:
  accepted direct MeasureFile()/MeasureCache() prewarmed queued-owned
  default-equivalent contour

milestone 018:
  accepted startup-prewarmed queued-owned as runtime-safe for scoped
  in-process runtime/archive replay surfaces when selected explicitly

milestone 019:
  accepted startup-prewarmed queued-owned as the omitted default for the
  scoped in-process runtime/archive queued-overlap provider path and as the
  baseline for remaining runtime/archive work
```
