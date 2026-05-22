# Milestone 016: Broader Cache-Level Default Readiness Performance Gate

Status: captured with primary spread warning.

This document records the milestone 016 broader cache-level Release gate
evidence. It is not the final readiness decision. The decision trace must
interpret these rows before closeout.

## Gate Purpose

Milestone 016 asks whether the queued-owned direct/default contour remains
ready as the broader cache-level benchmark/default posture for the available
local cache workloads.

The accepted direct/default contour under test remained unchanged:

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
```

Same-run explicit `BlockingBorrowed` rows were retained as the oracle for every
cache-level readiness row. Candidate rows omitted provider-related direct API
arguments so the rollout defaults were exercised naturally.

## Preconditions

```text
corpus inventory: complete
gate matrix selected before measurement interpretation: yes
focused regression passed before gate capture: yes
thresholds recorded before gate capture: yes
controlled consumer delay disabled: yes
same-run BlockingBorrowed oracle rows included: yes
product runtime behavior changes in milestone 016 before gate: none
```

Focused regression before gate capture:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"
```

Result:

```text
112 passed, 0 failed, 0 skipped
```

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Result:

```text
succeeded, 0 warnings, 0 errors
```

Temporary direct API gate runner build:

```powershell
dotnet build data\temp\m016-gate-runner\M016GateRunner.csproj -c Release --no-restore
```

Result:

```text
succeeded, 0 warnings, 0 errors
```

The temporary runner lived under `data\temp\m016-gate-runner`, wrote local
reports under `data\temp\m016-gate-output`, and was not committed as a product
surface.

## Local Corpus

```text
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
```

Representative file smoke path:

```text
data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
```

## Runner Configuration

Cache rows used:

```text
mode: RebalanceSession
iterations: 1
warmup iterations: 0
parallelism: 24
partitions: 24
shards: 4
decompressor: default
overlap consumer delay: 0
validation: diagnostic
```

Borrowed oracle rows used:

```text
providerMode: BlockingBorrowed
executionMode: AsyncShardTransport
asyncExecution: workerCount 4, queueCapacity 1
```

Candidate rows used omitted provider-related arguments so direct rollout
defaults supplied queued-owned provider mode, producer-consumer overlap,
pooled-copy retention, async shard transport, worker count 4, worker queue
capacity 8, provider queue capacity 8, and retained-byte budget 536870912.

## Minimum Gate Rows

| Row | Selector | Pairs | Status | Borrowed avg ms | Candidate avg ms | Elapsed ratio | Borrowed avg allocated | Candidate avg allocated | Allocation ratio |
| --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| primary | `data\nexrad --date 2026-05-04 --radar KTLX --max-files 220` | 3 | warning | 17_251.92 | 15_207.14 | 0.881x | 1_928_004_067 | 1_982_023_304 | 1.028x |
| risk | `data\nexrad --date 2026-05-05 --radar KTLX --max-files 220` | 2 | pass with timing note | 12_235.91 | 10_052.18 | 0.822x | 2_304_219_356 | 2_352_652_460 | 1.021x |
| kinx | `data\nexrad --date 2026-05-04 --radar KINX --max-files 220` | 1 | pass | 11_217.65 | 8_623.90 | 0.769x | 2_062_962_464 | 2_077_248_896 | 1.007x |
| mixed | `data\nexrad --max-files 1000000` | 1 | pass with worker-counter note | 91_455.59 | 79_799.99 | 0.873x | 3_805_371_240 | 3_830_097_280 | 1.006x |

## Optional Coverage Rows

