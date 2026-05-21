# Milestone 013: Post-Rollout Hardening And Broader Validation Implementation Plan

Status: draft.

This plan implements the milestone 013 architecture defined in
`013-post-rollout-hardening-broader-validation.md`.

The plan is intentionally scoped to post-rollout hardening for the milestone
012 default contour:
`queued-owned + pooled-copy + producer-consumer`. It should not migrate direct
`MeasureFile()` or `MeasureCache()` defaults, implement live ingestion, add
durable broker integration, add ordered concurrent rebalance, or change the
accepted rollout contour unless the decision trace explicitly records a new
scope.

## Goal

Milestone 013 proves whether the milestone 012 scoped default remains stable
after broader validation and regression hardening.

The scoped default under validation is:

```text
surface: processing benchmark rebalance-archive CLI omitted-provider path
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async
worker count: 4
provider queue capacity: 8
retained-byte budget: 536870912
queue telemetry: summary
overlap telemetry: summary
overlap consumer delay: 0
provider source: rollout-default
```

The most important rules are:

```text
do not change the milestone 012 default contour while validating it
pin omitted-provider behavior against drift
keep blocking-borrowed as explicit fallback and same-run oracle
keep direct MeasureFile()/MeasureCache() defaults borrowed
do not silently fall back from queued-owned failure to borrowed success
separate natural evidence from controlled consumer-delay proof
turn residual allocation overhead into measured attribution
record a stability decision before recommending the next expansion milestone
```

The implementation target is deliberately conservative:

```text
strengthen CLI default/fallback/output tests
make operator help/output clear about the scoped default and fallback
reuse milestone 012 rollout thresholds for post-rollout stability unless a
  threshold change is documented before gate capture
broaden the natural Release workload matrix
capture allocation attribution using existing allocation summaries first
add new attribution fields only where existing summaries leave material gaps
update handoff and closeout with an explicit next-milestone recommendation
```

## Starting Point

Milestone 012 is complete and provides:

```text
ProcessingBenchmarkArchiveRebalanceOptions
  DefaultRolloutWorkerCount = 4
  DefaultRolloutProviderQueueCapacity = 8
  DefaultRolloutRetainedPayloadBytes = 536870912
  EffectiveOptionProvenance
  IsExplicitBlockingBorrowedFallback
  IsRolloutDefaultExpandedContour

ProcessingBenchmarkOptionValueSource
  CurrentDefault
  RolloutDefault
  Explicit
  NotApplicable

RadarProcessingQueuedProviderRolloutThresholds
  release failures: 0
  current retained pressure at completion: 0
  combined retained payload byte budget: 536870912
  candidate-to-borrowed allocation ratio: <= 1.10
  candidate-to-borrowed elapsed ratio: <= 1.00
  candidate run spread / average elapsed ratio: <= 0.075

RadarProcessingQueuedProviderReadinessEvaluator
  EvaluateCorrectnessParity()
  EvaluateRetainedResourceReleaseHealth()
  EvaluateRetainedResourcePressure()
  EvaluateRetainedResourceCleanupCompletion()
  EvaluatePerformanceDelta()
  EvaluateAllocationMovement()
  EvaluateRunSpread()
  EvaluateNaturalEvidence()

RadarProcessingRebalanceAllocationSummary
  MeasuredAllocatedBytes
  ProcessingCallbackAllocatedBytes
  ReplayAndBatchConstructionAllocatedBytes
  OwnedSnapshotAllocatedBytes

RadarProcessingArchiveOverlapTelemetrySummary
  MeasuredAllocatedBytes
  RetentionAllocatedBytes
  UnattributedAllocatedBytes

CLI output
  provider mode/source
  provider overlap/source
  retention strategy/source
  execution/source
  worker count/source
  provider queue capacity/source
  retained-byte budget/source
  queue and overlap telemetry/source
  rollout default contour
  rollout default expansion
  fallback contour
  retained-resource pressure telemetry
  release telemetry
```

