# Milestone 016: Broader Cache-Level Default Readiness Implementation Plan

Status: complete.

This plan implements the milestone 016 architecture defined in
`016-broader-cache-level-default-readiness.md`.

The plan is intentionally scoped to broader cache-level readiness for the
already accepted queued-owned direct/default archive rebalance contour. It
should not implement live ingestion, durable broker integration, cross-process
workers, ordered concurrent rebalance, builder-transfer, file-level default
optimization, synthetic benchmark defaults, or non-benchmark archive publishing
defaults.

## Goal

Milestone 016 decides whether the queued-owned direct/default contour is ready
as the broader cache-level benchmark/default posture for available cache
workloads.

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

The milestone target is not to change that contour. The target is to broaden
the cache-level evidence, classify every cache shape explicitly, and decide
whether the readiness accepted in milestone 015 holds beyond the measured
milestone 015 contours.

The most important rules are:

```text
preserve direct MeasureFile()/MeasureCache() queued-owned omitted defaults
preserve explicit BlockingBorrowed direct calls as fallback and oracle
keep same-run borrowed rows in every readiness gate
keep CLI omitted-provider cache benchmark aligned with direct defaults
classify every cache shape individually before aggregate interpretation
keep the single-file cold warning visible as a file-level concern
do not silently fall back from queued-owned failure to borrowed success
do not tune the accepted rollout contour to make a cache row pass
do not broaden into live/runtime/durable surfaces
do not raise thresholds after seeing gate measurements
```

The implementation target is evidence-driven:

```text
inventory available cache data before selecting gate rows
repeat milestone 015 baseline cache shapes for drift detection
add or improve benchmark reporting only where reviewability requires it
prove direct and CLI omitted-provider alignment before Release gate capture
capture same-run BlockingBorrowed and queued-owned rows for every selected
  cache shape
classify correctness, release, cleanup, pressure, elapsed, allocation, spread,
  and attribution per shape
record whether broader readiness is accepted, accepted with warnings,
  rejected, coverage-insufficient, or deferred
```

## Starting Point

Milestone 015 is complete and provides:

```text
direct MeasureFile()/MeasureCache() omitted defaults:
  queued-owned rollout contour

explicit fallback/oracle:
  providerMode: BlockingBorrowed

CLI omitted-provider rebalance-archive path:
  aligned with the same queued-owned rollout contour

cache-level allocation posture:
  primary KTLX 2026-05-04 allocation ratio 1.042x borrowed
  KTLX 2026-05-05 allocation ratio 1.0392x borrowed average
  KTLX 2026-05-05 rows 1.0404x and 1.0381x borrowed
  KINX 2026-05-04 allocation ratio 1.042x borrowed
  mixed-cache allocation ratio 1.021x borrowed

file-level warning:
  representative KTLX single-file cold smoke allocation ratio 1.512x borrowed
  representative KTLX single-file cold smoke elapsed ratio 1.072x borrowed
  interpretation: file-level concern, not cache-level blocker
```

Milestone 015 gate facts to carry forward:

```text
primary KTLX 2026-05-04:
  elapsed ratio: 0.889x borrowed
  allocation ratio: 1.042x borrowed
  default timing spread: 1.10%
  retained high-water: 48257280 bytes

KTLX 2026-05-05:
  elapsed ratio: 0.943x borrowed average
  allocation ratio: 1.0392x borrowed average
  retained high-water: 52676640 bytes

KINX 2026-05-04:
  elapsed ratio: 0.899x borrowed
  allocation ratio: 1.042x borrowed
  retained high-water: 48342240 bytes

mixed cache:
  elapsed ratio: 0.871x borrowed
  allocation ratio: 1.021x borrowed
  retained high-water: 54413280 bytes

release and cleanup:
  release failures 0
  current pending, active, and combined retained pressure returned to 0
```

Known local cache data at milestone start:

```text
data\nexrad\level2\2026\05\04\KTLX: 244 files
data\nexrad\level2\2026\05\04\KINX: 462 files
data\nexrad\level2\2026\05\05\KTLX: 848 files
data\nexrad total files: 1554
```

Residual limits carried forward:

```text
single-file cold allocation is not a cache-level blocker but blocks any
  file-level default readiness claim

milestone 015 was a local gate over available NEXRAD cache shapes on
  2026-05-21

natural direct default rows kept queue depth at 1; queue-ahead mechanics are
  covered by controlled tests rather than natural gate rows

mixed-cache rows had matching worker failed batch/item counters in borrowed
  and direct default rows while validation still succeeded
```

## Scope

The implementation should explicitly scope milestone 016 to:

```text
local or explicitly approved NEXRAD cache inventory
cache-shape selection before Release gate interpretation
same-run explicit BlockingBorrowed oracle preservation
direct MeasureCache() omitted-default readiness rows
CLI omitted-provider cache alignment checks
explicit queued-owned rollout drift checks where useful
focused reporting or harness improvements needed for repeatable gate capture
correctness, validation, cleanup, release, retained pressure, timing, spread,
  allocation, and attribution interpretation
per-cache-shape pass/warning/fail/coverage classification
single-file cold warning scope preservation
natural Release gate over broader cache-level rows
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
file-level cold behavior as the primary target
```

## Readiness Thresholds

Milestone 016 starts from the milestone 015 thresholds. The plan should not
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
  borrowed allocated bytes for cache-level readiness rows

