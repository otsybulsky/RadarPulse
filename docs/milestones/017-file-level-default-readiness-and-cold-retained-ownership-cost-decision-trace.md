# Milestone 017 Decision Trace

Date: 2026-05-22

Decision: accept file-level and small-file default readiness for the
queued-owned direct/default archive rebalance contour, with retained payload
prewarm as the scoped direct benchmark default-equivalent path and with named
scoped warnings.

This decision accepts the current default contour for
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` and
`MeasureCache()` when provider/execution/queue/retention controls are omitted:
the queued-owned rollout contour plus automatic retained payload prewarm for
the direct benchmark default-equivalent path.

The decision does not accept the natural unprewarmed file/small-file contour.
Natural file and low-count cache rows were safety-clean but allocation-blocked.
Prewarm removes that allocation from the measured operation, but the up-front
allocation remains real and must stay explicitly attributed in result contracts
and CLI output.

The decision is not clean-green. It carries a named up-front prewarm cost, a
non-blocking filesystem timing note for file-level elapsed jitter, a local
corpus/benchmark-surface scope limit, and an explicit runtime/durable
out-of-scope boundary.

## Included Surface

Included direct API surface:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
```

Included CLI surface:

```text
processing benchmark rebalance-archive paths that use the same direct archive
rebalance benchmark APIs and omitted-provider defaults
```

Included default contour:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async shard transport
worker count: 4
worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536870912
overlap consumer delay: 0
retained payload prewarm: enabled for the direct benchmark
  default-equivalent contour
```

Default prewarm sizing:

```text
event count: 65_536
payload bytes: 67_108_864
retained batch count: 1
```

Excluded surfaces:

```text
live ingestion/runtime provider defaults
durable queues or brokers
cross-process provider or worker transport
ordered concurrent rebalance runtime
non-benchmark archive publishing API defaults
synthetic processing benchmark defaults
builder-transfer retained payload execution
automatic borrowed fallback
```

## Decision Matrix

```text
file-level default readiness:
  accepted with named scoped warnings for the queued-owned direct benchmark
  default-equivalent contour with retained payload prewarm

natural unprewarmed MeasureFile posture:
  not accepted; all selected natural file rows passed safety guardrails but
  failed allocation thresholds

prewarmed MeasureFile posture:
  accepted; measured allocation ratios were 0.980x to 1.026x, retained pool
  misses were 0, and fail-level elapsed outliers did not reproduce in targeted
  timing repeats

small-file natural MeasureCache posture:
  not accepted; natural low-count cache allocation ratios were 1.176x to
  2.168x and retained owned snapshot cost persisted through 2/4/8 published
  base-data files

small-file prewarmed MeasureCache posture:
  accepted; measured allocation ratios were 0.818x to 1.002x, elapsed ratios
  were 0.454x to 0.979x, and retained pool misses were 0

post-default cache regression posture:
  accepted; full cache matrix passed 16/16 groups and 28/28 safety pairs with
  processing completeness failures 0 and worker failed batches/items 0/0

mixed-cache worker-counter posture:
  resolved as a source-universe sizing bug; worker failed batches/items are
  now processing-completeness blockers and mixed-cache-all passes at 0/0

prewarm attribution posture:
  accepted as a named up-front default cost; not folded into measured row
  allocation and visible in result contracts and CLI output

explicit fallback/oracle posture:
  accepted and preserved; explicit BlockingBorrowed remains selectable,
  unprewarmed, and required for same-run gate comparisons

MeasureFile/MeasureCache alignment:
  accepted; omitted defaults stay aligned to the queued-owned rollout contour
  plus scoped retained payload prewarm

runtime expansion posture:
  not approved; live ingestion, durable queues, cross-process workers, ordered
  concurrent rebalance, and builder-transfer remain future milestones