Milestone 012 gate facts to carry forward:

```text
primary KTLX default elapsed ratio: 0.855x borrowed
primary KTLX default allocation ratio: 1.072x borrowed
primary KTLX default run spread: 2.39% of candidate average
mixed-cache default elapsed ratio: 0.777x borrowed
mixed-cache default allocation ratio: 1.064x borrowed
max observed combined retained payload high-water: 54413280 bytes
retained-byte budget: 536870912 bytes
release failures: 0
current pending, active, and combined retained pressure at completion: 0
```

Known local gate shapes from milestone 012:

```text
2026-05-04/KINX: 462 files
2026-05-04/KTLX: 244 files
2026-05-05/KTLX: 848 files
```

Current default split:

```text
CLI omitted-provider rebalance-archive:
  queued-owned rollout contour

direct RadarProcessingArchiveRebalanceBenchmark.MeasureFile()/MeasureCache():
  blocking-borrowed compatibility default
```

## Scope

The implementation should explicitly scope milestone 013 to:

```text
processing benchmark rebalance-archive CLI option parsing tests
processing benchmark rebalance-archive CLI help/output compatibility
RadarProcessingArchiveRebalanceBenchmark file/cache result attribution where
  needed for operator-visible allocation evidence
readiness/rollout threshold interpretation reuse
focused failure, cleanup, fallback, and default drift coverage
natural Release gate documentation across broader local data shapes
decision trace, closeout, and handoff
```

The implementation should not change:

```text
direct MeasureFile()/MeasureCache() default provider mode
synthetic benchmark defaults
non-benchmark archive publishing API defaults
domain enum numeric values
retained payload factory strategy semantics
queued session ordering and rebalance commit rules
worker topology capture timing
automatic fallback semantics
```

## Stability Thresholds

Milestone 013 starts from the milestone 012 thresholds. The plan should not
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
  default queued-owned allocated bytes must be <= 1.10x same-run borrowed
  allocated bytes

performance:
  default queued-owned elapsed time must be <= 1.00x same-run borrowed elapsed
  time on the primary repeated natural matrix

variance:
  default queued-owned repeated-run spread should be <= 7.50% of candidate
  average on the primary natural matrix

natural evidence:
  overlap consumer delay must be 0 and evidence scope must be natural rollout
  evidence, not controlled mechanics proof
```

Guardrail:

```text
If any threshold needs to change, record the reason in the plan or decision
trace before interpreting the measurement that depends on the new threshold.
```

## Target Implementation Shape

Presentation owns option default expansion, provenance, help text, and CLI
output. Domain owns stable readiness and allocation summary contracts.
Infrastructure owns benchmark execution and result aggregation. Tests own
default drift, fallback, direct-default compatibility, and guardrail coverage.

Candidate layering:

```text
src/Presentation/Program.cs
  option parsing and provenance
  help text and CLI output labels
  allocation attribution output rows if existing result fields are sufficient

src/Domain/Processing
  readiness threshold contracts only if milestone 012 contracts cannot express
  a post-rollout stability gate
  allocation summary helpers only if existing attribution is too ambiguous

src/Infrastructure/Processing
  archive rebalance benchmark result aggregation
  queue/overlap allocation attribution propagation if missing
  no provider default migration for direct methods

tests/RadarPulse.Tests/Presentation
  CLI default contour drift, fallback, help/output provenance

tests/RadarPulse.Tests/Archive
  direct MeasureFile()/MeasureCache() compatibility and benchmark attribution

tests/RadarPulse.Tests/Processing
  readiness threshold reuse, allocation summary, overlap telemetry, failure
  and cleanup guardrails

docs/milestones
  plan, performance gate, decision trace, closeout

docs/handoff.md
  active milestone state and carry-forward posture
```

The implementation should keep these responsibilities separate:

```text
default drift prevention:
  proves omitted provider flags still resolve to the milestone 012 contour

