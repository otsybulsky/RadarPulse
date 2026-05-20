# Milestone 011: Queued-Owned Default Readiness Plan

Status: in progress.

This plan implements the milestone 011 architecture defined in
`011-queued-owned-default-readiness.md`.

The plan is intentionally scoped to proving whether the existing optimized
`queued-owned + pooled-copy + producer-consumer` contour is ready to become a
future default candidate. It should not change the provider default, implement
durable broker integration, live ingestion, cross-process workers,
rebalance-enabled concurrent processing, `builder-transfer`, source-level
migration, partition splitting, or complex radar algorithms.

## Goal

Milestone 011 strengthens the evidence around the milestone 010 optimized
queued-owned contour by adding missing retained-resource pressure telemetry,
making the candidate configuration reproducible, and capturing repeated natural
Release gates before any default-provider decision.

The target execution shape remains:

```text
archive producer
  -> reads archive files from an explicit file or cache selection
  -> builds callback-scoped RadarEventBatch input
  -> retains owned payload through pooled-copy
  -> enqueues retained-owned input into a bounded provider queue
  -> records pending retained pressure while the batch waits in the queue
  -> continues until input completes or bounded backpressure applies

processing consumer
  -> dequeues retained-owned input in provider sequence order
  -> moves retained pressure from pending to active consumer accounting
  -> captures topology immediately before processing
  -> processes one rebalance-enabled batch at a time
  -> validates output and runs the rebalance control plane
  -> releases retained resources after final use
  -> records active and combined retained-resource high-water marks
```

The most important rules remain:

```text
blocking-borrowed stays the default provider mode.
queued-owned stays opt-in during milestone 011.
same-run blocking-borrowed stays the readiness oracle.
controlled consumer delay is mechanics proof tooling, not production evidence.
retained memory pressure must include pending queue bytes and active consumer
  bytes.
```

The first implementation target is deliberately conservative:

```text
no provider default change
same RadarEventBatch processing input contract
same ordered single-consumer rebalance-enabled queued drain
explicit active retained-resource accounting after dequeue
combined pending-plus-active retained pressure high-water telemetry
candidate contour exposed as reproducible configuration, not hidden default
same-run borrowed reference validation for readiness gates
repeated natural Release gate interpretation
controlled-delay proof kept separate from readiness evidence
```

## Starting Point

Milestone 010 is complete and provides:

```text
RadarProcessingRetainedPayloadStrategy
RadarProcessingRetainedPayloadOptions
RadarProcessingRetainedPayloadRetentionResult
RadarProcessingRetainedPayloadReleaseResult
RadarProcessingRetainedPayloadTelemetrySummary
RadarProcessingRetainedBatchResource
RadarProcessingRetainedQueuedBatch
RadarProcessingRetainedBatchLease
RadarProcessingProviderQueueOptions
RadarProcessingProviderQueueTelemetrySummary
RadarProcessingOwnedBatchQueue
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingArchiveQueuedOverlapRunner
RadarProcessingArchiveOverlapTelemetrySummary
RadarProcessingArchiveRebalanceBenchmark
RadarProcessingArchiveRebalanceBenchmarkResult
RadarProcessingArchiveRebalanceCacheBenchmarkResult
RadarProcessingQueuedProviderValidator
CLI flags for provider mode, overlap mode, retention strategy, queue retained
  bytes, queue telemetry, overlap telemetry, and controlled consumer delay
```

Important existing constraints:

```text
RadarEventBatch remains the processing input boundary.
Leased batches are valid only during the synchronous provider callback.
Queued-owned retains owned input before callback return.
The provider queue rejects non-owned batches.
Provider enqueue success is separate from processing completion.
Queued batches drain in provider sequence order.
SourceId -> PartitionId remains stable for a processing session.
PartitionId -> ShardId may change only through topology publication.
One processed batch captures one topology snapshot.
Rebalance publishes accepted moves only after completed processing.
Blocking-borrowed remains the default provider mode and correctness oracle.
Queued provider and overlap telemetry remain bounded.
```

Measured milestone 010 readiness inputs:

```text
pooled-copy non-overlap retained allocation:
  102_811_264 bytes versus 9_947_507_832 bytes for snapshot-copy

best repeated natural KTLX contour:
  queued-owned pooled-copy producer-consumer q8: 14_947.99 ms
  blocking-borrowed async: 16_915.80 ms
  non-overlapped queued-owned pooled-copy: 17_158.62 ms

natural queue-ahead:
  queue depth high-water mark stayed 1
  HasQueuedAheadOverlap stayed no

controlled queue-ahead:
  consumer delay 150 ms reached queue depth 8 and HasQueuedAheadOverlap yes
  full-cache control released 220 retained batches with 0 failed releases

telemetry gap:
  retained-byte high-water currently describes pending queued bytes, not the
  retained resources held by the active consumer after dequeue
```

