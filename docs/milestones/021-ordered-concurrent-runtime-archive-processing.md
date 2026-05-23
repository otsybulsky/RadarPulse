# Milestone 021: Ordered Concurrent Runtime/Archive Processing Architecture

Status: planned.

RadarPulse milestone 021 starts from the closed milestone 020 default-baseline
runtime/archive integration.

Milestone 020 closed with this answer:

```text
accepted with scoped warnings, the scoped in-process runtime/archive
integration boundary is ready to consume the accepted prewarmed queued-owned
plus async execution default baseline without reopening the provider default
decision
```

Milestone 021 should not re-prove whether queued-owned plus async shard
transport is the runtime/archive baseline. That decision is closed for the
scoped in-process runtime/archive path. This milestone adds ordered concurrent
multi-batch processing over that baseline and proves the new concurrency
boundary.

The important shift is:

```text
from:
  the queued-overlap runtime/archive consumer dequeues and processes one
  accepted batch to completion before dequeuing the next

to:
  the runtime/archive consumer can keep multiple accepted batches active at
  once, process them concurrently, and publish externally visible results in
  deterministic input order
```

This document is intentionally not an implementation plan. It records the
milestone 021 concept, architecture boundary, accepted baseline contract,
ordered concurrency model, safety invariants, fallback policy, gate
expectations, and expected decision before task breakdown is written.

## Milestone Goal

Milestone 021 should implement ordered concurrent runtime/archive processing
for the scoped in-process queued-overlap path.

The accepted runtime/archive default baseline inherited from milestone 020 is:

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
```

The milestone 021 concurrency target is:

```text
runtime/archive ordered concurrent surface:
  accepts provider batches in input order
  bounds the number of active processing batches
  processes active batches concurrently over the accepted baseline
  publishes batch results in deterministic input order
  preserves processing completeness and checksum parity
  preserves topology and rebalance safety
  drains, cancels, releases, and cleans retained pressure deterministically
```

## Architecture Boundary

Safe in milestone 021:

```text
add an explicit ordered concurrent runtime/archive consumer or session layer
  for the in-process queued-overlap path
add options that bound active concurrent batches separately from provider
  queue capacity and worker mailbox capacity
add ordered result buffering and drain semantics
add tests that prove completion can happen out of order while publication
  remains in input order
add pressure accounting for provider queue plus active retained batches
add failure, cancellation, cleanup, and release tests with concurrent active
  work
record temporary gate output sufficient for milestone review
```

Not safe in milestone 021 unless explicitly reprioritized:

```text
reopening the queued-owned provider default decision
changing the milestone 020 accepted async execution default baseline
adding automatic silent borrowed fallback after queued-owned failure
hiding startup retained payload prewarm cost inside steady allocation
claiming durable queue, broker, cross-process, or production runtime readiness
implementing cross-process provider or worker transport
making unordered publication externally visible
committing rebalance/topology changes from later batches before earlier
  ordered publication is safe
turning temporary gate output into a production operator contract
```

Out of scope for this milestone:

```text
durable queues or brokers
cross-process providers/workers
production deployment, rollback, and operator surfaces
product-facing radar workflows
automatic silent borrowed fallback
full durable retry/replay semantics
```

## Ordered Concurrency Model

Milestone 021 should separate three capacities that were previously easy to
conflate:

```text
provider queue capacity:
  bounds accepted but not yet consumed provider batches

active ordered batch capacity:
  bounds consumed batches currently processing or waiting for ordered
  publication

worker queue capacity:
  bounds shard work items inside async shard transport
```

The ordered concurrent runtime/archive consumer owns an input-order sequence
for accepted batches. Each consumed batch receives a monotonic sequence number.
Processing may complete in any order, but publication and externally visible
aggregate completion advance only from the lowest unpublished sequence.

The expected high-level state machine is:

```text
accepted:
  provider accepted the batch and retained payload ownership is established

active:
  consumer dequeued the batch and processing is running or complete but not
  yet publishable

publishable:
  processing completed successfully and all lower sequence numbers have been
  published

published:
  externally visible result has been recorded and retained resources for the
  batch have been released

faulted/canceled:
  processing cannot complete successfully; later active work is canceled or
  drained according to the fail-closed policy, and retained resources are
  released
```

## Deterministic Ordering Contract

The milestone should preserve deterministic behavior at the runtime/archive
boundary:

```text
input order is the order in which the provider accepts batches
processing completion order may differ from input order
published result order must match input order for all successful batches
processing completeness is evaluated against accepted/published counts
checksum and counter parity must match the sequential baseline for the same
input
```

Ordering is a runtime/archive publication contract, not permission to reorder
the archive input parser, direct benchmark rows, or lower-level shard worker
execution.

## Topology And Rebalance Safety

Milestone 021 must preserve topology and rebalance safety while batches
overlap.

The safe default posture is:

```text
do not let later-batch topology or rebalance publication become externally
visible before earlier ordered publication permits it
do not silently merge or discard accepted-move evidence from overlapping
  batches