fallback preservation:
  proves explicit --provider blocking-borrowed remains borrowed and visible

direct compatibility:
  proves direct infrastructure defaults remain borrowed

allocation attribution:
  explains residual overhead without changing provider behavior

broader gate:
  captures Release evidence across local data shapes

decision trace:
  records whether the scoped rollout is stable enough for the next expansion
```

## Implementation Slices

### 1. Post-Rollout Surface Audit

Freeze the current post-rollout behavior before adding new guardrails.

Candidate focus:

```text
src/Presentation/Program.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmarkResult.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceCacheBenchmarkResult.cs
src/Domain/Processing/RadarProcessingRebalanceAllocationSummary.cs
src/Infrastructure/Processing/RadarProcessingArchiveOverlapTelemetrySummary.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProviderReadinessGateTests.cs
docs/milestones/013-post-rollout-hardening-broader-validation-plan.md
```

Required behavior:

```text
document the current milestone 012 rollout contour in the plan
document every current provider-related parsed default and source label
document direct MeasureFile()/MeasureCache() compatibility defaults
document current allocation summary fields and output fields
identify which tests already pin omitted-provider rollout behavior
identify which tests already pin explicit borrowed fallback behavior
identify gaps in direct-default compatibility and allocation attribution
capture local data shapes available for broader gate planning
```

Expected tests or checks:

```text
existing CLI rebalance benchmark tests pass before behavior changes
existing direct benchmark compatibility tests pass before behavior changes
existing readiness gate tests pass before behavior changes
no runtime behavior changes in this slice unless the audit finds broken docs or
  missing test names
```

Candidate verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"
```

Guardrail:

```text
Do not change defaults, thresholds, or benchmark behavior in this slice.
```

Implemented in slice 1:

```text
status:
  complete

runtime changes:
  none

documentation changes:
  this plan now records the post-rollout baseline audit before adding new
  guardrails
  docs/handoff.md names milestone 013 slice 1 as complete and points the next
  implementation step at default contour drift guardrails
```

Current CLI parsed default posture:

```text
ProcessingBenchmarkArchiveRebalanceOptions constructor compatibility defaults:
  ProviderMode = BlockingBorrowed
  ProviderQueueCapacity = 1
  ProviderQueueTimeout = null
  ProviderOverlapMode = None
  RetentionStrategy = SnapshotCopy
  ProviderQueueRetainedPayloadBytes = null
  OverlapConsumerDelay = TimeSpan.Zero
  QueueTelemetryOutput = Summary
  OverlapTelemetryOutput = Summary
  ExecutionMode = PartitionedBarrier
  AsyncExecution = null

ProcessingBenchmarkArchiveRebalanceOptions.Parse() local defaults:
  providerMode = BlockingBorrowed
  providerOverlapMode = None
  retentionStrategy = SnapshotCopy
  queueRetainedPayloadBytes = null
  queueTimeout = null
  queueTelemetryOutput = Summary
  overlapTelemetryOutput = Summary
  overlapConsumerDelay = TimeSpan.Zero
  executionMode = PartitionedBarrier
  workerCount = null
  queueCapacity = null

omitted --provider rollout expansion:
  provider mode -> queued-owned
  provider overlap -> producer-consumer when omitted
  retention strategy -> pooled-copy when omitted
  execution -> async when omitted
  worker count -> 4 when omitted and async
  provider queue capacity -> 8 when omitted
  retained-byte budget -> 536870912 when omitted
  queue telemetry -> summary with rollout-default provenance when omitted
  overlap telemetry -> summary with rollout-default provenance when omitted
  overlap consumer delay -> 0 with rollout-default provenance when omitted

provenance:
  omitted rollout-expanded fields are RolloutDefault
  explicit fields are Explicit
  non-rollout inherited fields are CurrentDefault
  inapplicable fields print not-applicable in output
```