Milestone 011 must not weaken the correctness constraints while moving the
optimized contour from "successful opt-in benchmark" toward "auditable default
candidate."

## Target Implementation Shape

Domain contracts should describe retained-resource pressure, readiness gates,
queue/overlap telemetry summaries, validation errors, and stable result fields.
Infrastructure should own runtime accounting and benchmark integration.
Presentation should expose effective configuration and readiness telemetry
without changing defaults.

Candidate layering:

```text
src/Domain/Processing
  retained-resource pressure summary and recorder contracts
  provider queue telemetry extensions
  readiness gate status/result contracts if needed
  queued-provider validation/readiness error fields

src/Infrastructure/Processing
  active retained-resource accounting in queued drain/overlap consumers
  overlap telemetry propagation
  archive benchmark result propagation
  repeated readiness gate helpers where they stay infrastructure-specific

src/Infrastructure/Archive
  queueing publisher resource acquisition/release lifecycle changes
  retained-resource pressure integration around provider sequence ids

src/Presentation
  CLI candidate profile or explicit effective-configuration output
  active/combined retained pressure output
  controlled-proof labeling

tests/RadarPulse.Tests
  focused contract tests for retained-resource pressure invariants
  queue/publisher/overlap accounting tests
  CLI output and option tests
  validation/readiness gate tests
```

The implementation should keep these responsibilities separate:

```text
queue accounting:
  pending retained bytes and queue depth while accepted batches wait

active accounting:
  retained bytes held by the consumer after dequeue and before release

combined accounting:
  pipeline retained pressure across pending plus active states

release lifecycle:
  exact-once retained resource release and visible failures

candidate configuration:
  reproducible opt-in contour for readiness gates

readiness gate:
  interpretation of repeated natural evidence, not the runtime itself
```

## Implementation Slices

### 1. Baseline Readiness Audit And Candidate Contour Freeze

Freeze the milestone 010 behavior and field names before adding active
retained-resource pressure accounting.

Candidate focus:

```text
RadarProcessingProviderQueueTelemetrySummary
RadarProcessingArchiveOverlapTelemetrySummary
RadarProcessingArchiveRebalanceBenchmark
RadarProcessingArchiveRebalanceBenchmarkResult
RadarProcessingArchiveRebalanceCacheBenchmarkResult
RadarPulseCli rebalance benchmark output
RadarProcessingArchiveQueuedOverlapRunnerTests
RadarPulseCliRebalanceBenchmarkTests
010-owned-provider-overlap-cost-reduction-performance-gate.md
```

Required behavior:

```text
document current pending retained-byte fields and aliases
confirm RetainedPayloadBytesHighWatermark currently maps to queued pending bytes
confirm overlap telemetry exposes queue depth, provider blocked time, consumer
  idle time, retained allocation, release counts, and unattributed allocation
confirm controlled consumer delay is disabled by default
confirm blocking-borrowed is still the default provider mode
confirm queued-owned pooled-copy producer-consumer can be selected explicitly
define the milestone 011 default-candidate contour in the plan and handoff
```

Expected tests or checks:

```text
existing provider queue telemetry recorder tests still pass
existing owned batch queue tests still pass
existing retained payload contract tests still pass
existing archive queued overlap runner tests still pass
existing CLI rebalance benchmark tests still pass
documentation names the exact fields that later slices must preserve
```

Guardrail:

```text
Do not change runtime behavior or provider defaults in this slice. Establish
the baseline and the candidate contour vocabulary first.
```

Slice 1 baseline capture:

```text
status:
  complete

runtime changes:
  none

current pending retained-byte fields:
  RadarProcessingProviderQueueTelemetrySummary.QueuedPayloadBytesHighWatermark
  RadarProcessingProviderQueueTelemetrySummary.RetainedPayloadBytesHighWatermark

current compatibility alias:
  RetainedPayloadBytesHighWatermark => QueuedPayloadBytesHighWatermark

current overlap retained-byte field:
  RadarProcessingArchiveOverlapTelemetrySummary.RetainedPayloadBytesHighWatermark
  -> QueueTelemetry.RetainedPayloadBytesHighWatermark

current queue-ahead signal:
  RadarProcessingArchiveOverlapTelemetrySummary.HasQueuedAheadOverlap
  -> QueueDepthHighWatermark > 1

current active retained-resource gap:
  no active consumer retained-byte high-water field exists yet
  no combined pending-plus-active retained-byte high-water field exists yet

current CLI output:
  provider mode
  provider queue capacity
  provider overlap mode
  provider overlap consumer delay ms
  retention strategy
  provider queue retained byte capacity
  provider queue payload bytes high watermark
  provider queue retained payload bytes high watermark
  provider overlap retained payload bytes high watermark
  provider blocked ms
  consumer idle ms
  retained allocation and release counts

current defaults:
  provider mode: blocking-borrowed
  provider overlap: none
  retention strategy: snapshot-copy
  queue capacity: 1
  retained-byte budget: none
  overlap consumer delay: 0
  queue telemetry: summary
  overlap telemetry: summary

milestone 011 candidate contour:
  provider mode: queued-owned
  retention strategy: pooled-copy
  provider overlap: producer-consumer
  execution: async
  queue capacity: 8
  retained-byte budget: 536870912
  queue telemetry: summary or recent as required by the gate
  overlap telemetry: summary or recent as required by the gate
  overlap consumer delay: disabled for readiness gates
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
```

