# Milestone 009: Owned Payload Provider Decoupling Plan

Status: draft.

This plan implements the milestone 009 architecture defined in
`009-owned-payload-provider-decoupling.md`.

The plan is intentionally scoped to the first in-process owned payload handoff
between replay providers and processing. It should not introduce durable
broker integration, live ingestion, cross-process workers, concurrent
multi-batch processing, physical worker-local state transfer, source-level
migration, partition splitting, or complex radar algorithms.

## Goal

Milestone 009 adds a safe provider decoupling boundary while preserving the
batch-safe processing and rebalance semantics proven by milestones 004 through
008.

The target execution shape is:

```text
provider callback publishes one leased RadarEventBatch
  -> convert to owned RadarEventBatch before callback return
  -> enqueue owned batch into bounded provider-to-processing queue
  -> provider may continue until queue backpressure applies

processing consumer
  -> dequeue owned batches in provider sequence order
  -> process one active batch at a time
  -> use synchronous processing or milestone 008 async shard transport
  -> optionally run the rebalance control plane after successful processing
  -> publish topology N+1 only before the next processed batch
```

The most important rule remains:

```text
leased payload may not outlive the provider callback.
owned payload may be retained and queued.
```

The first implementation target is deliberately conservative:

```text
bounded in-process queue
owned RadarEventBatch input only
one active processing batch at a time
ordered provider sequence drain
same-run borrowed blocking versus owned queued comparison
explicit copy, enqueue, queue-wait, drain, worker, and rebalance telemetry
```

## Starting Point

Milestone 008 is complete and provides:

```text
RadarEventBatch lifetime: Owned or Leased
RadarEventBatch.ToOwnedSnapshot()
RadarEventBatchBuilder.ConsumeLeased()
IArchiveRadarEventBatchPublisher synchronous callback boundary
RadarProcessingCore
RadarProcessingRebalanceSession
RadarProcessingAsyncCoreSession
RadarProcessingAsyncRebalanceSession
RadarProcessingAsyncWorkerGroup
RadarProcessingWorkerTelemetryRecorder
RadarProcessingArchiveRebalanceBenchmark
processing benchmark rebalance-archive
```

Important existing constraints:

```text
RadarEventBatch is the processing input boundary.
Leased batches are valid only during the synchronous publish callback.
Publishers that retain data must call RadarEventBatch.ToOwnedSnapshot().
SourceId -> PartitionId remains stable for a processing session.
PartitionId -> ShardId may change only through topology publication.
One processed batch captures one topology snapshot.
Rebalance publishes accepted moves only after completed processing.
Async shard transport still waits for borrowed work before callback return.
Worker telemetry is bounded and comparable across sync/async contours.
```

Milestone 009 must not weaken these constraints while allowing provider replay
and processing to run at separate paces over owned input.

## Target Implementation Shape

Most provider-decoupling contracts should live under
`RadarPulse.Domain.Processing` when they describe queue status, result shape,
telemetry, validation, or options. Runtime queue mechanics and archive
benchmark integration can live under infrastructure.

Candidate layering:

```text
src/Domain/Streaming
  owned snapshot guardrails only if the existing RadarEventBatch API needs a
  small explicit helper; avoid changing the stream contract unless necessary

src/Domain/Processing
  provider queue options, sequence ids, enqueue/result contracts, session
  status, telemetry summaries, validation errors

src/Infrastructure/Processing
  bounded owned-batch queue, queued processing consumer, queued rebalance
  consumer, archive benchmark integration

src/Infrastructure/Archive
  provider adapter only if the queueing publisher belongs next to archive
  replay rather than processing benchmarks

src/Presentation
  CLI provider mode, queue capacity, queue timeout, and queued telemetry output
```

The implementation should keep these responsibilities separate:

```text
ownership:
  converts leased input to an owned RadarEventBatch before callback return

provider queue:
  accepts only owned batches, enforces capacity, and records backpressure

processing consumer:
  drains owned batches in sequence order and calls the existing processing or
  rebalance sessions

rebalance control plane:
  consumes completed processing telemetry and may publish topology N+1 between
  processed batches

telemetry:
  records bounded queue, copy, enqueue, drain, worker, and rebalance costs

validation:
  proves owned queued output matches borrowed blocking reference output

benchmarking:
  exposes where provider decoupling pays copy, allocation, queue, and worker
  costs
```

## Implementation Slices

### 1. Owned Snapshot Guardrails

Strengthen the retained-batch contract before adding a queue.

Candidate focus:

```text
RadarEventBatch.ToOwnedSnapshot()
RadarEventBatchBuilder.ConsumeLeased()
IArchiveRadarEventBatchPublisher callback documentation
```

Required behavior:

```text
owned batches return themselves from ToOwnedSnapshot()
leased batches return a new owned snapshot
owned snapshots preserve stream schema, dictionary version, source-universe
version, events, payload bytes, payload offsets, event order, and lifetime
owned snapshots preserve precomputed payload metrics
owned snapshots remain stable after builder buffers are reused
empty leased batches produce stable owned empty snapshots
```

Expected tests:

```text
owned snapshot preserves metadata and metrics
owned snapshot payload remains stable after ConsumeLeased returns
owned snapshot events remain stable after builder reuse
owned snapshot of empty leased batch uses owned lifetime
ToOwnedSnapshot for owned batch returns same instance
```

Guardrail:

```text
Do not add queue behavior in this slice. Prove the ownership conversion first.
```

### 2. Provider Queue Contract Surface

Introduce explicit result and option contracts for provider decoupling.

Candidate types:

```text
RadarProcessingQueuedBatchSequence
RadarProcessingProviderQueueOptions
RadarProcessingProviderQueueFullMode
RadarProcessingProviderQueueShutdownMode
RadarProcessingQueuedBatch
RadarProcessingQueuedBatchEnqueueStatus
RadarProcessingQueuedBatchEnqueueResult
RadarProcessingQueuedBatchProcessingStatus
RadarProcessingQueuedBatchProcessingResult
RadarProcessingQueuedSessionStatus
RadarProcessingQueuedSessionResult
RadarProcessingProviderQueueTelemetrySummary
RadarProcessingProviderQueueValidationError
RadarProcessingProviderQueueValidationResult
```

Required behavior:

```text
queue capacity is explicit and validated
optional enqueue timeout is explicit and validated
shutdown mode distinguishes drain from cancel where supported
enqueue status distinguishes accepted, full, timed out, canceled, closed, and
faulted
processing status distinguishes succeeded, failed processing, failed
validation, failed migration, canceled, and skipped after session fault
session result separates provider intake from processing completion
result snapshots are immutable or defensively copied
```

Expected tests:

```text
default options are valid and bounded
invalid queue capacity is rejected
invalid timeout is rejected
enqueue statuses are stable
processing statuses are stable
session result preserves immutable detail snapshots
empty result snapshots use shared empty arrays where local patterns allow
```

Guardrail:

```text
Do not make enqueue success imply processing success. They are separate
events and must stay separate in the contracts.
```

### 3. Bounded Owned Batch Queue Foundation

Implement the in-process queue that accepts only owned batches.

Candidate types:

```text
RadarProcessingOwnedBatchQueue
RadarProcessingOwnedBatchQueueItem
RadarProcessingOwnedBatchQueueState
RadarProcessingOwnedBatchQueueResult
```

Required behavior:

```text
enqueue rejects null batches
enqueue rejects leased batches
enqueue accepts owned batches
queue assigns monotonic provider sequence ids
dequeue returns owned batches in provider sequence order
capacity is bounded by batch count
queue depth high-water mark is tracked
close stops later enqueue
fault stops later enqueue and preserves fault reason
cancellation before enqueue returns canceled status
dispose or close behavior is deterministic with queued items
```

Expected tests:

```text
owned batches enqueue and dequeue FIFO
leased batch enqueue is rejected before retention
full queue returns or waits according to configured policy
canceled enqueue does not retain the batch
closed queue rejects later enqueue
faulted queue rejects later enqueue with fault status
queue depth and accepted counts are reported
dequeued item carries provider sequence and owned lifetime
```

Implementation note:

```text
A local bounded queue or a BoundedChannel-based implementation is acceptable if
the result contracts remain deterministic and close/fault/cancel behavior is
fully tested.
```

Guardrail:

```text
No unbounded queue and no silent dropping. Dropping is not a milestone 009
policy.
```

### 4. Queued Processing Consumer

Add a consumer that drains owned batches through the existing processing core.

Candidate types:

```text
RadarProcessingQueuedProcessingSession
RadarProcessingQueuedProcessingSessionOptions
RadarProcessingQueuedBatchProcessor
```

Required behavior:

```text
consumer processes queued batches in provider sequence order
only one batch is active in the first implementation
sync execution mode calls RadarProcessingCore.Process
async execution mode calls RadarProcessingAsyncCoreSession.ProcessAsync
processing completion records sequence id, topology version, validation, and
checksum context
processing failure faults the queued session and rejects later enqueue
DisposeAsync drains or cancels according to configured shutdown mode
```

Expected tests:

```text
queued sync processing matches borrowed blocking sync result
queued async processing matches borrowed blocking async result
provider sequence order is preserved
processing failure faults the session
faulted session rejects later enqueue
cancellation before dequeue is reported
cancellation while active processing is reported
DisposeAsync is idempotent
```

Guardrail:

```text
Do not process multiple queued batches concurrently in this milestone. That is
a separate topology-ordering problem.
```

### 5. Queued Rebalance Consumer

Integrate queued owned batches with the rebalance control plane.

Candidate types:

```text
RadarProcessingQueuedRebalanceSession
RadarProcessingQueuedRebalanceSessionResult
RadarProcessingQueuedRebalanceBatchResult
```

Required behavior:

```text
sync execution mode calls RadarProcessingRebalanceSession.Process
async execution mode calls RadarProcessingAsyncRebalanceSession.ProcessAsync
rebalance runs only after successful processing for the active batch
accepted migrations publish topology only before the next dequeued batch
failed migration preserves existing no-partial-publication behavior
queued session reports final topology version
accepted/skipped decision counts remain comparable with borrowed blocking
reference runs
```

Expected tests:

```text
queued rebalance preserves accepted move counts against borrowed reference
queued rebalance preserves skipped decision counts against borrowed reference
topology version advances monotonically across queued batches
batch N+1 captures topology after any accepted move from batch N
failed migration faults queued session and blocks later enqueue
async queued rebalance validates through existing async validator
```

Guardrail:

```text
Workers do not publish topology. Rebalance publication remains in the consumer
after completed processing.
```

### 6. Archive Provider Adapter

Connect archive replay callbacks to the owned provider queue.

Candidate types:

```text
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingArchiveQueuedProviderMode
RadarProcessingArchiveQueuedProviderResult
```

Required behavior:

```text
publisher receives leased RadarEventBatch during synchronous callback
publisher converts leased batch to owned snapshot before callback return
publisher enqueues only the owned snapshot
callback returns after enqueue succeeds or explicit backpressure wait completes
callback does not wait for processing completion in queued-owned mode
publisher reports enqueue failure through archive benchmark/session result
publisher stops accepting callbacks after processing session fault
```

Expected tests:

```text
archive queueing publisher never retains leased batch
owned snapshot is enqueued before callback returns
enqueue backpressure is visible in result
processing fault causes later publish to fail
cancellation during publish does not enqueue partial work
single-file archive queued run preserves deterministic checksum
```

Guardrail:

```text
Do not change IArchiveRadarEventBatchPublisher to async in this milestone.
Keep the provider callback synchronous and move decoupling behind owned input.
```

### 7. Queue Telemetry And Allocation Attribution

Add bounded telemetry for the provider decoupling layer.

Candidate types:

```text
RadarProcessingProviderQueueTelemetryRecorder
RadarProcessingProviderQueueTelemetrySummary
RadarProcessingProviderQueueRecentDetail
RadarProcessingOwnedSnapshotAllocationSummary
```

Required behavior:

```text
record owned snapshot count
record owned snapshot elapsed time
record owned snapshot allocated bytes where benchmark context can measure it
record owned snapshot payload bytes
record enqueue attempts, successes, waits, timeouts, cancellations, and faults
record queue depth high-water mark
record queued payload bytes high-water mark where available
record dequeued, completed, failed, and canceled batches
record provider-to-processing latency
bound recent detail retention
```

Expected tests:

```text
telemetry counters accumulate without retaining batch payload
recent detail cap drops oldest or increments dropped count according to policy
snapshot summary is immutable
reset clears counters where the recorder exposes reset
owned snapshot allocation summary computes bytes per payload value
```

Guardrail:

```text
Telemetry must not become the new payload retention mechanism. Store counters,
sequence ids, durations, statuses, and checksums, not RadarEventBatch payload.
```

### 8. Queued Validation

Add validation helpers for provider decoupling parity.

Candidate types:

```text
RadarProcessingQueuedProviderValidator
RadarProcessingQueuedProviderValidationProfile
RadarProcessingQueuedProviderValidationError
RadarProcessingQueuedProviderValidationResult
```

Required behavior:

```text
validate all queued batches are owned
validate provider sequence monotonicity
validate processed sequence monotonicity
validate no completed result is missing for accepted batches unless canceled
validate final topology version monotonicity
validate checksum parity against borrowed blocking reference where supplied
validate accepted/skipped/failure count parity where supplied
validate worker failure counts and queue fault state
```

Expected tests:

```text
diagnostic profile catches leased queued batch
diagnostic profile catches out-of-order processed sequence
diagnostic profile catches missing completion
diagnostic profile catches topology regression
reference comparison catches checksum mismatch
reference comparison catches accepted move mismatch
essential profile stays low-cost and catches structural failures
```

Guardrail:

```text
The synchronous borrowed path remains the oracle. Queued validation should
compare output semantics, not timing.
```

### 9. Archive Benchmark Integration

Expose borrowed blocking versus owned queued contours in benchmarks.

Candidate changes:

```text
RadarProcessingArchiveRebalanceBenchmark
RadarProcessingArchiveRebalanceBenchmarkResult
RadarProcessingArchiveRebalanceCacheBenchmarkResult
RadarProcessingRebalanceAllocationSummary
```

Possible new mode names:

```text
provider mode:
  blocking-borrowed
  queued-owned

execution mode:
  sync
  async
```

Required behavior:

```text
same-run benchmark can compare borrowed blocking sync, borrowed blocking async,
owned queued sync, and owned queued async
queue capacity is configurable
owned snapshot timing and allocation are reported separately
enqueue wait and queue drain timing are reported separately
worker telemetry remains visible for async queued runs
rebalance/sampling result fields remain comparable with milestone 008
benchmark result labels provider mode, execution mode, queue capacity, worker
count, validation profile, retention mode, and archive contour
```

Expected tests:

```text
single-file benchmark reports queued-owned provider mode
queued-owned benchmark preserves checksum against blocking reference
queue capacity option is validated
allocation summary includes owned snapshot cost
worker telemetry remains present for async queued mode
cache benchmark aggregates queued provider telemetry
```

Guardrail:

```text
Do not replace milestone 008 callback timing semantics. Add queued-provider
timing fields so the cost move is visible instead of hidden.
```

### 10. CLI Surface

Add only the CLI options needed to run and report the milestone benchmark.

Candidate command surface:

```text
processing benchmark rebalance-archive
  --provider blocking-borrowed|queued-owned
  --execution sync|async
  --queue-capacity <count>
  --queue-timeout-ms <ms>
  --queue-telemetry none|summary|recent
```

Required behavior:

```text
default provider mode remains blocking-borrowed
queued-owned provider mode requires bounded queue capacity
invalid queue capacity is rejected
invalid timeout is rejected
output labels provider mode clearly
output separates provider/replay, owned snapshot, enqueue wait, drain,
processing, worker, and rebalance costs where available
```