```

## Decision Explanations

### Accept File-Level Default Readiness With Scoped Prewarm

Decision: accept file-level `MeasureFile()` default readiness for the direct
benchmark default-equivalent contour after retained payload prewarm is part of
that default contour.

Why chosen: natural `MeasureFile()` rows proved correctness, validation,
release, cleanup, pressure, worker counters, fallback visibility, and
CLI/direct alignment, but failed allocation thresholds. The prewarmed
comparison removed the measured allocation blocker: selected prewarmed file
rows measured allocation ratios from `0.980x` to `1.026x` borrowed, retained
pool misses were `0`, release failures were `0`, worker failed batches/items
were `0/0`, and retained pressure returned to `0`.

Alternatives: reject file-level readiness, keep prewarm opt-in only, split
`MeasureFile()` omitted defaults away from `MeasureCache()`, or accept the
natural unprewarmed contour.

Rejected because: rejecting readiness would ignore that the blocker was a
first-use retained-pool allocation that prewarm addresses directly; keeping
prewarm opt-in only would preserve a known allocation-blocked default path;
splitting file defaults is unnecessary because `MeasureFile()` and
small-file `MeasureCache()` agree after prewarm; and accepting natural
unprewarmed defaults would hide the recorded allocation failures.

Trade-offs/debt: the accepted default has an up-front prewarm cost. This is a
benchmark-surface default decision, not a runtime provider default migration.

Review explanation: "File-level readiness is accepted for the prewarmed
direct default contour, not for the natural unprewarmed cold path."

### Reject Natural Unprewarmed File And Small-File Readiness

Decision: keep the natural unprewarmed file/small-file contour classified as
allocation-blocked for readiness.

Why chosen: the natural file gate passed all safety guardrails across 20
borrowed/default pairs, but every selected file row failed allocation
thresholds. Natural representative cold allocation ratios ranged from
`1.958x` to `2.995x`; representative warm allocation ratios ranged from
`1.916x` to `2.186x`; small and large warm file rows ranged from `1.443x` to
`2.207x`. Natural small-file `MeasureCache()` rows also stayed allocation
blocked, with ratios from `1.176x` to `2.168x`.

Alternatives: treat natural elapsed wins as sufficient, rely on broader
cache-level readiness, or average small-file rows into a pass.

Rejected because: milestone 017 thresholds were recorded before gate
interpretation and made allocation a readiness criterion; cache-level
readiness does not certify file-level cold behavior; and the milestone
explicitly forbids hiding cold retained cost behind aggregate small-cache
success.

Trade-offs/debt: the natural unprewarmed data remains useful as historical
evidence and as a warning for any future runtime surface that cannot prewarm.

Review explanation: "The unprewarmed path is safety-clean but not
file/small-cache ready because retained owned snapshot allocation dominates
the measured row."

### Accept Prewarm Cost As Named Up-Front Default Cost

Decision: accept retained payload prewarm as a named up-front cost of the
direct benchmark default-equivalent contour.

Why chosen: prewarm removes event-array and byte-array pool misses from the
measured row while preserving explicit attribution. Default prewarm sizing is
fixed at `65_536` events, `67_108_864` payload bytes, and `1` retained batch.
Result contracts expose prewarm attribution, and CLI output prints prewarm
enabled state, sizing, elapsed time, allocated bytes, and retained bytes.

Alternatives: hide prewarm inside measured allocation, ignore prewarm cost,
size prewarm dynamically without attribution, or avoid prewarm and keep the
file default allocation-blocked.

Rejected because: hidden or unattributed prewarm would make benchmark results
misleading; dynamic hidden sizing would be harder to review; and leaving the
default blocked would not use the simplest fix for the identified first-use
pool allocation cost.

Trade-offs/debt: the direct default path is faster after prewarm, but startup
or first-use cost has moved before the measured operation. Runtime surfaces
will need an explicit lifecycle and ownership decision before adopting the
same idea.

Review explanation: "Prewarm is accepted only because the cost is explicit,
stable, and separately reported."

### Accept Filesystem Timing Note

Decision: carry prewarmed file-level elapsed variance as a non-blocking
filesystem timing note.

Why chosen: the first prewarmed file gate had several elapsed outliers, but
the targeted timing rerun did not reproduce fail-level behavior. Five-pair
reruns measured average elapsed ratios from `1.017x` to `1.056x`, measured
allocation around `1.0x`, retained pool misses `0`, and safety guardrails
clean. The remaining spread is plausibly filesystem timing noise on local
archive reads rather than retained ownership cost.

Alternatives: fail the milestone on the original outliers, require more
targeted reruns before decision trace, or erase the timing note because the
allocation blocker was fixed.

Rejected because: the outliers were not reproducible in targeted repeats and
did not coincide with allocation, release, cleanup, pressure, correctness, or
worker failures; requiring more reruns would not change the attribution; and
erasing the note would hide real timing variance.

Trade-offs/debt: future file-level gates should keep elapsed spread visible.
If a later row combines repeated slower timing with allocation or safety
regressions, it should be treated as a fresh blocker.

Review explanation: "The timing note stays visible, but it no longer blocks
readiness after targeted repeats."

### Accept Small-File Cache Transition With Prewarm

Decision: accept small-file `MeasureCache()` default readiness for the
prewarmed direct benchmark default-equivalent contour.

Why chosen: natural low-count cache rows reproduced the retained allocation
blocker, but explicit prewarmed comparison rows all passed measured allocation
and elapsed thresholds. The prewarmed small-cache rows measured allocation
ratios from `0.818x` to `1.002x`, elapsed ratios from `0.454x` to `0.979x`,
candidate spread below `4.18%`, retained pool misses `0`, and all safety
guardrails clean. The post-default full cache regression matrix then passed
`16/16` group rows and `28/28` safety pairs with worst allocation `1.009x`.

Alternatives: keep small-file cache readiness separate from `MeasureFile()`,
require more low-count slices, or accept natural small-cache rows because
elapsed ratios were already below borrowed.

Rejected because: `MeasureFile()` and low-count `MeasureCache()` show the same
allocation shape and the same prewarm fix; the selected slices cover 2/4/8
published files across KTLX 2026-05-04, KINX 2026-05-04, and KTLX
2026-05-05; and elapsed alone is insufficient when allocation thresholds
failed.

Trade-offs/debt: the decision remains local-corpus scoped. It does not prove
all radar/date/file-count combinations or non-local cache behavior.

Review explanation: "Small-file cache readiness follows the same rule as
file readiness: natural is blocked, prewarmed default is accepted."

### Preserve BlockingBorrowed Oracle And Fail-Closed Behavior

Decision: preserve explicit `BlockingBorrowed` as fallback and same-run oracle
for file, small-cache, and cache regression gates.

Why chosen: same-run borrowed rows remain necessary to interpret correctness
parity, elapsed ratios, allocation ratios, topology counters, skipped
reasons, release health, cleanup, and regression diagnosis. Explicit
`BlockingBorrowed` remains unprewarmed, and queued-owned failures still fail
closed with no automatic silent borrowed fallback.

Alternatives: remove borrowed comparisons after accepting defaults, silently
fallback to borrowed after queued-owned failures, or prewarm borrowed rows for
symmetry.

Rejected because: removing borrowed rows would remove the oracle used by the
milestone; silent fallback would hide queued-owned failures; and prewarming
borrowed rows would change the explicit fallback contour being used as a
comparison baseline.

Trade-offs/debt: future gates must keep carrying same-run borrowed rows even
after direct defaults are accepted.

Review explanation: "Readiness does not remove the oracle; it depends on it."

### Resolve Mixed-Cache Worker Counters As Gate Safety

Decision: treat the old mixed-cache worker failed counters as resolved by the
source-universe sizing fix and require processing completeness in future
archive rebalance readiness rows.

Why chosen: the mixed-cache `221/881` worker failures were diagnosed as
`SourceOrderViolation` caused by unfiltered mixed KINX/KTLX files running
through `DefaultSingleRadar`. `MeasureCache()` now scans selected files when
no radar filter is supplied, sizes the source universe to distinct selected
radar ids, and keeps radar-filtered rows single-radar. Result contracts and
CLI output expose processing completeness, processing validation failed
batches, and worker failure counts. The post-fix mixed-cache-all rows passed
with worker failed batches/items `0/0`.

Alternatives: keep the old milestone 016 note as accepted warning, exclude
mixed-cache from evidence, or treat validation success alone as sufficient.

Rejected because: the old note had a concrete bug-level cause; mixed-cache is
valuable coverage; and validation success alone is insufficient if processing
invalid batches or worker failures occurred.

Trade-offs/debt: the source-universe auto-sizing guardrail is capped at 256
distinct radar ids. Larger synthetic or external corpora would need an
explicit sizing decision.

Review explanation: "Worker failures are not quietly ignored; they are now
processing-completeness blockers."

### Keep Runtime And Durable Surfaces Out Of Scope

Decision: do not migrate live ingestion, runtime provider defaults, durable
queues, brokers, cross-process workers, or ordered concurrent rebalance in
milestone 017.

Why chosen: milestone 017 evidence is direct benchmark and CLI benchmark
evidence over local archive files. Runtime/live surfaces need their own
lifecycle, startup/prewarm timing, backpressure, operator fallback, cleanup,
cancellation, release, and observability contracts.

Alternatives: promote the prewarmed default directly into runtime, treat
direct benchmark readiness as enough for runtime defaults, or design durable
transport inside the file-level milestone.

Rejected because: runtime startup and long-lived lifecycle costs are different
from a direct benchmark measurement window; durable/cross-process transport
has not been designed in this milestone; and adopting runtime prewarm without
operator-visible policy would hide a real resource cost.

Trade-offs/debt: runtime/live ingestion readiness is now the natural next
milestone input, but it must start from explicit runtime requirements rather
than inheriting benchmark defaults automatically.

Review explanation: "Milestone 017 closes the direct benchmark file/small-file
question; runtime remains a separate decision."

## Evidence

Thresholds were recorded before gate interpretation:

```text
cold MeasureFile allocation:
  pass <= 1.10x, warning <= 1.50x, optimize <= 1.75x, fail > 1.75x