Current option and output contracts:

```text
rollout constants:
  DefaultRolloutWorkerCount = 4
  DefaultRolloutProviderQueueCapacity = DefaultCandidateProviderQueueCapacity
  DefaultRolloutRetainedPayloadBytes = DefaultCandidateRetainedPayloadBytes

rollout identity:
  IsRolloutDefaultExpandedContour requires the default-candidate contour and
  provider-mode provenance RolloutDefault

explicit fallback identity:
  IsExplicitBlockingBorrowedFallback requires blocking-borrowed provider mode
  and explicit provider-mode provenance

evidence labels:
  default candidate -> natural-default-candidate / natural-readiness
  explicit producer-consumer queued-owned -> natural-opt-in / opt-in-diagnostic
  controlled consumer delay -> controlled-proof / controlled-mechanics-proof
  borrowed or non-overlap -> not-applicable / not-applicable

CLI output currently prints:
  provider-related source labels
  Provider default rollout contour
  Provider rollout default expansion
  Provider fallback contour
  Default-candidate contour
  Provider overlap evidence contour
  Provider overlap evidence scope
```

Current direct infrastructure default posture:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile() defaults:
  executionMode = PartitionedBarrier
  asyncExecution = null
  providerMode = BlockingBorrowed
  queueCapacity = 1
  queueTimeout = null
  providerOverlapMode = None
  retentionStrategy = SnapshotCopy
  queueRetainedPayloadBytes = null
  overlapConsumerDelay = TimeSpan.Zero

RadarProcessingArchiveRebalanceBenchmark.MeasureCache() defaults:
  same provider, queue, retention, overlap, and execution defaults as
  MeasureFile()

direct derived defaults:
  async execution without explicit async options uses workerCount = shardCount
  and worker queue capacity = 1
  blocking-borrowed result QueueCapacity is 0
  blocking-borrowed result QueueRetainedPayloadBytes is null
  queued-owned result QueueCapacity and QueueRetainedPayloadBytes reflect
  explicit queued-owned arguments
```

Current allocation and telemetry attribution surface:

```text
RadarProcessingRebalanceAllocationSummary:
  MeasuredAllocatedBytes
  ProcessingCallbackAllocatedBytes
  ReplayAndBatchConstructionAllocatedBytes
  OwnedSnapshotAllocatedBytes
  IncludesArchiveReplayAndBatchConstruction
  IncludesCliFormatting

RadarProcessingArchiveOverlapTelemetrySummary:
  MeasuredAllocatedBytes
  RetentionAllocatedBytes
  UnattributedAllocatedBytes

CLI output currently prints:
  End-to-end allocated bytes
  Processing callback allocated bytes
  Replay and batch construction allocated bytes
  Allocation includes CLI formatting
  Provider queue owned snapshot allocated bytes
  Retained payload allocated bytes
  Provider overlap retention allocated bytes
  Provider overlap measured allocated bytes
  Provider overlap unattributed allocated bytes
```

Existing tests that already pin milestone 013 baseline inputs:

```text
RadarPulseCliRebalanceBenchmarkTests.
  ArchiveRebalanceBenchmarkOptionsExpandOmittedProviderToRolloutDefaults
  ArchiveRebalanceBenchmarkOptionsTrackExplicitBorrowedFallbackProvenance
  ArchiveRebalanceBenchmarkOptionsExposeRolloutDefaultContourConstants
  ArchiveRebalanceBenchmarkOptionsRequireFileAndCompatibleTopology
  ArchiveRebalanceBenchmarkCommandUsesRolloutDefaultsWhenProviderOmitted
  ArchiveRebalanceBenchmarkCommandLabelsExplicitBorrowedFallback
  ArchiveRebalanceBenchmarkCommandLabelsDefaultCandidateContour
  ArchiveRebalanceBenchmarkCommandEmitsQueuedProviderTelemetry
  ArchiveRebalanceBenchmarkCommandEmitsOverlapTelemetry

