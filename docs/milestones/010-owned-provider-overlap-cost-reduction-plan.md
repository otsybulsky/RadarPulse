# Milestone 010: Owned Provider Overlap And Cost Reduction Plan

Status: draft.

This plan implements the milestone 010 architecture defined in
`010-owned-provider-overlap-cost-reduction.md`.

The plan is intentionally scoped to making the existing in-process
`queued-owned` provider boundary cheaper and genuinely overlapped. It should
not introduce durable broker integration, live ingestion, cross-process
workers, rebalance-enabled concurrent processing without ordered commit
semantics, physical worker-local state transfer, source-level migration,
partition splitting, or complex radar algorithms.

## Goal

Milestone 010 reduces the cost of retaining provider payloads and adds a real
producer/consumer archive contour while preserving the batch-safe processing
and rebalance semantics proven by milestones 004 through 009.

The target execution shape is:

```text
archive producer
  -> reads archive files or file segments
  -> builds callback-scoped RadarEventBatch input
  -> retains owned payload through the selected retention strategy
  -> enqueues owned retained input into a bounded provider queue
  -> continues until input completes or bounded backpressure applies

processing consumer
  -> dequeues owned input in provider sequence order
  -> captures topology immediately before processing
  -> processes one rebalance-enabled batch at a time
  -> validates output and runs the rebalance control plane
  -> publishes topology N+1 only before the next processed batch
  -> releases or recycles retained payload resources after final use
```

The most important rules remain:

```text
leased payload may not outlive the provider callback.
only owned payload may enter the provider queue.
queued batches may run ahead of processing.
rebalance-enabled processing commits in provider sequence order.
```

The first implementation target is deliberately conservative:

```text
same RadarEventBatch processing input contract
one lower-allocation retained payload strategy
explicit resource ownership and release lifecycle
bounded queue by item count and retained payload bytes where practical
real archive producer/consumer overlap across files or batches
one active rebalance-enabled processing batch at a time
same-run borrowed blocking versus optimized queued-owned validation
explicit replay, retention, queue, overlap, processing, worker, validation,
  rebalance, and resource lifecycle telemetry
```

## Starting Point

Milestone 009 is complete and provides:

```text
RadarEventBatch.ToOwnedSnapshot()
RadarEventBatch lifetime: Owned or Leased
RadarProcessingProviderQueueOptions
RadarProcessingQueuedBatch and provider sequence ids
RadarProcessingOwnedBatchQueue
RadarProcessingQueuedProcessingSession
RadarProcessingQueuedRebalanceSession
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingProviderQueueTelemetryRecorder
RadarProcessingQueuedProviderValidator
RadarProcessingArchiveRebalanceBenchmark
RadarProcessingArchiveProviderMode.BlockingBorrowed
RadarProcessingArchiveProviderMode.QueuedOwned
processing benchmark rebalance-archive provider controls
```

Important existing constraints:

```text
RadarEventBatch is the processing input boundary.
Leased batches are valid only during the synchronous publish callback.
The provider queue rejects non-owned batches.
Provider enqueue success is separate from processing completion.
Queued batches drain in provider sequence order.
SourceId -> PartitionId remains stable for a processing session.
PartitionId -> ShardId may change only through topology publication.
One processed batch captures one topology snapshot.
Rebalance publishes accepted moves only after completed processing.
Blocking-borrowed remains the default provider mode and correctness oracle.
Queued provider telemetry is bounded.
```

Measured milestone 009 cost anchors:

```text
full-cache queued-owned owned snapshot allocation: about 9.95 GB
full-cache queued-owned owned snapshot elapsed: about 529-576 ms
full-cache queued-owned enqueue wait: about 2 ms total
full-cache queue depth high-water mark: 1 for capacity 1 and 8
full-cache queued-owned async q8 end-to-end: 17_569.64 ms
full-cache blocking-borrowed async end-to-end: 16_955.62 ms
```

Milestone 010 must not weaken the correctness constraints while moving the
queued-owned path from "safe but copied and not overlapped" toward "safe,
measurably cheaper, and overlapped under bounded pressure."

## Target Implementation Shape