Recorded result:

```text
41 passed, 0 failed, 0 skipped.
```

### 2. Retained Resource Pressure Contracts

Introduce a stable vocabulary for pending, active, and combined retained
resource pressure.

Candidate types:

```text
RadarProcessingRetainedResourcePressureSummary
RadarProcessingRetainedResourcePressureRecorder
RadarProcessingRetainedResourcePressureSnapshot
```

Candidate summary fields:

```text
PendingRetainedBatchCountHighWatermark
PendingRetainedPayloadBytesHighWatermark
ActiveRetainedBatchCountHighWatermark
ActiveRetainedPayloadBytesHighWatermark
CombinedRetainedBatchCountHighWatermark
CombinedRetainedPayloadBytesHighWatermark
CurrentPendingRetainedBatchCount
CurrentPendingRetainedPayloadBytes
CurrentActiveRetainedBatchCount
CurrentActiveRetainedPayloadBytes
CurrentCombinedRetainedBatchCount
CurrentCombinedRetainedPayloadBytes
```

Required behavior:

```text
all counts and byte values reject negative input
combined current values equal pending current plus active current
combined high-water updates whenever pending or active state changes
active count is not assumed to be one, even though milestone 011 still uses one
  rebalance-enabled consumer
zero/default summary is valid
summary is immutable once created
recorder operations are thread-safe where they are used across producer and
  consumer tasks
```

Expected tests or checks:

```text
default summary has zero current and high-water values
constructor rejects negative values
recorder increments pending on enqueue acceptance
recorder moves pending to active on dequeue/consumer acquisition
recorder releases active on final release
combined high-water captures overlap of pending and active retained resources
active release without active acquisition is rejected or reported explicitly
```

Guardrail:

```text
This slice defines pressure accounting only. It should not reinterpret existing
queue telemetry fields or change benchmark output yet.
```

Slice 2 implementation capture:

```text
status:
  complete

runtime integration:
  not started; this slice adds contracts only

new contracts:
  RadarProcessingRetainedResourcePressureSnapshot
  RadarProcessingRetainedResourcePressureSummary
  RadarProcessingRetainedResourcePressureRecorder

summary behavior:
  current pending retained batch/byte counts
  current active retained batch/byte counts
  computed current combined pending-plus-active batch/byte counts
  pending retained batch/byte high-water marks
  active retained batch/byte high-water marks
  combined retained batch/byte high-water marks
  immutable current snapshot projection
  zero/default empty summary

recorder behavior:
  AddPending()
  RemovePending()
  MovePendingToActive()
  RemoveActive()
  CreateSnapshot()
  CreateSummary()
  thread-safe state transitions through a private lock

guardrails:
  negative counts and bytes are rejected
  operation batch counts must be positive
  high-water marks cannot be lower than current values
  combined high-water marks cannot be lower than pending or active high-water
  pending and active removals cannot underflow batch count or payload bytes
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingRetainedResourcePressureContractTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRetainedResourcePressureContractTests|FullyQualifiedName~RadarProcessingRetainedPayloadContractTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
6 passed, 0 failed, 0 skipped for retained-resource pressure contracts.
11 passed, 0 failed, 0 skipped for retained-resource pressure plus retained
payload contracts.
710 passed, 0 failed, 3 skipped for the full test project.
```

### 3. Provider Queue Telemetry Compatibility Extensions

Extend existing queue telemetry so old pending-byte fields remain compatible
while new pressure fields are explicit.

Candidate focus:

```text
RadarProcessingProviderQueueTelemetrySummary
RadarProcessingProviderQueueTelemetryRecorder
RadarProcessingOwnedBatchQueue
RadarProcessingProviderQueueContractTests
RadarProcessingProviderQueueTelemetryRecorderTests
RadarProcessingOwnedBatchQueueTests
```

Required behavior:

```text
QueuedPayloadBytesHighWatermark remains the pending queue high-water field
RetainedPayloadBytesHighWatermark remains compatible with milestone 010 output
  unless explicitly redirected by a documented compatibility alias
new pending retained fields make queue-only semantics explicit
active retained fields are present even when zero
combined retained fields are present even when equal to pending
existing constructors and call sites remain source-compatible where practical
telemetry recent details remain bounded and do not retain payload data
```

