# Milestone 015: Queued-Owned Allocation Readiness Implementation Plan

Status: in progress.

This plan implements the milestone 015 architecture defined in
`015-queued-owned-allocation-readiness.md`.

The plan is intentionally scoped to allocation readiness for the already
accepted queued-owned direct/default archive rebalance contour. It should not
implement live ingestion, durable broker integration, cross-process workers,
ordered concurrent rebalance, builder-transfer, synthetic benchmark defaults,
or non-benchmark archive publishing defaults.

## Goal

Milestone 015 decides whether the queued-owned direct/default allocation
profile is ready for the next broader benchmark or runtime-default decision.

The accepted direct/default contour remains:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async shard transport
worker count: 4
async worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536870912
overlap consumer delay: 0
```

The milestone target is not to change that contour. The target is to reduce,
bound, or deliberately accept the allocation warning with stronger attribution.

The most important rules are:

```text
preserve direct MeasureFile()/MeasureCache() queued-owned omitted defaults
preserve explicit BlockingBorrowed direct calls as fallback and oracle
keep same-run borrowed rows in allocation gates
keep CLI omitted-provider rollout contour aligned with direct defaults
carry KTLX 2026-05-05 allocation warning visibly through the gate
do not silently fall back from queued-owned failure to borrowed success
do not tune the accepted rollout contour to make allocation pass
do not broaden into live/runtime/durable surfaces
do not raise thresholds after seeing gate measurements
```

Milestone 015 also has an explicit optimization research requirement:

```text
use the best standard .NET allocation-reduction practices where they fit the
  existing codebase

actively search for, investigate, prototype, and evaluate non-standard or
  experimental allocation-reduction approaches where standard practice is not
  enough

accept an experimental approach only if it preserves lifetime, ownership,
  cleanup, release, correctness, attribution, and maintainability guardrails
```

The implementation target is deliberately evidence-driven:

```text
capture current allocation attribution before optimizing
identify the highest-confidence retained/owned and processing-callback costs
apply low-risk standard optimizations first
run bounded experimental spikes for harder allocation sources
promote only the safest measured improvement into production code
prove deterministic output and retained-resource health after each accepted
  change
record why rejected approaches were not used
capture a Release allocation-readiness gate before deciding the milestone
```

## Starting Point

Milestone 014 is complete and provides:

```text
direct MeasureFile()/MeasureCache() omitted defaults:
  queued-owned rollout contour

explicit fallback/oracle:
  providerMode: BlockingBorrowed

CLI omitted-provider rebalance-archive path:
  aligned with the same queued-owned rollout contour

allocation warning:
  KTLX 2026-05-05 averaged 1.0997x borrowed allocation
  row 1 was 1.1018x borrowed
  row 2 was 1.0976x borrowed
  threshold was <= 1.10x borrowed
  warning accepted for direct API migration, not clean green
```

Milestone 014 gate facts to carry forward:

```text
primary KTLX 2026-05-04:
  elapsed ratio: 0.911x borrowed
  allocation ratio: 1.071x borrowed
  all-row direct-default timing spread: 10.41%
  stabilized rows 2-4 timing spread: 0.39%

KTLX 2026-05-05:
  elapsed ratio: 0.960x borrowed average
  allocation ratio: 1.0997x borrowed average
  run 1 allocation ratio: 1.1018x borrowed
  run 2 allocation ratio: 1.0976x borrowed

KINX 2026-05-04:
  elapsed ratio: 0.906x borrowed
  allocation ratio: 1.069x borrowed

mixed cache:
  elapsed ratio: 0.878x borrowed
  allocation ratio: 1.066x borrowed

retained pressure:
  max observed combined retained payload high-water 54413280 bytes of the
  536870912 byte budget

release and cleanup:
  release failures 0
  current pending, active, and combined retained pressure returned to 0
```

Current attribution hypothesis:

```text
direct default allocation overhead is concentrated in processing callback
allocation and retained/owned snapshot work
```

Known local gate data availability from prior milestones:

```text
data\nexrad\level2\2026\05\04\KINX: 462 files
data\nexrad\level2\2026\05\04\KTLX: 244 files
data\nexrad\level2\2026\05\05\KTLX: 848 files
data\nexrad total files: 1554
```

## Scope

The implementation should explicitly scope milestone 015 to:

```text
queued-owned direct/default allocation baseline audit
allocation attribution audit
processing callback allocation reduction
retained/owned snapshot allocation reduction
safe queue, overlap, and telemetry allocation reduction
standard allocation optimization pass
experimental allocation optimization research and spikes
direct file/cache result contour assertions
direct explicit BlockingBorrowed fallback coverage
direct explicit queued-owned rollout equivalence coverage
CLI/direct rollout contour alignment checks
focused failure, cleanup, fallback, and default drift coverage
natural Release gate documentation for allocation readiness
decision trace, closeout, and handoff
```

The implementation should not change:

```text
queued-owned rollout defaults
synthetic processing benchmark defaults
archive download, inspection, replay-only, stream, parse, or publish defaults
non-benchmark archive publishing APIs
domain enum numeric values
retained payload factory strategy semantics
queued session ordering and rebalance commit rules
worker topology capture timing
automatic fallback semantics
live ingestion/runtime provider defaults
```

## Optimization Research Requirement

Milestone 015 should not limit allocation work to obvious local cleanup. It
must include both conventional engineering and deliberate research.

Standard practices to consider first:

```text
remove avoidable defensive copies
remove hot-path closures, iterator allocations, LINQ allocation, and delegate
  captures where direct loops fit the local style
pre-size collections when the final count is known
replace per-batch temporary collections with counters or reusable summaries
use ArrayPool<T> where ownership, clearing, and return timing are explicit
avoid boxing, interface dispatch allocation, and accidental record copying in
  measured hot paths
use readonly structs or existing immutable summaries where they reduce heap
  allocation without making contracts brittle
avoid repeated allocation snapshot capture when a coarser measurement gives
  the same attribution evidence
keep telemetry contracts stable while making recorder internals cheaper
```

Non-standard or experimental approaches to investigate:

```text
batch-scoped scratch arenas for short-lived retained/owned helper state
specialized retained payload wrappers that avoid closure-backed release
  callbacks while preserving release accounting
pooled telemetry accumulator instances with explicit reset/return ownership
struct-backed queue items or lightweight handles where the queue can preserve
  sequence and ownership without extra heap objects
source-local or contour-local allocation probes to split callback allocation
  into more precise buckets
feature-gated prototypes for alternate retained resource representation
micro-harnesses that isolate retained-copy, queue-enqueue, overlap, and
  processing-callback allocation from archive replay allocation