performance:
  queued-owned direct/default elapsed time should remain <= 1.00x same-run
  borrowed elapsed time for cache-level readiness rows, or be classified as a
  warning/blocker with attribution

variance:
  repeated natural direct/default spread should remain <= 7.50% of candidate
  average, or the decision trace must explain why the spread does not block
  the readiness conclusion

natural evidence:
  overlap consumer delay must be 0 and evidence scope must remain natural
  readiness evidence, not controlled mechanics proof
```

Per-shape status vocabulary:

```text
pass:
  all required thresholds pass and attribution is sufficient

warning:
  correctness, release, cleanup, and pressure pass, but elapsed, allocation,
  spread, worker counters, or attribution needs explicit scoped acceptance

fail:
  correctness, release, cleanup, pressure, fail-closed behavior, or an
  unaccepted elapsed/allocation threshold fails

coverage-only:
  row is useful evidence but cannot support readiness by itself because the
  corpus, file count, selector, or repeatability is insufficient
```

Guardrail:

```text
Do not change thresholds after seeing final gate measurements. If thresholds
need to change, record the reason before interpreting the gate.
```

## Target Implementation Shape

The safest implementation shape is to start with corpus inventory, then
verify guardrails, then capture natural Release evidence.

Candidate layering:

```text
Domain:
  owns allocation summary contracts, retained resource contracts, pressure
  summaries, and queued provider validation vocabulary

Infrastructure:
  owns archive rebalance benchmark execution, retained payload telemetry,
  queueing, overlap runner, direct result construction, and cache traversal

Presentation:
  owns CLI option parsing, help, output formatting, and option provenance

Tests:
  own deterministic contour assertions, fallback/oracle assertions, CLI/direct
  alignment, retained release/cleanup guardrails, and cache result contracts

Documentation:
  owns corpus inventory, gate matrix, per-shape interpretation, decision trace,
  closeout, and handoff posture
```

Implementation guidance:

```text
prefer documentation and reporting improvements before runtime changes
use existing structured result fields instead of parsing output text
keep direct defaults and CLI rollout constants pinned against drift
avoid changing result contract shape unless gate reviewability requires it
do not add new cache data without recording provenance and selection rules
do not make the gate depend on controlled consumer delay
record any rejected coverage or reporting idea before closeout
```

Primary files to inspect:

```text
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceRolloutDefaults.cs
src/Infrastructure/Processing/RadarProcessingArchiveProviderMode.cs
src/Infrastructure/Processing/RadarProcessingRetainedPayloadFactory.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapRunner.cs
src/Infrastructure/Processing/RadarProcessingOwnedBatchQueue.cs
src/Domain/Processing/RadarProcessingRebalanceAllocationSummary.cs
src/Domain/Processing/RadarProcessingOwnedSnapshotAllocationSummary.cs
src/Domain/Processing/RadarProcessingRetainedPayloadTelemetrySummary.cs
src/Domain/Processing/RadarProcessingProviderQueueTelemetrySummary.cs
src/Presentation/Program.cs
docs/milestones/015-queued-owned-allocation-readiness-performance-gate.md
docs/milestones/015-queued-owned-allocation-readiness-decision-trace.md
```

Primary tests to inspect or extend:

```text
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceAllocationSummaryTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedPayloadFactoryTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedBatchResourceTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProviderReadinessGateTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

## Implementation Slices

### 1. Corpus Inventory And Gate Matrix Design

Capture the available workload surface before changing code or interpreting
new measurements.

Questions to answer:

```text
which local NEXRAD cache roots are available now?
which radar/date/file-count selectors reproduce the milestone 015 rows?
which additional cache-level size variations are feasible without turning the
  gate into file-level readiness?
which rows need repetition for spread interpretation?
which workload classes remain absent after local inventory?
```

Files and data to inspect:

```text
data/nexrad
docs/milestones/015-queued-owned-allocation-readiness-performance-gate.md
docs/milestones/015-queued-owned-allocation-readiness-closeout.md
docs/milestones/016-broader-cache-level-default-readiness.md
```

Expected gate matrix draft:

```text
primary KTLX 2026-05-04 bounded cache, repeated
KTLX 2026-05-05 named risk bounded cache, repeated
KINX 2026-05-04 cross-radar bounded cache
mixed local cache over available roots
larger local cache slice where runtime cost is acceptable
smaller cache-level slice that still amortizes retained cold cost
representative single-file smoke only for warning visibility
```

Runtime behavior changes:

```text
none
```

Documentation updates:

```text
record corpus inventory and proposed gate matrix in this plan under the slice
1 completion notes
```

Focused verification:

```powershell
Get-ChildItem data\nexrad -Recurse -File | Measure-Object
```

Slice 1 completion notes:

```text
status: complete
runtime behavior changes: none

inventory command:
  Get-ChildItem data\nexrad -Recurse -File | Measure-Object -Property Length
  -Sum

local cache roots:
  data\nexrad\level2\2026\05\04\KTLX:
    files: 244
    bytes: 1_347_625_897

  data\nexrad\level2\2026\05\04\KINX:
    files: 462
    bytes: 1_404_452_903

  data\nexrad\level2\2026\05\05\KTLX:
    files: 848
    bytes: 2_232_493_336

  data\nexrad total:
    files: 1_554
    bytes: 4_984_572_136

available workload breadth:
  two radar sites: KTLX and KINX
  two dates: 2026-05-04 and 2026-05-05
  three radar/date roots
  one mixed local cache spanning all available roots

milestone 015 reproduction selectors:
  primary repeated contour:
    data\nexrad --date 2026-05-04 --radar KTLX --max-files 220

  named risk repeated contour:
    data\nexrad --date 2026-05-05 --radar KTLX --max-files 220

  cross-radar contour:
    data\nexrad --date 2026-05-04 --radar KINX --max-files 220

  mixed-cache contour:
    data\nexrad --max-files 1000000

  file-level smoke contour:
    data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06

proposed minimum milestone 016 gate matrix:
  primary drift/spread row:
    direct MeasureCache() and same-run explicit BlockingBorrowed over
    data\nexrad --date 2026-05-04 --radar KTLX --max-files 220,
    repeated 3 pairs

  named allocation-risk row:
    direct MeasureCache() and same-run explicit BlockingBorrowed over
    data\nexrad --date 2026-05-05 --radar KTLX --max-files 220,
    repeated 2 pairs, with a third pair if the first two are noisy

  cross-radar row:
    direct MeasureCache() and same-run explicit BlockingBorrowed over
    data\nexrad --date 2026-05-04 --radar KINX --max-files 220

  mixed-cache row:
    direct MeasureCache() and same-run explicit BlockingBorrowed over
    data\nexrad --max-files 1000000

  CLI/direct alignment spot-check:
    processing benchmark rebalance-archive --cache using an omitted provider
    selector that matches one direct cache row

  file-level warning visibility:
    representative MeasureFile() single-file smoke over
    data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06,
    interpreted only as file-level cold retained-ownership cost

proposed size-extension rows if Release runtime budget allows:
  KTLX 2026-05-04 full available root:
    data\nexrad --date 2026-05-04 --radar KTLX --max-files 244

  KINX 2026-05-04 larger cross-radar slice:
    data\nexrad --date 2026-05-04 --radar KINX --max-files 440

  KTLX 2026-05-05 larger named-risk slice:
    data\nexrad --date 2026-05-05 --radar KTLX --max-files 440

repetition decision:
  repeat the primary KTLX 2026-05-04 row for spread and drift comparison
  repeat the KTLX 2026-05-05 named-risk row because it carried the milestone
  014 allocation warning and milestone 015 reduction claim
  do not repeat every broader row unless a row is near threshold or noisy

workload classes still absent:
  additional radar sites beyond KTLX and KINX
  additional dates beyond 2026-05-04 and 2026-05-05
  multi-day cache roots outside the current local corpus
  live ingestion or durable queue workloads
  natural queue-ahead rows with provider queue depth above 1
  file-level default readiness workloads

slice 2 input:
  audit existing contracts and guardrails before any Release gate capture;
  no runtime or benchmark behavior change is justified by slice 1 alone
```

### 2. Existing Contract And Guardrail Audit

Confirm that the existing result contracts and tests can support a broader
cache-level decision without new runtime behavior.

Questions to answer:

```text
do direct MeasureCache() omitted controls still resolve to queued-owned rollout
  contour?
does explicit BlockingBorrowed remain visibly separate in direct and CLI
  output?
does explicit queued-owned still match omitted defaults where drift is likely?
are retained payload, event-array, and byte-array telemetry visible enough for
  gate attribution?
do existing tests cover failure, cleanup, release, and drift guardrails?
```

Files to inspect:

```text
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceRolloutDefaults.cs
src/Presentation/Program.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProviderReadinessGateTests.cs
```

Expected result:

```text
no product behavior change is needed before the first broader gate unless
contract drift or missing observability is found
```

Runtime behavior changes:

```text
none unless drift or missing observability is found
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"
```

Slice 2 completion notes:

```text
status: complete
runtime behavior changes: none

contract decision:
  no product behavior change is needed before the first broader cache-level
  Release gate

direct default audit:
  RadarProcessingArchiveRebalanceBenchmark.MeasureFile() and MeasureCache()
  still treat omitted providerMode as the rollout-default selector

  omitted providerMode resolves provider, overlap, retention, execution,
  provider queue capacity, retained-byte budget, and async worker settings
  through RadarProcessingArchiveRebalanceRolloutDefaults

  explicit providerMode: BlockingBorrowed still resolves to the borrowed
  fallback/oracle contour with no queued telemetry and no retained-byte budget

  explicit queued-owned rollout calls remain comparable to omitted defaults
  through AssertDirectQueuedOwnedRolloutContour() for file and cache results

CLI default audit:
  ProcessingBenchmarkArchiveRebalanceOptions.Parse() expands omitted provider
  to rollout-default provenance and keeps explicit blocking-borrowed as a
  fallback contour

  CLI output prints provider source, default rollout contour, rollout default
  expansion, fallback contour, natural-readiness scope, queue telemetry,
  retained payload telemetry, event-array pool telemetry, byte-array pool
  telemetry, overlap telemetry, allocation attribution, and retained pressure
  fields needed by the gate

retained telemetry audit:
  RadarProcessingRetainedPayloadTelemetrySummary keeps aggregate retained pool
  rent/return/miss telemetry plus split event-array and byte-array
  rent/return/miss telemetry

  RadarProcessingArchiveRebalanceBenchmark aggregates cache retained telemetry
  with checked event-array and byte-array counters

  direct rollout contour tests assert retained event-array and byte-array pool
  rents are returned and release failures remain 0

guardrail test coverage:
  NexradArchiveRadarEventBatchPublisherTests cover direct file/cache omitted
  defaults, explicit BlockingBorrowed fallback, explicit queued-owned rollout
  equivalence, no automatic borrowed fallback after direct default failure,
  builder-transfer rejection, cleanup returning current pressure to 0, release
  failure count 0, and retained pool split telemetry

  RadarPulseCliRebalanceBenchmarkTests cover CLI omitted-provider rollout
  expansion, explicit BlockingBorrowed fallback labeling, default/fallback
  provenance, natural-readiness scope, optional telemetry suppression, and
  operator-visible retained/overlap/allocation output

  RadarProcessingQueuedProviderReadinessGateTests cover stable threshold
  values, missing borrowed reference handling, correctness/topology failure
  diagnostics, release health, cleanup completion, pressure budget, natural
  evidence, allocation ratio, elapsed ratio, and run-spread gates

identified gaps:
  no blocking contract or guardrail gap found for milestone 016 gate capture

  Release gate capture still needs a repeatable command matrix or temporary
  direct API harness decision in slice 3 to avoid manual transcription errors

focused verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"

  result: 69 passed, 0 failed, 0 skipped

slice 3 input:
  decide whether existing CLI/direct output plus documented commands are
  enough for repeatable Release gate capture, or whether a temporary direct
  API gate runner/reporting helper is needed
```

