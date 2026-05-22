# Milestone 017: File-Level Default Readiness And Cold Retained-Ownership Cost Implementation Plan

Status: in progress.

This plan implements the milestone 017 architecture defined in
`017-file-level-default-readiness-and-cold-retained-ownership-cost.md`.

The plan is intentionally scoped to file-level `MeasureFile()` readiness and
small-file workload behavior for the already accepted queued-owned
direct/default archive rebalance contour. It should not implement live
ingestion, durable broker integration, cross-process workers, ordered
concurrent rebalance, builder-transfer, source-level migration, partition
splitting, product-facing radar analysis, synthetic benchmark defaults, or
non-benchmark archive publishing defaults.

## Goal

Milestone 017 decides whether the queued-owned direct/default contour is ready
for file-level `MeasureFile()` and small-file workloads, or whether file-level
needs a scoped optimization/default-posture decision before runtime expansion.

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

The milestone target is not to change that contour up front. The target is to
measure and interpret the file-level risk that milestone 015 exposed and
milestone 016 deliberately left outside its cache-level readiness claim.

The most important rules are:

```text
preserve direct MeasureFile()/MeasureCache() queued-owned omitted defaults
preserve explicit BlockingBorrowed direct calls as fallback and oracle
keep same-run borrowed rows in every readiness gate
separate cold MeasureFile() evidence from repeated/warm MeasureFile() evidence
keep CLI omitted-provider file benchmark aligned with direct defaults unless
  a scoped file-level decision changes that posture
use low-count MeasureCache() rows only as small-file transition evidence, not
  as substitute proof for MeasureFile()
classify every selected file and small-file slice individually before
  aggregate interpretation
keep cold retained-ownership cost visible rather than hidden by prewarm
do not silently fall back from queued-owned failure to borrowed success
do not tune the accepted rollout contour to make a file row pass
do not broaden into live/runtime/durable surfaces
do not raise thresholds after seeing gate measurements
```

The implementation target is evidence-driven:

```text
inventory available file-level samples before selecting gate rows
record file selection, file size, radar/date, and risk role before gate
  interpretation
define file-level thresholds before Release measurements are interpreted
prove direct and CLI omitted-provider alignment before Release gate capture
capture same-run BlockingBorrowed and queued-owned rows for every selected
  file and small-file slice
classify correctness, release, cleanup, pressure, elapsed, allocation, spread,
  and attribution per file or slice
record whether file-level readiness is accepted, accepted with warnings,
  optimization-bound, posture-changing, rejected, coverage-insufficient, or
  deferred
```

## Starting Point

Milestone 016 is complete and provides:

```text
broader cache-level default readiness:
  accepted with named scoped warnings

direct MeasureFile()/MeasureCache() omitted defaults:
  queued-owned rollout contour

explicit fallback/oracle:
  providerMode: BlockingBorrowed

CLI omitted-provider rebalance-archive path:
  aligned with the same queued-owned rollout contour

single-file scope:
  current single-file smoke did not reproduce the milestone 015 cold warning,
  but it remains coverage-only and does not certify file-level readiness
```

Milestone 016 cache-level gate facts to carry forward:

```text
primary KTLX 2026-05-04 max-files 220:
  elapsed ratio: 0.881x borrowed average
  allocation ratio: 1.028x borrowed average
  candidate spread: 12.01%, accepted as scoped spread warning

KTLX 2026-05-05 max-files 220:
  elapsed ratio: 0.822x borrowed average
  allocation ratio: 1.021x borrowed average
  one individual pair elapsed ratio: 1.001x borrowed

KINX 2026-05-04 max-files 220:
  elapsed ratio: 0.769x borrowed
  allocation ratio: 1.007x borrowed

mixed local cache:
  elapsed ratio: 0.873x borrowed
  allocation ratio: 1.006x borrowed
  candidate worker failed batches/items: 221/881
  validation succeeded and failed migrations remained 0

release and cleanup:
  retained payload failed releases 0
  provider overlap failed releases 0
  current pending, active, and combined retained pressure returned to 0
```

Milestone 015 file-level warning to carry forward:

```text
representative KTLX single-file cold smoke:
  allocation ratio: 1.512x borrowed
  elapsed ratio: 1.072x borrowed
  interpretation: expected cold retained-ownership price for the current
    queued-owned pooled-copy architecture, not a cache-level blocker
```

Milestone 016 file-level visibility row:

```text
representative KTLX single-file smoke:
  file: data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
  elapsed ratio: 0.675x borrowed
  allocation ratio: 1.041x borrowed
  status: coverage-only, not file-level default readiness proof
```

Known local cache data at milestone start:

```text
data\nexrad\level2\2026\05\04\KTLX:
  244 files, 1_347_625_897 bytes

data\nexrad\level2\2026\05\04\KINX:
  462 files, 1_404_452_903 bytes

data\nexrad\level2\2026\05\05\KTLX:
  848 files, 2_232_493_336 bytes

data\nexrad total:
  1_554 files, 4_984_572_136 bytes
```

Initial file candidates observed locally before the plan was written:

```text
prior file-smoke candidate:
  data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
  size: 5_406_854 bytes

KTLX 2026-05-04 smaller non-MDM candidates:
  KTLX20260504_220338_V06, 4_403_971 bytes
  KTLX20260504_123715_V06, 4_494_334 bytes

KTLX 2026-05-04 larger non-MDM candidates:
  KTLX20260504_034117_V06, 7_757_670 bytes
  KTLX20260504_035526_V06, 7_755_692 bytes

KINX 2026-05-04 smaller non-MDM candidates:
  KINX20260504_124819_V06, 5_012_884 bytes
  KINX20260504_123431_V06, 5_016_231 bytes

KINX 2026-05-04 larger non-MDM candidates:
  KINX20260504_035026_V06, 8_453_655 bytes
  KINX20260504_034322_V06, 8_452_883 bytes

KTLX 2026-05-05 larger non-MDM candidates:
  KTLX20260505_034612_V06, 8_656_438 bytes
  KTLX20260505_034226_V06, 8_633_851 bytes
```

These are seed candidates only. Slice 1 must validate actual publishability,
selection role, and whether `_MDM` files should be excluded or treated as a
separate coverage class before any Release gate is interpreted.

Residual limits carried forward:

```text
local corpus only:
  prior decisions cover available local NEXRAD data only

single-file uncertainty:
  milestone 015 and 016 file-smoke rows disagree on cost posture; the
  difference must be measured as cold/warm/file-shape evidence, not hand-waved

natural queue depth:
  natural direct default rows historically kept queue depth at 1; queue-ahead
  mechanics remain covered by controlled tests rather than natural gates

mixed-cache worker counters:
  candidate worker failed batches/items were visible while validation
  succeeded; file-level gate should keep worker counters visible but should
  not make them a blocker unless a threshold is defined before interpretation

runtime scope:
  live ingestion, durable queues, brokers, cross-process providers, ordered
  concurrent rebalance, builder-transfer, and runtime defaults remain outside
  the milestone
```

## Scope

The implementation should explicitly scope milestone 017 to:

```text
local or explicitly approved NEXRAD file-level corpus inventory
file sample selection before Release gate interpretation
small-file cache slice selection before Release gate interpretation
same-run explicit BlockingBorrowed oracle preservation
direct MeasureFile() omitted-default readiness rows
direct MeasureCache() low-count small-file transition rows
CLI omitted-provider file alignment checks
explicit queued-owned rollout drift checks where useful
focused reporting or temporary harness work needed for repeatable gate capture
correctness, validation, cleanup, release, retained pressure, timing, spread,
  allocation, and attribution interpretation
cold versus repeated/warm file-level classification
per-file and per-small-slice pass/warning/optimize/fail/coverage
  classification
cold retained-ownership cost attribution
natural Release gate over file-level and small-file rows
decision trace, closeout, project-progress update, and handoff
```

The implementation should not change:

```text
queued-owned rollout defaults unless a later decision trace explicitly changes
  file-level posture
synthetic processing benchmark defaults
archive download, inspection, replay-only, stream, parse, or publish defaults
non-benchmark archive publishing APIs
domain enum numeric values
retained payload factory strategy semantics
queued session ordering and rebalance commit rules
worker topology capture timing
automatic fallback semantics
live ingestion/runtime provider defaults
durable or cross-process execution semantics
```