```

Experimental approaches must follow these rules:

```text
prototype in a narrow slice before changing production behavior broadly
keep the old behavior available until tests and measurements justify removal
prove release and cleanup on success, failure, cancellation, validation
  failure, and queue acceptance failure
do not use unsafe memory, stack lifetime tricks, or pooled mutable state unless
  the ownership model is simpler than the allocation it removes
do not ship an experiment that depends on undocumented timing or GC behavior
do not hide an allocation warning by moving allocation out of the measured
  window unless the measurement contract is explicitly changed and justified
record rejected experiments in the plan or decision trace with the reason
```

Expected research outputs:

```text
short list of accepted standard optimizations
short list of experimental approaches investigated
decision for each experiment: adopt, defer, or reject
measurement or code-reading evidence for each adopted approach
explicit statement that lifetime and release contracts remain intact
```

## Readiness Thresholds

Milestone 015 starts from the milestone 014 thresholds. The plan should not
change them after any new gate measurements are captured.

Thresholds:

```text
correctness parity:
  required, same-run borrowed reference must match

topology/rebalance parity:
  required, topology versions, accepted moves, skipped decisions, failed
  migrations, validation checksum, and skipped reason counters must match

release failures:
  retained payload failed releases and provider overlap failed releases must
  equal 0

retained cleanup:
  current pending, active, and combined retained batch/byte counts must return
  to 0 at completion

retained pressure:
  combined retained payload high-water must be <= 536870912 bytes unless the
  configured retained-byte budget is explicitly changed in a future contour

allocation:
  queued-owned direct/default allocated bytes should remain <= 1.10x same-run
  borrowed allocated bytes; KTLX 2026-05-05 remains the named borderline risk
  contour and must be interpreted explicitly

preferred allocation readiness:
  each repeated KTLX 2026-05-05 row should be below 1.10x borrowed, not only
  the average

strong allocation readiness:
  KTLX 2026-05-05 average should move materially away from 1.10x rather than
  remain another borderline value

performance:
  queued-owned direct/default elapsed time should remain <= 1.00x same-run
  borrowed elapsed time on the primary repeated natural matrix

variance:
  repeated natural direct/default spread should remain <= 7.50% of candidate
  average, or the decision trace must explain why the spread does not block
  the allocation conclusion

natural evidence:
  overlap consumer delay must be 0 and evidence scope must remain natural
  readiness evidence, not controlled mechanics proof
```

Guardrail:

```text
Do not change thresholds after seeing final gate measurements. If thresholds
need to change, record the reason before interpreting the gate.
```

## Target Implementation Shape

The safest implementation shape is to start with attribution, then apply
bounded changes to the highest-confidence allocation source.

Candidate layering:

```text
Domain:
  owns allocation summary contracts, retained resource contracts, readiness
  vocabulary, pressure summaries, and queued provider validation

Infrastructure:
  owns archive rebalance benchmark execution, retained payload factory,
  queueing, overlap runner, and direct result construction

Presentation:
  owns CLI option parsing, help, output formatting, and option provenance

Tests:
  own deterministic contour assertions, allocation contract assertions,
  retained release/cleanup guardrails, and failure/cancellation coverage

Documentation:
  owns baseline interpretation, gate results, decision trace, closeout, and
  handoff posture
```

Implementation guidance:

```text
prefer simple local hot-path changes before broad abstraction
use structured allocation summary fields instead of parsing output text
keep direct defaults and CLI rollout constants pinned against drift
avoid changing result contract shape unless current attribution is too coarse
avoid pooling if the pool ownership story is harder to review than the saved
  allocation
add abstractions only when they reduce real allocation or clarify lifetime
record any intentionally rejected allocation idea before closeout
```

Primary files to inspect:

```text
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Infrastructure/Processing/RadarProcessingRetainedPayloadFactory.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapRunner.cs
src/Infrastructure/Processing/RadarProcessingOwnedBatchQueue.cs
src/Infrastructure/Processing/RadarProcessingQueuedProcessingSession.cs
src/Infrastructure/Processing/RadarProcessingQueuedRebalanceSession.cs
src/Infrastructure/Archive/ArchiveOwnedRadarEventBatchQueueingPublisher.cs
src/Domain/Processing/RadarProcessingRebalanceAllocationSummary.cs
src/Domain/Processing/RadarProcessingOwnedSnapshotAllocationSummary.cs
src/Domain/Processing/RadarProcessingRetainedBatchResource.cs
src/Domain/Processing/RadarProcessingRetainedPayloadRetentionResult.cs
src/Domain/Processing/RadarProcessingRetainedPayloadReleaseResult.cs
src/Domain/Processing/RadarProcessingRetainedPayloadTelemetrySummary.cs
src/Domain/Processing/RadarProcessingProviderQueueTelemetrySummary.cs
src/Infrastructure/Processing/RadarProcessingRetainedPayloadByteArrayPool.cs
src/Infrastructure/Processing/RadarProcessingRetainedEventArrayPool.cs
src/Presentation/Program.cs
```

Primary tests to inspect or extend:

```text
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Archive/ArchiveOwnedRadarEventBatchQueueingPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceAllocationSummaryTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedPayloadFactoryTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedPayloadContractTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedBatchResourceTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingOwnedBatchQueueTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProviderReadinessGateTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

## Implementation Slices

### 1. Baseline And Attribution Audit

Capture the current allocation posture before changing code.

Questions to answer:

```text
which result fields currently separate end-to-end, processing callback,
  replay/build, owned snapshot, and non-owned snapshot allocation?
which queued-owned hot paths allocate per batch?
which retained/owned snapshot objects are unavoidable because of lifetime?
which allocations are visible in unit tests versus only in Release gates?
which attribution fields are too coarse for a before/after decision?
```

Files to inspect:

```text
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Infrastructure/Processing/RadarProcessingRetainedPayloadFactory.cs
src/Domain/Processing/RadarProcessingRebalanceAllocationSummary.cs
src/Domain/Processing/RadarProcessingOwnedSnapshotAllocationSummary.cs
src/Presentation/Program.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceAllocationSummaryTests.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
docs/milestones/014-direct-archive-rebalance-api-default-migration-performance-gate.md
```

Expected findings:

```text
allocation summary already separates measured, processing callback,
  replay/build, owned snapshot, and callback non-owned snapshot bytes
KTLX 2026-05-05 remains the named risk contour
retained/owned snapshot work is likely the first optimization target
processing callback non-owned snapshot allocation is the second target
```

Runtime behavior changes:

```text
none
```

Documentation updates:

```text
record baseline observations in this plan under the slice 1 completion notes
if implementation begins before a separate performance gate exists
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests
```

