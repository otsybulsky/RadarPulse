# Milestone 013 Performance Gate

Date: 2026-05-21

Scope: broader natural Release gate capture for the milestone 013
post-rollout hardening milestone. This document records correctness, cleanup,
pressure, allocation, attribution, timing, variance, provenance, and fallback
evidence for the already-rolled-out scoped `processing benchmark
rebalance-archive` CLI default.

This document captures the gate evidence. It does not close milestone 013 or
replace the stability decision trace.

Gate status:

```text
captured with allocation warning
```

Summary:

```text
primary KTLX 2026-05-04 matrix: pass
correctness parity: pass across captured rows
release failures: 0 across captured rollout-default rows
current retained pressure at completion: 0 across captured rollout-default rows
combined retained pressure: under 536870912 byte budget across captured rows
primary elapsed ratio: pass, 0.911x borrowed
primary candidate spread: pass, 5.41% of candidate average
primary allocation ratio: pass, 1.071x borrowed
broader KINX 2026-05-04 allocation ratio: pass, 1.070x borrowed
broader mixed-cache allocation ratio: pass, 1.062x borrowed
broader KTLX 2026-05-05 allocation ratio: borderline, two-row average
  1.1005x borrowed with one row above and one row below the 1.10x threshold
decision trace must treat the KTLX 2026-05-05 allocation result as a
  follow-up signal rather than a clean green gate
```

## Contours

Rollout default contour:

```text
provider: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async
workers: 4
provider queue capacity: 8
retained-byte budget: 536870912
queue telemetry: summary
overlap telemetry: summary
overlap consumer delay: 0
provider source: rollout-default through omitted provider flags
```

Borrowed oracle contour:

```text
provider: blocking-borrowed
execution: async
workers: 4
worker queue capacity: 1
provider source: explicit --provider blocking-borrowed
```

Common command parameters:

```text
processing benchmark rebalance-archive
--mode rebalance
--iterations 1
--warmup-iterations 0
--parallelism 24
--partitions 24
--shards 4
```

Borrowed reference rows added:

```text
--provider blocking-borrowed
--execution async
--workers 4
```

Rollout-default rows omitted provider, execution, worker, queue, retained
budget, and telemetry flags. The CLI expanded those omitted flags to the
milestone 012 rollout default contour.

## Verification Before Capture

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Recorded result:

```text
Release build succeeded with 0 warnings and 0 errors.
```

Focused regression from slice 7 remained the precondition for this gate:

```text
RadarPulseCliRebalanceBenchmarkTests: 26 passed, 0 failed, 0 skipped
NexradArchiveRadarEventBatchPublisherTests: 22 passed, 0 failed, 0 skipped
Readiness/overlap/allocation filter: 31 passed, 0 failed, 0 skipped
Failure/cleanup filter: 24 passed, 0 failed, 0 skipped
Release build: succeeded, 0 warnings, 0 errors
```

## Local Data Availability

The local cache contains these radar/date shapes:

```text
2026-05-04/KINX: 462 files
2026-05-04/KTLX: 244 files
2026-05-05/KTLX: 848 files
data\nexrad total files: 1554
```

Captured shapes:

```text
primary repeated contour:
  --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220

broader single-shape contour:
  --cache data\nexrad --date 2026-05-04 --radar KINX --max-files 220

borderline repeated single-shape contour:
  --cache data\nexrad --date 2026-05-05 --radar KTLX --max-files 220

mixed-cache contour:
  --cache data\nexrad --max-files 1000000
```

## Primary Natural Matrix

The primary matrix used KTLX on 2026-05-04 with `--max-files 220`.

Shared output:

```text
examined files: 220
skipped files: 22
published files: 198
payload values: 7_660_888_320
raw value checksum: 245_554_417_487
topology versions: 2
rebalance evaluations: 198
accepted moves: 2
skipped decisions: 392
failed migrations: 0
validation: succeeded
validation checksum: 7_480_064_646_096_449_000
skipped reason counters: no-hot-shard=376, no-cold-target-shard=4,
  source-shard-move-budget-exhausted=12, global-move-budget-exhausted=12
```