## Readiness Thresholds

Milestone 017 must record file-level thresholds before Release measurements
are interpreted. Cache-level thresholds from milestone 016 remain safety
inputs, but file-level cost interpretation must be explicit.

Non-negotiable thresholds:

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

fail-closed behavior:
  queued-owned failures must fail closed and must not silently convert into
  borrowed success

natural evidence:
  overlap consumer delay must be 0 and evidence scope must remain natural
  readiness evidence, not controlled mechanics proof
```

File-level cost thresholds to choose in slice 3 before gate capture:

```text
cold MeasureFile() allocation ratio:
  define pass, warning, optimize, and fail bands against same-run borrowed

cold MeasureFile() elapsed ratio:
  define pass, warning, optimize, and fail bands against same-run borrowed

repeated/warm MeasureFile() allocation ratio:
  define whether warm rows must meet cache-level allocation threshold or a
  separate file-level threshold

repeated/warm MeasureFile() elapsed ratio:
  define whether warm rows must meet cache-level elapsed threshold or a
  separate file-level threshold

small-file cache allocation and elapsed ratios:
  define expected transition behavior for low-count MeasureCache() slices

candidate run spread:
  define acceptable spread for repeated file-level rows, starting from the
  milestone 016 7.50% natural-row threshold unless the plan records a
  file-level-specific reason to change it
```

Per-row status vocabulary:

```text
pass:
  all non-negotiable thresholds pass, cost thresholds pass, and attribution is
  sufficient

warning:
  correctness, release, cleanup, pressure, and validation pass, but elapsed,
  allocation, spread, worker counters, or attribution needs explicit scoped
  acceptance

optimize:
  correctness and lifecycle guardrails pass, but cost is too high or too
  poorly attributed for default readiness; the row names an optimization target

fail:
  correctness, release, cleanup, pressure, fail-closed behavior, validation,
  or an unaccepted cost threshold fails

posture-change:
  evidence suggests MeasureCache() defaults can remain queued-owned, but
  MeasureFile() omitted defaults need a scoped exception or explicit opt-in

coverage-only:
  row is useful evidence but cannot support readiness by itself because the
  corpus, selector, cold/warm state, or repeatability is insufficient
```

Guardrail:

```text
Do not change thresholds after seeing final gate measurements. If thresholds
need to change, record the reason before interpreting the gate.
```

## Target Implementation Shape

The preferred implementation shape is documentation and gate work first, code
changes only if the audit finds missing reporting, broken guardrails, or a
small targeted optimization needed before a decision can be made.

Expected implementation posture:

```text
use existing direct MeasureFile()/MeasureCache() APIs where possible
use existing CLI output for spot-checks where possible
use a temporary local direct API gate runner only if manual CLI capture would
  be too error-prone or direct structured fields are required
do not commit benchmark-reporting product surfaces unless reviewability
  requires them
do not optimize retained payload or queue paths before the cold/warm evidence
  and thresholds show the optimization target
preserve cache-level behavior unless a scoped file-level decision explicitly
  says otherwise
```

Potential code work, only if justified by earlier slices:

```text
focused tests for file-level omitted-default contour drift
focused tests for CLI --file omitted-provider provenance if current tests are
  cache-heavy
small reporting addition if retained pool/event-array/byte-array attribution
  cannot be reviewed from existing result contracts
temporary local gate runner under data/temp or another ignored workspace path
targeted allocation reduction only after evidence names a safe and narrow
  file-level cost source
```

Unsafe implementation directions:

```text
borrowing leased payload past the publish callback
sharing mutable batch storage across queued workers without ownership transfer
skipping retained resource release or validation to save allocation
collapsing queued-owned into blocking-borrowed behavior
turning off telemetry needed by readiness gates
raising retained-byte budget to hide pressure
prewarming as an unspoken benchmark policy
changing broad cache-level posture to solve a file-level issue
```

## Planned Slices

### 1. File Corpus Inventory And Gate Matrix Design

Build the file-level and small-file gate matrix before interpreting any
measurements.

Questions:

```text
which local files are valid base-data MeasureFile() candidates?
which files reproduce the milestone 015/016 representative KTLX row?
which files cover small, representative, large, KTLX, KINX, and KTLX
  2026-05-05 named-risk shapes?