| Row | Selector | Pairs | Status | Borrowed ms | Candidate ms | Elapsed ratio | Borrowed allocated | Candidate allocated | Allocation ratio |
| --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| ktlx-full | `data\nexrad --date 2026-05-04 --radar KTLX --max-files 244` | 1 | pass | 22_944.95 | 20_345.60 | 0.887x | 2_004_129_328 | 2_019_170_160 | 1.008x |
| kinx-440 | `data\nexrad --date 2026-05-04 --radar KINX --max-files 440` | 1 | pass | 23_825.53 | 18_630.13 | 0.782x | 2_208_616_928 | 2_207_652_128 | 1.000x |
| risk-440 | `data\nexrad --date 2026-05-05 --radar KTLX --max-files 440` | 1 | pass | 25_322.43 | 20_522.81 | 0.810x | 2_483_929_616 | 2_503_044_480 | 1.008x |

## File Smoke

The single-file row is warning visibility only. It is not a file-level default
readiness claim.

| Row | Selector | Status | Borrowed ms | Candidate ms | Elapsed ratio | Borrowed allocated | Candidate allocated | Allocation ratio |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| file-smoke | `data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06` | coverage-only | 416.72 | 281.25 | 0.675x | 136_150_208 | 141_680_656 | 1.041x |

This run did not reproduce the milestone 015 single-file cold warning
(`1.512x` allocation and `1.072x` elapsed in milestone 015). The row still
remains coverage-only because one single-file smoke cannot establish file-level
default readiness.

## Repeated-Row Details

Primary KTLX 2026-05-04, 220 files:

| Run | Borrowed ms | Candidate ms | Elapsed ratio | Borrowed allocated | Candidate allocated | Allocation ratio |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 17_140.14 | 14_596.99 | 0.852x | 1_974_835_376 | 1_988_951_640 | 1.007x |
| 2 | 16_489.99 | 14_600.80 | 0.885x | 1_906_037_248 | 1_978_257_208 | 1.038x |
| 3 | 18_125.61 | 16_423.62 | 0.906x | 1_903_139_576 | 1_978_861_064 | 1.040x |

Primary spread:

```text
candidate elapsed spread: 1_826.63 ms
candidate elapsed spread ratio: 12.01%
threshold: 7.50%
interpretation: warning, carry to slice 6
```

KTLX 2026-05-05 named risk row, 220 files:

| Run | Borrowed ms | Candidate ms | Elapsed ratio | Borrowed allocated | Candidate allocated | Allocation ratio |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 14_060.91 | 9_679.33 | 0.688x | 2_338_685_848 | 2_363_491_160 | 1.011x |
| 2 | 10_410.92 | 10_425.02 | 1.001x | 2_269_752_864 | 2_341_813_760 | 1.032x |

Named risk spread:

```text
candidate elapsed spread: 745.69 ms
candidate elapsed spread ratio: 7.42%
threshold: 7.50%
interpretation: pass, but close enough to carry as a timing note
```

The second named-risk pair was individually borderline on elapsed time
(`1.001x`), while the repeated average passed at `0.822x` and the optional
`risk-440` row passed at `0.810x`.

## Correctness And Rebalance Parity

Every captured cache-level row preserved validation success and same-run
borrowed/candidate output parity for the reported counters and checksums.

Primary KTLX 2026-05-04, 220 files:

```text
examined: 220
skipped: 22
published: 198
batches: 198
events: 6_401_760
payload bytes: 9_537_763_200
payload values: 7_660_888_320
raw checksum: 245_554_417_487
validation checksum: 7_480_064_646_096_449_000
topology versions: 2
rebalance evaluations: 198
accepted moves: 2
skipped decisions: 392
failed migrations: 0
skipped counters:
  NoHotShard: 376
  NoColdTargetShard: 4
  SourceShardMoveBudgetExhausted: 12
  GlobalMoveBudgetExhausted: 12
```

KTLX 2026-05-05 named risk, 220 files:

```text
examined: 220
skipped: 116
published: 104
batches: 104
events: 3_581_280
payload bytes: 5_156_988_480
payload values: 4_138_453_440
raw checksum: 151_996_073_495
validation checksum: 11_084_221_590_146_245_827
topology versions: 2
rebalance evaluations: 104
accepted moves: 2
skipped decisions: 204
failed migrations: 0
skipped counters:
  NoHotShard: 188
  NoColdTargetShard: 4
  SourceShardMoveBudgetExhausted: 12
  GlobalMoveBudgetExhausted: 12
```

