# Milestone 014: Direct Archive Rebalance API Default Migration Implementation Plan

Status: in progress.

This plan implements the milestone 014 architecture defined in
`014-direct-archive-rebalance-api-default-migration.md`.

The plan is intentionally scoped to direct
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` and `MeasureCache()`
default migration. It should not implement live ingestion, durable broker
integration, cross-process workers, ordered concurrent rebalance,
builder-transfer, synthetic benchmark defaults, or non-benchmark archive
publishing defaults.

## Goal

Milestone 014 decides whether the direct archive rebalance benchmark API should
use the same queued-owned rollout contour that milestones 012 and 013 accepted
for the scoped CLI omitted-provider path.

The target direct default contour is:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async
worker count: 4
async worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536870912
queue telemetry: summary where available in result contracts
overlap telemetry: summary where available in result contracts
overlap consumer delay: 0
```

The most important rules are:

```text
change direct MeasureFile()/MeasureCache() defaults as one focused milestone
preserve explicit blocking-borrowed direct calls as fallback and oracle
keep same-run borrowed rows in the migration gate
keep CLI omitted-provider rollout contour aligned with direct defaults
carry KTLX 2026-05-05 allocation warning visibly through the gate
do not silently fall back from queued-owned failure to borrowed success
do not tune the accepted rollout contour to make the direct gate pass
do not broaden into live/runtime/durable surfaces
```

The implementation target is deliberately conservative:

```text
pin the current direct borrowed baseline before changing defaults
introduce or centralize the direct rollout contour so file/cache and CLI do
  not drift
change omitted direct defaults only after drift/fallback tests exist
prove direct file and cache defaults match explicit queued-owned rollout calls
prove explicit borrowed direct calls still produce the borrowed contour
update CLI help/docs that still say direct defaults remain borrowed
capture focused tests before the Release gate
record a direct API default decision before closeout
```

## Starting Point

Milestone 013 is complete and provides:

```text
scoped CLI omitted-provider default:
  queued-owned + pooled-copy + producer-consumer
  async workers 4
  provider queue capacity 8
  retained-byte budget 536870912
  queue telemetry summary
  overlap telemetry summary
  overlap consumer delay 0
  provider source rollout-default

explicit CLI fallback:
  --provider blocking-borrowed

direct API compatibility posture:
  MeasureFile()/MeasureCache() defaults remain blocking-borrowed until
  milestone 014 changes them

allocation warning:
  KTLX 2026-05-05 averaged 1.1005x borrowed allocation with one row above and
  one row below the 1.10x threshold
```

Current direct method defaults before milestone 014 implementation:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile():
  executionMode = PartitionedBarrier
  asyncExecution = null
  providerMode = BlockingBorrowed
  queueCapacity = 1
  providerOverlapMode = None
  retentionStrategy = SnapshotCopy
  queueRetainedPayloadBytes = null
  overlapConsumerDelay = 0

RadarProcessingArchiveRebalanceBenchmark.MeasureCache():
  executionMode = PartitionedBarrier
  asyncExecution = null
  providerMode = BlockingBorrowed
  queueCapacity = 1
  providerOverlapMode = None
  retentionStrategy = SnapshotCopy
  queueRetainedPayloadBytes = null
  overlapConsumerDelay = 0
```

Current direct compatibility tests already prove:

```text
direct file default is blocking-borrowed
direct cache default is blocking-borrowed
direct explicit queued-owned rollout contour is supported
direct file/cache borrowed and explicit rollout rows preserve stable totals
queued-owned direct rows expose queue, retention, overlap, and worker telemetry
invalid queued-owned control combinations fail closed
```

Known local gate data availability from milestone 013:

```text
data\nexrad\level2\2026\05\04\KINX: 462 files
data\nexrad\level2\2026\05\04\KTLX: 244 files
data\nexrad\level2\2026\05\05\KTLX: 848 files
data\nexrad total files: 1554
```

Milestone 013 gate facts to carry forward:

```text
primary KTLX 2026-05-04:
  elapsed ratio: 0.911x borrowed
  allocation ratio: 1.071x borrowed
  candidate spread: 5.41%

KINX 2026-05-04:
  elapsed ratio: 0.939x borrowed
  allocation ratio: 1.070x borrowed

KTLX 2026-05-05:
  elapsed ratio: 0.943x borrowed average
  allocation ratio: 1.1005x borrowed average
  run 1 allocation ratio: 1.101x borrowed
  run 2 allocation ratio: 1.0996x borrowed