cold MeasureFile elapsed:
  pass <= 1.00x, warning <= 1.10x, optimize <= 1.25x, fail > 1.25x

warm MeasureFile allocation:
  pass <= 1.10x, warning <= 1.20x, optimize <= 1.35x, fail > 1.35x

warm MeasureFile elapsed:
  pass <= 1.00x, warning <= 1.10x, optimize <= 1.20x, fail > 1.20x

repeated/warm candidate elapsed spread:
  warning if spread > 7.50%
```

Natural `MeasureFile()` gate:

```text
selected file rows: 10
borrowed/default pairs: 20
safety guardrails: all passed
release failures: 0
worker failed batches/items: 0/0
current retained pressure at completion: 0
max retained high-water: 51_484_320 of 536_870_912 bytes

cold representative allocation ratios: 1.958x to 2.995x
warm representative allocation ratios: 1.916x to 2.186x
small/large warm allocation ratio range: 1.443x to 2.207x
result: allocation-blocked
```

Prewarmed `MeasureFile()` comparison:

```text
borrowed/prewarmed-candidate pairs: 20
safety guardrails: all passed
measured allocation ratios: 0.980x to 1.026x
retained pool misses: 0 for every row
release failures: 0
worker failed batches/items: 0/0
prewarm allocation: 35_651_808 to 71_303_392 bytes
targeted timing rerun: fail-level elapsed outliers did not reproduce
result: allocation-ready with filesystem timing note
```

Natural small-file `MeasureCache()` gate:

```text
borrowed/natural-candidate pairs: 18
selected published base-data counts: 2, 4, and 8
safety guardrails: all passed
worker failed batches/items: 0/0
current retained pressure at completion: 0
elapsed ratios: all <= 0.986x borrowed average
allocation ratios: 1.176x to 2.168x
result: allocation-blocked
```

Prewarmed small-file `MeasureCache()` comparison:

```text
borrowed/prewarmed-candidate pairs: 18
safety guardrails: all passed
measured allocation ratios: 0.818x to 1.002x
elapsed ratios: 0.454x to 0.979x
candidate spread: <= 4.18%
retained pool misses: 0
prewarm allocation: about 69_206_240 bytes per measured candidate row
result: measured allocation and elapsed pass
```

Post-default full cache regression matrix:

```text
runner:
  data\temp\m017-cache-regression-runner