Implemented in slice 1:

```text
status: complete
runtime behavior changes: none

audited allocation/result contracts:
  RadarProcessingArchiveRebalanceBenchmark captures end-to-end measured
  allocation around measured iterations and builds
  RadarProcessingRebalanceAllocationSummary.ForArchiveReplay from measured
  allocated bytes, aggregate processing callback allocated bytes, and
  aggregate queue owned snapshot allocated bytes

  RadarProcessingArchiveRebalanceBenchmarkResult and
  RadarProcessingArchiveRebalanceCacheBenchmarkResult expose allocated bytes,
  processing callback allocated bytes, replay/build allocated bytes, owned
  snapshot allocated bytes, and processing callback non-owned snapshot
  allocated bytes

  RadarProcessingRebalanceAllocationSummary keeps derived allocation buckets
  non-negative and already has focused contract coverage in
  RadarProcessingRebalanceAllocationSummaryTests

  RadarProcessingOwnedSnapshotAllocationSummary exposes owned snapshot count,
  payload bytes, payload values, allocated bytes, elapsed time, and per-unit
  ratios from provider queue telemetry

  Program prints end-to-end, processing callback, replay/build, owned
  snapshot, and callback non-owned snapshot allocation fields for archive
  rebalance output

attribution sufficiency:
  current attribution is sufficient for the first standard optimization pass
  because it separates measured, processing callback, replay/build, owned
  snapshot, and callback residual allocation

  current attribution is not yet fine-grained enough to prove every residual
  source independently; retained resource wrappers/release callbacks, queued
  batch objects, queue recent-detail records, bounded recent-detail snapshots,
  and telemetry-summary copy/allocation are visible only through processing
  callback residual or aggregate measured allocation

hot-path targets for slice 2 and slice 3:
  RadarProcessingRetainedPayloadFactory.RetainPooledCopy rents event and
  payload arrays but creates a retained RadarEventBatch plus a
  closure-backed RadarProcessingRetainedBatchResource release callback for
  pooled retained batches

  RadarProcessingRetainedBatchResource.NotRequired currently constructs a
  stateful resource with a default release callback; that path should be
  audited before changing because state transitions are part of the contract

  ArchiveOwnedRadarEventBatchQueueingPublisher.Publish passes a capturing
  onAccepted callback into RadarProcessingOwnedBatchQueue.EnqueueAsync so the
  retained resource can be tracked after a sequence is assigned

  RadarProcessingOwnedBatchQueue creates one RadarProcessingQueuedBatch for
  each accepted queued-owned batch and records queue telemetry on accepted,
  rejected, dequeued, and processed events

  RadarProcessingProviderQueueTelemetryRecorder creates bounded recent-detail
  records, snapshots them into arrays, and
  RadarProcessingProviderQueueTelemetrySummary defensively copies recent
  details through a List plus array/read-only wrapper

  RadarProcessingArchiveRebalanceBenchmark.AddQueueTelemetry aggregates
  bounded recent details through Concat/Skip/ToArray; this is bounded but is a
  low-risk standard optimization candidate if it appears in callback residual

slice 2 input:
  keep current result contracts for the first pass unless code inspection or
  focused measurements show that callback residual needs to be split before a
  safe optimization decision can be made
```

### 2. Allocation Instrumentation And Contract Check

Decide whether current allocation attribution is sufficient or whether the
milestone needs more precise fields.

Implementation targets:

```text
keep current result contracts if they can prove before/after changes
add allocation categories only if processing callback and retained/owned
  snapshot allocation cannot be separated enough for a readiness decision
ensure any new fields have unit tests and CLI output where operator-visible
ensure allocation summaries remain non-negative and ratio helpers remain
  stable for zero denominators
```

Candidate new attribution, only if needed:

```text
retained payload resource wrapper allocated bytes
retained payload copy allocated bytes
provider queue item allocated bytes
overlap retention allocated bytes
telemetry recorder allocated bytes
processing callback residual allocated bytes
```

Guardrails:

```text
do not add noisy fields that cannot be interpreted in gates
do not make infrastructure depend on presentation formatting
do not change CLI output labels without updating CLI tests
```

Runtime behavior changes:

```text
none unless instrumentation changes measurement granularity
```

Expected tests:

```text
allocation summary contract tests for any new category
archive benchmark result contract tests for non-negative derived values
CLI output tests if a new field is printed
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
```

Implemented in slice 2:

```text
status: complete
runtime behavior changes: none
contract decision:
  do not add new public result contract fields or CLI output fields before
  the first standard optimization pass

why:
  RadarProcessingArchiveRebalanceBenchmarkResult and
  RadarProcessingArchiveRebalanceCacheBenchmarkResult already expose the
  allocation buckets needed for first-pass before/after interpretation:
  measured allocated bytes, processing callback allocated bytes, replay/build
  allocated bytes, owned snapshot allocated bytes, and processing callback
  non-owned snapshot allocated bytes

  RadarProcessingRebalanceAllocationSummary already keeps these derived
  buckets non-negative and has focused contract coverage

  Program already prints the same allocation attribution fields for archive
  rebalance output, and RadarPulseCliRebalanceBenchmarkTests already assert
  the operator-visible allocation attribution lines

  NexradArchiveRadarEventBatchPublisherTests already connect queued-owned
  result owned snapshot allocation to QueueTelemetry.OwnedSnapshotAllocatedBytes
  for direct file/cache result contracts

residual bucket decision:
  retained resource wrapper/release callback allocation, queued batch object
  allocation, recent-detail allocation, bounded recent-detail snapshot
  allocation, defensive telemetry-summary copy allocation, and
  AddQueueTelemetry Concat/Skip/ToArray allocation will remain inside
  processing callback residual for slice 3

  slice 3 should use code inspection and focused tests for these targets
  before adding permanent attribution fields

  slice 4 may add temporary micro-harnesses or permanent attribution only if
  standard optimization leaves a residual allocation question that cannot be
  reviewed honestly with current contracts

rejected for slice 2:
  adding retained payload resource wrapper allocated bytes
  adding provider queue item allocated bytes
  adding telemetry recorder allocated bytes
  changing CLI allocation output labels
  changing archive rebalance result constructor shape

slice 3 input:
  proceed to the standard allocation optimization pass with stable public
  contracts and focus first on callback residual candidates that are visible
  by code inspection: release callback/resource allocation, queue recent
  details, defensive recent-detail copies, and bounded aggregation allocation
```

### 3. Standard Allocation Optimization Pass

Apply low-risk standard .NET allocation optimizations to the highest-confidence
queued-owned hot paths.

