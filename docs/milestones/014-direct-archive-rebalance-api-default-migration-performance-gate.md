# Milestone 014 Performance Gate

Date: 2026-05-21

Scope: direct API Release gate capture for the milestone 014 direct archive
rebalance API default migration. This document records natural Release
evidence for direct
`RadarProcessingArchiveRebalanceBenchmark.MeasureCache()` omitted-provider
defaults against same-run explicit blocking-borrowed oracle rows.

This document captures the gate evidence. It does not close milestone 014 or
replace the direct API migration decision trace.

Gate status:

```text
captured with allocation warning
```

Summary:

```text
direct default contour: queued-owned rollout contour
explicit borrowed oracle: preserved and separated
correctness parity: pass across captured rows
release failures: 0 across captured direct default rows
current retained pressure at completion: 0 across captured direct default rows
combined retained pressure: under 536870912 byte budget across captured rows
primary elapsed ratio: pass, 0.911x borrowed over four captured pairs
primary default timing spread: favorable outlier note
  all four direct default rows: 10.41% spread
  stabilized rows 2-4: 0.39% spread
primary allocation ratio: pass, 1.071x borrowed
KINX 2026-05-04 allocation ratio: pass, 1.069x borrowed
mixed-cache allocation ratio: pass, 1.066x borrowed
KTLX 2026-05-05 allocation ratio: warning, two-row average 1.0997x
  borrowed with one row above and one row below the 1.10x threshold
decision trace must keep the KTLX 2026-05-05 allocation result visible rather
  than treating the gate as clean green
```

## Contours

Direct omitted-default contour:

```text
API: RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
provider argument: omitted
execution argument: omitted
async execution argument: omitted
provider queue capacity argument: omitted
provider overlap argument: omitted
retention strategy argument: omitted
retained-byte budget argument: omitted
provider: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async shard transport
workers: 4
worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536870912
overlap consumer delay: 0
```

Borrowed oracle contour:

```text
API: RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
provider: explicit BlockingBorrowed
execution: explicit AsyncShardTransport
workers: 4
worker queue capacity: 1
provider queue capacity: not applicable
provider overlap: none
retention strategy: snapshot-copy
retained-byte budget: none
overlap consumer delay: 0
```

Explicit rollout spot-check contour:

```text
API: RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
provider: explicit QueuedOwned
provider overlap: explicit ProducerConsumer
retention strategy: explicit PooledCopy
execution: explicit AsyncShardTransport
workers: explicit 4
worker queue capacity: explicit 8
provider queue capacity: explicit 8
retained-byte budget: explicit 536870912
overlap consumer delay: explicit 0
```

Common direct API parameters:

```text
mode: RebalanceSession
iterations: 1
warmup iterations: 0
parallelism: 24
partitions: 24
shards: 4
validation profile: Diagnostic
decompressor: default RadarPulse decompressor
```

The gate used a temporary local harness that called `MeasureCache()` directly.
The harness was not a committed product surface. It existed only to avoid using
the CLI parser as the proof of direct omitted-argument behavior.

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
direct MeasureFile()/MeasureCache() default migration tests passed
explicit blocking-borrowed fallback/oracle tests passed
explicit queued-owned equivalence tests passed
CLI help and rollout contour alignment tests passed
queued-owned failure, cancellation, cleanup, and fallback tests passed
readiness threshold interpretation tests passed
allocation summary attribution tests passed

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"

84 passed, 0 failed, 0 skipped
```

## Local Data Availability

The local cache contained these file counts at gate capture time:

```text
data\nexrad\level2\2026\05\04\KTLX: 244 files
data\nexrad\level2\2026\05\04\KINX: 462 files
data\nexrad\level2\2026\05\05\KTLX: 848 files
data\nexrad total files: 1554
data\nexrad non-metadata files: 899
```

Captured shapes:

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

## Primary Natural Matrix

The primary matrix used KTLX on 2026-05-04 with `--max-files 220`.

Shared output:

```text
examined files: 220
skipped files: 22
published files: 198
batches: 198
events: 6_401_760
payload bytes: 9_537_763_200
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

| Run | Borrowed elapsed ms | Direct default elapsed ms | Direct delta ms | Elapsed ratio | Borrowed allocated bytes | Direct default allocated bytes | Allocation ratio |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 17_208.22 | 15_325.49 | -1_882.73 | 0.891x | 1_978_992_272 | 2_115_958_968 | 1.069x |
| 2 | 18_482.91 | 16_986.55 | -1_496.36 | 0.919x | 1_975_897_344 | 2_114_139_608 | 1.070x |
| 3 | 18_356.67 | 17_012.71 | -1_343.96 | 0.927x | 1_974_791_536 | 2_116_573_160 | 1.072x |
| 4 | 18_828.84 | 17_053.24 | -1_775.60 | 0.906x | 1_972_823_992 | 2_118_114_904 | 1.074x |