Most retained payload and queue result contracts should live under
`RadarPulse.Domain.Processing` when they describe retention strategy,
resource lifecycle, queue window, status, telemetry, validation, or options.
Streaming changes should stay minimal and should preserve `RadarEventBatch` as
the processing input shape. Runtime queue mechanics and archive benchmark
integration can remain under infrastructure.

Candidate layering:

```text
src/Domain/Streaming
  small RadarEventBatch or RadarEventBatchBuilder helpers only if a transfer or
  pooled retained representation needs a safe construction boundary

src/Domain/Processing
  retained payload strategy/options, retained resource lifecycle contracts,
  extended provider queue window options, overlap telemetry summaries,
  validation errors, benchmark result fields

src/Infrastructure/Processing
  retained payload factory, resource-owned queued batch wrapper, retained-byte
  queue enforcement, ordered processing consumer, overlap session mechanics

src/Infrastructure/Archive
  archive producer adapter or overlap runner integration around existing
  archive replay/publish sessions

src/Presentation
  CLI flags for retention strategy, overlap mode, retained-byte budget, and
  overlap telemetry output
```

The implementation should keep these responsibilities separate:

```text
retention:
  makes provider callback input safely retainable and reports allocation cost

resource ownership:
  releases or recycles retained storage after processing, validation, telemetry,
  and cleanup are complete

provider queue:
  accepts only owned retained input, enforces item and byte windows, and records
  backpressure

overlap runner:
  lets the producer continue while the consumer drains accepted work

processing consumer:
  processes queued input in provider sequence order and preserves topology rules

validation:
  proves optimized queued-owned output matches borrowed blocking references

benchmarking:
  explains whether cost moved, decreased, or was hidden by overlap
```

## Implementation Slices

### 1. Baseline Cost Anchors And Contract Audit

Freeze the milestone 009 behavior as the comparison point before optimizing.

Candidate focus:

```text
RadarEventBatch.ToOwnedSnapshot()
RadarEventBatchBuilder.ConsumeLeased()
RadarProcessingOwnedBatchQueue
RadarProcessingProviderQueueTelemetryRecorder
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingArchiveRebalanceBenchmark
RadarPulseCli rebalance benchmark output
```

Required behavior:

```text
document current allocation/time fields used by the performance gate
confirm current queued-owned path still reports owned snapshot allocation
confirm queue high-water mark and enqueue wait remain visible
confirm borrowed blocking reference remains available in benchmark output
identify the exact allocation buckets that must not disappear after refactor
```

Expected tests or checks:

```text
existing owned snapshot guardrail tests still pass
existing owned batch queue tests still pass
existing queued provider validator tests still pass
existing archive rebalance benchmark queued-provider tests still pass
CLI output still contains provider mode and owned snapshot summary fields
```

Guardrail:

```text
Do not optimize allocation in this slice. Establish the baseline field names,
current code paths, and validation commands that later slices must preserve.
```

### 2. Retained Payload Strategy Contracts

Introduce an explicit strategy vocabulary before changing retention mechanics.

Candidate types:

```text
RadarProcessingRetainedPayloadStrategy
RadarProcessingRetainedPayloadOptions
RadarProcessingRetainedPayloadRetentionStatus
RadarProcessingRetainedPayloadRetentionResult
RadarProcessingRetainedPayloadReleaseStatus
RadarProcessingRetainedPayloadReleaseResult
RadarProcessingRetainedPayloadTelemetrySummary
```

Candidate strategy values:

```text
SnapshotCopy:
  milestone 009 behavior; always copy to ordinary owned arrays

PooledCopy:
  copy leased data into rented storage and return storage after processing

BuilderTransfer:
  move builder-owned event/payload buffers into retained output when the
  provider can prove the buffers will not be reused
```

Required behavior:

```text
default strategy preserves milestone 009 SnapshotCopy behavior
strategy values are stable and explicitly named in telemetry
invalid strategy/options are rejected early
retention result separates success, unsupported strategy, failed copy, canceled,
  and invalid input
release result separates released, already released, failed, and not required
retention telemetry records batch count, event count, payload bytes, allocated
  bytes, elapsed time, transfer count, pool rent/return count, and failures
```

Expected tests:

```text
default retained payload options choose snapshot-compatible behavior
invalid retained-byte limits are rejected
strategy enum values are stable
retention result snapshots are immutable
release result statuses are stable
telemetry summary accepts empty and non-empty snapshots
```