NexradArchiveRadarEventBatchPublisherTests.
  RebalanceArchiveBenchmarkCachePreservesBorrowedDefaultAndExplicitRolloutContour
  RebalanceArchiveBenchmarkCacheAsyncMatchesSynchronousTotals
  RebalanceArchiveBenchmarkFileSupportsQueuedOwnedProviderMode
  RebalanceArchiveBenchmarkFileSupportsQueuedOwnedOverlapAndRetentionStrategy
  RebalanceArchiveBenchmarkQueuedOwnedAsyncKeepsWorkerTelemetry
  RebalanceArchiveBenchmarkCacheQueuedOwnedAggregatesQueueTelemetry
  RebalanceArchiveBenchmarkControlledConsumerDelayProvesQueuedAheadOverlap

RadarProcessingQueuedProviderReadinessGateTests.
  ReadinessContractsUseStableValuesAndRejectInvalidShapes
  CorrectnessGateRequiresBorrowedReferenceForDefaultReadiness
  AllocationGateHandlesMissingReferenceAndRegression
  RolloutThresholdsApplyAllocationPerformanceAndRunSpreadRatios

RadarProcessingRebalanceAllocationSummaryTests.
  ArchiveSummarySeparatesCallbackAndReplayAllocation
  ArchiveSummaryDoesNotReportNegativeReplayAllocation
  ArchiveBenchmarkResultKeepsEndToEndAndCallbackAllocationSeparate
```

Gaps carried into later slices:

```text
default contour drift guard should assert the full effective contour and
  provenance as a single regression contract, not only individual fields
direct MeasureFile() default compatibility is exercised through borrowed
  reference calls but still deserves a focused explicit guard in slice 3
CLI help still lists provider flags as optional without naming the rollout
  default contour or explicit borrowed fallback semantics
allocation attribution is present but not yet interpreted as a dominant-source
  overhead breakdown for broader gate reporting
```

Local data shapes available for the broader gate:

```text
data\nexrad\level2\2026\05\04\KINX: 462 files
data\nexrad\level2\2026\05\04\KTLX: 244 files
data\nexrad\level2\2026\05\05\KTLX: 848 files
data\nexrad total files: 1554
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"
```

Recorded result:

```text
60 passed, 0 failed, 0 skipped.
```

### 2. Default Contour Drift Guardrails

Add focused regression coverage that treats the whole rollout contour as the
default contract.

Candidate focus:

```text
src/Presentation/Program.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Required behavior:

```text
omitted provider-related flags expand to:
  provider mode queued-owned
  producer-consumer overlap
  pooled-copy retention
  async execution
  worker count 4
  provider queue capacity 8
  retained-byte budget 536870912
  queue telemetry summary
  overlap telemetry summary
  overlap consumer delay 0
  rollout-default provenance for omitted rollout-expanded fields

explicit queued-owned rollout-shaped runs remain explicit, not rollout-default
explicit --provider blocking-borrowed remains fallback
queued-owned-only controls remain rejected with explicit blocking-borrowed
builder-transfer remains rejected
controlled consumer delay remains outside default rollout evidence
```

Expected tests or checks:

```text
parse-level tests assert every effective rollout default and source
command-output smoke tests assert rollout-default source labels and contour
command-output smoke tests assert explicit fallback labels
negative validation tests assert fail-closed mixed options
```

Candidate verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
```

Guardrail:

```text
Do not make the tests depend only on ProviderMode. The drift guard must cover
the full effective contour and provenance.
```

### 3. Direct API Compatibility Guardrails

Pin the deliberate split between CLI defaults and direct benchmark method
defaults.

Candidate focus:

```text
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
```

Required behavior:

```text
MeasureFile() without provider arguments remains blocking-borrowed
MeasureCache() without provider arguments remains blocking-borrowed
direct borrowed defaults remain partitioned unless execution is explicitly set
direct explicit queued-owned rollout contour remains available and produces
  queued-owned telemetry
