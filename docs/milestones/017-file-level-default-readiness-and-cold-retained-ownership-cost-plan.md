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

File-level cost thresholds recorded in slice 3 before gate capture:

```text
cold MeasureFile() allocation ratio:
  pass: <= 1.10x same-run borrowed
  warning: > 1.10x and <= 1.50x same-run borrowed
  optimize: > 1.50x and <= 1.75x same-run borrowed
  fail: > 1.75x same-run borrowed

cold MeasureFile() elapsed ratio:
  pass: <= 1.00x same-run borrowed
  warning: > 1.00x and <= 1.10x same-run borrowed
  optimize: > 1.10x and <= 1.25x same-run borrowed
  fail: > 1.25x same-run borrowed

repeated/warm MeasureFile() allocation ratio:
  pass: <= 1.10x same-run borrowed
  warning: > 1.10x and <= 1.20x same-run borrowed
  optimize: > 1.20x and <= 1.35x same-run borrowed
  fail: > 1.35x same-run borrowed

repeated/warm MeasureFile() elapsed ratio:
  pass: <= 1.00x same-run borrowed average
  warning: > 1.00x and <= 1.10x same-run borrowed average
  optimize: > 1.10x and <= 1.20x same-run borrowed average
  fail: > 1.20x same-run borrowed average
  individual pair note: if the repeated average passes but one individual
    warm pair is > 1.00x and <= 1.05x, record a timing note rather than a
    warning; any individual warm pair > 1.10x escalates the row to at least
    warning even if the average passes

small-file cache allocation and elapsed ratios:
  classify by expected published base-data count, not raw max-files, because
  metadata and MDM files may be examined but skipped

  published base-data count 2:
    allocation pass <= 1.20x, warning <= 1.35x, optimize <= 1.50x,
      fail > 1.50x
    elapsed pass <= 1.05x, warning <= 1.15x, optimize <= 1.25x,
      fail > 1.25x

  published base-data count 4:
    allocation pass <= 1.15x, warning <= 1.25x, optimize <= 1.40x,
      fail > 1.40x
    elapsed pass <= 1.03x, warning <= 1.12x, optimize <= 1.22x,
      fail > 1.22x

  published base-data count 8 or more:
    allocation pass <= 1.10x, warning <= 1.20x, optimize <= 1.30x,
      fail > 1.30x
    elapsed pass <= 1.00x, warning <= 1.10x, optimize <= 1.20x,
      fail > 1.20x

candidate run spread:
  repeated/warm candidate elapsed spread must be <= 7.50% of the candidate
  average, preserving the milestone 016 natural-row spread threshold
  candidate-first cold rows are not included in repeated/warm spread
  allocation spread is recorded for diagnosis but does not create a separate
  blocker unless average allocation crosses its band or attribution is
  insufficient

threshold override rule:
  any non-negotiable correctness, release, cleanup, pressure, validation,
  topology, or fail-closed failure overrides cost bands and marks the row fail
  exact event-array versus byte-array allocated-byte attribution is not a
  threshold for milestone 017; existing pool rent/return/miss split is
  sufficient unless a row enters optimize/fail and cannot be explained by the
  current retained telemetry
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
status: complete
runtime behavior changes: none
code/reporting fixes required before measurement: none
```

Slice 2 completion notes:

```text
direct API contract:
  MeasureFile() and MeasureCache() omitted provider/execution/retention
  controls still resolve through the accepted queued-owned rollout default
  contour
  explicit BlockingBorrowed remains available as a same-run fallback/oracle
  contour
  queued-only controls remain guarded so borrowed mode cannot accidentally
  inherit queued provider settings
  queued-owned failure paths remain fail-closed rather than silently falling
  back to borrowed success

direct result fields:
  MeasureFile() exposes file identity, effective contour, validation,
  topology/rebalance counters, skipped reasons, worker telemetry, elapsed
  time, allocation attribution, queue telemetry, retained payload telemetry,
  retained pressure, and provider overlap telemetry
  MeasureCache() exposes the same readiness fields plus cache/date/radar
  identity and examined/skipped/published file counts for low-count slice
  interpretation
  low-count cache selection stops at examined max-files and skips non-base
  Archive Two data using the AR2V base-data signature check, which keeps
  metadata/MDM skip visibility reviewable

CLI reporting:
  rebalance-archive output prints provider mode, overlap mode, retention
  strategy, queue capacity, retained-byte budget, option provenance,
  default-candidate contour, rollout-default expansion, fallback contour,
  evidence contour/scope, validation, rebalance counters, allocation
  attribution, provider queue telemetry, retained payload telemetry,
  retained pressure, and provider overlap telemetry
  CLI --file omitted-provider output is sufficient for slice 4 spot-checks
  and direct/CLI alignment evidence
  CLI output is intentionally verbose and text-oriented, so it is not the
  preferred source for the full Release gate table

retained ownership attribution:
  existing allocation attribution separates measured allocation, processing
  callback allocation, replay/batch construction allocation, owned snapshot
  allocation, and processing callback non-owned snapshot allocation
  retained payload telemetry exposes total retained allocated bytes plus
  pool rent/return/miss counts split across event arrays and byte arrays
  exact event-array versus byte-array allocated bytes are not split today;
  this is acceptable for milestone 017 readiness unless slice 3 defines a
  threshold that requires exact per-pool allocated-byte attribution

guardrail test coverage:
  CLI tests cover help/default messaging, omitted-provider rollout expansion,
  explicit BlockingBorrowed fallback provenance, explicit queued-owned
  provenance, default-candidate contour, fallback contour, retained pressure
  output, allocation attribution output, and telemetry visibility
  queued overlap tests cover stable statuses, consumer fault behavior,
  producer failure releasing pending resources, cancellation after accepted
  enqueue releasing pending resources, validation failure releasing active
  resources, and fail-closed behavior without borrowed fallback
  retained payload factory/resource tests cover pooled-copy rent/return/miss
  behavior, cold/warm large-payload pool reuse, empty/owned-input behavior,
  builder-transfer unsupported status, and cleanup on copy failure

runner/reporting decision:
  use a temporary direct API gate runner for the full Release gate because
  structured result properties reduce manual capture error in paired
  borrowed/default rows
  keep CLI output as a spot-check and user-facing alignment surface
  do not commit a new product reporting surface before measurement
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
status: complete
runtime behavior changes: none
code/reporting fixes required before slice 4: none
runner posture: temporary direct API runner, not product reporting surface
```

Slice 3 completion notes:

```text
threshold posture:
  cold MeasureFile() rows have their own allocation and elapsed bands so the
  first retained-ownership cost is visible and cannot be hidden behind warm
  averages
  repeated/warm MeasureFile() rows use stricter cache-level-style pass bands
  because persistent single-file retained cost is the actual default-readiness
  risk
  low-count MeasureCache() rows use transition bands by expected published
  base-data count: 2-file slices get the widest cost allowance, 4-file slices
  are narrower, and 8-file-or-larger slices converge on cache-level
  expectations
  repeated candidate elapsed spread keeps the milestone 016 7.50% threshold
  candidate-first cold rows are classified separately and are not included in
  repeated/warm spread

same-run pair ordering:
  cold candidate-first probes run queued-owned omitted-default first, then
  explicit BlockingBorrowed in the same runner invocation, and are classified
  as first-use retained-ownership evidence
  warm/repeated pairs run explicit BlockingBorrowed first, then queued-owned
  omitted-default, so same-run oracle comparison remains visible and stable
  no queued-owned failure may be retried as borrowed success inside the same
  row; failure remains fail-closed evidence

required cold probes:
  prior representative KTLX 2026-05-04:
    KTLX20260504_000245_V06
  KTLX 2026-05-04 representative:
    KTLX20260504_144229_V06
  KINX 2026-05-04 representative:
    KINX20260504_093652_V06
  KTLX 2026-05-05 representative:
    KTLX20260505_154040_V06

repeat policy:
  prior representative KTLX row:
    1 candidate-first cold pair and 3 warm borrowed/default pairs
  KTLX 2026-05-04 representative row:
    1 candidate-first cold pair and 3 warm borrowed/default pairs
  KINX 2026-05-04 representative row:
    1 candidate-first cold pair and 2 warm borrowed/default pairs
  KTLX 2026-05-05 representative row:
    1 candidate-first cold pair and 2 warm borrowed/default pairs
  KTLX/KINX/KTLX-2026-05-05 small and large file rows:
    1 warm borrowed/default pair each
  primary KTLX 2/4/8 small-cache transition slices:
    2 warm borrowed/default pairs each
  KINX and KTLX-2026-05-05 small-cache transition slices:
    1 warm borrowed/default pair each
  conditional repeat rule:
    if any one-pair row enters warning/optimize by cost, lands within 0.03x
    of a band boundary, or shows surprising worker counters while safety
    guardrails pass, capture two more warm pairs before final interpretation

temporary runner output:
  location: data\temp\m017-gate-runner or equivalent ignored workspace path
  build mode: Release
  primary output: JSONL rows plus a generated Markdown summary table
  surfaces: direct MeasureFile() and direct MeasureCache()
  CLI role: spot-check only for omitted-provider alignment and explicit
  BlockingBorrowed visibility

runner row fields:
  run id, timestamp, build configuration, row id, group, surface, role,
  pair id, pair order, cold/warm classification, argument profile
  file path or cache path/date/radar/max-files, file size where applicable,
  expected published base-data count, examined/skipped/published counts for
  cache rows
  effective provider mode, overlap mode, retention strategy, execution mode,
  worker count, worker queue capacity, provider queue capacity, retained-byte
  budget, overlap consumer delay
  elapsed ms, processing elapsed ms, replay/batch-construction elapsed ms,
  allocated bytes, allocation ratio, elapsed ratio, spread inputs
  validation status/checksum, raw checksum, topology versions, rebalance
  evaluations, accepted moves, skipped decisions, failed migrations, skipped
  reason counters, worker failed batch/item counters
  queue depth high-water, retained payload high-water, retained resource
  current/high-water pending/active/combined counts and bytes
  retained payload failed copies, retained failed releases, provider overlap
  failed releases, release attempts/released/not-required counts
  retained allocated bytes, owned snapshot allocated bytes, processing
  callback non-owned snapshot bytes, replay/batch construction bytes
  event-array and byte-array pool rent/return/miss counts, overlap retained
  batches/events/payload bytes, overlap measured/unattributed allocation
  row status, warning/optimize/fail reason, attribution note
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
status: complete
runtime behavior changes: none
blockers before Release gate: none found
```

