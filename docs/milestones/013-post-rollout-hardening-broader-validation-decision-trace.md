# Milestone 013 Decision Trace

Date: 2026-05-21

Decision: the milestone 012 scoped queued-owned default remains accepted for
the `processing benchmark rebalance-archive` CLI omitted-provider path, and
the KTLX 2026-05-05 allocation warning is accepted as the cost of proceeding
to a direct archive rebalance API default migration milestone.

The default stays in place for the existing scoped CLI surface because the
broader gate preserved correctness, cleanup, release health, pressure budget,
operator provenance, fallback separation, and elapsed performance. The
borderline KTLX 2026-05-05 allocation signal is not treated as a blocker; it is
accepted as a known migration cost for the direct
`MeasureFile()`/`MeasureCache()` default migration. Live ingestion/runtime
defaults remain out of scope.

## Decision Matrix

```text
default stability posture:
  accepted for the existing scoped CLI omitted-provider rebalance-archive
  default; no rollback recommended

fallback/oracle posture:
  accepted; blocking-borrowed remains explicit fallback and same-run oracle

direct API compatibility posture:
  preserved in milestone 013; MeasureFile()/MeasureCache() defaults remain
  blocking-borrowed until the next migration milestone changes them

broader expansion posture:
  approved as next-milestone input for direct archive rebalance API defaults;
  not approved for live ingestion/runtime defaults

threshold acceptance:
  correctness, release health, cleanup, pressure, timing, and primary
  allocation accepted; broader KTLX 2026-05-05 allocation is accepted as an
  explicit migration cost

allocation attribution result:
  sufficient to identify retained owned snapshot allocation and shifted
  callback-side work as the main residual categories

operator-surface result:
  accepted; help/output/provenance make scoped default, explicit fallback, and
  direct API compatibility boundaries reproducible

recommended next milestone input:
  direct archive rebalance API default migration, carrying the accepted
  allocation cost and preserving same-run blocking-borrowed oracle coverage;
  live/runtime default migration remains out of scope
```

## Decision Explanations

### Keep The Scoped CLI Default

Decision: keep the milestone 012 queued-owned scoped CLI default in place for
the `processing benchmark rebalance-archive` omitted-provider path.

Why chosen: the broader milestone 013 gate preserved correctness, release
health, retained cleanup, retained pressure, elapsed performance, fallback
separation, and operator provenance across the captured local contours.

Alternatives: roll back the omitted-provider path to blocking-borrowed, require
explicit queued-owned flags again, or freeze the default until direct API
migration is complete.

Rejected because: rollback is not supported by the evidence; returning to
explicit-only usage would undo the milestone 012 rollout without a correctness
or cleanup failure; and tying CLI stability to direct API migration would block
a scoped default that already passed its own gate.

Trade-offs/debt: the scoped CLI default remains benchmark-only. It does not
become a live ingestion default, a durable queue design, or a cross-process
worker contract.

Review explanation: "The broader gate did not find a reason to undo the scoped
CLI default, so the default stays where it was already approved."

### Accept KTLX 2026-05-05 Allocation Cost

Decision: accept the KTLX 2026-05-05 allocation warning, including the
`1.1005x` two-run average, as the cost of proceeding to direct archive
rebalance API default migration.

Why chosen: KTLX 2026-05-05 was borderline only on allocation. Correctness,
release health, cleanup, retained pressure, elapsed timing, and fallback
separation still passed, and the allocation overhead is attributable enough to
carry forward as a visible migration cost.

Alternatives: block the next migration until allocation drops below threshold
on every broader row, roll back the scoped CLI default, raise the threshold, or
ignore the warning because the average was close.

Rejected because: blocking would treat a narrow allocation warning as stronger
than the rest of the passing gate; rollback would punish an already-stable
scoped CLI surface; raising the threshold after measurement would weaken the
gate; ignoring the warning would hide the main cost signal.

Trade-offs/debt: the next migration milestone must carry the KTLX 2026-05-05
row as a tracked risk. Allocation optimization remains debt, not a prerequisite
for starting the direct API default migration.

Review explanation: "I am not hiding the threshold tension; I am accepting it
as the explicit cost of the next migration."

### Direct API Migration Is Next, Not Done Here

