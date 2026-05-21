# Milestone 015 Performance Gate

Date: 2026-05-21

Scope: direct API Release allocation-readiness gate for the milestone 015
queued-owned direct/default allocation work. This document records natural
Release evidence for direct
`RadarProcessingArchiveRebalanceBenchmark.MeasureCache()` and
`MeasureFile()` omitted-provider defaults against same-run explicit
`BlockingBorrowed` oracle rows.

This document captures the gate evidence. It does not close milestone 015 or
replace the allocation-readiness decision trace.

Gate status:

```text
ready with file-level allocation warning
```

Summary:

```text
direct default contour: queued-owned rollout contour
explicit borrowed oracle: preserved and separated
correctness parity: pass across captured rows
release failures: 0 across captured direct default rows
current retained pressure at completion: 0 across captured direct default rows
combined retained pressure: under 536870912 byte budget across captured rows
primary elapsed ratio: pass, 0.889x borrowed over three captured pairs
primary default timing spread: pass, 1.10%
primary allocation ratio: pass, 1.042x borrowed
KTLX 2026-05-05 allocation ratio: pass, 1.0392x borrowed average
KTLX 2026-05-05 allocation rows: 1.0404x and 1.0381x borrowed
KINX 2026-05-04 allocation ratio: pass, 1.042x borrowed
mixed-cache allocation ratio: pass, 1.021x borrowed
file-level cold smoke allocation ratio: warning, 1.512x borrowed
file-level cold smoke elapsed ratio: warning, 1.072x borrowed
explicit rollout spot-check: matches shared rollout contour
```

The milestone 014 KTLX 2026-05-05 allocation warning is reduced and bounded
below the 1.10x threshold on both repeated rows. The gate is still not a
clean unrestricted expansion signal because the single-file cold smoke shows
the expected cost of one unamortized retained snapshot copy. This is not a
JIT warmup finding; it is the current queued-owned ownership model paying for
the first retained event-array and byte-array snapshot. The decision trace
must scope the readiness decision to the next named surface: cache-level
readiness can proceed with this warning, while file-level default
latency/allocation would need its own decision or optimization target.

## Contours

Direct omitted-default contour:

```text
API: RadarProcessingArchiveRebalanceBenchmark.MeasureCache()/MeasureFile()
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
API: RadarProcessingArchiveRebalanceBenchmark.MeasureCache()/MeasureFile()
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

The gate used a temporary local harness that called `MeasureCache()` and
`MeasureFile()` directly. The harness was not a committed product surface.
Default rows called the direct API overloads with provider-related arguments
omitted; borrowed rows supplied `providerMode: BlockingBorrowed` explicitly.

## Verification Before Capture

Focused regression from slice 7 remained the precondition for this gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"

112 passed, 0 failed, 0 skipped
```

Release build before measurements:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Recorded result:

```text
Release build succeeded with 0 warnings and 0 errors.
```

The temporary direct API gate runner also built in Release with 0 warnings and
0 errors before measurement capture.

## Local Data Availability

The local cache contained these file counts at gate capture time:

```text
data\nexrad\level2\2026\05\04\KTLX: 244 files
data\nexrad\level2\2026\05\04\KINX: 462 files
data\nexrad\level2\2026\05\05\KTLX: 848 files
data\nexrad total files: 1554
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

file-level smoke contour:
  data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
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
| 1 | 17_124.11 | 15_229.47 | -1_894.64 | 0.889x | 1_975_861_360 | 2_058_556_376 | 1.042x |
| 2 | 16_924.53 | 15_126.26 | -1_798.28 | 0.894x | 1_973_757_592 | 2_058_272_272 | 1.043x |
| 3 | 17_029.58 | 15_062.53 | -1_967.04 | 0.884x | 1_976_897_672 | 2_060_386_880 | 1.042x |

Primary contour summary:

```text
borrowed average elapsed ms: 17_026.07
direct default average elapsed ms: 15_139.42
direct default average delta ms: -1_886.65
direct default elapsed ratio to borrowed: 0.889x
borrowed average allocated bytes: 1_975_505_541
direct default average allocated bytes: 2_059_071_843
direct default allocation ratio: 1.042x borrowed
direct default timing spread: 166.94 ms
direct default timing spread / average: 1.10%
```

Primary direct-default retained-resource telemetry was stable across all three
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
provider overlap has producer-consumer overlap: yes
provider overlap has queued-ahead overlap: no
combined retained payload bytes high watermark: 48_257_280
high watermark / budget: 8.99%
retained payload allocated bytes: 69_252_024
retained pool rents/returns/misses: 396 / 396 / 2
retained event array pool rents/returns/misses: 198 / 198 / 1
retained byte array pool rents/returns/misses: 198 / 198 / 1
retained payload failed releases: 0
provider overlap failed releases: 0
current pending retained payload bytes at completion: 0
current active retained payload bytes at completion: 0
current combined retained payload bytes at completion: 0
direct default matches shared rollout contour: yes
```

