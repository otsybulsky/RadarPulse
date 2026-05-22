# Milestone 018: Runtime Surface Inventory And Lifecycle Audit

Status: captured for slice 1.

This document records milestone 018 slice 1. It audits the existing
runtime-shaped archive/provider/queue/session surfaces before any runtime
default, prewarm, pressure, or gate decision is made.

Runtime behavior changes in this slice:

```text
none
```

## Audited Surfaces

Primary archive/provider surfaces:

```text
src/Application/Archive/IArchiveRadarEventBatchPublisher.cs
src/Infrastructure/Archive/NexradArchiveRadarEventBatchPublisher.cs
src/Infrastructure/Archive/ArchiveOwnedRadarEventBatchQueueingPublisher.cs
```

Primary queued runtime surfaces:

```text
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapRunner.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapOptions.cs
src/Infrastructure/Processing/RadarProcessingOwnedBatchQueue.cs
src/Infrastructure/Processing/RadarProcessingQueuedProcessingSession.cs
src/Infrastructure/Processing/RadarProcessingQueuedRebalanceSession.cs
```

Retained payload and pressure surfaces:

```text
src/Infrastructure/Processing/RadarProcessingRetainedPayloadFactory.cs
src/Infrastructure/Processing/RadarProcessingRetainedPayloadPrewarmResult.cs
src/Domain/Processing/RadarProcessingRetainedResourcePressureRecorder.cs
src/Domain/Processing/RadarProcessingRetainedResourcePressureSummary.cs
src/Domain/Processing/RadarProcessingRetainedPayloadTelemetrySummary.cs
```

Queue, worker, and result contract surfaces:

```text
src/Domain/Processing/RadarProcessingProviderQueueOptions.cs
src/Domain/Processing/RadarProcessingProviderQueueTelemetrySummary.cs
src/Domain/Processing/RadarProcessingQueuedSessionResult.cs
src/Domain/Processing/RadarProcessingQueuedBatchEnqueueResult.cs
src/Domain/Processing/RadarProcessingQueuedBatchProcessingResult.cs
src/Domain/Processing/RadarProcessingWorkerTelemetrySummary.cs
src/Domain/Processing/RadarProcessingWorkerGroupStatus.cs
src/Domain/Processing/RadarProcessingAsyncFailureKind.cs
src/Domain/Processing/RadarProcessingAsyncCancellationKind.cs
```

Direct benchmark/result surfaces used as baseline context:

```text
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmarkResult.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceCacheBenchmarkResult.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceRolloutDefaults.cs
src/Presentation/Program.cs
```

Focused test areas reviewed:

```text
tests/RadarPulse.Tests/Archive/ArchiveOwnedRadarEventBatchQueueingPublisherTests.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingOwnedBatchQueueTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProcessingSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedRebalanceSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedPayloadFactoryTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedBatchResourceTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingProviderQueueContractTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingProviderQueueTelemetryRecorderTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingWorkerLifecycleContractTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingWorkerTelemetryContractTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

## Surface Classification

### Direct Benchmark Surface

Status:

```text
implemented and accepted by milestone 017
```

Concrete surfaces:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
CLI processing benchmark rebalance-archive paths that call those APIs
```

Current posture:

```text
omitted direct controls resolve to queued-owned rollout default plus retained
payload prewarm for the direct benchmark default-equivalent contour

explicit BlockingBorrowed remains selectable and unprewarmed

result contracts expose:
  RetainedPayloadPrewarm
  HasRetainedPayloadPrewarm
  RetainedPayloadPrewarmAllocatedBytes
  RetainedPayloadPrewarmRetainedBytes
  ProcessingSucceeded
  ProcessingValidationFailedBatchCount
  WorkerFailedBatchCount
  WorkerFailedWorkItemCount
```

Milestone 018 interpretation:

```text
baseline evidence only; not runtime approval
```

### Runtime Archive Provider Surface

Status:

```text
partially implemented as in-process archive provider decoupling
```

Concrete surfaces:

```text
IArchiveRadarEventBatchPublisher
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingArchiveQueuedOverlapRunner
RadarProcessingOwnedBatchQueue
RadarProcessingQueuedProcessingSession
RadarProcessingQueuedRebalanceSession
```

Current behavior:

```text
IArchiveRadarEventBatchPublisher.Publish() is synchronous and documents that
leased batches are valid only during the call

ArchiveOwnedRadarEventBatchQueueingPublisher retains incoming batches before
enqueue and rejects retention/enqueue failures visibly

RadarProcessingOwnedBatchQueue accepts only owned RadarEventBatch values

RadarProcessingArchiveQueuedOverlapRunner starts producer and consumer tasks,
closes/faults the queue on producer outcome, drains the consumer, releases
pending retained resources, and returns producer/consumer/queue/overlap
telemetry

RadarProcessingQueuedProcessingSession and RadarProcessingQueuedRebalanceSession
drain owned queued batches in sequence order and fault after validation,
processing, or migration failures
```

Milestone 018 interpretation:

```text
this is the strongest existing runtime-shaped in-process surface and should be
the first gate target
```