Guardrail:

```text
Do not make a strategy name imply zero-copy unless ownership transfer is
actually proven. Strategy telemetry must identify what happened, not what was
hoped for.
```

### 3. Resource-Owned Queued Batch Lifecycle

Extend the queued payload shape so retained resources can be released exactly
once after final use.

Candidate types:

```text
RadarProcessingRetainedQueuedBatch
RadarProcessingRetainedBatchResource
RadarProcessingRetainedBatchResourceState
RadarProcessingRetainedBatchReleaseResult
RadarProcessingRetainedBatchLease
RadarProcessingRetainedResourceCleanupResult
```

Possible implementation approaches:

```text
wrap existing RadarProcessingQueuedBatch with an optional resource handle
extend RadarProcessingQueuedBatch only if the existing immutable shape remains
  clear and tests stay focused
keep processing-facing APIs accepting RadarEventBatch
keep resource release in queue/session infrastructure, not in processing core
```

Required behavior:

```text
accepted enqueue transfers resource ownership from provider to queue
dequeue transfers resource ownership from queue to consumer
consumer releases resources after processing, validation, and telemetry capture
pending accepted resources are released on close, fault, cancellation, or dispose
release is idempotent where practical
release failures are counted and surfaced in the session result
recent telemetry details do not retain full payload buffers
```

Expected tests:

```text
accepted retained batch releases resource after successful processing
faulted queue releases pending retained resources
canceled session releases accepted but unprocessed resources according to
  shutdown policy
dispose releases pending resources and waiters
double release returns stable already-released status or is otherwise safe
release failure faults or marks the queued session unhealthy
telemetry recent details do not keep retained payload references
```

Guardrail:

```text
Do not return pooled or transferred storage until validation and checksum
inspection for the batch are finished.
```

### 4. Lower-Allocation Retention Implementation

Implement one lower-allocation strategy while preserving snapshot semantics.

Preferred first target:

```text
PooledCopy:
  copy leased event and payload data into rented arrays
  expose them through an owned RadarEventBatch while processing is active
  return rented arrays only after the retained resource lifecycle completes
```

Candidate focus:

```text
RadarEventBatch construction boundaries
RadarEventBatchBuilder leased output
retained payload factory in Infrastructure/Processing
ArchiveOwnedRadarEventBatchQueueingPublisher or successor adapter
RadarProcessingOwnedSnapshotAllocationSummary replacement or extension
```

Required behavior:

```text
owned/retained batch preserves stream schema, dictionary version,
  source-universe version, events, payload bytes, payload metrics, event order,
  payload offsets, and lifetime
retained contents remain stable after provider builder reuse
empty batches avoid unnecessary rentals
pooled buffers are not exposed after release
pooled strategy records rent/return/miss/allocation-equivalent counters
snapshot strategy remains available for compatibility and comparison
```

Expected tests:

```text
pooled retained batch preserves metadata and payload metrics
pooled retained payload remains stable after builder reuse
pooled retained events remain stable after builder reuse
empty pooled retained batch avoids non-empty rentals
released pooled batch cannot be read through internal resource handles
snapshot strategy still matches milestone 009 behavior
retention factory rejects leased input only when the selected strategy requires
  impossible ownership semantics
```

Guardrail:

```text
If pooled storage cannot be represented without unsafe lifetime exposure,
choose a narrower helper or defer pooling before weakening RadarEventBatch
immutability. Correct retention semantics beat lower allocation.
```

### 5. Retained-Byte-Aware Queue Window

Extend bounded queue behavior so retained payload memory pressure is visible
and enforceable.

Candidate changes:

```text
RadarProcessingProviderQueueOptions.MaxRetainedPayloadBytes
RadarProcessingProviderQueueOptions.ProviderWindowMode
RadarProcessingOwnedBatchQueue pending retained-byte accounting
RadarProcessingProviderQueueTelemetrySummary retained-byte high-water fields
enqueue result message when byte budget blocks acceptance
```

Required behavior:

```text
item capacity remains required and bounded
optional retained-byte budget is validated when configured
enqueue waits when either item capacity or retained-byte budget is exhausted
ReturnFull mode returns full when either item or byte capacity is exhausted
pending retained bytes decrement only after dequeue/resource ownership transfer
or after pending item cleanup, depending on the selected accounting point
high-water retained bytes are reported
full/timed-out/canceled/closed/faulted counters remain complete
```