outputs:
  data\temp\m017-cache-regression-runner\output\m017-cache-regression-20260522-110241.jsonl
  data\temp\m017-cache-regression-runner\output\m017-cache-regression-20260522-110241.md

group rows: 16 passed, 0 warning, 0 optimize, 0 failed
pairs: 28 passed safety, 0 failed safety
measurements: 56
worst measured allocation ratio: 1.009x on mixed-cache-all
worst elapsed ratio: 0.988x on KTLX 2026-05-05 2-file small-cache row
worst candidate spread: 4.60%, below the 7.50% threshold
retained payload pool misses: 0
validation failures: 0
processing completeness failures: 0
processing validation failed batches: 0
worker failed batches/items: 0/0
release failures: 0
current retained bytes after rows: 0
```

Final verification before closeout:

```text
Release build:
  succeeded, 0 warnings, 0 errors

focused mixed-radar/default regression:
  passed

full test project:
  771 passed, 0 failed, 3 skipped on stable rerun

mixed-cache-all borrowed async spot check:
  processing completeness succeeded
  worker failed batches/items: 0/0

mixed-cache-all omitted default spot check:
  processing completeness succeeded
  worker failed batches/items: 0/0
  retained payload pool misses: 0
  release failures: 0
```

## Operational Posture

Default direct API posture:

```text
MeasureFile() omitted defaults:
  queued-owned rollout contour plus retained payload prewarm

