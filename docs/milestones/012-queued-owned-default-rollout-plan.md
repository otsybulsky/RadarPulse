# Milestone 012: Queued-Owned Default Rollout Implementation Plan

Status: draft.

This plan implements the milestone 012 architecture defined in
`012-queued-owned-default-rollout.md`.

The plan is intentionally scoped to an explicit provider-default rollout
decision for the already-measured milestone 011 contour:
`queued-owned + pooled-copy + producer-consumer`. It should not implement
`builder-transfer`, durable broker integration, live ingestion, cross-process
workers, concurrent rebalance-enabled processing, source-level migration,
partition splitting, or complex radar algorithms.

## Goal

Milestone 012 decides whether and how the optimized queued-owned contour
becomes the default provider behavior for the scoped archive rebalance
benchmark/runtime surface.

The target default contour is:

```text
provider mode: queued-owned
retention strategy: pooled-copy
provider overlap: producer-consumer
execution: async
queue capacity: 8
retained-byte budget: 536870912
queue telemetry: summary
overlap telemetry: summary
overlap consumer delay: disabled
```

The most important rules are:

```text
rollout thresholds are documented before final gate interpretation
blocking-borrowed remains explicitly selectable
same-run blocking-borrowed remains available as the benchmark oracle
queued-owned failure must not silently fall back to borrowed success
controlled consumer delay remains mechanics-only proof
the default contour should not drift while rollout evidence is captured
```

The first implementation target is deliberately conservative:

```text
scope default changes to processing rebalance archive benchmark options
make default-derived provider settings explicit in parsed options and output
preserve explicit --provider blocking-borrowed behavior
preserve explicit opt-in diagnostic contours for non-default queued-owned
  combinations
use existing retained-resource pressure and readiness evaluator contracts
capture Release evidence before closeout
```

## Starting Point

Milestone 011 is complete and provides:

```text
ProcessingBenchmarkArchiveRebalanceOptions
  DefaultCandidateProviderQueueCapacity = 8
  DefaultCandidateRetainedPayloadBytes = 536870912
  MatchesDefaultCandidateContour()
  ProviderOverlapEvidenceContour
  ProviderOverlapEvidenceScope

RadarProcessingQueuedProviderReadinessEvaluator
  EvaluateCorrectnessParity()
  EvaluateRetainedResourceReleaseHealth()
  EvaluateRetainedResourcePressure()
  EvaluateNaturalEvidence()
  EvaluatePerformanceDelta()
  EvaluateAllocationMovement()
  EvaluateRunVariance()

RadarProcessingArchiveRebalanceBenchmark
  MeasureFile()
  MeasureCache()
  queued-owned provider execution
  producer-consumer overlap execution
  retained payload telemetry
  provider queue and overlap telemetry

CLI output
  provider mode
  default-candidate contour
  provider overlap evidence contour
  provider overlap evidence scope
  retained-resource pressure telemetry
  release telemetry
```

Current defaults before milestone 012 implementation:

```text
provider mode: blocking-borrowed
provider overlap: none
retention strategy: snapshot-copy
queue capacity: 1
retained-byte budget: none
execution: partitioned barrier unless --execution async is supplied
queue telemetry: summary
overlap telemetry: summary, but rejected unless provider overlap is enabled
```

Milestone 011 gate facts to carry forward:

```text
expanded mixed-cache correctness parity: passed
expanded mixed-cache release failures: 0
expanded mixed-cache combined retained payload high-water: 54_413_280 bytes
retained-byte budget: 536_870_912 bytes
expanded mixed-cache borrowed elapsed ms: 77_530.68
expanded mixed-cache candidate elapsed ms: 71_181.17
post-optimization candidate allocation ratio: 1.066x borrowed
natural queue depth high-water: 1
controlled proof remains mechanics-only evidence
```

## Rollout Scope

The implementation should explicitly scope the first default change to:

```text
processing benchmark rebalance-archive CLI option parsing
RadarProcessingArchiveRebalanceBenchmark file/cache benchmark invocation paths
CLI benchmark output for file and cache results
tests that exercise archive rebalance benchmark defaults
documentation and handoff
```

The implementation should not change:

```text
synthetic processing benchmark defaults unless a slice explicitly proves they
  use the same provider surface
archive download, inspection, replay-only, or parse benchmark defaults
domain enum numeric values
retained payload factory strategy semantics
queued session ordering and rebalance commit rules
```

## Rollout Thresholds

Thresholds must be recorded before the final performance gate is interpreted.
The implementation can refine exact names and constants, but the plan should
start with these rollout thresholds:

```text
correctness parity:
  required, same-run borrowed reference must match

topology/rebalance parity:
  required, accepted moves, skipped decisions, failed migrations, and topology
  versions must match the reference

release failures:
  must equal 0

retained cleanup:
  current pending, active, and combined retained batch/byte counts must return
  to 0 at completion

retained pressure:
  combined retained payload high-water must be <= configured retained-byte
  budget

allocation:
  candidate/default allocated bytes must be <= 1.10x same-run borrowed
  allocated bytes

performance:
  candidate/default elapsed time must be <= 1.00x same-run borrowed elapsed
  time on the primary repeated matrix, unless the decision trace explicitly
  defers the rollout

variance:
  candidate repeated-run spread should be <= 7.50% of candidate average on the
  primary matrix

natural evidence:
  overlap consumer delay must be 0 and evidence scope must be natural rollout
  evidence, not controlled mechanics proof
```

Rationale for initial thresholds:

```text
allocation threshold 1.10x gives narrow headroom over the milestone 011
  measured 1.066x residual overhead while still rejecting a regression toward
  the pre-optimization allocation profile

performance threshold 1.00x keeps the first default rollout conservative:
  the new default should not be slower than the same-run borrowed oracle on the
  primary repeated matrix

variance threshold 7.50% allows some noise above the milestone 011 candidate
  5.01% primary spread while still preventing a noisy result from being used
  as rollout proof
```

Guardrail:

```text
Do not change thresholds after seeing final gate measurements. If thresholds
need to change, record the reason in the decision trace before interpreting the
gate.
```

## Target Implementation Shape

Domain should own any stable rollout-gate vocabulary that is not already
covered by readiness contracts. Infrastructure should continue to own benchmark
execution. Presentation should own option default expansion and operator
output.

Candidate layering:

```text
src/Domain/Processing
  readiness/rollout threshold contracts if existing readiness result shapes are
  not sufficient
  no provider default policy if the default is presentation/runtime-specific

src/Infrastructure/Processing
  benchmark default helper or constants only if infrastructure methods need a
  reusable default contour
  no silent fallback behavior

src/Presentation
  CLI option parsing default expansion
  source-of-value flags for default-derived provider settings if needed
  output labels for default rollout contour and fallback selection

tests/RadarPulse.Tests
  CLI option and output defaults
  benchmark invocation defaults
  readiness/rollout threshold interpretation
  fallback and failure policy guardrails
```

The implementation should keep these responsibilities separate:

```text
default expansion:
  how omitted provider-related CLI options resolve

explicit fallback:
  how --provider blocking-borrowed preserves old behavior

validation oracle:
  how borrowed reference runs remain available for gate comparison

rollout gate:
  how Release evidence is interpreted

operator output:
  how defaults and explicit options are made reproducible
```

## Implementation Slices

### 1. Baseline Default Surface Audit

Freeze the current default behavior before changing it.

Candidate focus:

```text
src/Presentation/Program.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProviderReadinessGateTests.cs
docs/milestones/012-queued-owned-default-rollout-plan.md
```

Required behavior:

```text
document every current provider-related default used by
  ProcessingBenchmarkArchiveRebalanceOptions.Parse()
document record-constructor defaults in ProcessingBenchmarkArchiveRebalanceOptions
document MeasureFile/MeasureCache provider defaults in
  RadarProcessingArchiveRebalanceBenchmark
document help text that still describes provider selection as optional
identify tests that currently expect blocking-borrowed output by default
identify explicit queued-owned tests that must continue to work
```

Expected tests or checks:

```text
existing CLI rebalance benchmark tests pass before behavior changes
existing queued provider readiness gate tests pass before behavior changes
no runtime behavior changes in this slice
```

Guardrail:

```text
Do not change defaults in this slice. Establish the audit trail and edit points
first.
```

Slice 1 baseline capture:

```text
status:
  complete

runtime changes:
  none

CLI help surface:
  radarpulse processing benchmark rebalance-archive still presents provider
  controls as optional flags:
    --provider blocking-borrowed|queued-owned
    --provider-overlap none|producer-consumer
    --retention-strategy snapshot-copy|pooled-copy|builder-transfer
    --execution sync|async
    --workers n
    --queue-capacity n
    --queue-timeout-ms n
    --queue-retained-bytes n
    --queue-telemetry none|summary|recent
    --overlap-telemetry none|summary|recent
    --overlap-consumer-delay-ms n

ProcessingBenchmarkArchiveRebalanceOptions constructor defaults:
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

Parse() derived defaults:
  when execution is async:
    AsyncExecution worker count defaults to shard count
    AsyncExecution queue capacity defaults to 1
  when provider mode is queued-owned:
    ProviderQueueCapacity defaults to queueCapacity ?? 1
  when provider mode is blocking-borrowed:
    ProviderQueueCapacity remains 1
    ProviderQueueRetainedPayloadBytes remains null

Parse() current validation rules:
  --queue-timeout-ms requires --provider queued-owned
  --queue-retained-bytes requires --provider queued-owned
  --provider-overlap requires --provider queued-owned
  explicit --retention-strategy requires --provider queued-owned
  builder-transfer is rejected for queued-owned
  explicit non-none --overlap-telemetry requires
    --provider-overlap producer-consumer
  --overlap-consumer-delay-ms must be positive and requires
    --provider queued-owned --provider-overlap producer-consumer
  --workers requires --execution async
  --queue-capacity requires --execution async or --provider queued-owned

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

MeasureFile()/MeasureCache() derived defaults:
  async execution without explicit async options uses workerCount = shardCount
  and worker queue capacity = 1
  result.QueueCapacity is 0 for blocking-borrowed and queueCapacity for
  queued-owned
  result.QueueRetainedPayloadBytes is null for blocking-borrowed and the
  configured value for queued-owned

current milestone 011 candidate constants:
  DefaultCandidateProviderQueueCapacity = 8
  DefaultCandidateRetainedPayloadBytes = 536870912
  MatchesDefaultCandidateContour() requires:
    queued-owned
    queue capacity 8
    producer-consumer overlap
    pooled-copy retention
    retained-byte budget 536870912
    zero consumer delay
    queue telemetry not none
    overlap telemetry not none
    async execution

tests that currently pin blocking-borrowed or pre-rollout defaults:
  RadarPulseCliRebalanceBenchmarkTests.
    ArchiveRebalanceBenchmarkOptionsParseFileModeAndTopology
      -> ProviderMode BlockingBorrowed
      -> ProviderQueueCapacity 1
      -> ProviderOverlapMode None
      -> RetentionStrategy SnapshotCopy
      -> ProviderQueueRetainedPayloadBytes null
      -> ExecutionMode async only when explicitly supplied
      -> default-candidate no / not-applicable evidence
  RadarPulseCliRebalanceBenchmarkTests.
    ArchiveRebalanceBenchmarkOptionsRequireFileAndCompatibleTopology
      -> queued-only controls without --provider queued-owned are rejected
  NexradArchiveRadarEventBatchPublisherTests.
    RebalanceArchiveBenchmarkCacheAsyncMatchesSynchronousTotals
      -> direct MeasureCache() without provider args remains borrowed
  NexradArchiveRadarEventBatchPublisherTests.
    RebalanceArchiveBenchmarkFileSupportsQueuedOwnedProviderMode
      -> direct borrowed reference uses omitted provider args

explicit queued-owned tests that must continue to work after rollout:
  RadarPulseCliRebalanceBenchmarkTests.
    ArchiveRebalanceBenchmarkOptionsParseQueuedProviderSettings
  RadarPulseCliRebalanceBenchmarkTests.
    ArchiveRebalanceBenchmarkOptionsIdentifyDefaultCandidateContour
  RadarPulseCliRebalanceBenchmarkTests.
    ArchiveRebalanceBenchmarkCommandEmitsQueuedProviderTelemetry
  RadarPulseCliRebalanceBenchmarkTests.
    ArchiveRebalanceBenchmarkCommandEmitsOverlapTelemetry
  RadarPulseCliRebalanceBenchmarkTests.
    ArchiveRebalanceBenchmarkCommandLabelsDefaultCandidateContour
  RadarPulseCliRebalanceBenchmarkTests.
    ArchiveRebalanceBenchmarkCommandSuppressesOptionalTelemetryWhenNone
  NexradArchiveRadarEventBatchPublisherTests.
    RebalanceArchiveBenchmarkQueuedOwnedAsyncKeepsWorkerTelemetry
  NexradArchiveRadarEventBatchPublisherTests.
    RebalanceArchiveBenchmarkCacheQueuedOwnedAggregatesQueueTelemetry
  NexradArchiveRadarEventBatchPublisherTests.
    RebalanceArchiveBenchmarkCacheOverlapUsesSharedQueueAcrossFiles
  NexradArchiveRadarEventBatchPublisherTests.
    RebalanceArchiveBenchmarkControlledConsumerDelayProvesQueuedAheadOverlap

slice 2 implementation input:
  rollout threshold contracts should be added before changing defaults
  because slice 1 found no existing cleanup-completion gate for current
  pending/active/combined retained pressure returning to zero
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
38 passed, 0 failed, 0 skipped for focused CLI, readiness gate, and overlap
runner baseline coverage.
```

### 2. Rollout Threshold Contracts

Capture rollout thresholds in stable code or documentation before final gate
measurement.

Candidate focus:

```text
src/Domain/Processing/RadarProcessingQueuedProviderReadinessEvaluator.cs
src/Domain/Processing/RadarProcessingQueuedProviderReadinessResult.cs
src/Domain/Processing/RadarProcessingQueuedProviderReadinessGate.cs
src/Domain/Processing/RadarProcessingQueuedProviderReadinessError.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProviderReadinessGateTests.cs
docs/milestones/012-queued-owned-default-rollout-performance-gate.md
```

Required behavior:

```text
define the milestone 012 threshold values before final gate interpretation
reuse existing readiness evaluator methods where possible
add rollout-specific helper/contracts only if existing readiness methods cannot
  express completion pressure, allocation ratio, performance ratio, or variance
ensure cleanup completion checks include current pending, active, and combined
  retained counts/bytes returning to zero
ensure controlled evidence remains rejected for natural rollout evidence
```

Expected tests or checks:

```text
allocation ratio <= 1.10 passes and > 1.10 fails
performance ratio <= 1.00 passes and > 1.00 fails
candidate spread <= 7.50% passes and > 7.50% fails
release failures fail
missing active/combined retained pressure remains inconclusive where required
non-zero current retained pressure at completion fails rollout if represented
controlled consumer delay fails natural rollout evidence
```

Guardrail:

```text
Threshold tests should use synthetic values. Do not tune thresholds from a
fresh benchmark row in this slice.
```

Implemented in slice 2:

```text
status:
  complete

runtime provider default changes:
  none

new contract:
  RadarProcessingQueuedProviderRolloutThresholds

default rollout thresholds:
  RequiredReleaseFailureCount = 0
  RequiredCurrentRetainedBatchCount = 0
  RequiredCurrentRetainedPayloadBytes = 0
  CombinedRetainedPayloadBytesBudget = 536870912
  MaximumCandidateToBorrowedAllocationRatio = 1.10
  MaximumCandidateToBorrowedElapsedRatio = 1.00
  MaximumCandidateRunSpreadRatio = 0.075

new readiness evaluator behavior:
  EvaluateRetainedResourceCleanupCompletion()
    -> inconclusive when retained-resource pressure telemetry is missing
    -> passed when current pending/active/combined retained counts and bytes
       have returned to zero, even if high-water marks prove earlier pressure
    -> failed with RetainedResourceCleanupIncomplete when current combined
       retained batch count or payload bytes remain non-zero at completion

  EvaluateRunSpread()
    -> not evaluated when repeated natural candidate measurements are missing
    -> inconclusive when candidate average elapsed time is zero
    -> passed when run spread / average <= configured threshold
    -> failed with RunVarianceTooHigh when run spread / average exceeds the
       configured threshold

existing evaluator behavior reused for rollout thresholds:
  EvaluateRetainedResourceReleaseHealth()
    -> release failures and retention failures fail
  EvaluateRetainedResourcePressure()
    -> combined retained payload bytes above budget fail
  EvaluatePerformanceDelta()
    -> candidate elapsed / borrowed elapsed above 1.00 fails under default
       milestone 012 threshold
  EvaluateAllocationMovement()
    -> candidate allocation / borrowed allocation above 1.10 fails under
       default milestone 012 threshold
  EvaluateNaturalEvidence()
    -> controlled consumer-delay rows remain rejected as natural evidence
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
14 passed, 0 failed, 0 skipped for focused readiness gate coverage.
41 passed, 0 failed, 0 skipped for focused CLI, readiness gate, and overlap
runner coverage.
```

### 3. Default Contour Constants And Option Provenance

Introduce the rollout contour as the default source of truth for presentation
option expansion.

Candidate focus:

```text
src/Presentation/Program.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Required behavior:

```text
centralize rollout default constants next to existing milestone 011 candidate
  constants or in a small helper local to the CLI options type
track whether provider mode, retention strategy, provider overlap, queue
  capacity, retained-byte budget, queue telemetry, overlap telemetry, execution,
  and worker count were supplied explicitly or default-expanded
keep explicit --provider blocking-borrowed available
keep explicit --provider queued-owned variations available as opt-in
  diagnostics when they do not match the rollout contour
do not make source provenance leak into domain or infrastructure result types
  unless output cannot otherwise report it
```

Expected tests or checks:

```text
parsing with omitted provider options still preserves current blocking-borrowed
  defaults in this slice while marking provider-related fields as
  current-default sourced
parsing with --provider blocking-borrowed preserves borrowed provider,
  overlap none, no retained-byte budget, explicit fallback provenance, and
  fallback evidence labels
parsing the milestone 011 contour keeps all supplied provider fields marked
  explicit and does not mark them as rollout-default expanded yet
parsing with explicit queued-owned non-contour values remains opt-in diagnostic
  or is rejected according to existing validation rules
invalid builder-transfer remains rejected
controlled consumer delay remains rejected outside queued-owned
  producer-consumer contours
```

Guardrail:

```text
Do not change the candidate contour while making it default. The rollout
default should match milestone 011 unless a decision trace entry changes it
before gate capture.
```

Implemented in slice 3:

```text
status:
  complete

runtime provider default changes:
  none

new presentation contracts:
  ProcessingBenchmarkOptionValueSource
    -> CurrentDefault
    -> Explicit
    -> RolloutDefault

  ProcessingBenchmarkArchiveRebalanceOptionProvenance
    -> ProviderMode
    -> ProviderOverlapMode
    -> RetentionStrategy
    -> QueueCapacity
    -> QueueRetainedPayloadBytes
    -> QueueTelemetry
    -> OverlapTelemetry
    -> OverlapConsumerDelay
    -> ExecutionMode
    -> WorkerCount

new option helpers:
  EffectiveOptionProvenance
  IsExplicitBlockingBorrowedFallback
  IsRolloutDefaultExpandedContour