Candidate implementation targets:

```text
remove unnecessary defensive copies in retained/owned snapshot handling
avoid closure allocation in retained resource release paths where a small
  explicit resource type is clearer
replace hot-path LINQ/enumerator allocation with direct loops where present
pre-size collections used by queued-owned benchmark result aggregation
avoid per-batch telemetry allocations where counters or reused recorders are
  enough
avoid repeated allocation snapshot capture when current attribution remains
  sufficient with one capture around the measured block
```

Candidate files:

```text
src/Infrastructure/Processing/RadarProcessingRetainedPayloadFactory.cs
src/Domain/Processing/RadarProcessingRetainedBatchResource.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapRunner.cs
src/Infrastructure/Processing/RadarProcessingOwnedBatchQueue.cs
src/Infrastructure/Processing/RadarProcessingQueuedProcessingSession.cs
src/Infrastructure/Archive/ArchiveOwnedRadarEventBatchQueueingPublisher.cs
```

Compatibility guardrails:

```text
pooled arrays must always be returned exactly once
release failures must remain visible
cancellation must still release accepted retained resources
validation failures must not produce hidden borrowed success
retained pressure counters must still return to zero
telemetry summaries must remain semantically equivalent
```

Runtime behavior changes:

```text
allocation profile should improve; functional behavior should remain the same
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests"
```

Implemented in slice 3:

```text
status: complete
runtime behavior changes:
  allocation profile should improve on bounded recent-detail aggregation,
  defensive recent-detail copy, and not-required retained-resource release
  delegate paths
  direct/default contour, fallback behavior, telemetry semantics, release
  lifecycle, and result contracts did not change

accepted standard optimizations:
  RadarProcessingArchiveRebalanceBenchmark.CreateBoundedRecentDetails no
  longer uses Concat/Skip/ToArray for bounded recent-detail aggregation; it
  now copies the retained window directly into one destination array while
  preserving oldest-drop ordering and dropped-count semantics

  RadarProcessingProviderQueueTelemetrySummary.CopyRequired no longer builds
  a List and then a second array for defensive recent-detail copies; it now
  validates null entries while copying directly into one array and keeps the
  same read-only result shape

  RadarProcessingRetainedBatchResource no longer creates a capturing default
  release lambda for not-required resources; it uses static per-strategy
  not-required release delegates and preserves resource state transitions and
  release-result validation

deferred to slice 4:
  replacing the closure-backed pooled retained payload release callback with
  an explicit owner/resource type remains an experimental candidate because it
  touches pooled array ownership and release timing

  pooled telemetry accumulator and struct-backed queued work item ideas remain
  experimental because they are broader than this low-risk standard pass

verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests"
  passed, 76 passed, 0 failed, 0 skipped

next:
  slice 4 should investigate non-standard or experimental approaches,
  especially an explicit pooled retained payload release owner, but should
  adopt one only if it preserves lifetime, release, cleanup, and failure
  guardrails more clearly than the current callback path
```

### 4. Experimental Optimization Research And Spikes

Investigate non-standard or experimental allocation-reduction ideas after the
standard pass identifies remaining cost.

Required spike posture:

```text
each experiment must name the allocation source it targets
each experiment must name the lifetime or correctness risk
each experiment must have a reject condition before measurement
each adopted experiment must have focused tests before broader gates
each rejected experiment must be recorded with the reason
```

Candidate experiments:

```text
explicit retained pooled resource type:
  replace closure-backed release with a small resource object that owns rented
  arrays and returns them without captured delegates

batch-scoped scratch owner:
  group short-lived queued-owned helper state under an explicit owner that is
  reset or released at batch completion

pooled telemetry accumulator:
  reuse summary recorder internals while returning immutable result summaries
  at the API boundary

struct-backed queued work item:
  reduce per-item heap allocation if queue and session code can preserve
  sequence, cancellation, and ownership clearly

allocation probe split:
  add temporary or permanent probes that separate retained copy, queue
  enqueue, overlap retention, and processing callback residual allocation
```

Reject experiments if:

```text
ownership becomes less obvious
release timing becomes harder to prove
failure/cancellation cleanup requires fragile ordering
tests need sleeps or GC timing assumptions to pass
the optimization only moves allocation outside the measured window
the code becomes disproportionate to the measured gain
```

Runtime behavior changes:

```text
none for rejected spikes; adopted spikes should reduce allocation without
changing functional behavior
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests"
```

Implemented in slice 4:

```text
status: complete
runtime behavior changes:
  allocation profile should improve on pooled retained payload release owner
  creation because the pooled-copy path no longer creates a closure-backed
  release callback and delegate per retained batch
  retained payload release semantics, resource state transitions, cleanup,
  result contracts, and direct/default contour did not change

adopted experiment:
  explicit pooled retained payload release owner

implementation:
  Domain now exposes an internal IRadarProcessingRetainedPayloadReleaseOwner
  contract to RadarPulse.Infrastructure through the existing
  InternalsVisibleTo boundary

  RadarProcessingRetainedBatchResource keeps the public Func-based
  constructor for existing callers/tests and adds an internal constructor that
  stores an owner object directly, avoiding per-resource release delegate
  allocation for infrastructure-owned release implementations

  RadarProcessingRetainedPayloadFactory.RetainPooledCopy now creates a private
  PooledRetainedPayloadReleaseOwner that owns the rented event and payload
  arrays, the pools they return to, and the retained payload byte count

  pooled release still returns rented arrays exactly once through the existing
  RadarProcessingRetainedBatchResource.Release() state machine; second release
  attempts still return AlreadyReleased without invoking the owner again

deferred experiments:
  pooled telemetry accumulator remains deferred because immutable summary
  boundaries and recorder reset ownership need a broader design than this
  slice

  struct-backed queued work item remains deferred because queue/channel
  semantics and sequence ownership would make the change wider than the
  current allocation evidence justifies

  allocation probe split remains deferred until Release gate evidence shows
  current residual attribution is insufficient

rejected for slice 4:
  no public result or CLI attribution fields were added
  no unsafe memory, stack-lifetime, or measured-window-shifting approach was
  used

verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests"
  passed, 56 passed, 0 failed, 0 skipped

next:
  slice 5 should integrate the adopted standard and experimental
  optimizations into broader focused coverage, especially direct/default
  contour, fallback/oracle, retained cleanup, and allocation summary
  guardrails
```

### 5. Adopted Optimization Integration

Integrate only the standard or experimental changes that survive the previous
slices.

Implementation targets:

```text
keep production changes narrow and reviewable
delete temporary spike code unless it is intentionally kept behind a test or
  documented helper
update allocation summary fields only when they are part of the accepted
  evidence model
update CLI output only when the operator should see new allocation categories
update tests to assert lifetime, release, contour, and attribution behavior
```

Required tests:

```text
direct MeasureFile()/MeasureCache() defaults still use queued-owned rollout
explicit BlockingBorrowed fallback still omits queued-only telemetry
explicit queued-owned rollout still matches omitted defaults
retained resources release on success
retained resources release on queued-owned failure
retained resources release on cancellation
retained pressure returns to zero
allocation summary categories remain non-negative
```

Runtime behavior changes:

```text
allocation profile changes; direct/default contour and functional semantics do
not change
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Implemented in slice 5:

```text
status: complete
runtime behavior changes:
  direct/default contour, fallback behavior, and release lifecycle did not
  change
  retained payload retention/release result contracts now carry pool
  rent/miss/return counts so the existing retained telemetry summary and CLI
  pool lines are populated for pooled-copy rows

reverted standard optimization:
  RadarProcessingOwnedBatchQueue.EnqueueAsync wait-mode synchronous fast path
  was reverted before the Release gate

  reason:
    the measured benefit was noisy and not the dominant allocation source
    after retained event-array pooling
    carrying a special wait-mode pre-write path made queue semantics harder to
    reason about before the allocation readiness decision

  current posture:
    ReturnFull mode still uses the immediate non-waiting enqueue path because
    that is its explicit behavior
    Wait mode uses the async wait loop for capacity, retained-byte budget,
    state changes, timeout, and cancellation behavior

attribution refinement:
  RadarProcessingBenchmarkAllocationSnapshot now records whether a snapshot
  came from the global allocation counter or the current-thread allocation
  counter and rejects mixed-scope deltas

  retained payload factory allocation telemetry now uses current-thread
  snapshots because snapshot-copy and pooled-copy retention execute
  synchronously on the producer thread; this keeps retained-copy telemetry
  from counting concurrent consumer-side allocation during producer-consumer
  overlap

  end-to-end, processing callback, and overlap measured allocation remain
  global-counter measurements, so the Release gate allocation ratio is not
  hidden or moved out of the measured contract

  CLI output now labels allocation counter scopes:
    allocation measured counter scope: global
    allocation processing callback counter scope: global
    retained payload allocation counter scope: current-thread
    provider overlap measured allocation counter scope: global

  RadarProcessingRetainedPayloadByteArrayPool now counts rent attempts,
  return attempts, and large-array cold misses

  RadarProcessingRetainedPayloadFactory.RetainPooledCopy records per-retain
  pool rent count and exact retained byte-pool miss count; pooled resource release
  records returned event/payload arrays; ArchiveOwnedRadarEventBatchQueueingPublisher
  aggregates those counts into RadarProcessingRetainedPayloadTelemetrySummary

  CLI retained payload pool rents/returns/misses were already part of the
  operator output; after this slice they are actionable for pooled-copy rows
  instead of staying at zero

interim allocation sanity, not final gate:
  Release CLI omitted-provider KTLX 2026-05-05 --max-files 220 same-run
  borrowed/default pairs were captured to decide whether more optimization is
  needed before the milestone Release gate

  after slice 4 explicit release-owner optimization, before enqueue fast path:
    row 1 allocation ratio: 1.0943x borrowed
    row 2 allocation ratio: 1.0960x borrowed
    average allocation ratio: 1.0951x borrowed
    average elapsed ratio: 0.9370x borrowed

  after enqueue fast path, before it was reverted:
    row 1 allocation ratio: 1.0947x borrowed
    row 2 allocation ratio: 1.1014x borrowed
    average allocation ratio: 1.0980x borrowed
    average elapsed ratio: 0.9251x borrowed

interpretation:
  interim data around the enqueue fast path was better than the milestone 014
  KTLX 2026-05-05 average of 1.0997x borrowed, but it was not clean
  readiness evidence because one post-fast-path row still exceeded the 1.10x
  threshold

  no threshold was changed and this measurement does not replace the planned
  direct API Release gate

  the remaining actionable allocation source is retained pooled-copy
  cold/churn allocation plus overlap attribution ambiguity; provider enqueue
  fast-path overhead was not the dominant remaining allocation source and the
  wait-mode fast path should not be treated as accepted milestone output

retained allocation profile after current-thread attribution:
  one Release CLI omitted-provider KTLX 2026-05-05 --max-files 220 default row
  after current-thread retained telemetry reported:
    retained payload allocated bytes: 222323496
    provider overlap measured allocated bytes: 2577831248
    provider overlap unattributed allocated bytes: 2355507752
    end-to-end allocated bytes: 2581186808
    processing callback non-owned snapshot bytes: 1999761400

  interpretation:
    retained pooled-copy cold/churn allocation is real and remains roughly the
    same magnitude as the same-shape queued-owned allocation delta seen in the
    interim pairs

    the very large processing callback non-owned snapshot and provider overlap
    unattributed buckets are global-counter overlap attribution buckets, not
    precise retained-copy optimization targets

retained pooled-copy micro-harness:
  a deterministic retained-copy harness was added around
  RadarProcessingRetainedPayloadFactory.RetainPooledCopy using a synthetic
  leased payload above the retained byte-pool large-array threshold

  the cold same-shape retain records two pool rents, one retained byte-pool
  miss, and two pool returns after release

  the warm same-shape retain records two pool rents, zero retained byte-pool
  misses, and two pool returns after release

  the cold retain allocates more current-thread bytes than the warm retain,
  and the retained byte pool keeps one reusable large array after release

  interpretation:
    the first large retained byte-array rent is a real cold allocation
    source; same-shape warm reuse works

    pool policy should not be changed again until KTLX rows report real
    retained payload pool miss counts, because allocation alone cannot tell
    whether the remaining 222 MB retained-copy cost is cold shape diversity,
    pool churn, or a non-pool retained-copy cost

retained event-array allocation hypothesis:
  KTLX 2026-05-05 default rows retained 3581280 RadarStreamEvent values

  RadarStreamEvent is a 64-byte readonly struct, so the retained event
  metadata copy shape is about 229201920 bytes before array/object overhead

  the measured retained payload allocation after pool-miss telemetry was
  211837640 to 220226320 bytes, while retained byte-pool misses were only 1
  per row

  interpretation:
    the remaining retained-copy allocation is most likely dominated by
    retained event-array cold allocation/reuse behavior, not retained payload
    byte-array churn