Expected tests:

```text
retained-byte limit rejects or waits for oversized pending window
retained-byte high-water mark is recorded
dequeue or release reduces pending retained bytes at the documented boundary
item capacity and byte capacity can block independently
timeout while waiting for retained-byte budget returns timed-out status
dispose releases retained-byte waiters
faulted queue releases or reports all pending retained resources
```

Guardrail:

```text
Do not interpret retained-byte accounting as payload ownership by itself.
The lifecycle resource handle is still responsible for actual release.
```

### 6. Producer/Consumer Overlap Runner

Add the first archive runner where producer replay and processing drain can be
active during the same benchmark interval.

Candidate types:

```text
RadarProcessingArchiveQueuedOverlapOptions
RadarProcessingArchiveQueuedOverlapRunner
RadarProcessingArchiveQueuedOverlapResult
RadarProcessingArchiveQueuedOverlapStatus
RadarProcessingArchiveQueuedOverlapProducerResult
RadarProcessingArchiveQueuedOverlapConsumerResult
```

Candidate integration points:

```text
RadarProcessingArchiveRebalanceBenchmark
NexradArchiveRadarEventBatchPublishSession
ArchiveOwnedRadarEventBatchQueueingPublisher or successor
RadarProcessingQueuedRebalanceSession
```

Required behavior:

```text
producer can publish later files or batches while consumer processes earlier
accepted provider sequence ids are complete and monotonic
consumer drains accepted work in provider sequence order
producer observes queue item and retained-byte backpressure
producer completion does not imply session success until consumer drains or
  cancels all accepted work
processing failure stops later intake and faults the session
shutdown policy determines whether accepted work drains or cancels
```

Expected tests:

```text
producer can enqueue more than one batch before consumer completion when
  capacity allows
overlap runner drains all accepted work before success
processing failure faults producer intake and reports accepted pending work
cancellation before producer completion returns canceled intake status
cancellation after acceptance drains or cancels according to shutdown mode
provider sequence gaps are reported as validation errors
queue capacity one behaves as bounded backpressure contour
queue capacity greater than one can produce depth greater than one with a slow
  consumer test double
```

Guardrail:

```text
Do not use per-file drain as the only queued-owned archive contour. A
compatibility contour may keep that shape, but milestone 010 needs a contour
that can prove overlap.
```

### 7. Ordered Consumer And Topology Pinning

Preserve rebalance determinism while allowing the producer to run ahead.

Candidate focus:

```text
RadarProcessingQueuedRebalanceSession
RadarProcessingQueuedProcessingSession
RadarProcessingRebalanceSession
RadarProcessingTopologyVersion
queued provider validation
```

Required behavior:

```text
queued-but-not-processing batches have no topology pin
consumer captures latest topology immediately before processing starts
one active rebalance-enabled batch processes and commits at a time
processing result carries captured topology version
accepted migrations publish only after successful processing and validation
next dequeued batch observes the latest published topology
failed processing prevents rebalance publication
session result exposes last committed provider sequence and topology version
```

Expected tests:

```text
queued-ahead batch observes topology published by earlier committed batch
accepted move from batch N is visible before batch N+1 processing starts
failed batch does not publish topology and faults or marks session unhealthy
provider order is preserved through processing and validation details
topology versions remain monotonic across overlapped producer input
consumer does not capture topology at enqueue time
```

Guardrail:

```text
Do not process multiple rebalance-enabled batches concurrently in this
milestone unless an explicit ordered commit barrier and stale-topology policy
are implemented and validated.
```

### 8. Overlap Telemetry And Allocation Attribution

Extend telemetry so the performance gate can explain whether the new pipeline
is cheaper, overlapped, or merely moving cost.

Candidate types or fields:

```text
RadarProcessingProviderQueueTelemetrySummary overlap fields
RadarProcessingRetainedPayloadTelemetrySummary
RadarProcessingArchiveOverlapTelemetrySummary
RadarProcessingArchiveRebalanceBenchmarkResult overlap fields
RadarProcessingArchiveRebalanceCacheBenchmarkResult overlap fields
RadarProcessingRebalanceAllocationSummary retention fields
```