KINX 2026-05-04, 220 files:

```text
examined: 220
skipped: 121
published: 99
batches: 99
events: 3_207_600
payload bytes: 4_785_881_760
payload values: 3_843_560_160
raw checksum: 143_362_496_638
validation checksum: 1_465_969_045_420_103_918
topology versions: 2
rebalance evaluations: 99
accepted moves: 2
skipped decisions: 194
failed migrations: 0
skipped counters:
  NoHotShard: 178
  NoColdTargetShard: 4
  SourceShardMoveBudgetExhausted: 12
  GlobalMoveBudgetExhausted: 12
```

Mixed local cache:

```text
examined: 1_554
skipped: 726
published: 828
batches: 828
events: 27_254_760
payload bytes: 40_232_201_280
payload values: 32_306_203_200
raw checksum: 958_518_408_830
validation checksum: 615_051_108_812_661_629
topology versions: 2
rebalance evaluations: 607
accepted moves: 2
skipped decisions: 1_210
failed migrations: 0
skipped counters:
  NoHotShard: 1_194
  NoColdTargetShard: 4
  SourceShardMoveBudgetExhausted: 12
  GlobalMoveBudgetExhausted: 12
```

Optional KTLX 2026-05-04 full root, 244 files:

```text
examined: 244
skipped: 24
published: 220
batches: 220
events: 7_114_560
payload bytes: 10_599_423_360
payload values: 8_513_587_200
raw checksum: 266_648_133_947
validation checksum: 12_759_860_675_563_334_608
topology versions: 2
rebalance evaluations: 220
accepted moves: 2
skipped decisions: 436
failed migrations: 0
```

Optional KINX 2026-05-04, 440 files:

```text
examined: 440
skipped: 242
published: 198
batches: 198
events: 6_415_200
payload bytes: 9_571_763_520
payload values: 7_687_120_320
raw checksum: 265_400_614_436
validation checksum: 15_594_967_301_805_355_497
topology versions: 2
rebalance evaluations: 198
accepted moves: 2
skipped decisions: 392
failed migrations: 0
```

Optional KTLX 2026-05-05, 440 files:

```text
examined: 440
skipped: 232
published: 208
batches: 208
events: 6_950_880
payload bytes: 10_175_745_600
payload values: 8_169_393_600
raw checksum: 302_864_411_697
validation checksum: 18_353_139_347_576_513_864
topology versions: 2
rebalance evaluations: 208
accepted moves: 2
skipped decisions: 412
failed migrations: 0
```

File smoke:

```text
examined: 1
skipped: 0
published: 1
batches: 1
events: 32_400
payload bytes: 48_257_280
payload values: 38_759_040
raw checksum: 1_063_626_011
validation checksum: 3_750_039_633_875_006_276
topology versions: 2
rebalance evaluations: 1
accepted moves: 1
skipped decisions: 0
failed migrations: 0
```

## Release, Cleanup, And Pressure

Cache-level direct/default rows preserved release and cleanup guardrails:

```text
retained payload failed releases: 0
provider overlap failed releases: 0
current combined retained bytes at completion: 0
maximum observed combined high-water: 54_413_280 bytes
retained-byte budget: 536_870_912 bytes
maximum observed pressure share: 10.14%
```

Candidate retained pressure high-water by row:

```text
primary: 48_257_280
risk: 52_676_640
kinx: 48_342_240
mixed: 54_413_280
ktlx-full: 48_257_280
kinx-440: 48_342_240
risk-440: 52_676_640
file-smoke: 48_257_280
```

Representative candidate retained pool telemetry:

```text
primary 220 files:
  retained pool rents/returns/misses: 396/396/2
  event pool rents/returns/misses: 198/198/1
  byte pool rents/returns/misses: 198/198/1

risk 220 files:
  retained pool rents/returns/misses: 208/208/3
  event pool rents/returns/misses: 104/104/2
  byte pool rents/returns/misses: 104/104/1

kinx 220 files:
  retained pool rents/returns/misses: 198/198/2
  event pool rents/returns/misses: 99/99/1
  byte pool rents/returns/misses: 99/99/1

mixed cache:
  retained pool rents/returns/misses: 1_656/1_656/3
  event pool rents/returns/misses: 828/828/2
  byte pool rents/returns/misses: 828/828/1
```

The direct/default candidate queue depth stayed at 1 in natural rows. This is
consistent with previous natural readiness gates; queue-ahead mechanics remain
covered by controlled tests rather than natural Release gate rows.

## Mixed-Cache Worker Counter Note

The mixed-cache candidate row reported:

```text
worker failed batches: 221
worker failed items: 881
validation: succeeded
failed migrations: 0
```

This is the same mixed-cache counter shape that remained visible in milestone
015. The slice 5 temporary runner did not record borrowed worker failed counts
for this row, so slice 6 should decide whether the existing milestone 015
interpretation is still sufficient or whether borrowed counter capture is
needed before the decision trace.

## CLI Spot-Check

Omitted-provider CLI spot-check:

```powershell
dotnet run --project src\Presentation\RadarPulse.Cli.csproj -c Release --no-build -- processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 1 --mode static --partitions 4 --shards 2 --iterations 1 --warmup-iterations 0
```

Result:

```text
exit code: 0
provider mode: queued-owned
provider mode source: rollout-default
provider default rollout contour: yes
provider rollout default expansion: yes
provider fallback contour: no
evidence contour: natural-default-candidate
evidence scope: natural-readiness
retained payload telemetry: visible
event-array pool telemetry: visible
byte-array pool telemetry: visible
overlap telemetry: visible
allocation attribution: visible
validation: succeeded
```

Explicit borrowed fallback CLI spot-check:

```powershell
dotnet run --project src\Presentation\RadarPulse.Cli.csproj -c Release --no-build -- processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 1 --mode static --provider blocking-borrowed --partitions 4 --shards 2 --iterations 1 --warmup-iterations 0
```

Result:

```text
exit code: 0
provider mode: blocking-borrowed
provider mode source: explicit
provider default rollout contour: no
provider rollout default expansion: no
provider fallback contour: yes
queued telemetry: not present
retained payload telemetry: not present
allocation attribution: visible
validation: succeeded
```

## Gate Interpretation For Slice 6

Correctness parity:

```text
accepted for captured rows
validation succeeded across captured rows
same-run borrowed/candidate checksums and counters matched in the gate output
```

Release and cleanup:

```text
accepted
failed releases remained 0
current retained pressure returned to 0
```

Pressure:

```text
accepted
maximum high-water 54_413_280 bytes remained below the 536_870_912 byte budget
```

Allocation:

```text
accepted for cache-level rows
maximum cache-level average ratio: 1.028x
maximum cache-level individual measured pair ratio: 1.040x
threshold: <= 1.10x borrowed
```

Elapsed timing:

```text
accepted on cache-level averages
all cache-level average ratios were <= 0.887x borrowed
the mixed-cache average ratio was 0.873x borrowed
the named-risk row had one individual borderline pair at 1.001x borrowed
```

Spread:

```text
primary candidate spread was 12.01%, above the 7.50% threshold
named-risk candidate spread was 7.42%, below but near the 7.50% threshold
the primary row remains faster than borrowed in every individual pair
allocation, correctness, release, cleanup, and pressure all passed
slice 6 must decide whether the primary spread is an accepted warning or
requires a targeted rerun before the decision trace
```

File smoke:

```text
coverage-only
current file-smoke row did not reproduce the milestone 015 cold warning
still insufficient for a file-level default readiness claim
```

Gate status:

```text
captured with primary spread warning
sufficient evidence exists for slice 6 interpretation
not yet a final readiness decision
```