Expected tests or checks:

```text
existing queue telemetry summary construction still works
pending retained high-water equals existing queued payload byte high-water for
  queue-only flows
active and combined fields default to zero/queue-only values before runtime
  integration
invalid negative active or combined fields are rejected
CLI and benchmark tests that assert existing output labels still pass or are
  updated with explicit compatibility wording
```

Guardrail:

```text
Do not remove or silently rename milestone 009/010 fields. Readiness telemetry
adds explicit fields; it does not break historical comparison fields.
```

Slice 3 implementation capture:

```text
status:
  complete

runtime active accounting:
  not started; active retained pressure remains zero until slice 4

extended contract:
  RadarProcessingProviderQueueTelemetrySummary now carries
  RadarProcessingRetainedResourcePressureSummary through
  RetainedResourcePressure

new queue telemetry fields:
  CurrentPendingRetainedBatchCount
  CurrentPendingRetainedPayloadBytes
  PendingRetainedBatchCountHighWatermark
  PendingRetainedPayloadBytesHighWatermark
  CurrentActiveRetainedBatchCount
  CurrentActiveRetainedPayloadBytes
  ActiveRetainedBatchCountHighWatermark
  ActiveRetainedPayloadBytesHighWatermark
  CurrentCombinedRetainedBatchCount
  CurrentCombinedRetainedPayloadBytes
  CombinedRetainedBatchCountHighWatermark
  CombinedRetainedPayloadBytesHighWatermark

compatibility behavior:
  QueuedPayloadBytesHighWatermark remains the queue-only pending byte high-water
    field
  RetainedPayloadBytesHighWatermark remains a milestone 010 compatibility alias
    to QueuedPayloadBytesHighWatermark
  when no explicit RetainedResourcePressure is supplied, queue-only pressure is
    derived from QueueDepthHighWatermark and QueuedPayloadBytesHighWatermark
  RadarProcessingOwnedBatchQueue.CreateTelemetrySummary() now supplies current
    pending count/bytes plus queue-only pending and combined high-water marks
  active count/bytes and active high-water marks remain zero in this slice
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingProviderQueueContractTests|FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingRetainedResourcePressureContractTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
36 passed, 0 failed, 0 skipped for focused provider queue pressure coverage.
710 passed, 0 failed, 3 skipped for the full test project.
```

### 4. Active Consumer Resource Lifecycle Integration

Wire active retained-resource accounting around dequeue, processing, and final
release.

Candidate focus:

```text
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingOwnedBatchQueue
RadarProcessingArchiveRebalanceBenchmark
RadarProcessingArchiveQueuedOverlapRunner
RadarProcessingQueuedRebalanceSession
RadarProcessingRetainedBatchResource
RadarProcessingRetainedBatchLease
```

Implementation direction:

```text
pending retained bytes increase only after accepted enqueue
pending retained bytes decrease when a retained batch is acquired by the
  consumer, not when the resource is finally released
active retained bytes increase before processing starts
active retained bytes decrease after processing, validation, telemetry, and
  release complete
release failure keeps the failure visible and fails readiness
pending cleanup on producer/consumer fault records releases without pretending
  the batch became active
```

Possible API shape:

```text
ArchiveOwnedRadarEventBatchQueueingPublisher.AcquireConsumerResource(sequence)
  -> transfers the retained resource from pending to active
  -> returns a lease or handle that releases exactly once

ArchiveOwnedRadarEventBatchQueueingPublisher.ReleaseConsumerResource(sequence)
  -> remains available as compatibility wrapper if practical
  -> should not hide active-state timing as a zero-duration interval
```

Required behavior:

```text
normal queued-owned non-overlap path records active retained high-water
producer-consumer overlap path records active retained high-water
consumer-delay controlled path records combined pressure while producer queues
  ahead and consumer holds active resources
producer failure releases pending resources and records cleanup
consumer failure releases active resource and pending resources
cancellation releases accepted retained resources
release failures increment failed-release telemetry and do not become success
```

Expected tests or checks:

```text
single dequeued pooled-copy batch records active high-water equal to its payload
bytes
two queued batches with one active batch record combined high-water equal to
pending plus active bytes
consumer exception releases the active resource and reports failure status
producer exception releases pending resources
canceled drain releases accepted retained resources
release failure is visible in retention telemetry and pressure state is
terminal
```

Guardrail:

```text
Do not process multiple rebalance-enabled batches concurrently. Active
accounting should describe the current ordered consumer, not introduce a wider
scheduler.
```

Implemented in slice 4:

```text
ArchiveOwnedRadarEventBatchQueueingPublisher now owns a retained-resource
pressure recorder for accepted retained resources
accepted enqueue adds pending pressure after the queue accepts the batch
pressure bytes come from the retained queued batch payload length, so
  not-required release handles still report active batch pressure
consumer acquisition removes the sequence from pending resources, transfers
the retained resource to consumer ownership, and moves pressure from pending to
active
consumer leases release exactly once and remove active pressure after release
pending cleanup releases still-queued resources and removes pending pressure
queued processing/rebalance sessions accept an optional consumer-resource
lease factory and wrap every dequeued batch, including skipped-after-fault
batches
RunRebalanceAsync passes publisher leases into the queued rebalance session
overlap runner final queue telemetry carries provider pressure from the final
publisher result
benchmark queued-owned drains acquire retained-resource leases before
processing and release them after processing/cancellation/failure
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
35 passed, 0 failed, 0 skipped for focused active retained-resource lifecycle
coverage.
713 passed, 0 failed, 3 skipped for the full test project.
```

### 5. Overlap Telemetry And Allocation Attribution Propagation

Carry retained-resource pressure into overlap telemetry and benchmark result
objects.

Candidate focus:

```text
RadarProcessingArchiveOverlapTelemetrySummary
RadarProcessingArchiveQueuedOverlapResult
RadarProcessingArchiveQueuedOverlapProducerResult
RadarProcessingArchiveQueuedOverlapConsumerResult
RadarProcessingArchiveRebalanceBenchmarkResult
RadarProcessingArchiveRebalanceCacheBenchmarkResult
RadarProcessingRebalanceAllocationSummary
```

Required behavior:

```text
overlap telemetry exposes pending retained bytes high-water
overlap telemetry exposes active retained bytes high-water
overlap telemetry exposes combined retained bytes high-water
overlap telemetry exposes pending, active, and combined retained batch
  high-water marks
existing RetainedPayloadBytesHighWatermark remains understandable in output
unattributed allocation calculation remains comparable with milestone 010
snapshot-copy and pooled-copy retained allocation fields remain comparable
file and cache benchmark result shapes carry the same readiness fields
```

Expected tests or checks:

```text
overlap telemetry summary rejects negative active/combined fields
FromOverlapResult carries pressure summary from queue/provider telemetry
cache benchmark result preserves active/combined pressure fields
file benchmark result preserves active/combined pressure fields
allocation summary remains unchanged for blocking-borrowed runs
existing overlap telemetry tests are updated to assert new fields
```

Guardrail:

```text
Do not fold active retained bytes into retained allocation. Allocation and live
retained pressure are different metrics.
```

Implemented in slice 5:

```text
RadarProcessingArchiveOverlapTelemetrySummary now exposes the retained-resource
pressure summary plus pending, active, and combined current/high-water fields
it keeps RetainedPayloadBytesHighWatermark as the milestone 010 queue-only
compatibility alias
RadarProcessingArchiveQueuedOverlapResult exposes direct retained pressure
high-water accessors for overlap result consumers
RadarProcessingArchiveRebalanceBenchmarkResult and
RadarProcessingArchiveRebalanceCacheBenchmarkResult expose the same direct
pending/active/combined retained pressure fields carried by QueueTelemetry
overlap telemetry preserves the exact retained pressure summary from the final
queue/provider telemetry
file and cache benchmark tests assert pressure propagation through result
shapes without changing allocation attribution
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
53 passed, 0 failed, 0 skipped for focused overlap/benchmark retained pressure
propagation coverage.
713 passed, 0 failed, 3 skipped for the full test project.
```

### 6. Candidate Configuration Surface

Make the milestone 011 default-candidate contour reproducible without changing
the current default provider mode.

Candidate focus:

```text
RadarPulseCli rebalance benchmark option parsing
ProcessingBenchmarkOptions
RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
RadarPulseCliRebalanceBenchmarkTests
```

Candidate contour:

```text
--provider queued-owned
--provider-overlap producer-consumer
--retention-strategy pooled-copy
--execution async
--queue-capacity explicit, initially 8 for comparison with milestone 010
--queue-retained-bytes explicit, initially 536870912 for comparison with
  milestone 010 controlled gates
--queue-telemetry summary or recent as required by the gate
--overlap-telemetry summary or recent as required by the gate
--overlap-consumer-delay-ms disabled
```

Possible CLI direction:

```text
option A:
  keep explicit flags only and print an effective "default-candidate: yes/no"
  label when the exact contour is selected

option B:
  add a named benchmark profile that expands to the explicit flags while still
  printing every effective value
```

Required behavior:

```text
blocking-borrowed remains the default when no provider option is supplied
candidate selection is explicit and reproducible
effective configuration output includes provider, retention strategy, overlap,
  execution, queue capacity, retained-byte budget, telemetry levels, consumer
  delay, validation profile, workers, partitions, shards, and input selection
consumer-delay proof tooling cannot be active in a readiness candidate profile
builder-transfer remains rejected for execution
unsafe or ambiguous option combinations remain rejected early
```