### Live Ingestion Surface

Status:

```text
not implemented as a distinct live radar feed/runtime adapter in the audited
surfaces
```

Current substitute:

```text
deterministic archive replay can stand in for live-shaped input only if the
gate exercises runtime session lifecycle rather than direct benchmark rows
```

Milestone 018 interpretation:

```text
true live ingestion remains a coverage gap unless a live adapter is introduced
or explicitly brought into scope
```

### Durable And Cross-Process Surfaces

Status:

```text
not implemented and out of scope for milestone 018
```

Excluded surfaces:

```text
durable queues
brokers
cross-process providers/workers
distributed workers
ordered concurrent rebalance
```

## Lifecycle State Audit

Current lifecycle coverage:

```text
configure:
  queue options validate capacity, full mode, enqueue timeout, shutdown mode,
  recent detail capacity, and retained-byte budget

start:
  overlap runner creates queue and queueing publisher, then starts producer
  and consumer tasks

prewarm:
  retained payload factory has Prewarm(), but only direct benchmark code owns
  default prewarm policy today

accept intake:
  queueing publisher retains payload and calls queue.EnqueueAsync()

apply backpressure:
  owned queue supports wait-on-full, return-full, enqueue timeout,
  cancellation, closed, faulted, and retained-byte budget rejection/wait

process:
  queued processing/rebalance sessions dequeue owned batches and process them
  through sync or async processing/rebalance paths

drain:
  queued sessions drain until closed/faulted/canceled/disposed queue result

cancel:
  enqueue cancellation, dequeue cancellation, and drain cancellation have
  result shapes

fault:
  queue fault rejects later enqueue and returns faulted dequeue/session results

stop/dispose:
  queue dispose closes the channel and clears pending batches; queueing
  publisher dispose releases pending retained resources
```

Lifecycle gaps:

```text
runtime default selection:
  no distinct runtime default resolver was found; queued-owned direct benchmark
  defaults are not a runtime provider default

runtime prewarm owner:
  no runtime lifecycle owner currently decides startup/operator/lazy prewarm

shutdown mode behavior:
  RadarProcessingProviderQueueOptions exposes Drain and CancelQueued, but
  audited runtime sessions do not appear to apply CancelQueued as a distinct
  shutdown policy yet

operator lifecycle surface:
  CLI/result reporting is strong for direct benchmark rows, but there is no
  dedicated runtime command/result surface that names configure/start/prewarm/
  intake/drain/cancel/fault/stop/dispose phases

true live lifecycle:
  no network/live feed lifecycle was found
```

## Provider Enqueue Versus Processing Completion

Current separation:

```text
RadarProcessingQueuedBatchEnqueueResult reports enqueue acceptance/rejection
RadarProcessingQueuedBatchProcessingResult reports per-batch processing
RadarProcessingQueuedSessionResult carries both enqueue and processing results
RadarProcessingProviderQueueTelemetrySummary separates enqueue, dequeue,
completed, failed, canceled, skipped, and drain counters
RadarProcessingArchiveQueuedOverlapResult separates producer and consumer
results
```

Important guardrail:

```text
the existing contracts are capable of distinguishing provider enqueue success
from processing completion
```

Gap:

```text
runtime readiness still needs a gate/reporting shape that makes this
distinction operator-visible for runtime-shaped archive sessions, not only
unit tests and direct benchmark rows
```

## Retained Payload And Prewarm Audit

Current retained payload support:

```text
RadarProcessingRetainedPayloadFactory supports SnapshotCopy, PooledCopy, and
explicit unsupported BuilderTransfer

PooledCopy can retain leased batch payload into rented event and byte arrays

retained resource lifecycle tracks queue transfer, consumer transfer, release,
already released, failed release, and not required

ArchiveOwnedRadarEventBatchQueueingPublisher tracks retention telemetry,
release telemetry, pending retained resources, active retained resources, and
retained pressure
```

Current prewarm support:

```text
RadarProcessingRetainedPayloadFactory.Prewarm() can prewarm event and payload
pools and returns elapsed, allocated bytes, and retained bytes

direct archive rebalance benchmark code creates automatic default prewarm only
when the effective contour is the direct benchmark rollout default and no
caller-supplied retained payload factory is provided
```

Prewarm gaps:

```text
runtime has no accepted prewarm posture
runtime has no startup/operator/lazy prewarm result shape
runtime has no explicit prewarm failure policy
runtime has no documented prewarm cleanup/dispose gate
runtime has no live sizing decision; milestone 017 sizing is evidence only
```

## Pressure And Backpressure Audit

Current support:

```text
RadarProcessingOwnedBatchQueue is bounded by batch capacity

RadarProcessingProviderQueueOptions supports:
  capacity
  full mode: Wait or ReturnFull
  enqueue timeout
  shutdown mode: Drain or CancelQueued
  recent detail capacity
  max retained payload bytes

owned queue tracks:
  pending count
  pending payload bytes
  queue depth high-water
  queued payload bytes high-water
  enqueue attempts
  enqueued batches
  full, timed out, canceled, closed, and faulted enqueue counts
  total enqueue wait time
  total dequeue wait time
```