direct explicit queued-owned rollout contour matches the borrowed oracle on
  deterministic totals and checksums
direct compatibility tests explain that direct default migration is a future
  milestone decision
```

Expected tests or checks:

```text
direct MeasureFile()/MeasureCache() default tests assert borrowed result state
direct explicit queued-owned tests assert queue, retained, overlap, and worker
telemetry remain populated
same-run borrowed/direct queued-owned comparison remains available
```

Candidate verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
```

Guardrail:

```text
Do not migrate direct infrastructure defaults in milestone 013.
```

### 4. Operator Help And Output Compatibility Cleanup

Make the scoped default and fallback posture obvious to operators.

Candidate focus:

```text
src/Presentation/Program.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
docs/milestones/013-post-rollout-hardening-broader-validation-plan.md
```

Required behavior:

```text
CLI help for processing benchmark rebalance-archive names the scoped omitted
  provider default contour
help text names explicit --provider blocking-borrowed as fallback/oracle
output continues to print source labels for provider-related values
output distinguishes:
  rollout-default omitted-provider contour
  explicit blocking-borrowed fallback
  explicit queued-owned diagnostic contours
  controlled mechanics proof rows
output keeps direct API default migration out of scope
```

Expected tests or checks:

```text
CLI help smoke test asserts default/fallback wording if help capture exists or
  is added
output tests assert source labels and contour flags
existing command smoke tests remain stable
```

Candidate verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
```

Guardrail:

```text
Help text can clarify behavior, but it must not imply live ingestion or direct
API defaults are queued-owned.
```

### 5. Allocation Attribution Pass

Make residual allocation overhead measurable enough to guide future work.

Candidate focus:

```text
src/Domain/Processing/RadarProcessingRebalanceAllocationSummary.cs
src/Infrastructure/Processing/RadarProcessingArchiveOverlapTelemetrySummary.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmarkResult.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceCacheBenchmarkResult.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Presentation/Program.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceAllocationSummaryTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Required behavior:

```text
reuse existing allocation fields first:
  measured allocated bytes
  processing callback allocated bytes
  replay and batch construction allocated bytes
  owned snapshot allocated bytes
  retained payload allocated bytes
  overlap measured allocated bytes
  overlap retention allocated bytes
  overlap unattributed allocated bytes

add or expose new fields only if existing summaries cannot answer:
  how much of queued-owned overhead is retained payload copy cost
  how much is processing callback versus replay/build allocation
  how much overlap allocation is unattributed after retention accounting

CLI output should make attribution visible without unbounded per-batch detail
cache aggregation should sum attribution consistently across files
borrowed rows should report not-applicable or zero owned-retention categories
```

Expected tests or checks:

```text
allocation summary tests cover derived attribution math
archive benchmark tests prove file and cache result attribution propagation
CLI output tests prove new attribution rows are printed when applicable
borrowed fallback output does not pretend to have retained payload allocation
```

Candidate verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
```

Guardrail:

```text
Do not optimize allocation before attribution is clear unless a small fix is
obviously required to make attribution correct.
```

### 6. Failure, Cleanup, And Fallback Regression Pass

Keep milestone 012 fail-closed behavior active under the hardened default.

Candidate focus:

```text
tests/RadarPulse.Tests/Archive/ArchiveOwnedRadarEventBatchQueueingPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProcessingSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedRebalanceSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProviderReadinessGateTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Required behavior:

```text
retention failure stops intake and releases accepted resources
producer failure releases pending retained resources
processing failure releases active retained resources
validation failure reports candidate/default failure
release failure increments failed-release telemetry and fails readiness
cancellation releases accepted retained resources and leaves current pressure
  at zero
explicit borrowed fallback is selected only by explicit provider option
queued-owned failure does not become borrowed success
```

Expected tests or checks:

```text
existing failure/cancellation coverage still passes
readiness evaluator rejects release failures and incomplete cleanup
CLI fallback tests prove fallback remains explicit
```

Candidate verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
```

Guardrail:

```text
Do not add automatic fallback-to-borrowed behavior in milestone 013.
```

### 7. Focused Regression Pass Before Gate

Run focused verification after default, compatibility, allocation, and failure
guardrails are in place.

Candidate commands:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Expected behavior:

```text
focused CLI tests pass
direct benchmark compatibility tests pass
readiness, overlap, and allocation tests pass
failure and cleanup tests pass
Release build succeeds
```

Guardrail:

```text
Do not capture the post-rollout performance gate until focused default,
fallback, direct compatibility, allocation, readiness, and cleanup coverage is
green.
```

### 8. Broader Natural Release Gate

Capture the post-rollout stability gate across broader local workload shapes.

Candidate document:

```text
docs/milestones/013-post-rollout-hardening-broader-validation-performance-gate.md
```

Candidate common command parameters:

```text
processing benchmark rebalance-archive
--mode rebalance
--iterations 1
--warmup-iterations 0
--parallelism 24
--partitions 24
--shards 4
```

Borrowed reference adds:

```text
--provider blocking-borrowed
--execution async
--workers 4
```

Rollout default rows omit provider, execution, worker, queue, retained budget,
and telemetry flags. The CLI should expand those omitted flags to the
milestone 012 rollout default contour.

Candidate contours:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KINX --max-files 220 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KINX --max-files 220 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-05 --radar KTLX --max-files 220 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-05 --radar KTLX --max-files 220 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

Required capture:

```text
same-run borrowed reference rows
omitted-provider rollout-default rows
expanded effective configuration and option provenance
correctness parity fields
topology/rebalance parity fields
release telemetry
pending, active, and combined retained pressure
allocation summary and attribution fields
allocation ratio against borrowed
elapsed ratio against borrowed
queue depth and overlap indicators
evidence contour and scope
primary repeated matrix with enough rows to interpret spread
additional single-shape and mixed-cache rows for broader coverage
```

Expected interpretation:

```text
validation parity must pass
release failures must be 0
current retained pressure must return to 0
combined retained pressure must stay under 536870912 bytes
allocation ratio must be <= 1.10x borrowed
primary matrix elapsed ratio must be <= 1.00x borrowed
primary candidate spread must be <= 7.50% of candidate average
default expansion evidence must pass
fallback separation must pass
allocation attribution must identify dominant residual overhead categories or
  record a blocker if attribution remains too ambiguous
```

Guardrail:

```text
Do not close milestone 013 from a single favorable row. The gate should show
whether the already-rolled-out default remains stable across broader local
workload shapes.
```

### 9. Stability Decision Trace

Record the post-rollout stability decision after the gate.

Candidate document:

```text
docs/milestones/013-post-rollout-hardening-broader-validation-decision-trace.md
```

Required decisions:

```text
whether the milestone 012 scoped default remains stable
whether fallback/oracle posture remains adequate
whether direct API migration is a reasonable next milestone
whether residual allocation overhead is acceptable under the threshold
whether allocation attribution is sufficient to guide future work
whether retained pressure and release health support broader expansion
whether timing and variance support broader expansion
whether operator output/help is clear enough for compatibility
whether any blocker should be addressed before direct API or runtime rollout
```

Expected decision matrix:

```text
default stability posture
fallback/oracle posture
direct API compatibility posture
threshold acceptance
workload coverage limits
allocation attribution result
operator-surface result
residual risks
recommended next milestone input
```

Guardrail:

```text
If the gate misses a threshold, do not call the scoped default broadly stable.
Record whether the miss requires more hardening, more attribution, a contour
change proposal, or rollback consideration.
```

### 10. Closeout And Handoff

Finalize the milestone documentation and project handoff.

Candidate documents:

```text
docs/milestones/013-post-rollout-hardening-broader-validation-closeout.md
docs/handoff.md
```

Required closeout content:

```text
final status
implemented behavior
not implemented behavior
final verification commands and results
performance gate summary
post-rollout stability decision
current default posture
fallback/oracle posture
direct API compatibility posture
allocation attribution summary
residual risks
recommended next milestone input
```

Handoff must state one of:

```text
the scoped queued-owned default remains stable after broader validation and
the recommended next milestone is direct API migration or another named
expansion