should `_MDM` files be excluded, treated as coverage-only, or tested as a
  separate class?
which low-count MeasureCache() selectors best represent small-file
  amortization without turning into broad cache-level evidence?
is the available corpus broad enough for a file-level readiness decision?
```

Tasks:

```text
inventory candidate files under the known local NEXRAD roots
separate metadata files, MDM files, and non-MDM base-data files
confirm the prior representative KTLX file exists and is publishable
capture file path, radar/date, size, file role, and selection reason
choose initial cold MeasureFile() rows
choose repeated/warm MeasureFile() rows
choose cross-radar and named-risk file rows
choose low-count MeasureCache() slices such as max-files 2, 4, 8, or another
  documented selector
record any missing workload classes before gate interpretation
```

Expected output:

```text
file-level gate matrix candidate list
small-file cache slice candidate list
coverage statement
missing evidence statement, if any
```

Slice 1 status:

```text
status: complete
runtime behavior changes: none
```

Slice 1 completion notes:

```text
local corpus inventory:
  data\nexrad\level2\2026\05\04\KTLX:
    total files: 244
    Archive Two base-data candidates by name/signature class: 220
    MDM/compressed-stream candidates: 24
    metadata files: 0
    total bytes: 1_347_625_897
    base-data bytes: 1_330_687_937
    MDM bytes: 16_937_960

  data\nexrad\level2\2026\05\04\KINX:
    total files: 462
    Archive Two base-data candidates by name/signature class: 207
    MDM/compressed-stream candidates: 24
    metadata files: 231
    total bytes: 1_404_452_903
    base-data bytes: 1_387_499_247
    MDM bytes: 16_909_951
    metadata bytes: 43_705

  data\nexrad\level2\2026\05\05\KTLX:
    total files: 848
    Archive Two base-data candidates by name/signature class: 401
    MDM/compressed-stream candidates: 23
    metadata files: 424
    total bytes: 2_232_493_336
    base-data bytes: 2_215_239_828
    MDM bytes: 17_173_345
    metadata bytes: 80_163

file selection rule:
  primary MeasureFile() readiness rows use non-MDM Archive Two base-data files
  only; `_MDM` and `.metadata.json` files are excluded from primary
  MeasureFile() readiness because they are not Archive Two base-data volumes

base-data signature spot-check:
  selected MeasureFile() candidates start with AR2V and exist locally

prior representative file:
  data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
  size: 5_406_854 bytes
  signature: AR2V
  role: prior milestone 016 coverage-only file smoke; retained as primary
  drift/prior-warning visibility row

primary KTLX 2026-05-04 file rows:
  small:
    data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_220338_V06
    size: 4_403_971 bytes
  representative:
    data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_144229_V06
    size: 6_087_636 bytes
  large:
    data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_034117_V06
    size: 7_757_670 bytes

cross-radar KINX 2026-05-04 file rows:
  small:
    data\nexrad\level2\2026\05\04\KINX\KINX20260504_124819_V06
    size: 5_012_884 bytes
  representative:
    data\nexrad\level2\2026\05\04\KINX\KINX20260504_093652_V06
    size: 6_775_011 bytes
  large:
    data\nexrad\level2\2026\05\04\KINX\KINX20260504_035026_V06
    size: 8_453_655 bytes

named-risk KTLX 2026-05-05 file rows:
  small:
    data\nexrad\level2\2026\05\05\KTLX\KTLX20260505_220542_V06
    size: 2_120_538 bytes
  representative:
    data\nexrad\level2\2026\05\05\KTLX\KTLX20260505_154040_V06
    size: 5_094_087 bytes
  large:
    data\nexrad\level2\2026\05\05\KTLX\KTLX20260505_034612_V06
    size: 8_656_438 bytes

primary cold/repeated gate shape:
  use the prior representative KTLX 2026-05-04 file as the cold and
  repeated/warm drift row
  include KTLX small/representative/large rows for file-size variation
  include KINX small/representative/large rows for cross-radar variation
  include KTLX 2026-05-05 small/representative/large rows for named-risk date
  coverage