Slice 4 completion notes:

```text
focused regression:
  command:
    dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
  result:
    passed, 112 passed, 0 failed, 0 skipped

CLI omitted-provider file spot-check:
  command shape:
    dotnet run --project src\Presentation\RadarPulse.Cli.csproj -c Release --no-restore -- processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode static --partitions 4 --shards 2 --iterations 1 --warmup-iterations 0 --parallelism 1 --queue-telemetry summary --overlap-telemetry summary
  result:
    Provider mode: queued-owned
    Provider mode source: rollout-default
    Provider overlap source: rollout-default
    Retention strategy source: rollout-default
    Provider queue capacity source: rollout-default
    Worker queue capacity source: rollout-default
    Provider queue retained byte capacity source: rollout-default
    Provider default rollout contour: yes
    Provider rollout default expansion: yes
    Provider fallback contour: no
    Default-candidate contour: yes
    Provider overlap evidence scope: natural-readiness
    Validation: succeeded
    Retained payload failed releases: 0
    Provider overlap failed releases: 0
    current pending/active/combined retained pressure returned to 0

CLI explicit BlockingBorrowed file spot-check:
  first guardrail check:
    explicit blocking-borrowed with --overlap-telemetry summary was rejected
    with "--overlap-telemetry requires --provider-overlap producer-consumer",
    preserving queued-only option validation
  command shape:
    dotnet run --project src\Presentation\RadarPulse.Cli.csproj -c Release --no-restore -- processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode static --provider blocking-borrowed --partitions 4 --shards 2 --iterations 1 --warmup-iterations 0 --parallelism 1
  result:
    Provider mode: blocking-borrowed
    Provider mode source: explicit
    Provider default rollout contour: no
    Provider rollout default expansion: no
    Provider fallback contour: yes
    Default-candidate contour: no
    Provider overlap evidence scope: not-applicable
    Validation: succeeded
    Validation checksum matched the omitted-provider spot-check:
      2_813_350_973_321_257_292

selected file static sanity:
  command shape:
    dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file <selected-file> --mode static --partitions 4 --shards 2 --iterations 1 --warmup-iterations 0 --parallelism 1 --queue-telemetry none --overlap-telemetry none
  result:
    all selected MeasureFile() readiness files published with omitted-provider
    queued-owned mode and Validation: succeeded

  KTLX20260504_000245_V06:
    validation checksum 2_813_350_973_321_257_292, events 32_400,
    payload bytes 48_257_280
  KTLX20260504_220338_V06:
    validation checksum 11_291_311_070_573_145_995, events 18_960,
    payload bytes 31_079_040
  KTLX20260504_144229_V06:
    validation checksum 2_975_717_052_562_627_441, events 32_400,
    payload bytes 48_257_280
  KTLX20260504_034117_V06:
    validation checksum 7_945_987_576_042_852_707, events 32_400,
    payload bytes 48_257_280
  KINX20260504_124819_V06:
    validation checksum 11_253_733_498_806_519_418, events 32_400,
    payload bytes 48_342_240
  KINX20260504_093652_V06:
    validation checksum 6_904_323_739_345_245_968, events 32_400,
    payload bytes 48_342_240
  KINX20260504_035026_V06:
    validation checksum 5_096_944_298_289_890_017, events 32_400,
    payload bytes 48_342_240
  KTLX20260505_220542_V06:
    validation checksum 4_120_427_155_509_216_206, events 32_400,
    payload bytes 48_257_280
  KTLX20260505_154040_V06:
    validation checksum 7_676_054_101_070_045_969, events 32_400,
    payload bytes 48_257_280
  KTLX20260505_034612_V06:
    validation checksum 13_903_915_601_095_124_975, events 37_440,
    payload bytes 51_484_320

low-count cache static sanity:
  command shape:
    dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date <date> --radar <radar> --max-files <n> --mode static --partitions 4 --shards 2 --iterations 1 --warmup-iterations 0 --parallelism 1 --queue-telemetry none --overlap-telemetry none
  result:
    all selected low-count MeasureCache() slices published with
    omitted-provider queued-owned mode and Validation: succeeded

  KTLX 2026-05-04 max-files 2:
    examined 2, skipped 0, published 2
  KTLX 2026-05-04 max-files 4:
    examined 4, skipped 0, published 4
  KTLX 2026-05-04 max-files 8:
    examined 8, skipped 0, published 8
  KINX 2026-05-04 max-files 4:
    examined 4, skipped 2, published 2
  KINX 2026-05-04 max-files 8:
    examined 8, skipped 4, published 4
  KINX 2026-05-04 max-files 16:
    examined 16, skipped 8, published 8
  KTLX 2026-05-05 max-files 4:
    examined 4, skipped 2, published 2
  KTLX 2026-05-05 max-files 8:
    examined 8, skipped 4, published 4
  KTLX 2026-05-05 max-files 16:
    examined 16, skipped 8, published 8

slice 4 outcome:
  no focused regression, file publishability, low-count cache selector,
  omitted-provider alignment, fallback visibility, cleanup, release, or
  queued-only option-validation blocker was found before Release gate capture
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
status: complete
runtime behavior changes: none
gate document:
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-measurefile-gate.md
gate posture:
  captured with file-level allocation blocker
```