rollout contour constants now exposed next to existing default-candidate
constants:
  DefaultRolloutWorkerCount = 4
  DefaultRolloutProviderQueueCapacity = DefaultCandidateProviderQueueCapacity
  DefaultRolloutRetainedPayloadBytes = DefaultCandidateRetainedPayloadBytes

parse behavior preserved:
  omitted --provider still resolves to blocking-borrowed
  omitted --provider-overlap still resolves to none
  omitted --retention-strategy still resolves to snapshot-copy
  omitted --queue-capacity still resolves to provider queue capacity 1
  omitted --queue-retained-bytes still resolves to none
  omitted --execution still resolves to partitioned barrier
  omitted --workers still resolves to no async execution worker override

provenance behavior:
  omitted provider-related options are marked CurrentDefault
  explicitly supplied provider-related options are marked Explicit
  explicit --provider blocking-borrowed marks provider mode Explicit and
    IsExplicitBlockingBorrowedFallback true
  explicit milestone 011 candidate contour remains natural-readiness evidence
    but IsRolloutDefaultExpandedContour false until slice 4 changes expansion
  RolloutDefault source is available for slice 4 but not emitted by parsing yet
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
23 passed, 0 failed, 0 skipped for focused CLI rebalance benchmark coverage.
44 passed, 0 failed, 0 skipped for focused CLI, readiness gate, and overlap
runner coverage.
```

### 4. CLI Default Expansion And Validation Rules

Change `processing benchmark rebalance-archive` omitted provider options to
the rollout contour within the scoped surface.

Candidate focus:

```text
src/Presentation/Program.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Required behavior:

```text
omitting --provider uses queued-owned
omitting --retention-strategy under default queued-owned uses pooled-copy
omitting --provider-overlap under default queued-owned uses producer-consumer
omitting --execution under default queued-owned uses async
omitting --workers under default async uses the rollout worker count if the
  scoped contour requires workers 4; otherwise document why shard-count workers
  remain the default
omitting --queue-capacity under default queued-owned uses 8
omitting --queue-retained-bytes under default queued-owned uses 536870912
queue and overlap telemetry remain enabled at summary level for default rollout
overlap consumer delay remains zero unless explicitly supplied for controlled
  proof
explicit blocking-borrowed does not inherit queued-owned-only options
```

Expected tests or checks:

```text
default CLI smoke output prints Provider mode: queued-owned
default CLI smoke output prints Retention strategy: pooled-copy
default CLI smoke output prints Provider overlap mode: producer-consumer
default CLI smoke output prints Execution mode: async
default CLI smoke output prints provider queue capacity 8
default CLI smoke output prints retained-byte budget 536870912
explicit --provider blocking-borrowed output prints blocking-borrowed and does
  not print queued-owned readiness telemetry as default evidence
explicit --provider blocking-borrowed with queued-only options is rejected
existing explicit queued-owned controlled-proof tests still pass with updated
  labels
```

Guardrail:

```text
Do not silently reinterpret invalid mixed options as fallback. Reject impossible
or ambiguous option combinations.
```

Implemented in slice 4:

```text
status:
  complete

default behavior change:
  ProcessingBenchmarkArchiveRebalanceOptions.Parse() now expands omitted
  provider flags to the milestone 012 queued-owned rollout contour for the
  scoped processing benchmark rebalance-archive CLI surface

omitted provider expansion:
  ProviderMode = queued-owned
  ProviderOverlapMode = producer-consumer
  RetentionStrategy = pooled-copy
  ExecutionMode = async
  AsyncExecution.WorkerCount = 4
  AsyncExecution.QueueCapacity = 8
  ProviderQueueCapacity = 8
  ProviderQueueRetainedPayloadBytes = 536870912
  QueueTelemetryOutput = summary
  OverlapTelemetryOutput = summary
  OverlapConsumerDelay = 0

provenance:
  omitted rollout-expanded provider fields are marked RolloutDefault
  explicitly supplied provider fields remain Explicit
  explicit --provider blocking-borrowed remains Explicit and
    IsExplicitBlockingBorrowedFallback true
  explicit milestone 011 queued-owned contour remains natural-readiness but
    IsRolloutDefaultExpandedContour false because the contour was not selected
    by omitted provider defaults

fallback behavior:
  explicit --provider blocking-borrowed preserves borrowed provider,
  provider-overlap none, snapshot-copy retention, no retained-byte budget,
  provider queue capacity 1 in parsed options, and partitioned execution unless
  execution is explicitly supplied

validation changes:
  queued-owned-only controls without --provider are now valid because omitted
  provider means rollout-default queued-owned
  the same queued-owned-only controls remain rejected when --provider
  blocking-borrowed is explicit
  builder-transfer remains rejected
  controlled consumer delay remains rejected outside queued-owned
  producer-consumer contours

command output:
  a rebalance-archive command with omitted provider flags now prints
    Provider mode: queued-owned
    Provider queue capacity: 8
    Provider overlap mode: producer-consumer
    Retention strategy: pooled-copy
    Provider queue retained byte capacity: 536_870_912
    Default-candidate contour: yes
    Provider overlap evidence scope: natural-readiness
    Execution mode: async
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
24 passed, 0 failed, 0 skipped for focused CLI rebalance benchmark coverage.
45 passed, 0 failed, 0 skipped for focused CLI, readiness gate, and overlap
runner coverage.
```