small-file cache transition selectors:
  KTLX 2026-05-04:
    --date 2026-05-04 --radar KTLX --max-files 2
      examined 2, expected base-data 2, MDM 0, metadata 0
    --date 2026-05-04 --radar KTLX --max-files 4
      examined 4, expected base-data 4, MDM 0, metadata 0
    --date 2026-05-04 --radar KTLX --max-files 8
      examined 8, expected base-data 8, MDM 0, metadata 0
    optional skip-visibility row:
      --date 2026-05-04 --radar KTLX --max-files 16
      examined 16, expected base-data 15, MDM 1, metadata 0

  KINX 2026-05-04:
    --date 2026-05-04 --radar KINX --max-files 4
      examined 4, expected base-data 2, MDM 0, metadata 2
    --date 2026-05-04 --radar KINX --max-files 8
      examined 8, expected base-data 4, MDM 0, metadata 4
    --date 2026-05-04 --radar KINX --max-files 16
      examined 16, expected base-data 8, MDM 0, metadata 8

  KTLX 2026-05-05:
    --date 2026-05-05 --radar KTLX --max-files 4
      examined 4, expected base-data 2, MDM 0, metadata 2
    --date 2026-05-05 --radar KTLX --max-files 8
      examined 8, expected base-data 4, MDM 0, metadata 4
    --date 2026-05-05 --radar KTLX --max-files 16
      examined 16, expected base-data 8, MDM 0, metadata 8

MeasureCache selection caveat:
  SelectCacheArchiveFiles stops after examined file count reaches max-files
  and skips non-base-data after date/radar matching; therefore KINX 2026-05-04
  and KTLX 2026-05-05 low-count rows need max-files 4/8/16 to publish roughly
  2/4/8 base-data files because metadata files interleave in sorted order

coverage statement:
  selected local corpus is broad enough to start milestone 017 file-level
  readiness work over available data: it covers prior representative KTLX,
  KTLX size variation, KINX cross-radar variation, KTLX 2026-05-05 named-risk
  date variation, and small-file cache transition slices

coverage limits:
  local corpus only; no absent radar/date certification
  `_MDM` files are excluded from primary readiness and may be coverage-only if
  explicitly reprioritized
  metadata skip behavior affects low-count cache slices and must remain
  visible in gate interpretation
  publishability beyond AR2V signature spot-check remains a slice 4 sanity
  check before Release gate capture
```

Slice 2 input:

```text
audit existing direct/CLI result contracts and tests against the selected file
matrix, with special attention to cold/warm reporting, retained
event-array/byte-array attribution, and low-count cache skip visibility
```

### 2. Existing Contract, Reporting, And Guardrail Audit

Verify that existing APIs, CLI output, tests, and telemetry can support the
file-level gate without widening product surface unnecessarily.

Questions:

```text
do direct MeasureFile() result contracts expose enough retained, queue,
  overlap, allocation, validation, and effective-contour fields?
does CLI --file output expose omitted-provider provenance, fallback/oracle
  separation, and retained telemetry clearly enough for spot-checks?
do tests already cover MeasureFile() omitted defaults, explicit
  BlockingBorrowed, explicit queued-owned rollout, fail-closed behavior,
  cleanup, release, and retained pressure?
is a temporary direct API gate runner enough, or is a committed reporting/test
  change needed?
```

Tasks:

```text
audit direct MeasureFile() and MeasureCache() default-resolution tests
audit CLI rebalance-archive --file omitted-provider tests
audit retained payload, retained resource, provider queue, overlap runner, and
  readiness evaluator tests for file-level relevance
audit current result fields for retained pool/event-array/byte-array
  attribution
identify any reporting gaps before Release gate capture
record whether code changes are required before measurement
```

Expected output:

```text
guardrail audit notes in this plan
list of required code/reporting fixes, or explicit no-fix posture
decision on temporary runner versus existing CLI/direct capture
```

Slice 2 status:

```text
status: pending
runtime behavior changes: none expected
```

### 3. Threshold And Runner Design

Define file-level thresholds and the exact gate capture method before
measurements are interpreted.

Questions:

```text
what file-level allocation ratio is acceptable for cold MeasureFile() rows?
what file-level elapsed ratio is acceptable for cold MeasureFile() rows?
should warm MeasureFile() rows be held to cache-level thresholds, file-level
  thresholds, or both?