Slice 5 completion notes:

```text
Release solution build:
  dotnet build RadarPulse.sln -c Release --no-restore
  succeeded, 0 warnings, 0 errors

temporary runner:
  location: data\temp\m017-gate-runner
  product surface: none; ignored local measurement harness
  output:
    data\temp\m017-gate-runner\output\m017-measurefile-20260522-083951.jsonl
    data\temp\m017-gate-runner\output\m017-measurefile-20260522-083951.md

runner contour:
  surface: RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
  mode: RebalanceSession
  iterations: 1
  warmup iterations: 0
  parallelism: 24
  partitions: 24
  shards: 4
  candidate rows omitted provider/execution/retention controls and resolved to
    the queued-owned rollout default contour
  borrowed oracle rows used explicit BlockingBorrowed with AsyncShardTransport,
    worker count 4, queue capacity 1

cold representative rows:
  prior KTLX representative:
    status fail, elapsed 1.507x, allocation 2.995x
  KTLX 2026-05-04 representative:
    status fail, elapsed 1.735x, allocation 2.060x
  KINX 2026-05-04 representative:
    status fail, elapsed 0.585x, allocation 1.958x
  KTLX 2026-05-05 representative:
    status fail, elapsed 1.809x, allocation 2.235x

warm representative rows:
  prior KTLX representative:
    status fail, elapsed 1.077x, allocation 2.128x, spread 6.94%
  KTLX 2026-05-04 representative:
    status fail, elapsed 1.018x, allocation 2.012x, spread 3.90%
  KINX 2026-05-04 representative:
    status fail, elapsed 0.961x, allocation 1.916x, spread 1.42%
  KTLX 2026-05-05 representative:
    status fail, elapsed 1.029x, allocation 2.186x, spread 4.37%

small and large warm file rows:
  status: fail for every selected small/large row
  allocation ratio range: 1.443x to 2.207x
  elapsed ratio range: 0.950x to 1.160x

safety guardrails:
  all 20 borrowed/default pairs passed safety guardrails
  validation/checksum parity passed
  stable totals and topology parity passed
  retained payload failed releases 0
  provider overlap failed releases 0
  current retained pressure returned to 0
  max candidate combined retained high-water 51_484_320, below the
    536_870_912 retained-byte budget
  worker failed batches/items 0/0

attribution:
  candidate retained payload allocated bytes matched owned snapshot allocated
    bytes
  normal 32_400-event rows allocated approximately 69_206_296 to 69_206_320
    retained payload bytes
  smaller KTLX row allocated 35_651_864 retained payload bytes
  KTLX 2026-05-05 large row allocated 71_303_448 retained payload bytes
  event-array and byte-array pool misses were 1 per row group

slice 5 interpretation:
  MeasureFile() queued-owned omitted defaults failed the pre-recorded
    file-level cost thresholds across cold, warm, small, representative,
    large, primary, cross-radar, and named-risk rows
  the blocker is allocation cost from retained owned snapshots, not
    correctness, release, cleanup, pressure, fallback, or validation
  file-level readiness is not accepted by slice 5 evidence alone
  slice 6 remains required to determine whether low-count MeasureCache()
    slices amortize the retained cost or preserve the same blocker
```