Primary allocation attribution:

```text
borrowed average processing callback allocated bytes: 262_923_824
borrowed average replay and batch construction allocated bytes:
  1_712_581_717

direct default average processing callback allocated bytes: 1_840_466_747
direct default average replay and batch construction allocated bytes:
  218_605_096
direct default average owned snapshot allocated bytes: 69_252_024
direct default average processing callback non-owned snapshot bytes:
  1_771_214_723
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
elapsed ms: 15_128.65
allocated bytes: 2_060_470_688
retained event array pool rents/returns/misses: 198 / 198 / 1
retained byte array pool rents/returns/misses: 198 / 198 / 1
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
| KTLX 2026-05-05 run 1 | 9_816.18 | 9_393.43 | 0.957x | 2_337_600_040 | 2_431_951_440 | 1.040x | 52_676_640 |
| KTLX 2026-05-05 run 2 | 10_023.66 | 9_306.89 | 0.928x | 2_343_470_720 | 2_432_722_392 | 1.038x | 52_676_640 |
| KTLX 2026-05-05 average | 9_919.92 | 9_350.16 | 0.943x | 2_340_535_380 | 2_432_336_916 | 1.0392x | 52_676_640 |
| KINX 2026-05-04 | 9_129.10 | 8_210.00 | 0.899x | 2_061_413_304 | 2_148_539_496 | 1.042x | 48_342_240 |
| mixed cache | 69_973.41 | 60_961.04 | 0.871x | 3_806_813_096 | 3_887_267_272 | 1.021x | 54_413_280 |

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

Retained pool telemetry:

```text
KTLX 2026-05-05 retained allocated bytes: 73_424_544
KTLX 2026-05-05 retained pool rents/returns/misses: 208 / 208 / 3
KTLX 2026-05-05 event pool rents/returns/misses: 104 / 104 / 2
KTLX 2026-05-05 byte pool rents/returns/misses: 104 / 104 / 1

KINX 2026-05-04 retained allocated bytes: 69_229_056
KINX 2026-05-04 retained pool rents/returns/misses: 198 / 198 / 2
KINX 2026-05-04 event pool rents/returns/misses: 99 / 99 / 1
KINX 2026-05-04 byte pool rents/returns/misses: 99 / 99 / 1

mixed-cache retained allocated bytes: 73_592_512
mixed-cache retained pool rents/returns/misses: 1656 / 1656 / 3
mixed-cache event pool rents/returns/misses: 828 / 828 / 2
mixed-cache byte pool rents/returns/misses: 828 / 828 / 1
```

Mixed-cache note:

```text
worker failed batches: 221 in both borrowed and direct default mixed-cache rows
worker failed items: 881 in both borrowed and direct default mixed-cache rows
validation still succeeded and the counters are not direct-default-specific
```

## File-Level Smoke

The file-level smoke used one representative KTLX Archive Two file:

```text
data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
```

Shared output:

```text
batches: 1
events: 32_400
payload bytes: 48_257_280
payload values: 38_759_040
raw value checksum: 1_063_626_011
topology versions: 2
rebalance evaluations: 1
accepted moves: 1
failed migrations: 0
validation: succeeded
validation checksum: 3_750_039_633_875_006_276
```

| Shape | Borrowed elapsed ms | Direct default elapsed ms | Elapsed ratio | Borrowed allocated bytes | Direct default allocated bytes | Allocation ratio | Combined retained payload high watermark |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| KTLX single file cold smoke | 378.26 | 405.33 | 1.072x | 135_077_072 | 204_292_296 | 1.512x | 48_257_280 |

Direct-default retained-resource telemetry:

```text
retained allocated bytes: 69_206_320
retained pool rents/returns/misses: 2 / 2 / 2
retained event array pool rents/returns/misses: 1 / 1 / 1
retained byte array pool rents/returns/misses: 1 / 1 / 1
retained payload failed releases: 0
provider overlap failed releases: 0
current pending retained payload bytes at completion: 0
current active retained payload bytes at completion: 0
current combined retained payload bytes at completion: 0
direct default matches shared rollout contour: yes
```

Interpretation:

```text
the single-file cold smoke is a real allocation warning
the warning is dominated by the first retained event-array and byte-array
  pooled-copy allocation for a single retained batch
this is the expected cold retained-ownership price for queued-owned
  pooled-copy in the current architecture, not a JIT warmup artifact
cache-level contours amortize that cold retained representation cost across
  many retained batches; the file-level smoke does not
the warning does not block cache-level allocation readiness because the
  measured cache contours pass with the cold retained cost amortized
the decision trace must avoid turning cache readiness into an unrestricted
  file-level allocation claim