how should low-count MeasureCache() slices be classified when they partially
  amortize cold retained ownership?
what pair ordering makes same-run borrowed comparison fair while preserving
  cold queued-owned evidence?
how many repeats are required for primary and prior-warning file rows?
```

Tasks:

```text
record file-level pass/warning/optimize/fail bands before Release capture
record spread threshold before Release capture
define cold-row handling; do not discard first-use queued-owned cost silently
define repeated/warm handling separately from cold handling
define same-run pair ordering and whether a candidate-first row is required
define minimum repeat counts for primary, prior-warning, and named-risk rows
define low-count MeasureCache() thresholds and interpretation
prepare temporary gate runner if needed, keeping it out of product surface
```

Expected output:

```text
threshold table
pair ordering and repeat policy
runner/reporting decision
gate command or direct-runner invocation notes
```

Slice 3 status:

```text
status: pending
runtime behavior changes: none expected
```

### 4. Focused Regression And File Sanity Pass

Run focused tests and sanity checks before Release gate capture.

Questions:

```text
do existing tests still pass for direct/CLI defaults, retained cleanup,
  release, overlap, allocation attribution, and readiness evaluator behavior?
are selected files readable and publishable?
does explicit BlockingBorrowed remain visibly separate from omitted queued
  defaults?
does CLI --file omitted-provider still resolve to the rollout contour?
```

Tasks:

```text
run focused regression tests for archive publisher, CLI rebalance benchmark,
  queued provider readiness, queued overlap runner, allocation summary,
  retained payload factory, and retained batch resource behavior
run selected file sanity checks with explicit BlockingBorrowed and omitted
  provider where cheap enough
run CLI omitted-provider file spot-check
run explicit --provider blocking-borrowed file spot-check
capture any failures before Release gate
fix only blockers that affect milestone 017 readiness or reporting
```

Expected focused regression command:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
```

Expected output:

```text
focused test result
file sanity result
CLI/direct alignment result
list of blockers, if any
```

Slice 4 status:

```text
status: pending
runtime behavior changes: none expected
```

### 5. Cold And Warm MeasureFile Release Gate

Capture the primary file-level Release evidence.

Questions:

```text
does the prior-warning file still show cold retained-ownership cost?
does repeated/warm behavior differ materially from cold behavior?
do KTLX, KINX, and KTLX 2026-05-05 file rows stay within thresholds?
do retained cleanup, release health, pressure, validation, and attribution
  remain clean?
does any file row imply an optimization target or file-level default split?
```

Tasks:

```text
build in Release before gate capture
run same-run direct MeasureFile() borrowed/default pairs for selected files
capture candidate-first cold evidence where the threshold policy requires it
capture repeated/warm rows for primary and prior-warning shapes
capture cross-radar and named-risk file rows
capture effective contour, elapsed, allocation, checksums, validation,
  retained pressure, queue telemetry, overlap telemetry, release failures, and
  allocation attribution
classify every file row before aggregate interpretation
```

Expected Release build command:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Required rows, subject to slice 1 and 3 finalization:

```text
prior representative KTLX 2026-05-04 file:
  data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06

KTLX 2026-05-04 small/representative/large file rows
KINX 2026-05-04 small/representative/large file rows
KTLX 2026-05-05 named-risk file rows
```

Expected output:

```text
file-level Release gate raw results
per-file status table
cold versus warm comparison
retained pressure and release summary
allocation and elapsed attribution summary
```

Slice 5 status:

```text
status: pending
runtime behavior changes: none expected unless a blocker requires a targeted
  fix in a later slice
```

### 6. Small-File Cache Transition Gate

Capture low-count `MeasureCache()` evidence to understand where file-level
cold retained cost starts to amortize.

Questions:

```text
do 2/4/8-file or selected low-count cache slices behave closer to cold
  MeasureFile() rows or accepted cache-level rows?
does retained cold cost amortize predictably by file count?
does any small-file slice fail correctness, release, cleanup, pressure,
  allocation, elapsed, spread, or attribution thresholds?
does small-file evidence change the file-level readiness decision or merely
  provide context?
```