Primary contour summary:

```text
borrowed average elapsed ms: 18_219.16
direct default average elapsed ms: 16_594.50
direct default average delta ms: -1_624.66
direct default elapsed ratio to borrowed: 0.911x
borrowed average allocated bytes: 1_975_626_286
direct default average allocated bytes: 2_116_196_660
direct default allocation ratio: 1.071x borrowed
```

Primary timing spread:

```text
all four direct default rows:
  spread: 1_727.75 ms
  spread / average: 10.41%

stabilized direct default rows 2-4:
  spread: 66.69 ms
  spread / average: 0.39%
```

The first direct default row was a favorable timing outlier. It weakens a
clean variance claim, but it is not a slowdown regression: every captured
direct default row was faster than its same-run borrowed oracle row. The
decision trace should not overstate the primary timing evidence as perfectly
clean; it should state that the stabilized triplet passed while the all-row
spread includes a favorable outlier.

Primary direct-default retained-resource telemetry was stable across all four
rows:

```text
provider mode: queued-owned
execution mode: async shard transport
provider queue capacity: 8
provider overlap: producer-consumer
retention strategy: pooled-copy
retained-byte budget: 536_870_912
overlap consumer delay ms: 0
worker count: 4
worker queue capacity: 8
provider queue depth high watermark: 1
provider overlap queue depth high watermark: 1
provider overlap has producer-consumer overlap: yes
provider overlap has queued-ahead overlap: no
combined retained payload bytes high watermark: 48_257_280
high watermark / budget: 8.99%
retained payload attempts: 198
retained payload batches: 198
retained payload bytes: 9_537_763_200
retained payload values: 7_660_888_320
retained payload release attempts: 198
retained payload released batches: 198
retained payload failed releases: 0
provider overlap failed releases: 0
current pending retained payload bytes at completion: 0
current active retained payload bytes at completion: 0
current combined retained payload bytes at completion: 0
direct default matches shared rollout contour: yes
```

Primary allocation attribution:

```text
borrowed average processing callback allocated bytes: 262_906_694
borrowed average replay and batch construction allocated bytes: 1_712_719_592

direct default average processing callback allocated bytes: 1_841_523_536
direct default average replay and batch construction allocated bytes: 274_673_124
direct default average owned snapshot allocated bytes: 125_881_832
direct default average processing callback non-owned snapshot bytes:
  1_715_641_704
```

## Explicit Rollout Spot-Check

The explicit queued-owned rollout spot-check used the primary KTLX 2026-05-04
shape and supplied every rollout control argument explicitly.

Result:

```text
provider mode: queued-owned
execution mode: async shard transport
provider queue capacity: 8
provider overlap: producer-consumer
retention strategy: pooled-copy
retained-byte budget: 536_870_912
overlap consumer delay ms: 0
worker count: 4
worker queue capacity: 8
direct default matches shared rollout contour: yes
validation: succeeded
validation checksum: 7_480_064_646_096_449_000
published files: 198
payload values: 7_660_888_320
raw value checksum: 245_554_417_487
topology versions: 2
accepted moves: 2
skipped decisions: 392
failed migrations: 0
combined retained payload bytes high watermark: 48_257_280
current combined retained payload bytes at completion: 0
retained payload failed releases: 0
provider overlap failed releases: 0
elapsed ms: 16_907.68
allocated bytes: 2_116_239_056
```

Interpretation:

```text
direct omitted defaults and explicit queued-owned rollout controls produced
the same effective contour and deterministic output totals on the primary
shape.
```

## Broader Rows

| Shape | Borrowed elapsed ms | Direct default elapsed ms | Elapsed ratio | Borrowed allocated bytes | Direct default allocated bytes | Allocation ratio | Combined retained payload high watermark |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| KTLX 2026-05-05 run 1 | 10_840.74 | 10_510.13 | 0.970x | 2_342_060_840 | 2_580_445_936 | 1.102x | 52_676_640 |
| KTLX 2026-05-05 run 2 | 10_983.00 | 10_436.40 | 0.950x | 2_346_026_248 | 2_575_099_200 | 1.098x | 52_676_640 |
| KTLX 2026-05-05 average | 10_911.87 | 10_473.27 | 0.960x | 2_344_043_544 | 2_577_772_568 | 1.0997x | 52_676_640 |
| KINX 2026-05-04 | 10_140.09 | 9_191.03 | 0.906x | 2_060_823_744 | 2_202_824_808 | 1.069x | 48_342_240 |
| mixed cache | 77_798.24 | 68_273.34 | 0.878x | 3_803_774_048 | 4_053_667_864 | 1.066x | 54_413_280 |

Broader row correctness and cleanup:

```text
KTLX 2026-05-05 validation: succeeded
KTLX 2026-05-05 validation checksum: 11_084_221_590_146_245_827
KINX 2026-05-04 validation: succeeded
KINX 2026-05-04 validation checksum: 1_465_969_045_420_103_918
mixed-cache validation: succeeded
mixed-cache validation checksum: 615_051_108_812_661_629
failed migrations: 0 across captured broader rows
retained payload failed releases: 0 across captured direct default rows
provider overlap failed releases: 0 across captured direct default rows
current pending retained payload bytes at completion: 0 across direct default
  rows
current active retained payload bytes at completion: 0 across direct default
  rows
current combined retained payload bytes at completion: 0 across direct default
  rows
direct default matches shared rollout contour: yes across direct default rows
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
worker failed batches: 221 in both borrowed and direct default mixed-cache rows
validation still succeeded and the counters are not direct-default-specific
```

## Interpretation

Correctness:

```text
accepted
all captured rows reported validation succeeded
stable totals and validation checksums matched by shape
failed migrations remained 0
topology versions, accepted moves, skipped decisions, and skipped reason
  counters matched by shape
```

Release health and cleanup:

```text
accepted
retained payload failed releases: 0 across direct default rows
provider overlap failed releases: 0 across direct default rows
current pending retained pressure at completion: 0 across direct default rows
current active retained pressure at completion: 0 across direct default rows
current combined retained pressure at completion: 0 across direct default rows
```

Retained pressure budget:

```text
accepted
max observed combined retained payload high watermark: 54_413_280 bytes
configured budget: 536_870_912 bytes
max observed budget use: 10.14%
```

Elapsed timing:

```text
accepted
primary direct default average elapsed ratio: 0.911x borrowed
KTLX 2026-05-05 direct default average elapsed ratio: 0.960x borrowed
KINX 2026-05-04 direct default elapsed ratio: 0.906x borrowed
mixed-cache direct default elapsed ratio: 0.878x borrowed
```

Primary repeated-run spread:

```text
captured with favorable outlier note
all-row spread: 10.41%
stabilized rows 2-4 spread: 0.39%
the all-row spread is not a slowdown regression because the outlier was faster
than the stabilized group and every direct default row beat same-run borrowed
```

Allocation:

```text
captured with KTLX 2026-05-05 warning
primary allocation ratio: 1.071x borrowed
KINX 2026-05-04 allocation ratio: 1.069x borrowed
mixed-cache allocation ratio: 1.066x borrowed
KTLX 2026-05-05 allocation ratio: 1.0997x borrowed average
KTLX 2026-05-05 row 1: 1.1018x borrowed
KTLX 2026-05-05 row 2: 1.0976x borrowed
```

The KTLX 2026-05-05 result repeats the known milestone 013 allocation warning:
one row is above the 1.10x threshold and one row is below it. The average is
slightly below 1.10x in this capture, but the decision trace should still treat
the shape as a warning because the threshold tension is still present.

Direct default versus explicit queued-owned contour:

```text
accepted
direct default rows resolved to queued-owned, producer-consumer, pooled-copy,
async shard transport, workers 4, worker queue capacity 8, provider queue
capacity 8, retained-byte budget 536870912, and overlap consumer delay 0
the explicit rollout spot-check produced the same effective contour and
deterministic output totals on the primary shape
```

Explicit borrowed fallback/oracle separation:

```text
accepted
borrowed rows were selected by explicit providerMode: BlockingBorrowed
borrowed rows used async shard transport workers 4 with worker queue capacity 1
borrowed rows reported no provider queue, retention, overlap, or retained
pressure telemetry
direct default queued-owned rows reported queue, retention, overlap, worker,
and retained-pressure telemetry
no row used automatic fallback from direct default queued-owned to borrowed
```

Allocation attribution:

```text
accepted as explanatory evidence
the direct default allocation increase is concentrated in processing callback
allocation and retained/owned snapshot work, not in replay and batch
construction
the attribution matches the known queued-owned retained-payload cost profile
```

Workload coverage limits:

```text
limited to local NEXRAD cache shapes available on 2026-05-21
direct gate captured MeasureCache() natural rows; MeasureFile() direct default
behavior remains covered by focused regression tests from slices 3, 5, and 7
controlled overlap consumer delay rows were not part of this gate
live ingestion/runtime provider defaults, durable queues, cross-process
workers, ordered concurrent rebalance, and builder-transfer remain out of
scope
```

## Gate Result

The direct API Release gate is captured with the known allocation warning:

```text
direct MeasureCache() omitted defaults resolved to the queued-owned rollout
contour
explicit BlockingBorrowed remained a separate oracle
correctness, validation, cleanup, release health, retained pressure, and
elapsed timing passed the captured natural rows
KTLX 2026-05-05 allocation remains a visible warning and must be interpreted
in the milestone 014 decision trace
primary all-row timing spread includes a favorable outlier; stabilized rows
passed the spread threshold, but the decision trace should not overstate the
variance evidence as perfectly clean
```