if the next named surface is file-level default latency/allocation, this
  single-file cold retained snapshot cost becomes a separate blocker or
  optimization target
prewarming or sharing pools across direct calls would be a contract decision,
  not a way to hide allocation outside the measured window
```

## Before/After Movement

The valid comparison baseline is the milestone 014 direct API Release gate.

| Shape | Milestone 014 allocation ratio | Milestone 015 allocation ratio | Movement |
| --- | ---: | ---: | ---: |
| Primary KTLX 2026-05-04 | 1.071x | 1.042x | -0.029x |
| KTLX 2026-05-05 average | 1.0997x | 1.0392x | -0.0605x |
| KINX 2026-05-04 | 1.069x | 1.042x | -0.027x |
| Mixed cache | 1.066x | 1.021x | -0.045x |

KTLX 2026-05-05 movement:

```text
milestone 014 row 1: 1.1018x borrowed
milestone 014 row 2: 1.0976x borrowed
milestone 014 average: 1.0997x borrowed

milestone 015 row 1: 1.0404x borrowed
milestone 015 row 2: 1.0381x borrowed
milestone 015 average: 1.0392x borrowed
```

Interpretation:

```text
the named KTLX 2026-05-05 allocation warning moved materially away from the
1.10x threshold
both repeated KTLX 2026-05-05 rows are below the threshold
the retained event-array pool is the dominant accepted allocation improvement
visible in this gate
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
all retained event-array and byte-array pool rents were returned
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
accepted for cache contours
primary direct default average elapsed ratio: 0.889x borrowed
KTLX 2026-05-05 direct default average elapsed ratio: 0.943x borrowed
KINX 2026-05-04 direct default elapsed ratio: 0.899x borrowed
mixed-cache direct default elapsed ratio: 0.871x borrowed
file-level cold smoke elapsed ratio: warning, 1.072x borrowed
```

Primary repeated-run spread:

```text
accepted
threshold: direct default spread <= 7.50% of average
captured primary direct default spread: 1.10%
```

Allocation:

```text
accepted for cache contours
primary allocation ratio: 1.042x borrowed
KTLX 2026-05-05 allocation ratio: 1.0392x borrowed average
KTLX 2026-05-05 row 1: 1.0404x borrowed
KTLX 2026-05-05 row 2: 1.0381x borrowed
KINX 2026-05-04 allocation ratio: 1.042x borrowed
mixed-cache allocation ratio: 1.021x borrowed
file-level cold smoke allocation ratio: warning, 1.512x borrowed
```

The KTLX 2026-05-05 result no longer sits on the 1.10x threshold. The
single-file cold smoke remains a named warning because the retained snapshot
copy is not amortized when one file produces one retained batch. For the
current queued-owned pooled-copy architecture, that cold cost is expected and
necessary unless the product explicitly adds a prewarm/shared-pool contract,
a file-only fast path, or a different file-level default posture.

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
retained pooled-copy allocation is now bounded mainly to cold event-array and
byte-array pool misses
cache-level rows show low retained pool miss counts with all rents returned
processing callback allocation remains the largest candidate-side bucket
because direct default shifts retained/owned work into the callback path
the file-level smoke isolates the cost of one unamortized retained snapshot
```

Optimization posture:

```text
accepted standard optimizations:
  direct bounded recent-detail copying
  direct provider queue recent-detail summary copy
  static not-required retained resource release delegates

accepted experimental optimizations:
  explicit pooled retained payload release owner
  dedicated retained RadarStreamEvent[] pool with split event/byte telemetry

deferred experiments:
  pooled telemetry accumulator
  struct-backed queued work item
  additional source-local allocation probe split

rejected approaches:
  unsafe memory or stack-lifetime tricks
  hiding allocation by moving cost out of the measured window
  tuning retained byte-pool capacity from insufficient evidence
  carrying the wait-mode synchronous enqueue fast path into the gate
```

Workload coverage limits:

```text
limited to local NEXRAD cache shapes available on 2026-05-21
direct gate captured natural rows with overlap consumer delay 0
controlled overlap consumer delay rows were not part of this gate
single-file smoke is captured as warning, not as proof of cache readiness
live ingestion/runtime provider defaults, durable queues, cross-process
workers, ordered concurrent rebalance, and builder-transfer remain out of
scope
```

## Gate Result

The direct API Release allocation-readiness gate is captured:

```text
direct MeasureCache()/MeasureFile() omitted defaults resolved to the
queued-owned rollout contour
explicit BlockingBorrowed remained a separate oracle
correctness, validation, cleanup, release health, retained pressure, and
cache-level elapsed timing passed the captured natural rows
KTLX 2026-05-05 allocation warning was materially reduced and both repeated
rows are below the 1.10x threshold
primary repeated timing spread passed
cache-level allocation readiness is supported for the measured local contours
single-file cold allocation remains a warning and should scope the milestone
015 decision trace
```
