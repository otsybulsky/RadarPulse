# Milestone 012 Performance Gate

Date: 2026-05-21

Scope: natural Release gate capture for the milestone 012 queued-owned default
rollout. This document records performance and safety evidence for the scoped
`processing benchmark rebalance-archive` CLI default change. It does not close
the milestone or replace the decision trace.

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

## Verification Before Capture

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Recorded result:

```text
Release build succeeded with 0 warnings and 0 errors.
```

Focused regression from slice 8 remained the precondition for this gate:

```text
25 passed, 0 failed, 0 skipped for focused CLI rebalance benchmark coverage.
22 passed, 0 failed, 0 skipped for focused readiness gate and overlap runner
coverage.
24 passed, 0 failed, 0 skipped for focused failure and cleanup coverage.
Release build succeeded with 0 warnings and 0 errors.
```

## Local Data Availability

The local cache contains these radar/date shapes:

```text
2026-05-04/KINX: 462 files
2026-05-04/KTLX: 244 files
2026-05-05/KTLX: 848 files
```

Measured contours:

```text
primary contour:
  --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220

mixed-cache contour:
  --cache data\nexrad --max-files 1000000
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

Borrowed reference adds:

```text
--provider blocking-borrowed
--execution async
--workers 4
```

Rollout default candidate omits provider, execution, worker, queue, retained
budget, and telemetry flags. The CLI expands those omitted flags to the
rollout default contour.

## Primary Natural Matrix

All rows used the primary KTLX contour.

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

| Run | Borrowed elapsed ms | Candidate elapsed ms | Candidate delta ms | Candidate delta | Borrowed allocated bytes | Candidate allocated bytes |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 19_763.98 | 15_031.10 | -4_732.88 | -23.95% | 1_985_290_656 | 2_115_442_512 |
| 2 | 16_797.47 | 15_394.48 | -1_402.99 | -8.35% | 1_972_689_848 | 2_119_908_128 |
| 3 | 17_034.03 | 15_396.90 | -1_637.13 | -9.61% | 1_971_379_744 | 2_118_187_424 |

Primary contour summary:

```text
borrowed average elapsed ms: 17_865.16
candidate average elapsed ms: 15_274.16
candidate average delta ms: -2_591.00
candidate average delta: -14.50%
candidate elapsed ratio to borrowed: 0.855x
borrowed run spread: 2_966.51 ms, 16.61% of average
candidate run spread: 365.80 ms, 2.39% of average
borrowed average allocated bytes: 1_976_453_416
candidate average allocated bytes: 2_117_846_021
candidate allocation ratio: 1.072x borrowed
```

Primary candidate retained-resource telemetry was stable across all three
rows:

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
provider queue retained payload bytes high watermark: 48_257_280
provider queue combined retained payload bytes high watermark: 48_257_280
provider overlap combined retained payload bytes high watermark: 48_257_280
retained-byte budget: 536_870_912
high watermark / budget: 8.99%
retained payload attempts: 198
retained payload batches: 198
retained payload bytes: 9_537_763_200
retained payload values: 7_660_888_320
retained payload allocated bytes: 125_880_560
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

## Mixed-Cache Natural Row

The mixed-cache row used all available local radar/date shapes:

```text
examined files: 1_554
skipped files: 726
published files: 828
payload values: 32_306_203_200
raw value checksum: 958_518_408_830
topology versions: 2
rebalance evaluations: 607
accepted moves: 2
skipped decisions: 1_210
failed migrations: 0
validation: succeeded
validation checksum: 615_051_108_812_661_629
skipped reason counters: no-hot-shard=1_194, no-cold-target-shard=4,
  source-shard-move-budget-exhausted=12, global-move-budget-exhausted=12