| Run | Borrowed elapsed ms | Candidate elapsed ms | Candidate delta ms | Candidate delta | Borrowed allocated bytes | Candidate allocated bytes | Allocation ratio |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 18_624.73 | 16_725.69 | -1_899.04 | -10.20% | 1_972_671_336 | 2_117_524_280 | 1.073x |
| 2 | 18_535.24 | 16_581.01 | -1_954.23 | -10.54% | 1_974_521_704 | 2_113_175_096 | 1.070x |
| 3 | 18_609.67 | 17_496.73 | -1_112.94 | -5.98% | 1_976_893_528 | 2_116_754_424 | 1.071x |

Primary contour summary:

```text
borrowed average elapsed ms: 18_589.88
candidate average elapsed ms: 16_934.48
candidate average delta ms: -1_655.40
candidate average delta: -8.90%
candidate elapsed ratio to borrowed: 0.911x
candidate run spread: 915.72 ms, 5.41% of average
borrowed average allocated bytes: 1_974_695_523
candidate average allocated bytes: 2_115_817_933
candidate allocation ratio: 1.071x borrowed
```

Primary rollout-default retained-resource telemetry was stable across all
three rows:

```text
provider mode: queued-owned
provider mode source: rollout-default
provider default rollout contour: yes
provider rollout default expansion: yes
provider fallback contour: no
provider overlap evidence contour: natural-default-candidate
provider overlap evidence scope: natural-readiness
provider overlap consumer delay ms: 0.00
worker count: 4
worker queue capacity: 8
provider queue depth high watermark: 1
provider overlap queue depth high watermark: 1
provider overlap has producer-consumer overlap: yes
provider overlap has queued-ahead overlap: no
provider queue combined retained payload bytes high watermark: 48_257_280
provider overlap combined retained payload bytes high watermark: 48_257_280
retained-byte budget: 536_870_912
high watermark / budget: 8.99%
retained payload attempts: 198
retained payload batches: 198
retained payload bytes: 9_537_763_200
retained payload values: 7_660_888_320
retained payload release attempts: 198
retained payload released batches: 198
retained payload failed releases: 0
provider overlap failed releases: 0
current pending retained batches at completion: 0
current pending retained payload bytes at completion: 0
current active retained batches at completion: 0
current active retained payload bytes at completion: 0
current combined retained batches at completion: 0
current combined retained payload bytes at completion: 0
```

Primary allocation attribution:

```text
candidate average allocation over borrowed: +141_122_411 bytes
candidate retained owned snapshot allocation range: 123_784_656 to
  127_980_376 bytes
candidate average retained owned snapshot allocation: 125_881_864 bytes
candidate processing callback non-owned snapshot allocation range:
  1_711_159_408 to 1_721_909_448 bytes
candidate replay/build allocation range: 268_964_416 to 278_384_496 bytes
borrowed replay/build allocation range: 1_709_761_152 to 1_713_983_128 bytes
```

Interpretation: primary residual allocation is dominated by the retained owned
snapshot cost and shifted callback-side work. The retained owned snapshot cost
accounts for most of the candidate total allocation increase over borrowed in
the primary matrix. Candidate replay/build allocation is much lower than the
borrowed replay/build allocation, so the total remains under the 1.10x
threshold.

## Broader Rows

| Shape | Borrowed elapsed ms | Candidate elapsed ms | Elapsed ratio | Borrowed allocated bytes | Candidate allocated bytes | Allocation ratio | Combined retained payload high watermark |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| KINX 2026-05-04 | 9_899.03 | 9_295.43 | 0.939x | 2_059_252_720 | 2_202_500_688 | 1.070x | 48_342_240 |
| KTLX 2026-05-05 run 1 | 10_732.39 | 10_036.17 | 0.935x | 2_343_100_296 | 2_580_864_640 | 1.101x | 52_676_640 |
| KTLX 2026-05-05 run 2 | 10_744.80 | 10_215.22 | 0.951x | 2_342_863_632 | 2_576_188_440 | 1.100x | 52_676_640 |
| KTLX 2026-05-05 average | 10_738.60 | 10_125.70 | 0.943x | 2_342_981_964 | 2_578_526_540 | 1.101x | 52_676_640 |
| mixed cache | 77_036.11 | 69_853.68 | 0.907x | 3_803_946_448 | 4_041_337_744 | 1.062x | 54_413_280 |