keep rebalance/session ownership explicit
keep caller-supplied processing cores and rebalance sessions explicit
preserve validation failures as visible failures, not fallback successes
```

If implementation discovers that concurrent rebalance commit semantics require
a larger design, the milestone should stop and record that as a blocker rather
than hide ordering risk behind buffering.

## Failure, Cancellation, Drain, And Release

The ordered concurrent path must remain fail-closed.

Required invariants:

```text
queued-owned failure does not trigger automatic borrowed fallback
validation failure remains visible as failed processing
cancellation stops accepting or publishing additional work according to the
  ordered drain policy
retained release failures remain readiness blockers
terminal combined retained pressure returns to zero after success, failure,
  cancellation, and cleanup
startup retained payload prewarm remains visible as lifecycle cost
```

Later active batches may have already started when an earlier batch fails.
The architecture must define whether they are canceled, drained without
publication, or allowed to finish internally before cleanup. In all cases,
externally visible publication must remain deterministic and retained pressure
must be released.

## Reporting And Provenance

The ordered concurrent surface should make the new posture reviewable:

```text
provider baseline:
  queued-owned, producer-consumer overlap, pooled-copy, queue capacity 8,
  retained-byte budget 536870912, startup prewarm enabled

execution baseline:
  async shard transport, worker count 4, worker queue capacity 8

ordered concurrency:
  active batch capacity, accepted count, processed count, published count,
  failed/canceled count, terminal retained pressure

cost attribution:
  startup prewarm remains outside steady processing allocation
  ordered result buffering and active batch pressure remain visible in tests
  or temporary gate output
```

The milestone should not require a production result-contract change unless
the implementation exposes an attribution gap that cannot be tested or audited
otherwise.

## Expected Gate Shape

The milestone should collect focused evidence before decision trace:

```text
contract tests:
  ordered concurrent options expose active batch capacity independently from
  provider and worker queue capacity

ordering tests:
  multiple accepted batches can complete out of order while published result
  order remains deterministic

baseline tests:
  omitted ordered concurrent runtime/archive construction still composes the
  milestone 020 provider and execution defaults

lifecycle tests:
  success, validation failure, cancellation, drain, release, cleanup, and
  terminal retained pressure remain healthy with concurrent active work

parity tests:
  ordered concurrent processing matches sequential/runtime baseline counts
  and checksums for deterministic archive-shaped input

performance evidence:
  Release matrix compares ordered concurrent path against the milestone 020
  accepted baseline
```

The full suite may continue to carry the known allocation-sensitive synthetic
benchmark caveat unless a change touches that surface directly.

## Expected Outcome

At the end of milestone 021, RadarPulse should have a clear answer to this
question:

```text
Is the scoped in-process runtime/archive path ready to process multiple
accepted batches concurrently while preserving deterministic ordered
publication, baseline provenance, topology safety, fail-closed behavior, and
retained-pressure cleanup?
```

Acceptable outcomes:

```text
accepted:
  ordered concurrent runtime/archive processing is implemented for the scoped
  in-process path, focused gates pass, ordering is deterministic, and cleanup
  invariants are preserved

accepted with scoped warnings:
  ordered concurrency is accepted for named in-process runtime/archive
  surfaces, with warnings assigned to local corpus breadth, repeatability,
  performance variance, reporting detail, or deferred durable/cross-process
  semantics

deferred:
  topology or rebalance commit ordering requires a larger design before
  concurrent active batches can be accepted

rejected:
  ordered concurrency breaks correctness, deterministic publication,
  retained-pressure cleanup, release health, processing completeness, or
  fail-closed behavior
```

The milestone should stop before decision trace for review of implemented
surfaces, tests, gate evidence, and any remaining warnings.

## Starting Implementation Surface

Primary runtime/archive files:

```text
src/Infrastructure/Processing/RadarProcessingRuntimeArchiveBaseline.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapOptions.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapRunner.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapResult.cs
src/Infrastructure/Processing/RadarProcessingQueuedProcessingSession.cs
src/Infrastructure/Processing/RadarProcessingQueuedRebalanceSession.cs
src/Domain/Processing/RadarProcessingCoreOptions.cs
src/Domain/Processing/RadarProcessingAsyncExecutionOptions.cs
```

Primary test surfaces:

```text
tests/RadarPulse.Tests/Processing/RadarProcessingRuntimeArchiveBaselineTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProcessingSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedRebalanceSessionTests.cs
```

Primary evidence carried forward:

```text
milestone 018:
  scoped live-adapter-shaped runtime evidence over deterministic archive input

milestone 019:
  startup-prewarmed queued-owned accepted as omitted default for the scoped
  runtime/archive queued-overlap provider path

milestone 020:
  RadarProcessingRuntimeArchiveBaseline accepted as the named construction
  profile composing queued-owned provider defaults with async shard transport
  execution defaults
```