```

| Contour | End-to-end ms | Callback ms | Replay/build ms | Allocated bytes | Allocated bytes / payload value |
| --- | ---: | ---: | ---: | ---: | ---: |
| blocking-borrowed async | 77_542.34 | 9_336.95 | 68_205.39 | 3_804_238_496 | 0.12 |
| omitted-provider rollout default | 60_229.87 | 12_342.37 | 47_887.51 | 4_049_279_144 | 0.13 |

Mixed-cache summary:

```text
candidate delta ms: -17_312.47
candidate delta: -22.33%
candidate elapsed ratio to borrowed: 0.777x
candidate allocation ratio: 1.064x borrowed
```

Mixed-cache candidate retained-resource telemetry:

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
provider queue retained payload bytes high watermark: 54_413_280
provider queue combined retained payload bytes high watermark: 54_413_280
provider overlap combined retained payload bytes high watermark: 54_413_280
retained-byte budget: 536_870_912
high watermark / budget: 10.14%
retained payload attempts: 828
retained payload batches: 828
retained payload bytes: 40_232_201_280
retained payload values: 32_306_203_200
retained payload allocated bytes: 237_191_544
retained payload release attempts: 828
retained payload released batches: 828
retained payload failed releases: 0
provider overlap failed releases: 0
current pending retained batches at completion: 0
current pending retained payload bytes at completion: 0
current active retained batches at completion: 0
current active retained payload bytes at completion: 0
current combined retained batches at completion: 0
current combined retained payload bytes at completion: 0
```

## Gate Interpretation

Correctness parity: passed for every captured natural row. Published file
count, payload values, raw checksum, topology count, accepted moves, skipped
decisions, failed migrations, validation status, validation checksum, and
skipped reason counters matched the same-run borrowed reference for each input
contour.

Default expansion evidence: passed. Candidate rows omitted provider, execution,
worker, queue, retained budget, and telemetry flags. Output reported
`Provider mode source: rollout-default`, `Provider rollout default expansion:
yes`, `Provider default rollout contour: yes`, and `Provider fallback contour:
no`.

Fallback separation: passed. Borrowed rows used explicit
`--provider blocking-borrowed`, reported `Provider fallback contour: yes`, and
did not emit queued-owned retained-resource telemetry.

Release health: passed. Retained payload failed releases and provider overlap
failed releases stayed at 0 on every natural candidate row.

Retained-resource cleanup: passed. Current pending, active, and combined
retained batch counts and payload bytes returned to 0 at completion on every
natural candidate row.

Retained-resource pressure: passed. The primary combined retained payload
high-water mark was 48_257_280 bytes, about 8.99% of the 536_870_912 byte
budget. The mixed-cache high-water mark was 54_413_280 bytes, about 10.14% of
the budget.

Allocation threshold: passed. The primary candidate averaged 1.072x borrowed,
below the 1.10x rollout threshold. The mixed-cache candidate measured 1.064x
borrowed.

Elapsed threshold: passed. The primary candidate averaged 0.855x borrowed,
below the <= 1.00x rollout threshold. The mixed-cache candidate measured
0.777x borrowed.

Run variance threshold: passed for the primary candidate matrix. Candidate
spread was 365.80 ms, 2.39% of the candidate average, below the 7.50%
threshold. Borrowed spread was higher at 16.61% of its average, but the
threshold applies to candidate repeated natural spread.

Natural evidence separation: passed. Natural rows used
`Provider overlap consumer delay ms: 0.00` and were labeled
`natural-default-candidate` / `natural-readiness`. Controlled proof rows were
not included in this natural gate matrix.

Natural queue backlog: not accumulated. Producer/consumer lifetime overlap is
present while queue depth high watermark remains 1 and queued-ahead overlap is
`no`. This is favorable for retained pressure and does not contradict the
separate controlled-delay proof of queued-ahead mechanics.

## Gate Result

The natural rollout performance gate passes for the measured local contours:

```text
validation parity: pass
release failures: pass
cleanup at completion: pass
retained pressure budget: pass
allocation ratio: pass
elapsed ratio: pass
candidate spread: pass
default expansion evidence: pass
fallback separation: pass
```

The decision trace should use this gate as evidence that the scoped CLI default
can move to omitted-provider queued-owned rollout behavior while preserving
explicit blocking-borrowed as the fallback and same-run oracle.