### 5. Operator Output For Default Versus Explicit Selection

Make the new default auditable in CLI output.

Candidate focus:

```text
src/Presentation/Program.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Required behavior:

```text
print whether provider mode came from default expansion or explicit option
print whether retention strategy, provider overlap, queue capacity, retained
  byte budget, execution, and worker count came from default expansion where
  useful
rename or supplement "Default-candidate contour" if needed so the output
  distinguishes rollout-default evidence from opt-in readiness evidence
keep existing evidence contour/scope strings stable unless tests and docs are
  intentionally updated
ensure fallback borrowed runs are visibly fallback/explicit, not natural
  queued-owned rollout evidence
```

Candidate output fields:

```text
Provider mode source: default|explicit
Retention strategy source: default|explicit|not-applicable
Provider overlap source: default|explicit|not-applicable
Provider queue capacity source: default|explicit|not-applicable
Provider queue retained byte capacity source: default|explicit|not-applicable
Provider default rollout contour: yes|no
Provider fallback contour: yes|no
```

Expected tests or checks:

```text
default run prints provider source default and rollout contour yes
explicit queued-owned rollout flags print provider source explicit and rollout
  contour yes
explicit borrowed run prints provider source explicit and fallback contour yes
natural opt-in diagnostic rows remain distinguishable from rollout evidence
controlled proof rows remain controlled-mechanics-proof
```

Guardrail:

```text
Do not remove milestone 011 evidence labels unless the replacement remains
clear for existing gate interpretation.
```

Implemented in slice 5:

```text
status:
  complete

output changes:
  rebalance-archive file and cache output now receive the parsed CLI options
  alongside benchmark results so operator-visible output can print option
  provenance without changing MeasureFile/MeasureCache invocation behavior

source vocabulary:
  rollout-default:
    value came from omitted-provider milestone 012 default expansion
  explicit:
    value came from an explicit CLI option
  current-default:
    value came from a non-rollout existing default under an explicit provider
    contour
  not-applicable:
    source belongs to queued-owned or async-only behavior that is not active
    for the printed run

new source fields:
  Provider mode source
  Provider overlap source
  Retention strategy source
  Provider queue capacity source
  Worker queue capacity source
  Provider queue retained byte capacity source
  Queue telemetry source
  Provider overlap telemetry source
  Provider overlap consumer delay source
  Execution mode source
  Worker count source

new contour fields:
  Provider default rollout contour:
    yes when the printed run matches the queued-owned default rollout shape
  Provider rollout default expansion:
    yes only when the rollout shape came from omitted provider defaults
  Provider fallback contour:
    yes only for explicit --provider blocking-borrowed fallback runs

preserved fields:
  Default-candidate contour remains printed
  Provider overlap evidence contour remains stable
  Provider overlap evidence scope remains stable

covered output rows:
  omitted provider default expansion prints rollout-default sources and
    Provider rollout default expansion: yes
  explicit queued-owned rollout-shape run prints explicit sources,
    Provider default rollout contour: yes, and Provider rollout default
    expansion: no
  explicit blocking-borrowed run prints Provider fallback contour: yes and
    not-applicable for queued-owned-only sources
  natural opt-in diagnostic run remains opt-in-diagnostic
  controlled proof run remains controlled-mechanics-proof
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
25 passed, 0 failed, 0 skipped for focused CLI rebalance benchmark coverage.
46 passed, 0 failed, 0 skipped for focused CLI, readiness gate, and overlap
runner coverage.
```

### 6. Benchmark Invocation Defaults And Same-Run Oracle Preservation

Ensure benchmark invocation paths receive the intended defaults and can still
run borrowed reference comparisons.

Candidate focus:

```text
src/Presentation/Program.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
```

Required behavior:

```text
CLI passes expanded default rollout options to MeasureFile/MeasureCache
explicit --provider blocking-borrowed passes borrowed options to
  MeasureFile/MeasureCache
same-run borrowed reference commands remain documented for performance gates
infrastructure method optional parameters can remain blocking-borrowed if direct
  API callers rely on old defaults, as long as CLI rollout scope is explicit
if infrastructure method defaults are changed too, direct API tests must prove
  explicit blocking-borrowed remains available
```

Expected tests or checks:

```text
CLI default cache benchmark reaches queued-owned code path in smoke coverage
CLI explicit borrowed benchmark reaches blocking-borrowed code path
direct MeasureFile/MeasureCache tests are updated only if the method default
  scope changes
provider overlap is not allowed with borrowed provider
retention strategy is not allowed with borrowed provider unless explicitly
  documented otherwise
```

Guardrail:

```text
Do not remove the borrowed benchmark path. It remains the oracle even if it is
no longer the scoped CLI default.
```

Implemented in slice 6:

```text
status:
  complete