### 3. Reporting And Harness Readiness

Make the broader gate repeatable and reviewable. Prefer temporary local harness
or documentation-driven capture when production code already reports enough.

Questions to answer:

```text
can direct MeasureCache() gate rows be captured without manual result
transcription errors?
does CLI output show enough option provenance and fallback/oracle separation?
does the result output include retained payload pool, event-array pool, and
byte-array pool telemetry needed for attribution?
does the gate need a committed test helper, a temporary harness, or only a
documented command matrix?
```

Implementation targets, only if needed:

```text
add small reporting helpers for existing structured fields
add tests for any newly printed line or result contract field
add a narrow test helper for contour comparison if current tests duplicate
fragile assertions
```

Guardrails:

```text
do not change benchmark semantics to simplify reporting
do not move allocation outside the measured window
do not add noisy output that is not used by the gate
do not make the product depend on a local-only temporary harness
```

Focused verification:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
```

Slice 3 completion notes:

```text
status: complete
runtime behavior changes: none

reporting decision:
  use a temporary local direct API gate runner for the milestone 016 Release
  gate matrix, following the milestone 015 pattern

  the temporary runner must not be committed as a product surface

  CLI output remains required for the omitted-provider alignment spot-check
  and for operator-visible reporting contract coverage

why a temporary direct API runner is needed:
  the broader gate needs repeated same-run borrowed/default pairs, per-shape
  ratios, spread calculations, and aggregate table rows across several cache
  selectors

  existing CLI output contains the required fields, but CLI-only capture would
  require too much manual transcription and ratio calculation for the primary
  Release gate

  direct MeasureCache() calls let the runner omit provider-related arguments
  for default rows and pass providerMode: BlockingBorrowed explicitly for
  oracle rows without ambiguity

why no committed helper is needed:
  existing product CLI output already exposes the gate fields needed by
  operators and tests

  existing direct API result contracts already expose the structured fields
  needed by a temporary runner

  adding a committed benchmark report surface would widen the milestone beyond
  readiness evidence and would need its own compatibility contract

required temporary runner posture:
  build the runner in Release before measurement capture
  call RadarProcessingArchiveRebalanceBenchmark.MeasureCache() for paired
  direct omitted-default and explicit BlockingBorrowed rows
  call RadarProcessingArchiveRebalanceBenchmark.MeasureFile() only for the
  representative single-file warning smoke
  keep mode RebalanceSession, iterations 1, warmup iterations 0, parallelism
  24, partitions 24, shards 4, Diagnostic validation, default decompressor,
  overlap consumer delay 0, and retained-byte budget from rollout defaults
  print or export one row per measurement with cache selector, effective
  contour, elapsed ms, allocated bytes, validation checksum, file counts,
  rebalance counters, worker counters, retained pressure, release failures,
  retained pool telemetry, event-array pool telemetry, and byte-array pool
  telemetry
  compute same-run elapsed and allocation ratios from the paired rows
  compute repeated-row spread for the primary and named-risk contours
  mark the runner as temporary in the performance gate document

CLI spot-check posture:
  run processing benchmark rebalance-archive --cache with omitted provider for
  one selected cache row after focused regression and before closeout
  confirm provider source rollout-default, default rollout contour yes,
  rollout default expansion yes, fallback contour no, natural-readiness scope,
  retained payload telemetry, event-array pool telemetry, byte-array pool
  telemetry, overlap telemetry, retained pressure, allocation attribution, and
  validation output remain visible
  run or inspect explicit --provider blocking-borrowed output where fallback
  separation needs operator-visible confirmation

reporting field sufficiency:
  Program prints provider provenance, default rollout contour, rollout default
  expansion, fallback contour, natural readiness scope, cache selector, file
  counts, validation checksum, topology/rebalance counters, skipped reason
  counters, queue telemetry, retained pressure, retained payload telemetry,
  event-array pool telemetry, byte-array pool telemetry, overlap telemetry,
  worker failed counters, elapsed time, allocation totals, and allocation
  attribution

  RadarProcessingArchiveRebalanceBenchmark result contracts expose the same
  structured data for temporary direct API gate rows