retained event-array pool spike:
  status: accepted into the milestone implementation; broader focused
  regression passed and final Release gate confirmation is pending

  implementation:
    RadarProcessingRetainedEventArrayPool was added as a dedicated
    RadarStreamEvent[] pool for retained pooled-copy event metadata

    the pool mirrors the retained byte-array pool shape:
      best-fit reuse
      explicit retained array count cap
      explicit retained byte cap
      rent/return/miss counters
      small-array fallback to ArrayPool<RadarStreamEvent>.Shared

    RadarProcessingRetainedPayloadFactory now uses
    RadarProcessingRetainedEventArrayPool by default, alongside
    RadarProcessingRetainedPayloadByteArrayPool for bytes

    retained payload retention/release results and retained telemetry summary
    now split pool counters into:
      total retained pool rents/returns/misses
      retained event array pool rents/returns/misses
      retained byte array pool rents/returns/misses

    CLI retained payload telemetry now prints the split event-array and
    byte-array pool counters

  focused coverage:
    cold/warm retained event-array micro-harness proves a cold same-shape
    retained event rent records one event-array miss and a warm same-shape
    rent records zero event-array misses

    event-array pool unit coverage validates large-array reuse, small-array
    fallback, and retained-byte bounds

    publisher aggregation coverage validates split pool counters across
    retention and release

  KTLX 2026-05-05 Release CLI same-run pairs after the event-array pool:
    row 1:
      borrowed elapsed ms: 10524.71
      default elapsed ms: 10442.83
      elapsed ratio: 0.9922x borrowed
      borrowed allocated bytes: 2339719360
      default allocated bytes: 2433261904
      allocation ratio: 1.0400x borrowed
      retained payload allocated bytes: 73424544
      retained event array pool rents: 104
      retained event array pool returns: 104
      retained event array pool misses: 2
      retained byte array pool rents: 104
      retained byte array pool returns: 104
      retained byte array pool misses: 1

    row 2:
      borrowed elapsed ms: 10870.08
      default elapsed ms: 10021.61
      elapsed ratio: 0.9219x borrowed
      borrowed allocated bytes: 2342342616
      default allocated bytes: 2432259200
      allocation ratio: 1.0384x borrowed
      retained payload allocated bytes: 73424544
      retained event array pool rents: 104
      retained event array pool returns: 104
      retained event array pool misses: 2
      retained byte array pool rents: 104
      retained byte array pool returns: 104
      retained byte array pool misses: 1

    average:
      elapsed ratio: 0.9565x borrowed
      allocation ratio: 1.0392x borrowed
      borrowed allocated bytes average: 2341030988
      default allocated bytes average: 2432760552
      retained payload allocated bytes average: 73424544

  interpretation:
    the event-array hypothesis was confirmed by the retained allocation drop:
    the previous post-telemetry average retained allocation was about
    216031980 bytes, and the event-array pool reduced it to 73424544 bytes
    on the same KTLX 2026-05-05 shape

    both KTLX 2026-05-05 allocation rows moved well below the 1.10x warning
    threshold in this interim CLI sanity pass

    release and cleanup remained clean in the measured rows:
      retained failed copies: 0
      retained failed releases: 0
      provider overlap failed releases: 0
      current pending/active/combined retained pressure returned to 0

    builder-transfer and RadarStreamEvent packing/redesign should remain
    deferred unless the final Release gate exposes a new retained
    representation blocker

KTLX 2026-05-05 pool-miss sanity, not final gate:
  Release CLI same-run borrowed/default pairs were rerun after retained
  payload pool telemetry was populated

  commands:
    dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-05 --radar KTLX --max-files 220 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
    dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-05 --radar KTLX --max-files 220 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

  row 1:
    borrowed elapsed ms: 10804.30
    default elapsed ms: 10211.49
    elapsed ratio: 0.9451x borrowed
    borrowed allocated bytes: 2340781368
    default allocated bytes: 2577312360
    allocation ratio: 1.1010x borrowed
    retained payload allocated bytes: 220226320
    retained payload pool rents: 208
    retained payload pool returns: 208
    retained payload pool misses: 1

  row 2:
    borrowed elapsed ms: 10448.97
    default elapsed ms: 10123.30
    elapsed ratio: 0.9688x borrowed
    borrowed allocated bytes: 2344037920
    default allocated bytes: 2565238576
    allocation ratio: 1.0944x borrowed
    retained payload allocated bytes: 211837640
    retained payload pool rents: 208
    retained payload pool returns: 208
    retained payload pool misses: 1

  average:
    elapsed ratio: 0.9568x borrowed
    allocation ratio: 1.0977x borrowed
    borrowed allocated bytes average: 2342409644
    default allocated bytes average: 2571275468
    retained payload allocated bytes average: 216031980
    retained payload pool miss rate: 2 misses / 416 rents, 0.4808%

  safety:
    correctness validation succeeded in both rows
    validation checksum matched borrowed:
      11084221590146245827
    retained payload failed copies: 0
    retained payload failed releases: 0
    provider overlap failed releases: 0
    current pending, active, and combined retained pressure returned to 0

  interpretation:
    the warning is still not clean green because row 1 remained slightly above
    the 1.10x allocation threshold, although the two-row average stayed below
    it and elapsed remained faster than borrowed

    retained byte-pool churn is not the blocker: each default row had only one
    retained byte-pool miss for 104 retained batches and 208 total rents, and
    all rents were returned

    raising retained byte-pool capacity or changing eviction policy is not
    justified by this evidence; the remaining warning should be treated as
    cold retained representation cost plus global overlap/non-pool allocation
    attribution unless a later gate proves otherwise

rejected spike:
  increasing RadarProcessingRetainedPayloadByteArrayPool default retained
  bytes from 128 MiB to 256 MiB was tested as a bounded pool-tuning spike and
  reverted because the measured KTLX 2026-05-05 row still allocated about
  220 MB in retained payload snapshots and produced about 1.1003x borrowed
  allocation while increasing memory retention without proof of benefit

verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests"
  passed, 83 passed, 0 failed, 0 skipped

  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors

  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
  passed, 82 passed, 0 failed, 0 skipped

  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors

  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedPayloadContractTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
  passed, 125 passed, 0 failed, 0 skipped

  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors

  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedPayloadContractTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
  passed, 129 passed, 0 failed, 0 skipped

  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors

  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  passed, 768 passed, 0 failed, 3 skipped

next:
  capture slice 8 allocation readiness Release gate with direct API default
  rows and same-run explicit BlockingBorrowed oracle rows

  do not tune retained byte-pool shape retention from this evidence; pool
  misses are too low to explain the remaining KTLX allocation warning

  do not treat processing callback non-owned snapshot bytes as an actionable
  retained-copy target without a more precise per-source profile