Expected tests:

```text
CLI accepts queued-owned provider mode
CLI rejects invalid queue capacity
CLI preserves existing blocking-borrowed defaults
CLI output includes provider mode and queue capacity
CLI output includes owned snapshot and queue telemetry fields for queued mode
```

Guardrail:

```text
Do not make queued-owned the default execution path before closeout benchmark
evidence justifies it.
```

### 11. Performance Gate And Closeout

Capture the final milestone assessment.

Required benchmark contours:

```text
single-file Archive Two:
  blocking-borrowed sync
  blocking-borrowed async
  queued-owned sync
  queued-owned async

full local KTLX cache where available:
  blocking-borrowed sync
  blocking-borrowed async
  queued-owned sync
  queued-owned async

queue capacities:
  1 for conservative backpressure
  one larger bounded capacity to show provider/processing overlap
```

Required interpretation:

```text
deterministic parity versus borrowed reference
owned snapshot allocation cost
owned snapshot elapsed time
enqueue wait and queue depth behavior
processing drain cost
async worker cost under queued-owned input
rebalance accepted/skipped/failure parity
whether the cost is acceptable as a provider-decoupling substrate
what should be optimized next if the cost is not acceptable
```

Closeout artifacts:

```text
docs/milestones/009-owned-payload-provider-decoupling-decision-trace.md
docs/milestones/009-owned-payload-provider-decoupling-closeout.md
docs/handoff.md update
```

## Verification Strategy

Focused verification should grow with each slice:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarEventBatchBuilderTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Queued
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Benchmark verification should use Debug for fast smoke while developing and a
Release CLI build for the final performance gate.

Candidate smoke commands:

```powershell
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --queue-capacity 1 --iterations 1 --warmup-iterations 0 --parallelism 24
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode rebalance --provider queued-owned --execution async --workers 4 --queue-capacity 1 --iterations 1 --warmup-iterations 0 --parallelism 24
```

The final command set should be adjusted to the actual CLI option names after
implementation.

## Completion Checklist

```text
[ ] owned snapshot guardrails are strengthened and tested
[ ] provider queue contracts are implemented and tested
[ ] bounded owned batch queue is implemented and tested
[ ] queued processing consumer is implemented and tested
[ ] queued rebalance consumer is implemented and tested
[ ] archive provider adapter converts leased input to owned before enqueue
[ ] provider enqueue and processing completion results are separated
[ ] queue telemetry is bounded and tested
[ ] queued validation proves parity against borrowed blocking references
[ ] archive benchmark exposes blocking-borrowed and queued-owned provider modes
[ ] CLI exposes provider mode, queue capacity, and queued telemetry options
[ ] same-run single-file comparisons are captured
[ ] same-run full-cache comparisons are captured where local data is available
[ ] performance assessment interprets owned-copy, queue, worker, and rebalance costs
[ ] decision trace is written
[ ] closeout is written
[ ] handoff is updated for the next milestone
```

## Non-Goals

Milestone 009 does not implement:

```text
durable broker integration
live ingestion
cross-process provider or worker transport
concurrent multi-batch processing with out-of-order completion
topology publication from worker threads
physical worker-local state residency
source-level migration
partition splitting or repartitioning
dropping queued batches as a normal policy
unbounded queues
zero-copy ownership transfer as a required closeout condition
queued-owned processing as the default mode
complex radar algorithms
```

## Closeout Question

The closeout should answer one practical question:

```text
Can RadarPulse safely decouple replay/provider progress from processing
completion by paying an explicit owned-payload cost, while preserving
deterministic processing, rebalance behavior, bounded telemetry, and clear
backpressure?
```

If the answer is yes, the next milestone can choose between production
configuration, live/durable provider integration, or multi-batch scheduling.
If the answer is no because owned snapshots are too expensive, the next
milestone should optimize ownership transfer before broadening the pipeline.