identified gaps:
  no committed reporting or result-contract gap found
  no product behavior change needed before slice 4 focused regression

focused verification:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"

  result: 35 passed, 0 failed, 0 skipped

slice 4 input:
  run the broader focused regression and cache sanity pass before building the
  temporary Release gate runner or capturing expensive gate rows
```

### 4. Focused Regression And Cache Sanity Pass

Run focused tests before expensive Release gate capture and record whether the
implementation is still safe to measure.

Required focused regression:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
```

Expected checks:

```text
direct omitted defaults remain queued-owned rollout contour
explicit BlockingBorrowed remains selectable and distinct
explicit queued-owned rollout equals omitted default where expected
CLI omitted-provider cache benchmark remains aligned with direct defaults
retained cleanup and release guardrails remain covered
allocation summary contracts remain non-negative
```

Runtime behavior changes:

```text
none unless earlier slices required reporting or contract fixes
```

Slice 4 completion notes:

```text
status: complete
runtime behavior changes: none

cache sanity:
  data\nexrad\level2\2026\05\04\KTLX:
    files: 244
    bytes: 1_347_625_897

  data\nexrad\level2\2026\05\04\KINX:
    files: 462
    bytes: 1_404_452_903

  data\nexrad\level2\2026\05\05\KTLX:
    files: 848
    bytes: 2_232_493_336

  representative file exists:
    data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06

focused regression:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"

  result: 112 passed, 0 failed, 0 skipped

checks covered:
  direct omitted defaults remain queued-owned rollout contour
  explicit BlockingBorrowed remains selectable and distinct
  explicit queued-owned rollout equals omitted default where expected
  CLI omitted-provider cache benchmark remains aligned with direct defaults
  retained cleanup and release guardrails remain covered
  allocation summary contracts remain non-negative
  retained payload/event-array/byte-array telemetry remains covered

slice 5 input:
  build the solution in Release, build the temporary direct API gate runner in
  Release, capture the broader cache-level Release gate, and write
  docs/milestones/016-broader-cache-level-default-readiness-performance-gate.md
```

### 5. Broader Cache-Level Release Gate

Capture the natural Release evidence for broader cache-level readiness.

Preconditions:

```text
corpus inventory completed
gate matrix selected before measurement interpretation
focused regression passed
Release build passed
thresholds recorded
controlled consumer delay disabled
same-run BlockingBorrowed oracle rows included for every readiness row
```

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Required gate rows:

```text
primary KTLX 2026-05-04 direct MeasureCache() omitted-default row
primary KTLX 2026-05-04 same-run explicit BlockingBorrowed row
KTLX 2026-05-05 direct MeasureCache() omitted-default row
KTLX 2026-05-05 same-run explicit BlockingBorrowed row
KINX 2026-05-04 direct MeasureCache() omitted-default row
KINX 2026-05-04 same-run explicit BlockingBorrowed row
mixed-cache direct MeasureCache() omitted-default row
mixed-cache same-run explicit BlockingBorrowed row
additional selected size/coverage rows
CLI omitted-provider cache spot-check
representative single-file smoke for warning visibility only
```

Each row should report:

```text
cache root, radar/date selector, max-files selector, and effective file count
effective direct configuration
borrowed elapsed and allocated bytes
queued-owned elapsed and allocated bytes
queued-owned-to-borrowed elapsed ratio
queued-owned-to-borrowed allocation ratio
published/skipped/examined file counts
payload values and raw checksum
validation checksum and validation status
topology versions and rebalance counters
skipped reason counters
worker failed batch/item counters
queue depth and overlap indicators
retained pending, active, and combined high-water values
current retained pressure at completion
release attempts and failed releases
retained payload pool rent/return/miss telemetry
event-array pool rent/return/miss telemetry
byte-array pool rent/return/miss telemetry
dominant allocation attribution categories
per-shape status: pass, warning, fail, or coverage-only
```

Documentation updates:

```text
create docs/milestones/016-broader-cache-level-default-readiness-performance-gate.md
record raw command matrix or harness description
record per-shape interpretation before aggregate interpretation
carry single-file cold warning as file-level scope
```

Slice 5 completion notes:

```text
status: complete
runtime behavior changes: none

Release build:
  dotnet build RadarPulse.sln -c Release --no-restore

  result:
    succeeded, 0 warnings, 0 errors

temporary direct API gate runner:
  location during capture:
    data\temp\m016-gate-runner

  output location during capture:
    data\temp\m016-gate-output

  product posture:
    temporary local harness, not committed as a product surface

  build:
    dotnet build data\temp\m016-gate-runner\M016GateRunner.csproj
      -c Release --no-restore

  result:
    succeeded, 0 warnings, 0 errors

runner contour:
  candidate rows omitted provider-related direct API arguments so the
  accepted queued-owned rollout defaults were exercised naturally

  same-run oracle rows passed providerMode: BlockingBorrowed explicitly

  common row settings:
    mode RebalanceSession
    iterations 1
    warmup iterations 0
    parallelism 24
    partitions 24
    shards 4
    default decompressor
    overlap consumer delay 0

performance gate document:
  docs/milestones/016-broader-cache-level-default-readiness-performance-gate.md

gate status:
  captured with primary spread warning

minimum cache-level rows:
  primary KTLX 2026-05-04, max-files 220, repeated 3 pairs:
    borrowed avg elapsed: 17_251.92 ms
    candidate avg elapsed: 15_207.14 ms
    elapsed ratio: 0.881x
    borrowed avg allocated: 1_928_004_067
    candidate avg allocated: 1_982_023_304
    allocation ratio: 1.028x
    candidate spread: 12.01%
    status: warning because spread exceeded the 7.50% threshold

  KTLX 2026-05-05 named risk, max-files 220, repeated 2 pairs:
    borrowed avg elapsed: 12_235.91 ms
    candidate avg elapsed: 10_052.18 ms
    elapsed ratio: 0.822x
    borrowed avg allocated: 2_304_219_356
    candidate avg allocated: 2_352_652_460
    allocation ratio: 1.021x
    candidate spread: 7.42%
    status: pass with timing note because one individual pair was 1.001x
      borrowed while the repeated average and larger optional row passed

  KINX 2026-05-04, max-files 220:
    elapsed ratio: 0.769x
    allocation ratio: 1.007x
    status: pass

  mixed local cache, max-files 1000000:
    elapsed ratio: 0.873x
    allocation ratio: 1.006x
    status: pass with worker-counter note

optional cache-level rows:
  KTLX 2026-05-04 full root, max-files 244:
    elapsed ratio: 0.887x
    allocation ratio: 1.008x
    status: pass

  KINX 2026-05-04 larger slice, max-files 440:
    elapsed ratio: 0.782x
    allocation ratio: 1.000x
    status: pass

  KTLX 2026-05-05 larger named-risk slice, max-files 440:
    elapsed ratio: 0.810x
    allocation ratio: 1.008x
    status: pass

file-level smoke:
  representative KTLX single file:
    elapsed ratio: 0.675x
    allocation ratio: 1.041x
    status: coverage-only

  interpretation:
    current run did not reproduce the milestone 015 single-file cold warning,
    but a single-file smoke remains insufficient for a file-level default
    readiness claim

correctness, release, cleanup, and pressure:
  validation succeeded across captured rows
  same-run borrowed/candidate counters and checksums matched in gate output
  retained payload failed releases: 0
  provider overlap failed releases: 0
  current combined retained bytes returned to 0
  maximum observed retained high-water: 54_413_280 bytes
  retained-byte budget: 536_870_912 bytes

allocation interpretation:
  accepted for cache-level rows
  maximum cache-level average ratio: 1.028x
  maximum cache-level individual measured pair ratio: 1.040x
  threshold: <= 1.10x borrowed

elapsed interpretation:
  accepted on cache-level averages
  all cache-level average ratios were <= 0.887x borrowed
  primary row was faster than borrowed in every individual pair
  the named-risk row had one individual borderline pair at 1.001x borrowed

spread interpretation:
  primary candidate spread was 12.01%, above the 7.50% threshold
  named-risk candidate spread was 7.42%, below but near the 7.50% threshold
  spread warning is not a correctness, allocation, release, cleanup, or
  pressure blocker by itself, but must be decided in slice 6

mixed-cache worker-counter note:
  candidate reported worker failed batches/items as 221/881 while validation
  succeeded and failed migrations remained 0
  the same mixed-cache counter shape was visible in milestone 015
  this slice 5 runner did not record borrowed worker failed counts, so slice 6
  should decide whether the prior interpretation is sufficient or whether a
  borrowed-counter recapture is needed before decision trace

CLI spot-checks:
  omitted-provider cache row:
    exit code 0
    provider mode queued-owned
    provider mode source rollout-default
    default rollout contour yes
    rollout default expansion yes
    fallback contour no
    natural-readiness evidence visible
    retained payload, event-array pool, byte-array pool, overlap, allocation,
    and validation output visible

  explicit --provider blocking-borrowed row:
    exit code 0
    provider mode blocking-borrowed
    provider mode source explicit
    default rollout contour no
    rollout default expansion no
    fallback contour yes
    queued and retained telemetry absent as expected
    allocation and validation output visible

slice 6 input:
  interpret the captured gate before a readiness decision
  decide whether the primary spread warning is accepted or needs a targeted
  rerun
  decide whether the named-risk borderline individual elapsed pair needs more
  evidence
  decide whether the mixed-cache worker counter note needs borrowed counter
  recapture
  preserve the file-smoke result as coverage-only despite the warning not
  reproducing in this run
```

### 6. Gate Interpretation And Follow-Up Fixes

Interpret the Release gate before deciding the milestone. Only implement
follow-up fixes if the gate exposes a narrow, clearly attributable problem.

Questions to answer:

```text
which cache shapes pass cleanly?
which cache shapes have warnings and why?
does any row fail correctness, release, cleanup, pressure, allocation, elapsed,
  spread, or attribution thresholds?
does any warning belong to cache-level readiness, file-level readiness, or
  workload coverage?
does the mixed-cache row hide a shape-specific warning?
does broader evidence materially add to milestone 015 or merely repeat it?
```

Allowed follow-up work:

```text
small reporting or test fixes for reviewability
small deterministic guardrail fixes if a drift or cleanup issue is found
documentation-only classification if the warning is real but acceptable
rerun of a noisy row when the original row is not interpretable
```

Not allowed without replanning:

```text
new rollout contour
allocation optimization campaign
file-level cold optimization campaign
runtime/live default migration
durable transport design
threshold changes after gate capture
```

Slice 6 completion notes:

```text
status: complete
runtime behavior changes: none
follow-up fixes implemented: none
targeted reruns required before decision trace: no
borrowed worker-counter recapture required before decision trace: no

interpretation result:
  the captured broader cache-level evidence is positive but not clean-green

  the slice 7 decision trace should use this posture:
    accept broader cache-level default readiness with named scoped warnings

  this slice does not write the final decision trace and does not close the
  milestone

clean pass rows:
  KINX 2026-05-04 max-files 220:
    elapsed ratio: 0.769x
    allocation ratio: 1.007x

  KTLX 2026-05-04 full root max-files 244:
    elapsed ratio: 0.887x
    allocation ratio: 1.008x

  KINX 2026-05-04 larger slice max-files 440:
    elapsed ratio: 0.782x
    allocation ratio: 1.000x

  KTLX 2026-05-05 larger named-risk slice max-files 440:
    elapsed ratio: 0.810x
    allocation ratio: 1.008x

named scoped warnings and notes:
  primary KTLX 2026-05-04 max-files 220:
    candidate spread was 12.01%, above the 7.50% threshold
    accepted as a scoped spread warning for decision trace input
    not treated as a blocker because every individual primary candidate run
    remained faster than same-run borrowed, allocation stayed below threshold,
    validation matched, release failures stayed 0, cleanup returned to 0, and
    pressure stayed far below budget
    no targeted rerun is required before decision trace

  KTLX 2026-05-05 named-risk max-files 220:
    one individual elapsed pair was 1.001x borrowed
    accepted as a timing note, not a blocker, because the repeated average
    passed at 0.822x and the larger risk-440 row passed at 0.810x
    allocation stayed below threshold and lifecycle/correctness guardrails
    passed
    no additional evidence is required before decision trace

  mixed local cache:
    candidate worker failed batches/items were 221/881 while validation
    succeeded and failed migrations remained 0
    accepted as a named mixed-cache worker-counter note for decision trace
    input because the same counter shape was already visible in milestone 015
    and this gate preserved output parity, validation success, release health,
    cleanup, pressure, elapsed, and allocation thresholds
    borrowed worker-counter recapture is not required before decision trace
    the decision trace must still state that slice 5 did not recapture
    borrowed worker counters for this row

  representative single-file smoke:
    current row did not reproduce the milestone 015 single-file cold warning
    remains coverage-only and must not be used to claim file-level default
    readiness
    file-level cold behavior remains out of scope for this cache-level
    readiness decision

threshold interpretation:
  correctness parity: accepted
  topology/rebalance parity: accepted for captured counters and checksums
  release failures: accepted, failed releases 0
  retained cleanup: accepted, current retained pressure returned to 0
  retained pressure: accepted, max high-water 54_413_280 bytes of
    536_870_912 byte budget
  allocation: accepted, max cache-level average 1.028x and max individual
    measured pair 1.040x against the <= 1.10x threshold
  elapsed timing: accepted on cache-level averages; only the named-risk
    individual 1.001x pair remains a timing note
  variance: accepted with primary scoped warning
  attribution: sufficient for decision trace, with the mixed-cache worker
    counter note carried explicitly

why no runtime or reporting follow-up is needed:
  no correctness, validation, release, cleanup, pressure, fail-closed,
  allocation, direct/CLI alignment, or attribution blocker was found
  no threshold change is needed
  no product behavior change is justified before the decision trace

slice 7 input:
  write the durable decision trace using the posture:
    accept broader cache-level default readiness with named scoped warnings

  carry these named warnings:
    primary spread warning
    named-risk borderline individual elapsed pair timing note
    mixed-cache worker-counter note with no slice 5 borrowed counter recapture
    file-smoke coverage-only scope
```

### 7. Broader Cache-Level Readiness Decision Trace

Record the durable decision and the reasoning behind it.

Create:

```text
docs/milestones/016-broader-cache-level-default-readiness-decision-trace.md
```

The decision trace must include:

```text
decision date
top-level readiness answer
corpus coverage summary
per-shape pass/warning/fail/coverage classification
same-run BlockingBorrowed oracle posture
direct default and CLI omitted-provider alignment posture
correctness, release, cleanup, pressure, elapsed, spread, allocation, and
  attribution interpretation
single-file cold warning scope
whether broader readiness is accepted, accepted with scoped warnings,
  rejected, coverage-insufficient, or deferred
what future milestone input follows from the decision
```

Decision options:

```text
accept broader cache-level default readiness
accept broader cache-level default readiness with named warnings
reject broader cache-level default readiness with named blocker
declare workload coverage insufficient with named missing evidence
defer because safety, correctness, lifecycle, timing, variance, or
  repeatability regressed
```

Slice 7 completion notes:

```text
status: complete
runtime behavior changes: none

decision trace:
  docs/milestones/016-broader-cache-level-default-readiness-decision-trace.md

top-level decision:
  accept broader cache-level default readiness with named scoped warnings

closeout answer selected:
  yes with warnings, broader cache-level default readiness is accepted with
  named scoped warnings

named warnings and scope limits carried:
  primary spread warning:
    candidate spread was 12.01%, above the 7.50% threshold, accepted as a
    scoped warning because every individual candidate run remained faster than
    same-run borrowed and correctness, lifecycle, pressure, and allocation
    guardrails passed

  named-risk timing note:
    one KTLX 2026-05-05 individual pair was 1.001x borrowed, accepted because
    the repeated average was 0.822x and the larger same-shape row was 0.810x

  mixed-cache worker-counter note:
    candidate worker failed batches/items were 221/881 while validation
    succeeded and failed migrations remained 0
    borrowed worker failed counters were not recaptured in slice 5

  file-smoke coverage-only note:
    current single-file smoke did not reproduce the milestone 015 cold warning
    but does not certify file-level default readiness

preserved posture:
  explicit BlockingBorrowed remains fallback and same-run oracle
  CLI omitted-provider cache path remains aligned with direct defaults
  queued-owned failures remain fail-closed
  live/runtime/durable defaults remain out of scope
  no targeted rerun, borrowed worker-counter recapture, runtime change,
  reporting fix, or threshold change is required before closeout

slice 8 input:
  write closeout and update handoff with the accepted-with-warnings broader
  cache-level default-readiness posture, final verification, residual limits,
  and next milestone input
```

### 8. Closeout And Handoff

Finalize milestone documentation and project handoff.

Create:

```text
docs/milestones/016-broader-cache-level-default-readiness-closeout.md
```

Update:

```text
docs/handoff.md
```

Closeout must include:

```text
final status
implemented changes
not implemented
final readiness posture
gate summary
decision trace pointer
preserved invariants
residual risks and limits
recommended next milestone input
final verification
```

Handoff must include:

```text
current milestone status
current direct/default contour
broader cache-level readiness answer
same-run BlockingBorrowed oracle posture
single-file cold warning posture
live/runtime/durable out-of-scope posture
final verification
recommended next milestone input
```

Slice 8 completion notes:

```text
status: complete
runtime behavior changes: none

closeout:
  docs/milestones/016-broader-cache-level-default-readiness-closeout.md

handoff:
  docs/handoff.md

final closeout answer:
  yes with warnings, broader cache-level default readiness is accepted with
  named scoped warnings

decision trace:
  docs/milestones/016-broader-cache-level-default-readiness-decision-trace.md

final verification:
  focused regression before gate:
    112 passed, 0 failed, 0 skipped

  Release build before gate:
    succeeded, 0 warnings, 0 errors

  temporary direct API gate runner build:
    succeeded, 0 warnings, 0 errors

  full test project before closeout:
    768 passed, 0 failed, 3 skipped

recommended next milestone input:
  File-Level Default Readiness And Cold Retained-Ownership Cost

next milestone scope:
  decide whether queued-owned direct/default contour is ready for file-level
  MeasureFile() and small-file workloads, or whether file-level needs a scoped
  optimization/default decision before runtime expansion
```

## Verification Strategy

Use focused tests before gate capture, then Release build and natural Release
benchmarks for the readiness decision.

Focused regression:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
```

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Full test project before closeout:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Release gate:

```text
run direct MeasureCache() omitted-default rows and same-run explicit
BlockingBorrowed rows for the selected cache matrix

run CLI omitted-provider cache spot-check for direct/CLI alignment

run representative MeasureFile() single-file smoke only to keep the file-level
cold warning visible
```

## Completion Checklist

Milestone 016 is complete when:

```text
[x] cache corpus inventory is captured with radar/date/file-count/selection
    details
[x] selected cache-level shapes are broad enough for a readiness decision, or
    coverage insufficiency is explicitly recorded
[x] same-run explicit BlockingBorrowed oracle rows remain available,
    documented, and visibly separate
[x] direct MeasureCache() omitted defaults still resolve to the accepted
    queued-owned rollout contour
[x] CLI omitted-provider cache benchmark remains aligned with direct API
    defaults
[x] correctness parity against borrowed rows is preserved for every readiness
    row
[x] retained cleanup returns current pressure to zero in natural direct/default
    rows
[x] release failures remain 0
[x] retained pressure stays within the configured 536870912 byte budget
[x] allocation overhead is classified per cache shape against the recorded
    threshold
[x] elapsed timing and variance are classified per cache shape against the
    recorded thresholds
[x] single-file cold retained-ownership cost remains explicitly scoped as a
    file-level concern, not cache-level blocker
[x] queued-owned failures remain fail-closed with no automatic borrowed
    fallback
[x] performance gate is captured
[x] decision trace records the broader cache-level default-readiness decision
[x] closeout records verification, gate results, residual risks, and carry
    forward items
[x] handoff states the current broader cache-level readiness posture and
    recommended next milestone unambiguously
```

## Non-Goals

Milestone 016 does not implement:

```text
new default rollout contour
file-level default latency/allocation optimization
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
```

## Closeout Question

The milestone closes by answering:

```text
Is the queued-owned direct/default contour ready as the broader cache-level
benchmark/default posture for available cache workloads?
```

Valid answers:

```text
yes, broader cache-level default readiness is accepted

yes with warnings, broader cache-level default readiness is accepted with
named scoped warnings

no, broader cache-level default readiness is rejected with a named cache
shape, threshold, or attribution blocker

coverage insufficient, broader cache-level default readiness cannot be decided
from the available workload evidence

defer, broader cache-level default readiness cannot be decided because
correctness, cleanup, release health, pressure, fail-closed behavior, timing
variance, or benchmark repeatability regressed
```