```

### 6. Fallback, Failure, Cleanup, And Drift Guardrails

Run and extend guardrails around the optimized queued-owned paths.

Required coverage:

```text
queued-owned direct default failure does not fall back to borrowed
explicit BlockingBorrowed remains selectable
builder-transfer remains unsupported
controlled consumer delay remains mechanics-only proof
retained payload failed releases are visible
provider overlap failed releases are visible
cancellation after accepted enqueue releases retained resources
validation failure prevents later success claims
direct/CLI rollout contour still matches shared defaults
```

Expected tests:

```text
NexradArchiveRadarEventBatchPublisherTests direct default and fallback tests
RadarProcessingArchiveQueuedOverlapRunnerTests failure and cancellation tests
RadarProcessingQueuedProviderReadinessGateTests threshold and cleanup tests
RadarPulseCliRebalanceBenchmarkTests rollout contour and help tests
```

Runtime behavior changes:

```text
none beyond allocation changes from earlier slices
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
```

Implemented in slice 6:

```text
status: complete
runtime behavior changes: none

guardrail coverage:
  direct MeasureFile()/MeasureCache() omitted defaults remain pinned to the
  queued-owned rollout contour
  explicit BlockingBorrowed direct calls remain selectable and continue to
  omit queued telemetry
  direct default queued-owned failure still fails closed and does not
  automatically fall back to borrowed
  builder-transfer remains explicitly unsupported
  controlled overlap consumer delay remains classified as mechanics-only
  proof, not natural readiness evidence
  retained payload release failures and retention failures remain visible to
  readiness evaluation
  cancellation after accepted enqueue releases retained resources
  validation failure releases active retained resources and prevents a later
  success claim
  CLI omitted-provider rollout still expands through the shared rollout
  defaults

new drift assertions:
  direct queued-owned rollout contour tests now require split retained pool
  telemetry to sum to total pool telemetry and require event/byte pool rents
  to be returned at completion
  CLI omitted-provider output now asserts the retained event-array and
  byte-array pool telemetry labels are present

verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
  passed, 77 passed, 0 failed, 0 skipped
```

### 7. Focused Regression And Allocation Sanity Pass

Run focused tests before capturing the Release gate.

Required focused checks:

```text
direct MeasureFile()/MeasureCache() default contour tests
direct explicit BlockingBorrowed fallback tests
direct explicit queued-owned equivalence tests
CLI help and rollout contour alignment tests
retained payload factory and retained resource release tests
queued-owned failure, cancellation, cleanup, and fallback tests
readiness threshold interpretation tests
allocation summary attribution tests
small allocation-sensitive regression tests added by milestone 015
```

Expected commands:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Gate precondition:

```text
Do not capture the Release allocation-readiness gate if focused tests fail or
the Release build has warnings/errors.
```

Runtime behavior changes:

```text
none
```

Implemented in slice 7:

```text
status: complete
runtime behavior changes: none
gate precondition:
  focused regression passed and Release build succeeded with no warnings or
  errors, so slice 8 Release allocation-readiness gate capture is unblocked

verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
  passed, 112 passed, 0 failed, 0 skipped

  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
```

Post-slice 7 follow-up: wait-mode enqueue fast-path revert:

```text
status: complete
runtime behavior changes:
  RadarProcessingOwnedBatchQueue.EnqueueAsync no longer uses the special
  wait-mode synchronous pre-write fast path
  ReturnFull mode still uses the immediate non-waiting enqueue path because
  that is the explicit ReturnFull behavior
  direct/default contour, fallback behavior, retained ownership, and release
  lifecycle did not change

reason:
  the fast-path measurement was noisy and did not become the dominant
  allocation win after retained event-array pooling
  removing it keeps the Release gate focused on the retained representation
  optimization and avoids carrying an extra queue-semantics risk into the
  allocation readiness decision

focused verification after revert:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"
  passed, 105 passed, 0 failed, 0 skipped

  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
  passed, 112 passed, 0 failed, 0 skipped

  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors

KTLX 2026-05-05 one-row Release CLI same-run sanity after revert, not final
gate:
  borrowed elapsed ms: 10121.18
  default elapsed ms: 8947.04
  elapsed ratio: 0.8840x borrowed
  borrowed allocated bytes: 2347585456
  default allocated bytes: 2432466384
  allocation ratio: 1.0362x borrowed
  retained payload allocated bytes: 73424544
  retained event array pool rents/returns/misses: 104 / 104 / 2
  retained byte array pool rents/returns/misses: 104 / 104 / 1
  validation succeeded and checksum matched borrowed:
    11084221590146245827
  retained payload failed releases: 0
  provider overlap failed releases: 0
  current pending, active, and combined retained pressure returned to 0

interpretation:
  reverting the wait-mode fast path did not remove the event-array pool
  allocation improvement on the KTLX 2026-05-05 risk shape
  this is only a sanity row; slice 8 still needs the planned direct API
  Release allocation-readiness gate
```

### 8. Allocation Readiness Release Gate

Capture natural Release evidence for allocation readiness.

Candidate document:

```text
docs/milestones/015-queued-owned-allocation-readiness-performance-gate.md
```

Required command posture:

```text
Release build before measurements
direct API default rows, not only CLI rows
same-run explicit BlockingBorrowed oracle rows
controlled consumer delay 0
same mode, partition count, shard count, parallelism, iterations, and input
between borrowed and default rows
allocation attribution visible in result contracts
```

Required contours:

```text
primary repeated contour:
  data\nexrad --date 2026-05-04 --radar KTLX --max-files 220

borderline repeated contour:
  data\nexrad --date 2026-05-05 --radar KTLX --max-files 220

broader single-shape contour:
  data\nexrad --date 2026-05-04 --radar KINX --max-files 220

mixed-cache contour:
  data\nexrad --max-files 1000000

file-level smoke contour:
  one representative KTLX Archive Two file through MeasureFile()