runtime changes:
  none

CLI invocation proof:
  omitted-provider rebalance-archive cache smoke output now asserts the
  queued-owned batch lifetime, queue telemetry, retained payload telemetry,
  overlap telemetry, pooled-copy overlap retention strategy, async execution,
  rollout-default provenance, and default rollout contour

fallback invocation proof:
  explicit --provider blocking-borrowed cache smoke output now asserts the
  borrowed batch lifetime, blocking-borrowed provider result, provider queue
  capacity 0 in the benchmark result, no queue/retained/overlap telemetry, no
  worker telemetry, partitioned execution, explicit provider source, and
  Provider fallback contour: yes

direct benchmark oracle proof:
  RadarProcessingArchiveRebalanceBenchmark.MeasureCache() without provider
  arguments remains blocking-borrowed with partitioned execution, no queue
  telemetry, no retained telemetry, no overlap telemetry, and no worker
  telemetry

direct explicit rollout proof:
  MeasureCache() with explicit queued-owned + producer-consumer + pooled-copy
  + async + worker count 4 + queue capacity 8 + retained byte capacity
  536870912 returns queued-owned result state, worker telemetry, queue
  telemetry, retained telemetry, overlap telemetry, zero release failures, and
  the same stable totals/checksum as the same-run borrowed oracle

guardrail status:
  the direct infrastructure defaults were not changed
  blocking-borrowed remains the same-run benchmark oracle
  queued-owned default rollout remains scoped to CLI omitted-provider expansion
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Recorded result:

```text
46 passed, 0 failed, 0 skipped for focused CLI and archive benchmark coverage.
67 passed, 0 failed, 0 skipped for focused CLI, archive benchmark, readiness
gate, and overlap runner coverage.
```

### 7. Failure, Cleanup, And Fallback Guardrails Under Default Queued-Owned

Prove that default queued-owned failure paths fail closed.

Candidate focus:

```text
tests/RadarPulse.Tests/Archive/ArchiveOwnedRadarEventBatchQueueingPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProcessingSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedRebalanceSessionTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Required behavior:

```text
retention failure does not silently rerun borrowed
producer failure releases accepted pending retained resources
processing failure releases active retained resources
validation failure is reported as candidate/default failure
release failure increments failed-release telemetry and fails readiness
cancellation releases accepted retained resources and leaves current pressure
  at zero
explicit borrowed fallback is selected only by explicit provider option
```

Expected tests or checks:

```text
existing milestone 011 failure/cancellation coverage still passes under the
  new default-expansion code
CLI or options tests prove fallback is explicit
readiness/rollout evaluator rejects release failures and non-zero cleanup
  pressure if represented
```

Guardrail:

```text
Do not add automatic fallback-to-borrowed behavior in milestone 012.
```

Implemented in slice 7:

```text
status:
  complete

runtime changes:
  none

new active release failure guardrail:
  ArchiveOwnedRadarEventBatchQueueingPublisher now has focused coverage proving
  that a consumer-side retained payload release failure increments release
  failure telemetry, clears active and combined current pressure, and fails
  RadarProcessingQueuedProviderReadinessEvaluator release-health evaluation

new overlap validation failure guardrail:
  RadarProcessingArchiveQueuedOverlapRunner now has focused coverage proving
  that an invalid queued-owned producer-consumer rebalance batch faults the
  consumer result, reports FailedValidation, keeps the producer completed,
  releases the active retained resource, leaves current pending/active/combined
  retained pressure at zero, and does not turn the failure into borrowed
  success

existing guardrails kept active:
  retention failure stops the current publish and leaves only already accepted
  resources for terminal cleanup
  producer failure releases accepted pending retained resources
  cancellation after accepted enqueue releases pending resources and leaves
  current retained pressure at zero
  queued processing validation failure releases active retained resources
  explicit borrowed fallback remains selected only through explicit
  --provider blocking-borrowed
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"
```

Recorded result:

```text
71 passed, 0 failed, 0 skipped for focused failure, cleanup, CLI, and readiness
coverage.
78 passed, 0 failed, 0 skipped for focused CLI, archive benchmark, failure,
cleanup, readiness, and overlap coverage.
```

### 8. Focused Regression Pass Before Gate

Run focused verification after default behavior changes and before performance
measurement.

Candidate commands:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Expected behavior:

```text
focused CLI tests pass with updated default expectations
focused readiness tests pass with rollout thresholds
focused failure/cleanup tests pass
Release build succeeds
```

Guardrail:

```text
Do not capture the rollout performance gate until focused defaults, readiness,
fallback, and cleanup coverage pass.
```

### 9. Natural Rollout Performance Gate

Capture the Release gate that decides the default rollout.

Candidate document:

```text
docs/milestones/012-queued-owned-default-rollout-performance-gate.md
```

Candidate commands:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

Required capture:

```text
same-run borrowed reference rows
default queued-owned rows using omitted provider flags
expanded effective configuration proving default expansion
primary repeated KTLX matrix with enough rows to interpret spread
expanded mixed-cache row or matrix
correctness parity fields
topology/rebalance parity fields
release telemetry
pending, active, and combined retained pressure
allocation ratio against borrowed
elapsed ratio against borrowed
queue depth and HasQueuedAheadOverlap
evidence contour and scope
```

Expected interpretation:

```text
validation parity must pass
release failures must be 0
current retained pressure must return to 0
combined retained pressure must stay under 536_870_912 bytes
allocation ratio must be <= 1.10x borrowed
primary matrix elapsed ratio must be <= 1.00x borrowed
primary candidate spread must be <= 7.50% of candidate average
controlled proof rows, if captured, remain in a separate mechanics section
```

Guardrail:

```text
Do not close the milestone from a single favorable row. The gate must show
whether omitted provider flags truly use the rollout default.
```

### 10. Rollout Decision Trace

Record the default decision after the gate.

Candidate document:

```text
docs/milestones/012-queued-owned-default-rollout-decision-trace.md
```

Required decisions:

```text
whether queued-owned becomes the scoped default
which command/runtime surfaces are included in the default rollout
whether blocking-borrowed remains explicit fallback
whether same-run borrowed comparison remains required for future benchmark
  gates