Expected tests or checks:

```text
default CLI options still select blocking-borrowed and snapshot-copy
explicit candidate flags parse to queued-owned pooled-copy producer-consumer
effective configuration output prints the candidate contour fields
candidate profile, if implemented, expands to explicit option values
candidate profile rejects controlled consumer delay
provider overlap still requires queued-owned
queue retained bytes still require queued-owned
builder-transfer still reports unsupported execution
```

Guardrail:

```text
Do not change the default provider, default overlap, or default retention
strategy globally in this slice.
```

Implemented in slice 6:

```text
the CLI keeps explicit candidate flags only; no named profile or runtime default
change was introduced
ProcessingBenchmarkArchiveRebalanceOptions exposes IsDefaultCandidateContour,
IsControlledProviderOverlapProof, and ProviderOverlapEvidenceContour
the exact default-candidate contour is queued-owned producer-consumer
pooled-copy async, queue capacity 8, retained-byte budget 536870912,
non-none queue/overlap telemetry, and no controlled consumer delay
archive rebalance file/cache output now prints Default-candidate contour and
Provider overlap evidence contour
controlled delay producer-consumer runs are labeled controlled-proof, exact
natural candidate runs are labeled natural-default-candidate, and unrelated
contours are labeled not-applicable
CLI tests cover option parsing, controlled-proof labeling, and exact candidate
output without changing blocking-borrowed/snapshot-copy defaults
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
39 passed, 0 failed, 0 skipped for focused CLI/archive candidate surface
coverage.
715 passed, 0 failed, 3 skipped for the full test project.
```

### 7. Readiness Validation And Gate Contracts

Define how a candidate run is judged beyond raw throughput.

Candidate types:

```text
RadarProcessingQueuedProviderReadinessStatus
RadarProcessingQueuedProviderReadinessResult
RadarProcessingQueuedProviderReadinessError
RadarProcessingQueuedProviderReadinessGate
```

Candidate readiness dimensions:

```text
correctness parity
topology and rebalance parity
retained resource release health
pending/active/combined retained memory bounds
allocation movement versus snapshot-copy where available
performance delta versus same-run blocking-borrowed
run-to-run variance interpretation
effective configuration reproducibility
controlled-proof exclusion
```

Required behavior:

```text
readiness gate can pass correctness and fail memory/performance independently
failed release fails readiness even when checksums match
missing active retained telemetry fails or warns readiness according to an
  explicit status
controlled consumer delay marks a run as controlled, not natural
blocking-borrowed reference is required for default-readiness conclusions where
  practical
status/result objects preserve enough message text for docs and CLI reporting
```

Expected tests or checks:

```text
readiness result can represent passed, failed, inconclusive, and not evaluated
release failure produces failed readiness
missing borrowed reference produces inconclusive default-readiness result
controlled-delay run cannot pass natural readiness
combined retained high-water over configured budget fails memory dimension
checksum or rebalance mismatch fails correctness dimension
```

Guardrail:

```text
This gate interprets evidence. It must not mutate runtime provider defaults or
silently rerun failed queued-owned work through the borrowed path.
```

Implemented in slice 7:

```text
RadarProcessingQueuedProviderReadinessStatus defines passed, failed,
inconclusive, and not-evaluated readiness outcomes
RadarProcessingQueuedProviderReadinessGate identifies independent readiness
dimensions for correctness parity, topology/rebalance parity, release health,
retained pressure, allocation movement, performance delta, variance,
configuration, and natural evidence
RadarProcessingQueuedProviderReadinessError preserves explicit failure and
inconclusive reasons for borrowed-reference absence, validation mismatch,
release failure, cleanup incompleteness, missing active pressure telemetry,
combined retained payload budget excess, controlled-proof exclusion,
candidate-contour mismatch, performance regression, variance, and allocation
regression
RadarProcessingQueuedProviderReadinessResult carries gate status, message text,
checksum/count/byte/ratio diagnostics, and rejects invalid result shapes
RadarProcessingQueuedProviderReadinessEvaluator interprets existing queued
provider validation results, retained payload release telemetry, retained
resource pressure summaries, natural-vs-controlled evidence, and same-run
borrowed performance deltas, allocation movement ratios, and repeated-run
variance without changing provider runtime behavior
readiness dimensions are independent, so correctness can pass while memory,
release, or performance gates fail
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingQueuedProviderValidatorTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
30 passed, 0 failed, 0 skipped for focused queued-provider readiness gate and
validator coverage.
726 passed, 0 failed, 3 skipped for the full test project.
```

### 8. Failure, Cancellation, And Cleanup Gate