mixed cache:
  elapsed ratio: 0.907x borrowed
  allocation ratio: 1.062x borrowed

retained pressure:
  max observed combined retained payload high-water 54413280 bytes of the
  536870912 byte budget

release and cleanup:
  release failures 0
  current pending, active, and combined retained pressure returned to 0
```

## Scope

The implementation should explicitly scope milestone 014 to:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile() direct defaults
RadarProcessingArchiveRebalanceBenchmark.MeasureCache() direct defaults
direct file/cache result contour assertions
direct explicit blocking-borrowed fallback coverage
direct explicit queued-owned rollout equivalence coverage
CLI/direct rollout contour alignment checks
CLI help and tests that mention direct default posture
focused failure, cleanup, fallback, and default drift coverage
natural Release gate documentation for direct default rows
decision trace, closeout, and handoff
```

The implementation should not change:

```text
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

## Migration Thresholds

Milestone 014 starts from the milestone 012 and 013 thresholds. The plan should
not change them after any new gate measurements are captured.

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
  direct default queued-owned allocated bytes should be <= 1.10x same-run
  borrowed allocated bytes; KTLX 2026-05-05 remains a known borderline warning
  that must be interpreted explicitly

performance:
  direct default queued-owned elapsed time should be <= 1.00x same-run
  borrowed elapsed time on the primary repeated natural matrix

variance:
  repeated natural direct default spread should be <= 7.50% of candidate
  average on the primary matrix

natural evidence:
  overlap consumer delay must be 0 and evidence scope must remain natural
  migration evidence, not controlled mechanics proof
```

Guardrail:

```text
Do not change thresholds after seeing final gate measurements. If thresholds
need to change, record the reason before interpreting the gate.
```

## Target Implementation Shape

The safest implementation shape is to make the rollout contour explicit and
shared before changing direct defaults.

Candidate layering:

```text
Infrastructure:
  owns RadarProcessingArchiveRebalanceBenchmark direct default behavior
  owns direct file/cache result contracts
  owns direct benchmark execution and telemetry surfaces

Presentation:
  owns CLI option parsing, provenance, and help text
  should stay aligned with the shared rollout contour

Domain:
  may own threshold/readiness vocabulary already used by gates
  should not gain presentation-only option provenance concerns

Documentation:
  owns milestone gate interpretation, residual risks, and handoff posture
```

Implementation guidance:

```text
prefer one local definition of the rollout contour over copying literals into
  unrelated tests and methods

if optional parameter defaults cannot express a dependent default cleanly,
  use internal effective-default resolution inside MeasureFile()/MeasureCache()
  while keeping explicit caller arguments honored

keep explicit blocking-borrowed calls readable in tests and gate commands

avoid changing result contract shape unless needed to make the direct default
  contour observable

do not add direct API provenance fields unless result fields are insufficient
```

The direct default migration must change the full contour together:

```text
providerMode: QueuedOwned
providerOverlapMode: ProducerConsumer
retentionStrategy: PooledCopy
executionMode: AsyncShardTransport
asyncExecution: workerCount 4, queueCapacity 8
queueCapacity: 8
queueRetainedPayloadBytes: 536870912
overlapConsumerDelay: 0
```

Changing only `providerMode` is not sufficient.

## Implementation Slices

### 1. Direct API Baseline Audit

Capture the current direct API default and compatibility surfaces before
changing behavior.

Files to inspect:

```text
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmarkResult.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceCacheBenchmarkResult.cs
src/Presentation/Program.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
docs/handoff.md
```

Questions to answer:

```text
which optional parameters currently encode borrowed direct defaults?
which tests assert borrowed direct defaults?
which tests already assert explicit queued-owned rollout contours?
which result fields fully prove direct default contour?
which CLI help text and tests mention direct defaults remain borrowed?
which helpers can be reused for direct file/cache contour assertions?
```

Expected findings:

```text
MeasureFile() and MeasureCache() default providerMode is BlockingBorrowed
MeasureFile() and MeasureCache() default executionMode is PartitionedBarrier
direct explicit queued-owned rollout contour already exists in tests
CLI help currently says direct MeasureFile()/MeasureCache() defaults remain
  blocking-borrowed
```

Runtime behavior changes:

```text
none
```

Verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
```

Implemented in slice 1:

```text
status: complete
runtime behavior changes: none
audited direct defaults:
  MeasureFile() still defaults to BlockingBorrowed, PartitionedBarrier,
  queue capacity parameter 1, overlap None, retention SnapshotCopy,
  retained-byte budget null, and overlap consumer delay 0
  MeasureCache() still defaults to the same borrowed partitioned-barrier
  contour
audited direct tests:
  then-current file/cache direct default contour tests covered borrowed
  omitted defaults and explicit queued-owned rollout contours; later slices
  renamed those tests while preserving explicit BlockingBorrowed
  fallback/oracle coverage
  queued-owned provider, overlap/retention, async worker telemetry, cache
  aggregation, invalid option, and cleanup-related direct coverage already
  exists in NexradArchiveRadarEventBatchPublisherTests
audited result fields:
  ProviderMode, ExecutionMode, QueueCapacity, ProviderOverlapMode,
  RetentionStrategy, QueueRetainedPayloadBytes, OverlapConsumerDelay,
  WorkerTelemetry, QueueTelemetry, RetentionTelemetry, OverlapTelemetry,
  RetainedResourcePressure, current retained pressure counters,
  OwnedSnapshotAllocatedBytes, ProcessingCallbackNonOwnedSnapshotAllocatedBytes,
  and AllocationSummary are sufficient to prove borrowed and queued-owned
  direct contours
audited operator text:
  Program usage and RadarPulseCliRebalanceBenchmarkTests still state that
  direct MeasureFile()/MeasureCache() defaults remain blocking-borrowed
implementation implication:
  slice 2 should centralize or otherwise pin the accepted rollout contour
  before changing direct defaults, because tests already have enough result
  fields to verify the migration without adding direct API provenance fields
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
  passed, 22 passed, 0 failed, 0 skipped
```

### 2. Shared Rollout Contour Contract

Create or identify a single direct-code contract for the accepted rollout
contour.

Implementation targets:

```text
shared constants or resolver for:
  provider mode queued-owned
  provider overlap producer-consumer
  retention strategy pooled-copy
  execution async
  worker count 4
  worker queue capacity 8
  provider queue capacity 8
  retained-byte budget 536870912
  overlap consumer delay 0

tests that compare CLI rollout constants/options to direct API target contour
tests that fail if direct file and cache target defaults drift apart
```

Design choices to settle:

```text
whether direct defaults are expressed as optional parameter defaults or as
  effective-default resolution after parameters are received

whether an internal helper such as a rollout contour factory is useful enough
  to avoid duplication

whether CLI constants remain in presentation or move to a shared location
  without introducing presentation dependencies into infrastructure
```

Guardrails:

```text
do not make CLI option provenance a dependency of infrastructure
do not add a broad configuration abstraction for one contour unless it removes
  real duplication
do not change direct behavior in this slice unless tests already pin both old
  and target contours
```

Runtime behavior changes:

```text
none unless implementation chooses a no-op internal helper
```

Expected tests:

```text
direct target rollout contour constants match CLI rollout contour constants
direct file target contour equals direct cache target contour
explicit queued-owned rollout helper produces the accepted contour
```

Implemented in slice 2:

```text
status: complete
runtime behavior changes: none
new shared contract:
  src/Infrastructure/Processing/RadarProcessingArchiveRebalanceRolloutDefaults.cs
  defines ProviderMode queued-owned, ProviderOverlapMode producer-consumer,
  RetentionStrategy pooled-copy, ExecutionMode async shard transport,
  WorkerCount 4, WorkerQueueCapacity 8, ProviderQueueCapacity 8,
  RetainedPayloadBytes 536870912, and OverlapConsumerDelay 0
  exposes CreateAsyncExecution() and Matches() so direct and CLI contours can
  be checked without duplicating literals
CLI alignment:
  ProcessingBenchmarkArchiveRebalanceOptions rollout constants and omitted
  provider expansion now read the shared infrastructure contract
  CLI option provenance remains presentation-owned; infrastructure does not
  depend on CLI provenance
direct test alignment:
  direct explicit queued-owned rollout file/cache tests now use the shared
  contract for explicit options
  direct queued-owned contour assertion helpers now verify result fields
  against the shared contract
new drift guards:
  RebalanceArchiveBenchmarkRolloutDefaultContractPinsAcceptedContour pins the
  accepted exact contour values and CreateAsyncExecution()/Matches() behavior
  ArchiveRebalanceBenchmarkOptionsRolloutContourMatchesSharedContract proves
  CLI omitted-provider rollout options match the shared contract
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
  passed, 50 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