or

the scoped queued-owned default remains in place but broader expansion is
blocked by a named issue

or

the scoped queued-owned default should be revisited because broader validation
found a named correctness, cleanup, release, pressure, or compatibility
regression
```

Expected verification before closeout:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"

dotnet build RadarPulse.sln -c Release --no-restore

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Guardrail:

```text
Do not mark the milestone complete until handoff names the current default
posture, fallback/oracle posture, direct API compatibility posture, and next
milestone recommendation unambiguously.
```

## Verification Strategy

Use focused tests after each implementation slice and broader verification
before the Release gate.

Expected focused coverage:

```text
CLI option parsing defaults
CLI output source/provenance labels
CLI help text default/fallback wording
explicit blocking-borrowed fallback
explicit queued-owned diagnostic contours
controlled consumer-delay rejection and labeling
direct MeasureFile()/MeasureCache() compatibility defaults
direct explicit queued-owned rollout contour
readiness threshold interpretation
allocation summary attribution
archive benchmark result attribution propagation
retained-resource release health
retained pressure cleanup at completion
queued-owned failure and cancellation cleanup
```

Expected broad checks:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Expected performance gate:

```text
Release build before measurements
same-run blocking-borrowed async reference
default queued-owned rollout contour selected through omitted provider flags
active/combined retained pressure visible in output
allocation attribution visible in output
repeated natural rows and variance interpretation
broader single-shape rows over available local data
mixed-cache row over all local data
allocation ratio checked against 1.10x threshold
performance ratio checked against 1.00x threshold
controlled proof rows separated if captured
```

## Completion Checklist

```text
[ ] post-rollout surface audit is captured
[ ] milestone 012 rollout contour is pinned against drift
[ ] explicit blocking-borrowed fallback remains selectable and visible
[ ] direct MeasureFile()/MeasureCache() defaults remain borrowed
[ ] operator help/output makes scoped default and fallback reproducible
[ ] allocation attribution is visible enough to explain residual overhead
[ ] failure, cancellation, release, and cleanup guardrails remain covered
[ ] focused regression pass succeeds before gate capture
[ ] broader natural Release gate is captured
[ ] performance gate interprets correctness, cleanup, pressure, allocation,
    timing, variance, provenance, and attribution
[ ] decision trace records the post-rollout stability decision
[ ] closeout is written
[ ] handoff is updated with current default, fallback/oracle, compatibility,
    and next-milestone posture
```

## Non-Goals

Milestone 013 does not implement:

```text
direct MeasureFile()/MeasureCache() default migration
synthetic benchmark default migration
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
```

## Closeout Question

The milestone closes by answering:

```text
Is the milestone 012 scoped queued-owned default stable enough to be the
baseline for the next expansion decision?
```

The acceptable answers are:

```text
yes:
  the scoped default remains stable, fallback/oracle posture is preserved,
  allocation overhead is bounded and attributed, and the next milestone can
  consider direct API migration or another named expansion

not yet:
  the scoped default remains in place, but broader expansion waits on a named
  blocker such as allocation attribution, retained pressure, variance,
  operator ambiguity, or workload coverage

revisit:
  broader validation found a named correctness, cleanup, release, pressure, or
  compatibility regression that should be addressed before treating the scoped
  default as stable
```

The milestone should not close with an ambiguous monitoring posture. It should
either confirm the scoped default as a stable baseline or name the blocker that
must be handled next.