MeasureCache() omitted defaults:
  queued-owned rollout contour plus retained payload prewarm
  mixed-radar source universe auto-sizes when no radar filter is supplied

explicit BlockingBorrowed:
  remains selectable
  remains unprewarmed
  remains same-run oracle

explicit retained payload factory:
  caller-owned
  passed through
  not reported as automatic default prewarm
```

Failure posture:

```text
queued-owned failures fail closed
processing validation failed batches block processing completeness
worker failed batches/items block processing completeness
there is no automatic borrowed fallback after queued-owned failure
```

Attribution posture:

```text
prewarm allocation is not folded into measured row allocation
prewarm enabled state, sizing, elapsed time, allocated bytes, and retained
bytes are exposed through result contracts and CLI output
```

Runtime posture:

```text
live ingestion/runtime defaults remain unchanged and out of scope
durable queues, brokers, cross-process providers, ordered concurrent
rebalance, and builder-transfer require future milestones
```

## Residual Risks And Limits

```text
local corpus only:
  the decision covers the available local NEXRAD file and cache shapes used by
  milestone 017; it does not certify absent radar sites, absent dates,
  non-local corpora, or every possible file size

natural unprewarmed path:
  still allocation-blocked for file/small-file readiness and should not be
  described as ready without prewarm

prewarm cost:
  accepted as an explicit up-front direct benchmark default cost; runtime
  startup and lifecycle policy remain undecided

filesystem timing note:
  fail-level prewarmed elapsed outliers did not reproduce, but local file I/O
  variance remains a visible future gate note

mixed-radar source universe:
  unfiltered cache rows now auto-size to selected radar count with a guardrail
  of 256 distinct radar ids

no runtime claim:
  live ingestion, durable queues, brokers, cross-process workers, ordered
  concurrent rebalance, builder-transfer, and runtime defaults remain outside
  milestone 017
```

## Decision

Milestone 017 answers the closeout question with **yes, with warnings**:

```text
file-level and small-file default readiness is accepted for the queued-owned
direct benchmark default-equivalent contour with retained payload prewarm
```

Milestone 017 answers **no** for the natural unprewarmed path:

```text
natural file and low-count cache rows are safety-clean but allocation-blocked
and are not the accepted file/small-cache default-readiness posture
```

Milestone 017 answers **accepted as named cost** for prewarm:

```text
retained payload prewarm is an explicit up-front default cost, not hidden
inside measured row allocation
```

Milestone 017 answers **accepted as timing note** for file-level elapsed
variance:

```text
fail-level elapsed outliers did not reproduce in targeted timing repeats;
remaining spread is carried as a non-blocking filesystem timing note
```

Milestone 017 answers **yes** for preserving direct API and CLI alignment:

```text
MeasureFile(), MeasureCache(), and CLI omitted-provider benchmark paths stay
aligned to the queued-owned rollout contour plus scoped retained payload
prewarm
```

Milestone 017 answers **yes** for preserving explicit borrowed fallback and
same-run oracle coverage:

```text
providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed
```

Milestone 017 answers **no** for live/runtime default migration:

```text
live ingestion, durable queues, brokers, cross-process providers, ordered
concurrent rebalance, builder-transfer, and runtime defaults remain out of
scope
```

Recommended next milestone input:

```text
close out milestone 017, then design a runtime/live ingestion readiness
milestone that treats direct benchmark file/cache readiness as evidence, not
as automatic approval for runtime defaults
```

The milestone can proceed to closeout without additional runtime changes,
targeted reruns, threshold changes, or borrowed fallback behavior changes.