Decision: use milestone 013 as approval input for a separate direct archive
rebalance API default migration milestone.

Why chosen: direct `MeasureFile()` and `MeasureCache()` defaults still need a
focused compatibility change. Milestone 013 proved enough stability to plan
that change, but it did not change direct API default behavior.

Alternatives: change direct defaults inside milestone 013, postpone all direct
API work until allocation is optimized, or broaden immediately into live
runtime defaults.

Rejected because: changing direct defaults in the decision trace would skip the
implementation and test slice; postponing all migration would waste a passing
stability gate; live runtime defaults need different durability, failure, and
operator contracts.

Trade-offs/debt: the handoff must be explicit that direct API defaults remain
blocking-borrowed until the next milestone changes code and tests.

Review explanation: "Milestone 013 authorizes the next migration target; it
does not silently perform that migration."

### Preserve Fallback And Borrowed Oracle

Decision: preserve explicit `--provider blocking-borrowed` fallback and
same-run borrowed oracle coverage.

Why chosen: the broader gate depends on borrowed rows to interpret correctness,
allocation, elapsed time, and provenance. Keeping that oracle is also the
practical rollback and diagnosis path for the next migration.

Alternatives: remove borrowed coverage after rollout, keep borrowed only for
manual commands, or replace same-run comparison with historical thresholds.

Rejected because: removing borrowed coverage would make regressions harder to
classify; manual-only coverage would be easier to skip; historical thresholds
are weaker than same-run comparisons on the same cache shape.

Trade-offs/debt: future migration gates remain heavier because they must carry
borrowed/default pairs. That cost is intentional because it protects the
default decision.

Review explanation: "Borrowed remains the comparison baseline for the
migration, even when it is no longer the omitted-provider default."

### Preserve Fail-Closed Behavior

Decision: keep queued-owned failures fail-closed with no automatic fallback to
borrowed success.

Why chosen: a default migration should surface queued-owned failures directly.
Automatic fallback would make a run look successful while silently switching to
a different provider contract.

Alternatives: retry failed queued-owned runs with blocking-borrowed, treat
borrowed success as final success after queued-owned failure, or downgrade
queued-owned failures to warnings.

Rejected because: each alternative hides provider-specific failure behavior and
would corrupt rollout evidence, operator provenance, and future gate results.

Trade-offs/debt: operators must choose fallback explicitly when they need it.
The default path remains stricter, which is the right posture for migration
evidence.

Review explanation: "A queued-owned failure has to stay visible; borrowed
success can be requested, but it cannot overwrite the failed default path."

### Allocation Attribution Is Sufficient For Follow-Up

Decision: treat the retained owned snapshot allocation and shifted callback-side
work attribution as sufficient to guide follow-up work.

Why chosen: the milestone identified the clearest residual allocation category
without needing to solve allocation optimization before the next migration
starts.

Alternatives: require full attribution of every byte before migration, remove
the attribution block from the decision, or optimize allocation immediately in
the stability milestone.

Rejected because: full attribution would overfit the milestone to one warning;
removing attribution would make the decision less reviewable; immediate
optimization would mix a stability decision with a new performance-change
slice.

Trade-offs/debt: attribution is directional rather than exhaustive. The next
milestone should keep the attribution rows visible while it changes direct API
defaults.

Review explanation: "The allocation data is sufficient to proceed deliberately,
while leaving optimization as tracked work."

## Included Surface

The decision applies to the already-rolled-out CLI surface only:

```text
processing benchmark rebalance-archive
  omitted --provider
  omitted --provider-overlap
  omitted --retention-strategy
  omitted --execution
  omitted --workers
  omitted --queue-capacity
  omitted --queue-retained-bytes
  omitted --queue-telemetry
  omitted --overlap-telemetry
  omitted --overlap-consumer-delay-ms
```

Effective scoped default:

```text
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

Excluded from the milestone 013 behavior change:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile() direct default
RadarProcessingArchiveRebalanceBenchmark.MeasureCache() direct default
synthetic processing benchmark defaults
non-benchmark archive publishing APIs
live ingestion/runtime provider defaults
durable queue or broker integration
cross-process workers
builder-transfer retained payload execution
automatic fallback from queued-owned failure to blocking-borrowed success
```