```

Required interpretation:

```text
correctness parity
release health
retained cleanup
retained pressure budget
elapsed timing
primary repeated-run spread
allocation ratio, with KTLX 2026-05-05 explicit
before/after comparison against milestone 014 gate where valid
direct default versus explicit queued-owned contour equivalence
explicit borrowed fallback/oracle separation
allocation attribution
standard optimizations accepted or rejected
experimental optimizations accepted, deferred, or rejected
workload coverage limits
```

Expected gate status values:

```text
ready
ready with allocation warning
not ready because of named allocation blocker
blocked by named correctness/cleanup/release/pressure regression
```

Guardrail:

```text
Do not call the gate clean green if KTLX 2026-05-05 remains at or above the
1.10x allocation threshold. Name the warning and decide what it means.
```

Runtime behavior changes:

```text
none; this slice records evidence only
```

### 9. Allocation Readiness Decision Trace

Record the allocation readiness decision after the gate.

Candidate document:

```text
docs/milestones/015-queued-owned-allocation-readiness-decision-trace.md
```

Required decisions:

```text
whether allocation readiness is accepted
whether KTLX 2026-05-05 warning was reduced, bounded, or remains debt
whether each repeated KTLX 2026-05-05 row is below threshold
whether the average moved materially away from threshold
whether standard optimizations were sufficient
whether any experimental approach was adopted
whether rejected experimental approaches should be revisited
whether explicit BlockingBorrowed fallback/oracle posture remains adequate
whether CLI and direct rollout contours remain aligned
whether any broader benchmark/runtime expansion is approved as next input
whether live/runtime defaults remain out of scope
```

Decision explanation fields:

```text
Decision
Why chosen
Alternatives
Rejected because
Trade-offs/debt
Review explanation
```

Guardrail:

```text
If the gate misses a correctness, release, cleanup, or pressure threshold, do
not accept allocation readiness. Record the blocker and next milestone input.
```

Runtime behavior changes:

```text
none
```

### 10. Closeout And Handoff

Finalize milestone documentation and project handoff.

Candidate documents:

```text
docs/milestones/015-queued-owned-allocation-readiness-closeout.md
docs/handoff.md
```

Required closeout content:

```text
final status
implemented behavior
not implemented behavior
final allocation posture
standard optimization summary
experimental optimization summary
rejected optimization summary
explicit fallback/oracle posture
focused verification commands and results
Release gate summary
KTLX 2026-05-05 allocation interpretation
residual risks
recommended next milestone input
```

Handoff must state one of:

```text
allocation warning reduced enough for the next named expansion

or

allocation warning remains but is deliberately accepted with stronger
attribution for the next named surface

or

allocation warning remains a blocker and the next milestone should target the
named allocation source

or

allocation readiness could not be decided because correctness, cleanup,
release health, pressure, fail-closed behavior, or variance regressed
```

Expected verification before closeout:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"

dotnet build RadarPulse.sln -c Release --no-restore

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Guardrail:

```text
Do not mark milestone 015 complete until handoff names the current allocation
posture, fallback/oracle posture, KTLX allocation posture, standard and
experimental optimization posture, and next milestone recommendation
unambiguously.
```

Runtime behavior changes:

```text
none
```

## Verification Strategy

Use focused tests after each implementation slice and broader verification
before the Release gate.

Expected focused coverage:

```text
direct MeasureFile() omitted defaults
direct MeasureCache() omitted defaults
direct explicit BlockingBorrowed fallback
direct explicit queued-owned rollout contour
direct file/cache contour symmetry
CLI/direct rollout contour alignment
CLI help text direct default wording
retained payload factory allocation and release behavior
retained resource lifecycle behavior
queued-owned invalid option rejection
builder-transfer unsupported posture
controlled consumer-delay rejection and labeling
retained-resource release health
retained pressure cleanup at completion
queued-owned failure and cancellation cleanup
allocation summary attribution
standard optimization contract tests
experimental optimization contract tests for any adopted experiment
```

Expected broad checks:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingRetainedBatchResourceTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Expected performance gate:

```text
Release build before measurements
same-run explicit BlockingBorrowed async reference
direct default queued-owned rollout contour selected through omitted direct
  provider/execution/queue/retention arguments
active/combined retained pressure visible in result contracts
allocation attribution visible in result contracts
repeated natural rows and variance interpretation
broader single-shape rows over available local data
mixed-cache row over all local data
KTLX 2026-05-05 allocation warning repeated and interpreted
allocation ratio checked against 1.10x threshold
performance ratio checked against 1.00x threshold
controlled proof rows separated if captured
standard and experimental optimization outcomes recorded
```

## Completion Checklist

```text
[x] allocation baseline audit is captured
[x] attribution sufficiency decision is recorded
[x] allocation instrumentation and contract check is complete
[x] standard allocation optimization pass is complete or explicitly rejected
[x] experimental optimization research/spike pass is complete
[x] adopted optimizations are integrated with focused tests
[x] rejected standard and experimental approaches are recorded
[x] direct MeasureFile()/MeasureCache() defaults remain queued-owned rollout
[x] explicit direct BlockingBorrowed fallback remains selectable and covered
[x] direct explicit queued-owned rollout calls match omitted direct defaults
[x] CLI omitted-provider rollout contour remains aligned with direct defaults
[x] failure, cancellation, release, and cleanup guardrails remain covered
[x] focused regression pass succeeds before gate capture
[ ] allocation readiness Release gate is captured
[ ] KTLX 2026-05-05 allocation warning is repeated and interpreted
[ ] performance gate interprets correctness, cleanup, pressure, allocation,
    timing, variance, fallback/oracle posture, attribution, and optimization
    posture
[ ] decision trace records the allocation readiness decision
[ ] closeout is written
[ ] handoff is updated with current allocation posture, fallback/oracle,
    standard and experimental optimization posture, allocation risk, and next
    milestone recommendation
```

## Non-Goals

Milestone 015 does not implement:

```text
new queued-owned default contour
synthetic processing benchmark default migration
non-benchmark archive publishing API default migration
live ingestion/runtime provider defaults
durable queue or broker integration
cross-process provider or worker transport
ordered concurrent rebalance commit barrier
multiple active rebalance-enabled processing batches
builder-transfer retained payload execution
source-level migration or partition splitting
physical worker-local state transfer
complex radar algorithms
visualization or product-facing radar analysis features
automatic silent fallback from queued-owned failure to borrowed success
threshold changes after gate capture
allocation optimization that hides cost by moving it outside the measured
  contract
```

## Closeout Question

The milestone closes by answering:

```text
Is the queued-owned direct/default allocation profile ready to support the next
broader benchmark or runtime-default decision?
```

The acceptable answers are:

```text
ready:
  allocation warning is reduced or bounded below the accepted threshold on the
  repeated risk contour, safety gates pass, and the next milestone can
  consider a named broader expansion

ready with warning:
  allocation remains near the threshold, but attribution is stronger, the cost
  is deliberately accepted for the next named surface, and the warning remains
  visible

not ready:
  allocation remains too close to or above threshold without enough reduction
  or attribution, and the decision trace names the next optimization blocker

defer:
  correctness, release health, cleanup, retained pressure, fail-closed
  behavior, or benchmark variance regressed, so allocation readiness cannot be
  decided safely
```

The milestone should not close with an ambiguous monitoring posture. It should
either accept allocation readiness or name the blocker that must be handled
next.