Tasks:

```text
run same-run direct MeasureCache() borrowed/default pairs for selected
  low-count slices
capture one or more radar/date slices that align with selected MeasureFile()
  rows
capture retained pressure, release, validation, queue, overlap, allocation,
  elapsed, and effective contour fields
classify low-count slices separately from broad cache-level rows
compare low-count slices against cold and warm MeasureFile() rows
```

Candidate small-file slices, subject to slice 1 and 3 finalization:

```text
data\nexrad --date 2026-05-04 --radar KTLX --max-files 2/4/8
data\nexrad --date 2026-05-04 --radar KINX --max-files 2/4/8
data\nexrad --date 2026-05-05 --radar KTLX --max-files 2/4/8
```

Expected output:

```text
small-file cache Release gate raw results
per-slice status table
amortization interpretation
comparison to accepted milestone 016 cache-level posture
```

Slice 6 status:

```text
status: pending
runtime behavior changes: none expected unless a blocker requires a targeted
  fix in a later slice
```

### 7. Gate Interpretation And Follow-Up Fixes

Interpret the file-level and small-file evidence before writing the decision
trace. Only implement fixes if the evidence names a blocker that can be
resolved inside the milestone without changing scope.

Questions:

```text
does any row fail correctness, release, cleanup, pressure, validation, or
  fail-closed behavior?
does cold retained-ownership cost pass, warn, require optimization, or block
  file-level default posture?
do repeated/warm rows show acceptable steady behavior?
do low-count cache slices show a clear amortization transition?
is allocation/timing attribution strong enough to support a decision?
does CLI/direct alignment still hold?
is a targeted optimization warranted before decision trace, or should the
  milestone close with an optimization/posture recommendation?
```

Allowed follow-up fixes:

```text
test or reporting fixes required to make existing behavior reviewable
small guardrail fixes if an existing invariant is not covered by tests
small attribution fixes if missing attribution is the only blocker to a
  decision
targeted allocation fix only if the gate names a narrow safe source and the
  fix does not alter retained lifetime semantics or broaden scope
```

Not allowed as follow-up fixes without replanning:

```text
new default rollout contour
silent borrowed fallback
prewarm policy hidden inside benchmark measurement
durable queue or cross-process work
builder-transfer implementation
runtime ingestion default migration
broad retained payload architecture rewrite
```

Expected output:

```text
gate interpretation notes
follow-up fix list, or explicit no-fix posture
decision-trace input
residual warnings and blockers
```

Slice 7 status:

```text
status: pending
runtime behavior changes: none unless a targeted fix is accepted
```

### 8. File-Level Readiness Decision Trace

Write the formal decision trace.

Create:

```text
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-decision-trace.md
```

Decision trace must include:

```text
date
top-level decision
included direct API and CLI surfaces
excluded runtime/durable surfaces
file-level corpus and small-file slice summary
threshold decisions recorded before interpretation
cold MeasureFile() posture
repeated/warm MeasureFile() posture
small-file cache transition posture
explicit BlockingBorrowed fallback/oracle posture
CLI/direct alignment posture
correctness, release, cleanup, pressure, elapsed, allocation, spread, and
  attribution interpretation
accepted warnings, optimization targets, blockers, or coverage gaps
whether MeasureFile() defaults stay aligned with MeasureCache() defaults
recommended next milestone input
```

Valid decision-trace outcomes:

```text
accept file-level default readiness
accept file-level default readiness with named scoped warnings
require named optimization before runtime expansion
change or split file-level default posture from cache-level posture
reject file-level default readiness with named blocker
coverage insufficient for file-level default readiness
defer because gate health or repeatability regressed
```

Slice 8 status:

```text
status: pending
runtime behavior changes: none expected
```

### 9. Closeout, Handoff, And Project Progress

Finalize milestone documentation and project handoff.

Create:

```text
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-closeout.md
```

Update:

```text
docs/handoff.md
docs/project-progress.md
```

Closeout must include:

```text
final status
implemented changes
not implemented
final file-level readiness posture
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
file-level readiness answer
cold retained-ownership posture
small-file cache transition posture
same-run BlockingBorrowed oracle posture
cache-level readiness carry-forward posture
live/runtime/durable out-of-scope posture
final verification
recommended next milestone input
```

Project progress must include:

```text
milestone 017 final answer
what was achieved
what it prepared
important warnings or scope limits
verification summary
recommended next milestone input
whether the project chain changed
```

Slice 9 status:

```text
status: pending
runtime behavior changes: none expected
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
run direct MeasureFile() omitted-default rows and same-run explicit
BlockingBorrowed rows for the selected file matrix

run direct MeasureCache() omitted-default rows and same-run explicit
BlockingBorrowed rows for the selected low-count small-file slices

run CLI omitted-provider file spot-check for direct/CLI alignment

run explicit CLI --provider blocking-borrowed file spot-check for fallback
visibility
```

The Release gate should capture:

```text
effective direct configuration
borrowed elapsed and allocated bytes
queued-owned direct/default elapsed and allocated bytes
queued-owned-to-borrowed elapsed ratio
queued-owned-to-borrowed allocation ratio
cold/warm classification
file size, compressed records, batch count, event count, payload bytes, and
  checksum fields where available
validation status and checksum
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
default contour and CLI/direct alignment indicators
per-row status
```

## Completion Checklist

Milestone 017 is complete when:

```text
[x] file-level corpus inventory is captured with file path, radar/date,
    size/count, and selection details
[x] small-file cache slices are selected with max-files and selection rules
    before interpretation
[x] selected file-level and small-file shapes are broad enough for a readiness
    decision, or coverage insufficiency is explicitly recorded
[ ] file-level thresholds are recorded before Release gate interpretation
[ ] same-run explicit BlockingBorrowed oracle rows remain available,
    documented, and visibly separate
[ ] direct MeasureFile() omitted defaults still resolve to the accepted
    queued-owned rollout contour unless a scoped file-level decision changes
    that posture
[ ] direct MeasureCache() omitted defaults preserve the milestone 016 accepted
    cache-level posture
[ ] CLI omitted-provider file benchmark remains aligned with direct API
    defaults unless a scoped file-level decision changes that posture
[ ] correctness parity against borrowed rows is preserved for every readiness
    row
[ ] retained cleanup returns current pressure to zero in natural direct/default
    rows
[ ] release failures remain 0
[ ] retained pressure stays within the configured 536870912 byte budget
[ ] cold file-level allocation and elapsed cost are classified against
    thresholds recorded before gate interpretation
[ ] repeated/warm file-level behavior is classified separately from cold
    behavior
[ ] small-file cache slices show whether cold retained cost is partially
    amortized or remains a blocker
[ ] allocation and timing attribution are sufficient to explain remaining cost
    or the decision trace names attribution as a blocker
[ ] queued-owned failures remain fail-closed with no automatic borrowed
    fallback
[ ] performance gate is captured
[ ] decision trace records the file-level default-readiness decision
[ ] closeout records verification, gate results, residual risks, and carry
    forward items
[ ] handoff and project-progress state the current file-level readiness posture
    and recommended next milestone unambiguously
```

## Non-Goals

Milestone 017 does not implement:

```text
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
prewarm-as-policy unless explicitly designed as a file-level default decision
new broad cache-level readiness campaign unless regression evidence requires it
```

## Closeout Question

The milestone closes by answering:

```text
Is the queued-owned direct/default contour ready for file-level MeasureFile()
and small-file workloads?
```

Valid answers:

```text
yes, file-level default readiness is accepted

yes with warnings, file-level default readiness is accepted with named scoped
warnings

optimize, file-level default readiness requires a named optimization before
runtime expansion

posture change, file-level defaults should split from cache-level defaults or
move behind explicit opt-in with a named reason

no, file-level default readiness is rejected with a named file, small-file
slice, threshold, lifecycle, validation, or attribution blocker

coverage insufficient, file-level default readiness cannot be decided from
the available workload evidence

defer, file-level default readiness cannot be decided because correctness,
cleanup, release health, pressure, fail-closed behavior, timing variance, or
benchmark repeatability regressed
```