Slice 5 cold-start prewarm follow-up:

```text
status:
  opt-in prototype implemented; full prewarmed MeasureFile gate captured

gate document:
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-prewarmed-measurefile-gate.md

default posture:
  unchanged; queued-owned omitted defaults still use the accepted rollout
    contour without hidden prewarm

implemented opt-in mechanics:
  retained event-array pool prewarm
  retained payload byte-array pool prewarm
  retained payload factory prewarm
  optional retained payload factory passthrough for MeasureFile(),
    MeasureCache(), and queued overlap options

full prewarmed gate result:
  all 20 borrowed/prewarmed-candidate pairs passed safety guardrails
  all prewarmed candidate rows had retained pool misses 0
  measured allocation ratios across the selected file matrix were 0.980x to
    1.026x against same-run borrowed rows
  explicit prewarm allocation remained real and ranged from 35_651_808 bytes
    to 71_303_392 bytes by file shape
  initial elapsed outliers were captured for targeted recheck, including
    prior representative prewarmed-probe 3.632x, KINX small prewarmed-warm
    1.253x, KTLX 2026-05-04 representative prewarmed-probe 1.167x, and
    KTLX 2026-05-05 small prewarmed-warm 1.140x

targeted timing rerun:
  repeated the elapsed outlier rows with five pairs each after one excluded
    stabilization row
  raw output:
    data\temp\m017-prewarmed-timing-runner\output\m017-prewarmed-timing-20260522-092557.md
    data\temp\m017-prewarmed-timing-runner\output\m017-prewarmed-timing-20260522-092557.csv
  prior-probe:
    filesystem timing note, average elapsed 1.034x, max elapsed 1.097x,
    allocation 0.995x
  prior-warm:
    filesystem timing note, average elapsed 1.043x, max elapsed 1.143x,
    allocation 0.999x
  KTLX representative probe:
    filesystem timing note, average elapsed 1.046x, max elapsed 1.085x,
    allocation 1.000x
  KTLX representative warm:
    filesystem timing note, average elapsed 1.017x, max elapsed 1.040x,
    allocation 1.000x
  KINX small warm:
    filesystem timing note, average elapsed 1.042x, max elapsed 1.051x,
    allocation 1.003x
  KTLX 2026-05-05 small warm:
    filesystem timing note, average elapsed 1.056x, max elapsed 1.125x,
    allocation 1.000x
  rerun outcome:
    fail-level elapsed outliers did not reproduce; elapsed spread is treated
    as non-blocking filesystem jitter/spread

verification:
  focused regression passed, 54 passed, 0 failed, 0 skipped
  Release build passed, 0 warnings, 0 errors

interpretation:
  prewarm removes the measured MeasureFile() allocation blocker
  the prewarm allocation is real and must remain explicitly attributed
  the prewarmed MeasureFile contour is allocation-ready with a non-blocking
    filesystem timing note
  slice 7 must decide the scoped prewarm-contour posture and how the
    filesystem timing note is carried into the decision trace
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
[x] file-level thresholds are recorded before Release gate interpretation
[x] same-run explicit BlockingBorrowed oracle rows remain available,
    documented, and visibly separate
[x] direct MeasureFile() omitted defaults still resolve to the accepted
    queued-owned rollout contour unless a scoped file-level decision changes
    that posture
[x] direct MeasureCache() omitted defaults preserve the milestone 016 accepted
    cache-level posture
[x] CLI omitted-provider file benchmark remains aligned with direct API
    defaults unless a scoped file-level decision changes that posture
[ ] correctness parity against borrowed rows is preserved for every readiness
    row
[ ] retained cleanup returns current pressure to zero in natural direct/default
    rows
[ ] release failures remain 0
[ ] retained pressure stays within the configured 536870912 byte budget
[x] cold file-level allocation and elapsed cost are classified against
    thresholds recorded before gate interpretation
[x] repeated/warm file-level behavior is classified separately from cold
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