next:
  slice 3 can migrate direct MeasureFile() omitted defaults using the shared
  rollout contract
```

### 3. Direct File Default Migration

Change direct `MeasureFile()` omitted provider/execution/queue/retention
behavior to the accepted rollout contour.

Implementation targets:

```text
MeasureFile() direct default provider mode becomes queued-owned
MeasureFile() direct default execution becomes async shard transport
MeasureFile() direct default async execution uses workers 4 and queue capacity 8
MeasureFile() direct default provider queue capacity becomes 8
MeasureFile() direct default provider overlap becomes producer-consumer
MeasureFile() direct default retention strategy becomes pooled-copy
MeasureFile() direct default retained-byte budget becomes 536870912
MeasureFile() direct default overlap consumer delay remains 0
```

Tests to update or add:

```text
MeasureFile() omitted direct defaults produce queued-owned rollout contour
MeasureFile() explicit blocking-borrowed still produces borrowed contour
MeasureFile() explicit queued-owned rollout matches omitted default contour
MeasureFile() direct default and explicit borrowed preserve stable totals and
  validation checksum on same fake archive input
MeasureFile() direct default exposes queue, retention, overlap, pressure, and
  worker telemetry expected for queued-owned async execution
```

Compatibility guardrails:

```text
explicit caller-supplied providerMode must remain authoritative
explicit caller-supplied executionMode must remain authoritative
explicit borrowed calls must not accidentally inherit queued-owned-only queue
  or retention options
invalid queued-owned control combinations must still fail closed
```

Runtime behavior changes:

```text
direct MeasureFile() omitted defaults migrate from blocking-borrowed
partitioned barrier to queued-owned async rollout contour
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
```

Implemented in slice 3:

```text
status: complete
runtime behavior changes:
  direct MeasureFile() omitted provider/execution/queue/retention arguments now
  resolve to the shared queued-owned rollout contour
  direct MeasureCache() omitted defaults remained blocking-borrowed before
  slice 4
implementation:
  MeasureFile() now resolves nullable direct control arguments into effective
  values so omitted provider uses rollout defaults while explicit
  providerMode: BlockingBorrowed remains a borrowed fallback contour
  omitted MeasureFile() uses RadarProcessingArchiveRebalanceRolloutDefaults for
  provider mode, overlap, retention, execution, async worker settings,
  provider queue capacity, retained-byte budget, and overlap consumer delay
  explicit queued-owned and explicit borrowed calls remain authoritative
tests:
  RebalanceArchiveBenchmarkFileUsesRolloutDefaultAndPreservesBorrowedFallback
  now proves omitted MeasureFile() is the rollout contour, explicit
  BlockingBorrowed is still borrowed, explicit queued-owned rollout matches
  the omitted default, and stable totals/checksums match the borrowed oracle
  RebalanceArchiveBenchmarkFileSupportsQueuedOwnedProviderMode now requests
  blocking-borrowed explicitly for fallback coverage
  invalid queued-owned-only control tests now use explicit BlockingBorrowed
  where they are proving borrowed rejection
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
  passed, 23 passed, 0 failed, 0 skipped
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
  passed, 27 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
next:
  slice 4 migrated direct MeasureCache() omitted defaults using the same
  effective-default pattern
```

### 4. Direct Cache Default Migration

Change direct `MeasureCache()` omitted provider/execution/queue/retention
behavior to the accepted rollout contour.

Implementation targets:

```text
MeasureCache() direct default provider mode becomes queued-owned
MeasureCache() direct default execution becomes async shard transport
MeasureCache() direct default async execution uses workers 4 and queue capacity 8
MeasureCache() direct default provider queue capacity becomes 8
MeasureCache() direct default provider overlap becomes producer-consumer
MeasureCache() direct default retention strategy becomes pooled-copy
MeasureCache() direct default retained-byte budget becomes 536870912
MeasureCache() direct default overlap consumer delay remains 0
```

Tests to update or add:

```text
MeasureCache() omitted direct defaults produce queued-owned rollout contour
MeasureCache() explicit blocking-borrowed still produces borrowed contour
MeasureCache() explicit queued-owned rollout matches omitted default contour
MeasureCache() direct default and explicit borrowed preserve published file
  count, batch count, payload values, validation checksum, and skipped counters