whether residual allocation overhead is acceptable under the threshold
whether timing and variance support the default switch
whether retained pressure and release health support the default switch
```

Expected decision matrix:

```text
default provider posture
fallback posture
borrowed oracle posture
rollout threshold acceptance
controlled proof separation
residual allocation risk
out-of-scope runtime expansions
```

Guardrail:

```text
If the gate misses a threshold, do not quietly keep the implementation and call
the milestone successful. Either revert the default change before closeout or
record an explicit defer outcome where blocking-borrowed remains default.
```

### 11. Closeout And Handoff

Finalize the milestone documentation and project handoff.

Candidate documents:

```text
docs/milestones/012-queued-owned-default-rollout-closeout.md
docs/handoff.md
```

Required closeout content:

```text
final status
implemented behavior
not implemented behavior
final verification commands and results
performance gate summary
final default posture
fallback posture
residual risks
carry-forward items
```

Handoff must state one of:

```text
queued-owned + pooled-copy + producer-consumer is now the scoped default,
with blocking-borrowed available as explicit fallback

or

blocking-borrowed remains the default because the rollout gate missed a named
threshold
```

Expected verification before closeout:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"

dotnet build RadarPulse.sln -c Release --no-restore

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Guardrail:

```text
Do not mark the milestone complete until handoff names the current provider
default unambiguously.
```

## Verification Strategy

Use focused tests after each implementation slice and broader verification
before the performance gate.

Expected focused coverage:

```text
CLI option parsing defaults
CLI output source/provenance labels
explicit blocking-borrowed fallback
explicit queued-owned diagnostic contours
controlled consumer-delay rejection and labeling
rollout threshold interpretation
retained-resource release health
retained pressure cleanup at completion
archive queued overlap default behavior
benchmark result propagation
```

Expected broad checks:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Expected performance gate:

```text
Release build before measurements
same-run blocking-borrowed async reference
default queued-owned rollout contour selected through omitted provider flags
active/combined retained pressure visible in output
repeated natural rows and variance interpretation
allocation ratio checked against 1.10x threshold
performance ratio checked against 1.00x threshold
controlled proof rows separated if captured
```

## Completion Checklist

```text
[x] baseline default surface audit is captured
[x] rollout thresholds are recorded before final gate interpretation
[x] default contour constants/provenance are implemented or documented
[x] CLI default expansion resolves to queued-owned rollout contour
[x] explicit blocking-borrowed fallback remains selectable
[x] same-run borrowed benchmark oracle remains available
[x] operator output distinguishes default expansion, fallback, rollout
    contour, opt-in diagnostic, and controlled proof
[x] invalid mixed provider options fail closed
[x] failure, cancellation, and cleanup guardrails remain covered
[ ] focused regression pass succeeds before gate capture
[ ] natural rollout performance gate is captured
[ ] performance gate interprets correctness, cleanup, pressure, allocation,
    timing, and variance thresholds
[ ] decision trace records whether the provider default changed
[ ] closeout is written
[ ] handoff is updated with the current default provider posture
```

## Non-Goals

Milestone 012 does not implement:

```text
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
automatic silent fallback from queued-owned failure to borrowed success
```

## Closeout Question

The milestone closes by answering:

```text
Does queued-owned + pooled-copy + producer-consumer become the scoped default
provider behavior now?
```

The acceptable answers are:

```text
yes:
  queued-owned becomes the scoped default, blocking-borrowed remains explicit
  fallback, and the rollout gate passes under recorded thresholds

not yet:
  blocking-borrowed remains the default, queued-owned remains opt-in, and the
  decision trace names the missed threshold or missing evidence
```

The milestone should not close with an ambiguous "ready later" conclusion. It
should either change the default deliberately or preserve the old default with
a concrete blocker.