Broader row correctness and cleanup:

```text
KINX 2026-05-04 validation: succeeded
KINX 2026-05-04 validation checksum: 1_465_969_045_420_103_918
KTLX 2026-05-05 validation: succeeded
KTLX 2026-05-05 validation checksum: 11_084_221_590_146_245_827
mixed-cache validation: succeeded
mixed-cache validation checksum: 615_051_108_812_661_629
failed migrations: 0 across captured broader rows
retained payload failed releases: 0 across captured rollout-default rows
provider overlap failed releases: 0 across captured rollout-default rows
current pending retained payload bytes at completion: 0 across rollout-default
  rows
current active retained payload bytes at completion: 0 across rollout-default
  rows
current combined retained payload bytes at completion: 0 across rollout-default
  rows
```

Pressure budget:

```text
budget: 536_870_912 bytes
primary KTLX 2026-05-04 high watermark: 48_257_280 bytes, 8.99%
KINX 2026-05-04 high watermark: 48_342_240 bytes, 9.00%
KTLX 2026-05-05 high watermark: 52_676_640 bytes, 9.81%
mixed-cache high watermark: 54_413_280 bytes, 10.14%
```

Mixed-cache note:

```text
worker failed batches: 221 in both borrowed and rollout-default mixed-cache
  rows
worker failed items: 881 in both borrowed and rollout-default mixed-cache rows
validation still succeeded and the counters are not candidate-specific
```

## Interpretation

Correctness:

```text
accepted
all captured rows reported validation: succeeded
stable totals and validation checksums matched by shape
failed migrations remained 0
```

Cleanup and release health:

```text
accepted
release failures remained 0
current pending, active, and combined retained pressure returned to 0
```

Pressure:

```text
accepted
maximum captured combined retained payload high watermark was 54_413_280 bytes
against a 536_870_912 byte budget, or 10.14%
```

Timing:

```text
accepted
primary matrix candidate elapsed ratio was 0.911x borrowed
primary candidate spread was 5.41%, below the 7.50% threshold
broader rows also stayed faster than borrowed
```

Allocation:

```text
accepted for primary matrix, KINX, and mixed cache
warning for KTLX 2026-05-05
primary matrix allocation ratio was 1.071x borrowed
KINX allocation ratio was 1.070x borrowed
mixed-cache allocation ratio was 1.062x borrowed
KTLX 2026-05-05 two-run average allocation ratio was 1.1005x borrowed
KTLX 2026-05-05 run 1 was above threshold at 1.101x borrowed
KTLX 2026-05-05 run 2 was below threshold at 1.0996x borrowed
```

Attribution:

```text
accepted with follow-up signal
retained owned snapshot allocation is the clearest incremental category
non-owned callback allocation is the largest candidate-side bucket
the borderline KTLX 2026-05-05 allocation result is consistent with retained
  owned snapshot bytes being enough to tip a shape near the 1.10x threshold
```

Provenance and fallback:

```text
accepted
borrowed rows used explicit --provider blocking-borrowed
rollout-default rows used omitted provider flags
rollout-default rows reported Provider rollout default expansion: yes
rollout-default rows reported Provider fallback contour: no
borrowed rows reported Provider fallback contour: yes
```

## Gate Conclusion

The broader natural Release gate is captured. The scoped default remains
correct, fail-closed, pressure-safe, and faster than borrowed across the
captured shapes. The primary matrix passes all configured timing, spread, and
allocation thresholds.

The gate is not a clean green result because the broader KTLX 2026-05-05
allocation measurement sits directly on the 1.10x threshold: one row exceeded
it and the two-row average was 1.1005x. The stability decision trace should
decide whether that borderline allocation signal blocks the next expansion,
requires another allocation-focused follow-up, or is acceptable because the
primary matrix and mixed-cache row remained under threshold with clear
attribution.