## Evidence

Primary source documents:

```text
docs/milestones/013-post-rollout-hardening-broader-validation.md
docs/milestones/013-post-rollout-hardening-broader-validation-plan.md
docs/milestones/013-post-rollout-hardening-broader-validation-performance-gate.md
```

Focused regression before the gate:

```text
RadarPulseCliRebalanceBenchmarkTests: 26 passed, 0 failed, 0 skipped
NexradArchiveRadarEventBatchPublisherTests: 22 passed, 0 failed, 0 skipped
Readiness/overlap/allocation filter: 31 passed, 0 failed, 0 skipped
Failure/cleanup filter: 24 passed, 0 failed, 0 skipped
Release build: succeeded, 0 warnings, 0 errors
```

Broader natural Release gate:

```text
Release build succeeded with 0 warnings and 0 errors.

primary repeated contour:
  --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
  three borrowed/default pairs

broader single-shape contour:
  --cache data\nexrad --date 2026-05-04 --radar KINX --max-files 220

borderline repeated single-shape contour:
  --cache data\nexrad --date 2026-05-05 --radar KTLX --max-files 220
  two borrowed/default pairs

mixed-cache contour:
  --cache data\nexrad --max-files 1000000
```

## Threshold Decisions

Correctness parity: accepted.

```text
validation succeeded across captured rows
failed migrations: 0
primary KTLX 2026-05-04 validation checksum:
  7_480_064_646_096_449_000
KINX 2026-05-04 validation checksum:
  1_465_969_045_420_103_918
KTLX 2026-05-05 validation checksum:
  11_084_221_590_146_245_827
mixed-cache validation checksum:
  615_051_108_812_661_629
```

Release health: accepted.

```text
retained payload failed releases: 0
provider overlap failed releases: 0
```

Retained cleanup: accepted.

```text
current pending retained payload bytes at completion: 0
current active retained payload bytes at completion: 0
current combined retained payload bytes at completion: 0
```

Retained pressure budget: accepted.

```text
budget: 536_870_912 bytes
primary KTLX 2026-05-04 high watermark: 48_257_280 bytes, 8.99%
KINX 2026-05-04 high watermark: 48_342_240 bytes, 9.00%
KTLX 2026-05-05 high watermark: 52_676_640 bytes, 9.81%
mixed-cache high watermark: 54_413_280 bytes, 10.14%
```

Elapsed threshold: accepted.

```text
threshold: <= 1.00x borrowed
primary KTLX 2026-05-04 elapsed ratio: 0.911x borrowed
KINX 2026-05-04 elapsed ratio: 0.939x borrowed
KTLX 2026-05-05 elapsed ratio: 0.943x borrowed average
mixed-cache elapsed ratio: 0.907x borrowed
```

Run spread threshold: accepted for the primary matrix.

```text
threshold: candidate spread <= 7.50% of candidate average
primary KTLX 2026-05-04 candidate spread: 915.72 ms
primary KTLX 2026-05-04 candidate spread / average: 5.41%
```

Allocation threshold: accepted for primary and most broader rows; warning for
KTLX 2026-05-05.

```text
threshold: <= 1.10x borrowed
primary KTLX 2026-05-04 allocation ratio: 1.071x borrowed
KINX 2026-05-04 allocation ratio: 1.070x borrowed
mixed-cache allocation ratio: 1.062x borrowed
KTLX 2026-05-05 run 1 allocation ratio: 1.101x borrowed
KTLX 2026-05-05 run 2 allocation ratio: 1.0996x borrowed
KTLX 2026-05-05 two-run average allocation ratio: 1.1005x borrowed
decision: accepted as explicit migration cost
```

Default expansion evidence: accepted.

```text
rollout-default rows omitted provider, execution, worker, queue, retained
budget, and telemetry flags
Provider mode source: rollout-default
Provider default rollout contour: yes
Provider rollout default expansion: yes
Provider fallback contour: no
Provider overlap evidence contour: natural-default-candidate
Provider overlap evidence scope: natural-readiness
```

Fallback separation: accepted.