MeasureCache() direct default aggregates queue, retention, overlap, pressure,
  and worker telemetry across files
cache filtering by date/radar remains unchanged
```

Compatibility guardrails:

```text
file and cache direct defaults must remain symmetrical
explicit cache borrowed oracle must be easy to invoke for gates
mixed-cache worker failure counters, if present, must be interpreted against
  same-run borrowed rows
```

Runtime behavior changes:

```text
direct MeasureCache() omitted defaults migrate from blocking-borrowed
partitioned barrier to queued-owned async rollout contour
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
```

Implemented in slice 4:

```text
status: complete
runtime behavior changes:
  direct MeasureCache() omitted provider/execution/queue/retention arguments
  now resolve to the shared queued-owned rollout contour
  direct MeasureFile() and MeasureCache() omitted defaults are now symmetric
implementation:
  MeasureCache() now uses the same nullable effective-default resolution
  pattern as MeasureFile()
  omitted provider uses RadarProcessingArchiveRebalanceRolloutDefaults for
  provider mode, overlap, retention, execution, async worker settings,
  provider queue capacity, retained-byte budget, and overlap consumer delay
  explicit providerMode: BlockingBorrowed remains a borrowed fallback/oracle
tests:
  RebalanceArchiveBenchmarkCacheUsesRolloutDefaultAndPreservesBorrowedFallback
  proves omitted MeasureCache() is rollout, explicit BlockingBorrowed remains
  borrowed, explicit queued-owned rollout matches omitted default, and stable
  cache totals/checksums match the borrowed oracle
  borrowed cache comparison rows now request providerMode: BlockingBorrowed
  explicitly
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
  passed, 23 passed, 0 failed, 0 skipped
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
  passed, 27 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
next:
  slice 5 should tighten fallback, failure, cancellation, release, and cleanup
  guardrails around the now-migrated direct defaults
```

### 5. Fallback, Failure, And Cleanup Guardrails

Strengthen regressions that protect the direct migration semantics.

Required coverage:

```text
explicit direct blocking-borrowed does not report queue, retention, or overlap
  telemetry

direct default queued-owned failure does not automatically rerun borrowed

retention failure stops intake and releases accepted retained resources

validation failure reports queued-owned failure rather than borrowed success

cancellation returns current pending, active, and combined retained pressure to
  zero where resources were accepted

builder-transfer remains unsupported

controlled overlap consumer delay remains mechanics proof only and is not part
  of the direct default
```

Existing tests to reuse where possible:

```text
RadarProcessingArchiveQueuedOverlapRunnerTests
RadarProcessingQueuedProviderReadinessGateTests
NexradArchiveRadarEventBatchPublisherTests
RadarPulseCliRebalanceBenchmarkTests for CLI fallback separation
```

Runtime behavior changes:

```text
none beyond the direct default migration from slices 3 and 4
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Implemented in slice 5:

```text
status: complete
runtime behavior changes: none beyond slices 3 and 4
direct API guardrails:
  RebalanceArchiveBenchmarkDirectBorrowedFallbackOmitsQueuedTelemetry proves
  explicit BlockingBorrowed file/cache calls remain selectable and do not
  report queue, retention, overlap, retained-pressure, or worker telemetry
  RebalanceArchiveBenchmarkDirectDefaultFailureDoesNotFallbackToBorrowed
  proves an omitted-provider queued-owned retained-byte-budget failure is
  surfaced as a failure instead of silently rerunning the borrowed path
  RebalanceArchiveBenchmarkDirectDefaultRejectsBuilderTransfer proves
  omitted-provider direct file/cache calls still reject builder-transfer
  retained payload execution
reused lower-level guardrails:
  RadarProcessingArchiveQueuedOverlapRunnerTests cover producer failure,
  cancellation after accepted enqueue, validation failure, release of pending
  and active retained resources, retained-pressure cleanup, and no fallback
  success after queued-owned faults
  RadarProcessingQueuedProviderReadinessGateTests cover queued validation
  failures, retention/release failures, cleanup incompleteness, natural
  evidence exclusion for controlled delay, and threshold interpretation
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
  passed, 50 passed, 0 failed, 0 skipped
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
  passed, 27 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
next:
  slice 6 updated operator help and CLI tests to describe direct
  MeasureFile()/MeasureCache() defaults as the queued-owned rollout contour
```

### 6. Operator Help And Documentation Cleanup

Update user-visible text and docs that still describe direct defaults as
borrowed.