Required behavior:

```text
report retention strategy name
report retained batch count, event count, payload bytes, allocated bytes, and
  retention elapsed time
report pool rent/return/miss or transfer counts where applicable
report queue depth and retained-byte high-water marks
report enqueue wait, provider blocked time, producer active time, consumer
  active time, consumer idle time, provider-to-processing latency, and overlap
  elapsed time
report release counts, pending retained resources at completion, and release
  failures
keep worker, validation, rebalance, checksum, accepted move, skipped decision,
  and failed migration fields comparable with milestone 009
```

Expected tests:

```text
empty telemetry summary uses stable zero values
overlap elapsed is zero when producer and consumer intervals do not overlap
overlap elapsed is positive when intervals overlap in a controlled test
retention allocation fields are reported separately from processing allocation
release failure count is surfaced in session and benchmark summaries
recent details are capped and do not retain RadarEventBatch payload memory
```

Guardrail:

```text
Do not hide allocation by moving it from owned snapshot fields into replay or
processing fields. The gate must still say where bytes were allocated or rented.
```

### 9. Optimized Queued Validation

Extend queued validation so lower-allocation and overlapped runs still compare
against the borrowed blocking oracle.

Candidate focus:

```text
RadarProcessingQueuedProviderValidator
RadarProcessingQueuedProviderValidationProfile
RadarProcessingQueuedProviderReference
RadarProcessingQueuedProviderMetrics
RadarProcessingQueuedProviderValidationError
```

Required behavior:

```text
same-run borrowed blocking reference remains available
optimized queued-owned validates checksum and payload value count parity
rebalance contours validate accepted moves, skipped decisions, failed
  migrations, and topology version sanity
provider sequence completeness is validated: no gaps, duplicates, or out of
  order processing commits
resource lifecycle is validated: no accepted retained resources remain pending
telemetry completeness is validated for queue, retention, overlap, and release
```

Expected tests:

```text
validator accepts optimized queued-owned parity with borrowed reference
validator rejects checksum mismatch
validator rejects accepted move mismatch on rebalance contour
validator rejects provider sequence gap
validator rejects out-of-order processing commit
validator rejects pending retained resource count at session completion
validator surfaces retention strategy and overlap mode in diagnostics
```

Guardrail:

```text
Do not compare a rebalance-disabled concurrent experiment as equivalent to a
rebalance-enabled borrowed reference. The validation profile must name the
semantic surface being compared.
```

### 10. Archive Benchmark And CLI Integration

Expose the optimized retained strategy and overlap contour without changing
the default provider mode.

Candidate benchmark controls:

```text
--provider blocking-borrowed|queued-owned
--provider-overlap none|producer-consumer
--retention-strategy snapshot-copy|pooled-copy|builder-transfer
--queue-capacity <count>
--queue-retained-bytes <bytes>
--queue-timeout-ms <ms>
--queue-telemetry none|summary|recent
--overlap-telemetry none|summary|recent
```

The exact option names should follow existing CLI conventions when implemented.

Required behavior:

```text
blocking-borrowed remains the default provider mode
queued-owned can select snapshot-compatible and optimized retention strategies
overlap mode requires queued-owned provider mode
retained-byte budget requires queued-owned provider mode
single-file compatibility contour remains available
multi-file overlap contour can run against local cache input
CLI output clearly separates provider, retention, queue, overlap, processing,
  worker, validation, rebalance, and resource lifecycle fields
invalid option combinations fail with clear messages
```

Expected tests:

```text
CLI accepts retention strategy option for queued-owned
CLI rejects retention strategy option for blocking-borrowed when unsupported
CLI accepts overlap mode with queued-owned cache input
CLI rejects overlap mode with blocking-borrowed provider
CLI prints retained allocation and overlap fields for queued-owned overlap
CLI keeps existing milestone 009 provider output compatibility where practical
benchmark result includes retention strategy and overlap mode fields
```

Guardrail:

```text
Do not make queued-owned or overlap mode the default in this milestone. The
performance gate must justify any later default-mode decision.
```

### 11. Performance Gate And Closeout

Capture the final milestone assessment.

Required Release gate contours:

```text
single-file compatibility:
  blocking-borrowed sync
  blocking-borrowed async
  queued-owned snapshot-compatible sync or async
  queued-owned optimized sync or async

full local KTLX cache where available:
  blocking-borrowed sync
  blocking-borrowed async
  queued-owned snapshot-compatible async
  queued-owned optimized overlap async

queue windows:
  capacity 1 as conservative backpressure contour
  larger bounded capacity such as 8 to prove useful overlap
  retained-byte budget where implementation supports it
```

Required interpretation:

```text
deterministic parity versus borrowed reference
allocation removed versus milestone 009 snapshot contour
retention elapsed time and pool/transfer behavior
queue depth and retained-byte high-water behavior
producer blocked time and consumer idle time
overlap elapsed and whether capacity produced useful overlap
processing, worker, validation, and rebalance costs
resource release success/failure counts
whether queued-owned remains only a measurement mode or is ready for production
configuration work in a later milestone
```

Closeout artifacts:

```text
docs/milestones/010-owned-provider-overlap-cost-reduction-decision-trace.md
docs/milestones/010-owned-provider-overlap-cost-reduction-closeout.md
docs/handoff.md update
```

Guardrail:

```text
Do not close the milestone with a throughput number alone. The gate must say
whether allocation decreased, overlap happened, parity held, and resources were
released.
```

## Verification Strategy

Focused verification should grow with each slice:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarEventBatchBuilderTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~OwnedBatchQueue
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Queued
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Presentation
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Benchmark verification should use Debug for fast smoke while developing and a
Release CLI build for the final performance gate.

Candidate smoke commands:

```powershell
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode rebalance --provider queued-owned --execution async --workers 4 --queue-capacity 1 --retention-strategy snapshot-copy --iterations 1 --warmup-iterations 0 --parallelism 24
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache-root data\nexrad --date 2026-05-04 --radar KTLX --max-files 32 --mode rebalance --provider queued-owned --provider-overlap producer-consumer --execution async --workers 4 --queue-capacity 8 --retention-strategy pooled-copy --iterations 1 --warmup-iterations 0 --parallelism 24
```

The final command set should be adjusted to the actual CLI option names after
implementation.

## Completion Checklist

```text
[x] milestone 009 cost anchors are confirmed and preserved as comparison fields
[x] retained payload strategy contracts are implemented and tested
[x] retained resource lifecycle is implemented and tested
[x] one lower-allocation retained payload strategy is implemented and tested
[x] retained-byte-aware queue window is implemented and tested where practical
[x] producer/consumer archive overlap runner is implemented and tested
[x] ordered consumer topology pinning rules are implemented and tested
[ ] overlap telemetry and allocation attribution are implemented and tested
[ ] optimized queued validation proves borrowed-reference parity
[ ] archive benchmark exposes retention strategy and overlap contours
[ ] CLI exposes retention strategy, overlap mode, and retained-byte controls
[ ] same-run single-file compatibility comparisons are captured
[ ] same-run full-cache overlap comparisons are captured where local data exists
[ ] performance assessment interprets allocation, overlap, queue, worker,
    validation, rebalance, and resource lifecycle costs
[ ] decision trace is written
[ ] closeout is written
[ ] handoff is updated for the next milestone
```

## Non-Goals

Milestone 010 does not implement:

```text
queued-owned as the default provider mode before the performance gate supports it
durable queue or broker integration
live ingestion
cross-process provider or worker transport
unbounded queues
silent provider batch dropping
retaining borrowed payload after callback return
rebalance-enabled concurrent processing without ordered commit semantics
topology publication from worker threads
physical worker-local state residency
source-level migration
partition splitting or repartitioning
complex radar algorithms
```

## Closeout Question

The closeout should answer one practical question:

```text
Can RadarPulse keep the explicit owned provider boundary while reducing
retained payload allocation and proving real producer/consumer overlap, without
breaking borrowed-reference parity, topology ordering, bounded telemetry, or
resource lifecycle cleanup?
```

If the answer is yes, the next milestone can choose between production
configuration, larger-corpus default-readiness work, live/durable provider
integration, or ordered concurrent processing. If the answer is no because
allocation or overlap remains weak, the next milestone should keep optimizing
the owned provider boundary before broadening the pipeline.