Retained pressure support:

```text
queue telemetry carries retained resource pressure
queueing publisher augments queue telemetry with pending/active/combined
retained resource pressure
overlap telemetry exposes pending, active, combined, and high-water values
```

Backpressure gaps:

```text
CancelQueued shutdown mode is contractual but not wired into audited drain
behavior

runtime readiness needs a scenario-level policy for queue full, retained-byte
budget exhaustion, processing fault during intake, cancellation during intake,
and drain/stop with pending work

no runtime/default decision states which full mode, timeout, shutdown mode, or
retained-byte budget applies outside direct benchmark defaults
```

## Failure And Cancellation Audit

Current support:

```text
publisher rejects canceled publish before retention
publisher rejects queue closed/faulted before retention
publisher releases retained resource when enqueue fails after retention
producer failure faults the queue
consumer failure faults the queue and releases pending resources
consumer cancellation closes queue and releases pending resources
queued sessions fault on processing validation failure, processing exception,
and rebalance migration/validation failure
queued sessions close queue on cancellation
owned queue rejects later enqueue after fault
worker telemetry has failure, cancellation, timeout, and health vocabulary
```

Current focused coverage signals:

```text
owned queue tests cover full, timeout, retained-byte budget exhaustion,
cancellation before/while waiting, close, fault, dequeue cancellation, dispose,
and retained-byte wait release

queued session tests cover deterministic processing, validation fault,
skipped-after-fault behavior, cancellation, and async wrapper behavior

overlap runner tests cover producer/consumer completion, cancellation, fault,
retained cleanup, pressure, and telemetry

worker lifecycle/telemetry tests cover worker states, faulted/disposed health,
timeouts, and bounded worker telemetry contracts
```

Gaps:

```text
milestone 018 still needs runtime-shaped integrated gates that combine archive
input, retained payload, provider queue, queued rebalance session, worker
telemetry, cancellation/failure, and operator-visible results

release failure gates depend on available deterministic release-failure
injectors; if no injector exists, this must be a named coverage gap or a small
test helper task in slice 5
```

## Observability Audit

Strong existing direct benchmark observability:

```text
direct benchmark result contracts expose retained payload prewarm,
processing completeness, processing validation failed batches, worker failed
batches/items, queue telemetry, overlap telemetry, retained pressure, and
release telemetry

CLI rebalance-archive prints provider provenance, prewarm attribution,
processing completeness, retained pressure, queue, overlap, worker, and
release telemetry for direct benchmark rows
```

Runtime-shaped observability already available in lower-level contracts:

```text
producer and consumer status
provider result and retention telemetry
queue telemetry
overlap telemetry
queued session enqueue and processing results
final topology version for queued rebalance sessions
retained resource pressure
worker telemetry through processing/rebalance result surfaces
```

Observability gaps:

```text
no single runtime readiness result currently names:
  runtime provider mode source
  runtime default candidate yes/no
  runtime fallback/oracle contour yes/no
  runtime prewarm source
  startup/prewarm/first-use/steady-state/drain phase cost split
  lifecycle phase status across configure/start/prewarm/intake/drain/cancel/
    fault/stop/dispose

direct CLI reporting is not itself a runtime operator surface
```

## Test Coverage Gap List

Coverage that appears already strong enough to reuse:

```text
owned queue capacity, retained-byte budget, timeout, cancellation, close,
fault, dispose, and telemetry contracts

queued processing/rebalance deterministic success and fault propagation

archive queued overlap producer/consumer separation, retained cleanup,
pressure telemetry, and failure/cancellation result shapes

retained payload factory prewarm and pooled-copy telemetry for direct
benchmark use

worker lifecycle and telemetry contract vocabulary
```

Coverage that slice 2-5 should decide or add:

```text
runtime default versus explicit opt-in provider selection
runtime prewarm lifecycle success/failure/cancel/cleanup
shutdownMode CancelQueued behavior or a decision to defer/reject it for 018
integrated runtime-shaped archive session gates with operator-visible summary
release-failure injection for runtime retained resources, if feasible
runtime phase cost split across startup/prewarm/first-use/steady/drain
true live ingestion coverage statement
```

## Slice 1 Decision

Slice 1 does not change runtime behavior and does not decide runtime defaults.

The audited codebase has enough in-process queued-owned runtime-shaped
foundation to proceed to slice 2:

```text
archive provider can retain leased input and enqueue owned batches
queue is bounded and pressure-aware
queued processing/rebalance sessions drain sequence-ordered batches
producer and consumer results are separate
retained cleanup and pressure telemetry exist
worker and processing failure vocabulary exists
direct benchmark prewarm and processing completeness are visible
```

The main gaps to carry into slice 2 are:

```text
no runtime default selection decision
no runtime prewarm lifecycle policy
shutdownMode CancelQueued is not wired into audited runtime drain behavior
no single runtime readiness result/operator surface
no true live ingestion adapter evidence
integrated runtime-shaped gates still need to be designed
```

Proceed to slice 2:

```text
runtime readiness contract and gate matrix design
```