Implementation targets:

```text
CLI help text:
  remove or replace "direct MeasureFile()/MeasureCache() defaults remain
  blocking-borrowed"

CLI help tests:
  assert the new direct default statement

docs:
  keep milestone 012/013 historical statements intact
  update handoff with milestone 014 current state and plan
  later closeout should state the final direct API posture
```

Expected new help posture if migration is accepted:

```text
rebalance-archive omitted-provider default: queued-owned + pooled-copy +
  producer-consumer, async workers 4, queue capacity 8, retained-byte budget
  536870912.

rebalance-archive direct MeasureFile()/MeasureCache() defaults use the same
  queued-owned rollout contour.

rebalance-archive fallback/oracle: use --provider blocking-borrowed for the
  borrowed path and same-run comparison.
```

Guardrails:

```text
do not imply live ingestion/runtime defaults changed
do not imply automatic fallback exists
do not rewrite closed milestone 012/013 docs except handoff carry-forward
```

Runtime behavior changes:

```text
none
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
```

Implemented in slice 6:

```text
status: complete
runtime behavior changes: none
operator help:
  rebalance-archive usage still names the omitted-provider rollout default as
  queued-owned + pooled-copy + producer-consumer, async workers 4, queue
  capacity 8, retained-byte budget 536870912
  rebalance-archive usage now states direct MeasureFile()/MeasureCache()
  defaults use the same queued-owned rollout contour
  rebalance-archive usage still names --provider blocking-borrowed as the
  fallback/oracle path for same-run comparison
  controlled overlap consumer delay remains documented as mechanics proof
  rather than natural rollout evidence
tests:
  RadarPulseCliRebalanceBenchmarkTests now assert the new direct default
  help posture
docs:
  this plan and handoff record slice 6 completion; historical milestone 012
  and 013 statements remain unchanged as closed-context history
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
  passed, 27 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  passed, 761 passed, 0 failed, 3 skipped
next:
  slice 7 should run the focused regression pass before Release gate capture
```

### 7. Focused Regression Pass Before Gate

Run focused tests before recording the Release gate.

Required focused checks:

```text
direct MeasureFile()/MeasureCache() default migration tests
direct explicit blocking-borrowed fallback tests
direct explicit queued-owned equivalence tests
CLI help and rollout contour alignment tests
queued-owned failure, cancellation, cleanup, and fallback tests
readiness threshold interpretation tests
allocation summary attribution tests
```

Expected commands:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Gate precondition:

```text
Do not capture the Release migration gate if focused tests fail or the Release
build has warnings/errors.
```

Implemented in slice 7:

```text
status: complete
runtime behavior changes: none
focused regression:
  direct MeasureFile()/MeasureCache() default migration tests passed
  explicit blocking-borrowed fallback/oracle tests passed
  explicit queued-owned equivalence tests passed
  CLI help and rollout contour alignment tests passed
  queued-owned failure, cancellation, cleanup, and fallback tests passed
  readiness threshold interpretation tests passed
  allocation summary attribution tests passed
verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
  passed, 84 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors
next:
  slice 8 should capture the direct API Release gate
```

### 8. Direct API Release Gate

Capture natural Release evidence for the direct default migration.

Candidate document:

```text
docs/milestones/014-direct-archive-rebalance-api-default-migration-performance-gate.md
```

Required command posture:

```text
Release build before measurements
direct API default rows, not only CLI rows
same-run explicit blocking-borrowed oracle rows
controlled consumer delay 0
same mode, partition count, shard count, parallelism, iterations, and input
between borrowed and default rows
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
```

Required interpretation:

```text
correctness parity
release health
retained cleanup
retained pressure budget
elapsed timing
primary repeated-run spread
allocation ratio, with KTLX 2026-05-05 warning explicit
direct default versus explicit queued-owned contour equivalence
explicit borrowed fallback/oracle separation
allocation attribution
workload coverage limits
```

Expected gate status values:

```text
pass
captured with allocation warning
blocked by named regression
```

Guardrail:

```text
Do not call the gate clean green if KTLX 2026-05-05 remains at or above the
1.10x allocation threshold. Name the warning and decide what it means.
```

### 9. Direct API Migration Decision Trace

Record the direct default migration decision after the gate.

Candidate document:

```text
docs/milestones/014-direct-archive-rebalance-api-default-migration-decision-trace.md
```

Required decisions:

```text
whether direct MeasureFile()/MeasureCache() defaults migrated
whether file and cache surfaces migrated symmetrically
whether explicit blocking-borrowed fallback/oracle posture remains adequate
whether CLI and direct rollout contours remain aligned
whether KTLX 2026-05-05 allocation warning is accepted for direct defaults
whether residual allocation should become the next milestone or remain debt
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
not accept the direct default migration. Record the blocker and the next
milestone input.
```

### 10. Closeout And Handoff

Finalize milestone documentation and project handoff.

Candidate documents:

```text
docs/milestones/014-direct-archive-rebalance-api-default-migration-closeout.md
docs/handoff.md
```

Required closeout content:

```text
final status
implemented behavior
not implemented behavior
final direct API default posture
explicit fallback/oracle posture
focused verification commands and results
Release gate summary
KTLX 2026-05-05 allocation interpretation
residual risks
recommended next milestone input
```

Handoff must state one of:

```text
direct MeasureFile()/MeasureCache() defaults migrated to queued-owned rollout
contour, explicit blocking-borrowed remains fallback/oracle, and the next
milestone can consider a named broader expansion or targeted allocation work

or

direct defaults remain blocking-borrowed because the migration gate found a
named blocker

or

direct defaults migrated only partially, with the asymmetric surface and
follow-up requirement explicitly named
```

Expected verification before closeout:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"

dotnet build RadarPulse.sln -c Release --no-restore

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Guardrail:

```text
Do not mark milestone 014 complete until handoff names the current direct API
default posture, fallback/oracle posture, KTLX allocation posture, and next
milestone recommendation unambiguously.
```

## Verification Strategy

Use focused tests after each implementation slice and broader verification
before the Release gate.

Expected focused coverage:

```text
direct MeasureFile() omitted defaults
direct MeasureCache() omitted defaults
direct explicit blocking-borrowed fallback
direct explicit queued-owned rollout contour
direct file/cache contour symmetry
CLI/direct rollout contour alignment
CLI help text direct default wording
queued-owned invalid option rejection
builder-transfer unsupported posture
controlled consumer-delay rejection and labeling
retained-resource release health
retained pressure cleanup at completion
queued-owned failure and cancellation cleanup
allocation summary attribution
```

Expected broad checks:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Expected performance gate:

```text
Release build before measurements
same-run explicit blocking-borrowed async reference
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
```

## Completion Checklist

```text
[x] direct API baseline audit is captured
[x] shared rollout contour contract is pinned against drift
[x] direct MeasureFile() omitted defaults migrate to queued-owned rollout
[x] direct MeasureCache() omitted defaults migrate to queued-owned rollout
[x] explicit direct blocking-borrowed fallback remains selectable and covered
[x] direct explicit queued-owned rollout calls match omitted direct defaults
[x] CLI omitted-provider rollout contour remains aligned with direct defaults
[x] operator help/docs no longer claim direct defaults remain borrowed
[x] failure, cancellation, release, and cleanup guardrails remain covered
[x] focused regression pass succeeds before gate capture
[ ] direct API Release gate is captured
[ ] KTLX 2026-05-05 allocation warning is repeated and interpreted
[ ] performance gate interprets correctness, cleanup, pressure, allocation,
    timing, variance, fallback/oracle posture, and attribution
[ ] decision trace records the direct API default decision
[ ] closeout is written
[ ] handoff is updated with current direct default, fallback/oracle,
    allocation-risk, and next-milestone posture
```

## Non-Goals

Milestone 014 does not implement:

```text
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
allocation optimization unless required to unblock the migration decision
```

## Closeout Question

The milestone closes by answering:

```text
Should direct RadarProcessingArchiveRebalanceBenchmark.MeasureFile() and
MeasureCache() omitted defaults migrate to the accepted queued-owned rollout
contour?
```

The acceptable answers are:

```text
yes:
  direct file/cache defaults migrate symmetrically, fallback/oracle posture is
  preserved, allocation warning is accepted or bounded, and the next milestone
  can consider a named broader expansion or targeted allocation work

not yet:
  direct defaults remain borrowed, and the decision trace names a blocker such
  as allocation growth, retained pressure, variance, operator ambiguity,
  fallback weakness, or workload coverage

partial:
  one direct surface is not ready, and the decision trace records why the
  migration is asymmetric and what must happen next
```

The milestone should not close with an ambiguous monitoring posture. It should
either accept the direct API default migration or name the blocker that must be
handled next.