Strengthen focused tests around failure paths before relying on retained
resource pressure numbers.

Candidate focus:

```text
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingArchiveQueuedOverlapRunner
RadarProcessingArchiveRebalanceBenchmark
RadarProcessingRetainedBatchResource
RadarProcessingRetainedResourceCleanupResult
RadarProcessingArchiveQueuedOverlapRunnerTests
ArchiveOwnedRadarEventBatchQueueingPublisherTests
RadarProcessingRetainedBatchResourceTests
```

Required behavior:

```text
retention failure stops intake and releases any accepted retained resources
processing failure faults the queued session and releases active resource
validation failure prevents later success claims
migration failure preserves existing failed-migration semantics
producer failure releases pending resources
consumer failure releases active and pending resources
cancellation closes intake and releases accepted resources deterministically
release failure remains visible in release telemetry and readiness result
```

Expected tests or checks:

```text
consumer processing exception releases active resource
producer exception releases pending resources and faults queue
cancellation after accepted enqueue releases retained resource
release callback exception increments failed release count
failed release does not report candidate readiness success
pending and active current pressure returns to zero after terminal cleanup
```

Guardrail:

```text
Failure-path cleanup should be tested with small synthetic batches. Do not make
Release performance gates depend on injected fault behavior.
```

### 9. CLI And Operator Telemetry Output

Print readiness-critical fields in a stable, bounded, operator-readable shape.

Candidate focus:

```text
Program.cs queue telemetry output
Program.cs overlap telemetry output
RadarPulseCliRebalanceBenchmarkTests
```

Required output fields:

```text
provider mode
retained payload strategy
provider overlap mode
queue capacity
retained-byte budget
provider queue pending retained bytes high watermark
provider queue active retained bytes high watermark
provider queue combined retained bytes high watermark
provider overlap pending retained bytes high watermark
provider overlap active retained bytes high watermark
provider overlap combined retained bytes high watermark
active and combined retained batch high-water marks
provider blocked elapsed
consumer idle elapsed
release attempts, released batches, not-required releases, failed releases
same-run borrowed comparison fields where available
candidate/natural/controlled labels where applicable
```

Required behavior:

```text
summary telemetry prints the new pressure fields
recent telemetry remains bounded
none telemetry suppresses optional details but does not make readiness gates
  claim success without required fields
controlled consumer-delay output is labeled as controlled proof
candidate contour output prints every effective option needed to reproduce it
```

Expected tests or checks:

```text
CLI emits active and combined retained pressure fields for queued-owned overlap
CLI preserves existing milestone 010 output labels where compatibility matters
CLI labels controlled consumer-delay runs clearly
CLI default run still does not print queued-owned-only telemetry as if it were
  active
```

Guardrail:

```text
Do not add unbounded per-batch output to default summary telemetry. Use recent
mode for bounded details.
```

### 10. Natural Release Gate Matrix

Capture repeated natural Release gates using the default-candidate contour and
same-run borrowed reference.

Candidate document:

```text
docs/milestones/011-queued-owned-default-readiness-performance-gate.md
```

Candidate commands:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --provider queued-owned --provider-overlap producer-consumer --retention-strategy pooled-copy --execution async --workers 4 --queue-capacity 8 --queue-retained-bytes 536870912 --queue-telemetry summary --overlap-telemetry summary --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

The exact command matrix can be adjusted for local data availability, but the
gate should capture:

```text
same-run blocking-borrowed async reference
same-run queued-owned pooled-copy producer-consumer candidate
current KTLX 2026-05-04 contour used by milestone 010
larger local cache contour where available
at least one different radar/date shape where local data exists
repeated rows enough to show variance instead of one best run
controlled consumer delay disabled
```

Required interpretation:

```text
published file count, payload value count, checksum, accepted moves, skipped
  decisions, failed migrations, and validation status must match expectations
candidate end-to-end time is compared against same-run borrowed reference
candidate active/combined retained pressure is reported with the retained-byte
  budget
release failures must be zero for readiness
run-to-run spread is reported before making any default-readiness claim
natural queue depth greater than 1 is not required by itself
controlled proof rows are kept in a separate section if captured
```

Expected tests or checks:

```text
Release build succeeds before gate capture
focused CLI/overlap tests pass before gate capture
full test suite passes before closeout if feasible
performance gate document records commands, inputs, output totals, timing,
memory pressure, allocation, release health, and interpretation
```

Guardrail:

```text
Do not close the milestone with a single favorable row. The gate must separate
natural readiness evidence from controlled mechanics proof and from run-to-run
variance.
```

### 11. Controlled Proof Separation Hardening

Keep controlled queue-ahead proof available while preventing it from being
mistaken for natural readiness evidence.

Candidate focus:

```text
RadarProcessingArchiveRebalanceBenchmark
RadarProcessingArchiveOverlapTelemetrySummary
Program.cs option validation and output
RadarPulseCliRebalanceBenchmarkTests
011 performance gate document
```

Required behavior:

```text
consumer delay remains disabled by default
consumer delay remains rejected outside queued-owned producer-consumer contours
consumer delay marks the run as controlled in output or gate interpretation
readiness gate treats controlled rows as mechanics proof only
controlled rows may still validate queue capacity, retained-byte backpressure,
  active pressure accounting, and release cleanup
```

Expected tests or checks:

```text
controlled consumer delay still parses and runs only for allowed contours
controlled run output identifies the delay value
readiness gate or documentation does not include controlled rows in natural
  performance aggregates
```

Guardrail:

```text
Controlled queue-ahead proof may demonstrate mechanics. It must not justify a
provider default switch.
```

### 12. Decision Trace, Closeout, And Handoff

Capture the final milestone assessment.

Candidate documents:

```text
docs/milestones/011-queued-owned-default-readiness-performance-gate.md
docs/milestones/011-queued-owned-default-readiness-decision-trace.md
docs/milestones/011-queued-owned-default-readiness-closeout.md
docs/handoff.md
```

Required closeout decisions:

```text
whether the optimized contour is ready to propose as a future default
whether queued-owned remains opt-in
whether more telemetry or optimization is required
whether retained memory pressure is bounded enough under measured contours
whether release health and failure cleanup are strong enough for production
whether the next milestone should be default rollout, optimization, or a
  different production-readiness slice
```

Expected tests or checks:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
focused CLI/overlap/readiness tests pass
performance gate captures Release commands and results
decision trace records default-readiness conclusion
handoff points at milestone 011 architecture, plan, gate, decision trace, and
  closeout when complete
```

Guardrail:

```text
Do not mark queued-owned as the default in closeout. If the evidence supports a
default switch, record that as next milestone input for an explicit rollout
decision.
```

## Verification Strategy

Use focused tests after each implementation slice and broader verification
before the performance gate.

Expected focused coverage:

```text
retained-resource pressure contract tests
provider queue telemetry summary tests
owned batch queue retained pressure tests
queueing publisher active-resource lifecycle tests
archive queued overlap runner telemetry tests
archive rebalance benchmark result propagation tests
queued provider readiness gate tests, if gate contracts are implemented
CLI option/output tests
failure and cancellation cleanup tests
```

Expected broad checks:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Expected performance gate:

```text
Release build before measurements
same-run blocking-borrowed async reference
same-run queued-owned pooled-copy producer-consumer candidate
active/combined retained pressure visible in output
repeated natural rows and variance interpretation
controlled proof rows separated if captured
```

## Completion Checklist

```text
[x] baseline readiness audit is captured
[x] default-candidate contour is frozen and reproducible
[x] retained-resource pressure contracts are implemented and tested
[x] provider queue telemetry exposes pending, active, and combined retained
    pressure without breaking milestone 010 fields
[x] active consumer retained-resource accounting is wired into queued drains
[x] pending cleanup and active release paths update pressure and release
    telemetry correctly
[x] overlap telemetry carries active and combined retained pressure
[x] archive benchmark file/cache result shapes carry readiness pressure fields
[ ] CLI output prints active and combined retained pressure fields
[x] candidate configuration output is explicit and reproducible
[x] readiness validation/gate contracts are implemented or documented
[ ] failure and cancellation cleanup paths are tested
[x] controlled consumer-delay proof is labeled and separated from natural gates
[ ] repeated natural Release gate matrix is captured
[ ] performance gate interprets correctness, allocation, memory pressure,
    cleanup, throughput, and variance
[ ] decision trace is written
[ ] closeout is written
[ ] handoff is updated for the next milestone
```

## Non-Goals

Milestone 011 does not implement:

```text
queued-owned as the default provider mode
builder-transfer retained payload execution
durable queue or broker integration
live ingestion
cross-process provider or worker transport
multiple active rebalance-enabled processing batches
ordered concurrent rebalance commit barrier
source-level migration or partition splitting
physical worker-local state transfer
complex radar algorithms
visualization or product-facing radar analysis features
```

## Closeout Question

The milestone closes by answering:

```text
Is queued-owned + pooled-copy + producer-consumer ready to be proposed as the
next default provider mode?
```

The acceptable answers are:

```text
yes:
  evidence supports a later explicit default rollout milestone

not yet:
  optimized queued-owned remains opt-in, with named blockers from telemetry,
  variance, memory pressure, cleanup, or performance

no for this contour:
  the candidate should be narrowed, optimized, or replaced before another
  default-readiness pass
```

If the answer is yes, the next milestone should be a controlled default rollout
decision, not an implicit default change inside milestone 011. If the answer is
not yet or no, the next milestone should target the measured blocker.