```text
borrowed rows used explicit --provider blocking-borrowed
borrowed rows reported Provider fallback contour: yes
rollout-default rows reported Provider fallback contour: no
queued-owned failure remains fail-closed; no automatic borrowed fallback is
part of this decision
```

## Allocation Decision

The residual allocation overhead is acceptable for keeping the existing scoped
CLI default and for using the contour as the next direct API default migration
baseline. The cost is explicitly accepted rather than hidden.

Primary attribution:

```text
primary candidate average allocation over borrowed: +141_122_411 bytes
primary candidate average retained owned snapshot allocation: 125_881_864 bytes
primary candidate retained owned snapshot allocation range:
  123_784_656 to 127_980_376 bytes
primary candidate processing callback non-owned snapshot allocation range:
  1_711_159_408 to 1_721_909_448 bytes
primary candidate replay/build allocation range:
  268_964_416 to 278_384_496 bytes
borrowed replay/build allocation range:
  1_709_761_152 to 1_713_983_128 bytes
```

Interpretation:

```text
retained owned snapshot allocation is the clearest incremental category
non-owned callback allocation is the largest candidate-side bucket
candidate replay/build allocation remains much lower than borrowed replay/build
allocation attribution is sufficient to direct follow-up work
the KTLX 2026-05-05 threshold tension is accepted as migration cost and should
remain visible in the next milestone gate
```

## Operational Posture

Default posture:

```text
For processing benchmark rebalance-archive, omitting --provider continues to
select the queued-owned rollout contour.
```

Fallback posture:

```text
Operators can still run the borrowed path with:
  --provider blocking-borrowed
```

Oracle posture:

```text
Same-run blocking-borrowed rows remain the comparison oracle for future
benchmark gates, performance regressions, allocation follow-up, and any direct
API migration proposal.
```

Failure posture:

```text
queued-owned failures fail closed
there is no automatic borrowed fallback after queued-owned failure
fallback is an explicit operator/provider choice only
```

Direct API posture:

```text
direct MeasureFile()/MeasureCache() defaults remain blocking-borrowed
explicit queued-owned options remain available for direct benchmark calls
direct default migration is the recommended next milestone input
```

## Residual Risks And Limits

```text
allocation warning:
  KTLX 2026-05-05 reached a two-run average allocation ratio of 1.1005x
  borrowed, with one run above the 1.10x threshold; this is accepted as a
  migration cost, not a rollback blocker

local gate only:
  the broader gate used locally available NEXRAD cache shapes

benchmark surface only:
  the accepted posture applies to the rebalance-archive CLI benchmark surface

natural queue depth:
  natural runs kept queue depth at 1; queue-ahead mechanics remain covered by
  controlled proof tests rather than this natural gate

mixed-cache worker failure counters:
  mixed-cache rows reported matching worker failed batch/item counters in both
  borrowed and rollout-default rows while validation still succeeded

no runtime ingestion claim:
  live ingestion, durable queues, brokers, and cross-process providers remain
  outside milestone 013
```

## Decision

Milestone 013 answers the closeout question with **yes** for the existing
scoped CLI default:

```text
the milestone 012 scoped queued-owned default remains stable enough to keep as
the processing benchmark rebalance-archive omitted-provider default
```

Milestone 013 answers **yes** for direct archive rebalance API default
migration as the next milestone input:

```text
the KTLX 2026-05-05 allocation warning is accepted as the cost of migration,
so direct MeasureFile()/MeasureCache() default migration can proceed as a
separate milestone with the same borrowed oracle and gate discipline
```

Milestone 013 answers **no** for runtime/live ingestion default migration:

```text
live ingestion, durable queues, brokers, cross-process providers, and runtime
defaults remain out of scope
```

Recommended next milestone input:

```text
direct archive rebalance API default migration for MeasureFile()/MeasureCache()
with explicit allocation-cost acceptance, same-run blocking-borrowed oracle
coverage, and a repeated KTLX 2026-05-05 gate row carried forward as a tracked
risk
```

Blocking-borrowed remains the explicit fallback and same-run oracle:

```text
--provider blocking-borrowed
```

The milestone can proceed to closeout without reverting the scoped CLI default.
The allocation warning remains a tracked cost and risk for the next direct API
migration milestone, not a blocker to that migration.
