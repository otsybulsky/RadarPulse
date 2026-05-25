# Milestone 022: Full-Cache Performance Matrix

Status: captured after milestone 022 gate.

This matrix records full-cache regression evidence after ordered
rebalance/topology commit work. It complements the milestone 022
processing-bottleneck synthetic matrix by replaying the whole local
`data\nexrad` cache through the archive benchmark surfaces.

The result is not a broader default-promotion decision. It is post-gate
performance evidence that the milestone 022 ordered rebalance work did not
regress the accepted full-cache benchmark contours.

## Build

```text
dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Full-Cache Workload

All rows used the same local cache:

```text
cache:
  data\nexrad

examined files:
  1_554

skipped files:
  726

published files:
  828

file size bytes:
  4_933_427_012

compressed records:
  46_070

compressed bytes:
  4_933_222_860

decompressed bytes:
  42_313_659_680

batches:
  828

stream events:
  27_254_760

payload bytes:
  40_232_201_280

payload values:
  32_306_203_200

raw value checksum:
  958_518_408_830
```

## Rebalance-Archive Matrix

Explicit oracle row:

```text
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll
  processing benchmark rebalance-archive
  --cache data\nexrad
  --max-files 1000000
  --mode all
  --provider blocking-borrowed
  --execution async
  --workers 4
  --iterations 1
  --warmup-iterations 0
  --parallelism 24
  --partitions 24
  --shards 4
  --validation-profile benchmark
```

Default queued-owned row:

```text
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll
  processing benchmark rebalance-archive
  --cache data\nexrad
  --max-files 1000000
  --mode all
  --execution async
  --workers 4
  --iterations 1
  --warmup-iterations 0
  --parallelism 24
  --partitions 24
  --shards 4
  --validation-profile benchmark
```

End-to-end result:

| Mode | Borrowed elapsed ms | Default elapsed ms | Default elapsed ratio | Borrowed allocated bytes | Default allocated bytes | Default allocation ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| static | 76_216.14 | 67_300.94 | 0.883x | 3_795_562_848 | 3_803_586_072 | 1.002x |
| sampling | 76_249.11 | 67_939.62 | 0.891x | 3_739_408_752 | 3_743_147_336 | 1.001x |
| rebalance-session | 77_355.46 | 67_405.69 | 0.871x | 3_755_492_608 | 3_762_642_072 | 1.002x |

Interpretation:

```text
no end-to-end full-cache regression was observed versus the explicit
BlockingBorrowed oracle

the omitted/default queued-owned contour was faster than borrowed in static,
sampling, and rebalance-session modes

total allocation stayed effectively flat against borrowed, at 1.001x to
1.002x
```

Correctness and rebalance parity:

```text
all rows:
  validation: succeeded
  processing completeness: succeeded
  processing validation failed batches: 0
  worker failed batches: 0
  worker failed items: 0

static validation checksum:
  borrowed: 14_394_292_751_884_722_580
  default:  14_394_292_751_884_722_580

sampling validation checksum:
  borrowed: 14_685_941_263_972_259_783
  default:  14_685_941_263_972_259_783

rebalance-session:
  borrowed accepted moves: 4
  default accepted moves: 4
  borrowed failed migrations: 0
  default failed migrations: 0
  borrowed validation checksum: 8_775_520_679_090_824_038
  default validation checksum:  8_775_520_679_090_824_038
```

Default queued-owned lifecycle:

```text
provider queue enqueued/dequeued/completed batches:
  828 / 828 / 828

provider queue failed batches:
  0

provider queue canceled batches:
  0

provider queue skipped after fault batches:
  0

retained payload pool misses:
  0

event array pool misses:
  0

byte array pool misses:
  0

retained payload failed releases:
  0

current combined retained batches:
  0

current combined retained payload bytes:
  0
```

Rebalance-session accepted move evidence:

```text
topology versions:
  2

rebalance evaluations:
  828

accepted moves:
  4

direct hot relief moves:
  3

cold evacuation moves:
  1

skipped decisions:
  1_649

skipped reason counters:
  no-hot-shard=4
  no-cold-target-shard=32
  direct-hot-partition-has-no-safe-target=8
  target-would-become-warm=811
  target-shard-receive-budget-exhausted=4
  global-move-budget-exhausted=6
  partition-classified-intrinsic-hot=804
```

Internal callback attribution:

| Mode | Borrowed callback elapsed ms | Default callback elapsed ms | Default callback elapsed ratio | Borrowed callback allocated bytes | Default callback allocated bytes | Default callback allocation ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| static | 10_773.21 | 14_123.90 | 1.311x | 1_105_434_496 | 3_629_645_240 | 3.283x |
| sampling | 10_775.21 | 14_164.51 | 1.315x | 1_109_289_192 | 3_636_224_304 | 3.278x |
| rebalance-session | 11_103.24 | 14_306.24 | 1.288x | 1_125_510_440 | 3_657_634_512 | 3.250x |

Attribution note:

```text
queued-owned still shifts retained ownership, queueing, and overlap work into
the processing callback attribution bucket

the end-to-end rows remain faster because replay and batch construction time
offsets the heavier callback attribution bucket

this is the same known cost shape carried from milestone 021 and does not
represent an end-to-end regression in this full-cache matrix
```

## Ordered-Archive-Processing Matrix

Default active-batch row:

```text
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll
  processing benchmark ordered-archive-processing
  --cache data\nexrad
  --max-files 1000000
  --iterations 1
  --warmup-iterations 0
  --parallelism 24
  --partitions 24
  --shards 4
```

Same-path active=1 baseline row:

```text
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll
  processing benchmark ordered-archive-processing
  --cache data\nexrad
  --max-files 1000000
  --iterations 1
  --warmup-iterations 0
  --parallelism 24
  --partitions 24
  --shards 4
  --active-batches 1
```

End-to-end result:

| Row | Active batches | Elapsed ms | Elapsed ratio vs active=1 | Steady allocated bytes | Allocation ratio vs active=1 |
| --- | ---: | ---: | ---: | ---: | ---: |
| same-path baseline | 1 | 67_803.08 | 1.000x | 3_801_252_144 | 1.000x |
| ordered default | 4 | 67_720.71 | 0.999x | 3_826_825_400 | 1.007x |

Correctness and lifecycle:

```text
both rows:
  run status: completed
  consumer status: completed
  processing completeness: succeeded
  processing succeeded batches: 828
  processing failed batches: 0
  processing validation failed batches: 0
  processing canceled batches: 0
  processing skipped after fault batches: 0
  final processed batches: 828
  final processed stream events: 27_254_760
  final processed payload values: 32_306_203_200
  final raw value checksum: 958_518_408_830
  final processing checksum: 2_294_439_733_285_583_699
  worker failed batches/items: 0/0
  retained payload pool misses: 0
  retained payload failed releases: 0
  terminal combined retained pressure: 0
```

Retained pressure:

| Row | Active batches | Queue depth high watermark | Active retained batches high watermark | Active retained payload bytes high watermark | Combined retained batches high watermark | Combined retained payload bytes high watermark |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| same-path baseline | 1 | 1 | 1 | 54_413_280 | 1 | 54_413_280 |
| ordered default | 4 | 1 | 4 | 213_402_240 | 4 | 213_402_240 |

Startup prewarm:

| Row | Active batches | Prewarm batch count | Prewarm allocated bytes | Prewarm retained bytes |
| --- | ---: | ---: | ---: | ---: |
| same-path baseline | 1 | 1 | 71_303_416 | 71_303_168 |
| ordered default | 4 | 4 | 285_213_112 | 285_212_672 |

Interpretation:

```text
ordered active=4 did not regress end-to-end elapsed time on the full cache

steady allocation stayed close to the same-path active=1 row at 1.007x

the full-cache workload remains archive-producer dominated: queue depth high
watermark stayed at 1, so active-batch concurrency has limited elapsed-time
leverage on this cache shape
```

## Comparison With Prior Accepted Full-Cache Evidence

The previous milestone 021 full-cache rows recorded:

```text
rebalance-archive default elapsed ratios:
  0.965x static
  0.878x sampling
  0.884x rebalance-session

rebalance-archive default allocation ratios:
  1.003x static
  1.001x sampling
  1.000x rebalance-session

ordered-archive-processing active=4 elapsed ratio versus active=1:
  0.994x

ordered-archive-processing active=4 steady allocation ratio versus active=1:
  1.006x
```

Current milestone 022 rows are consistent with that accepted shape:

```text
rebalance-archive default elapsed ratios:
  0.883x static
  0.891x sampling
  0.871x rebalance-session

rebalance-archive default allocation ratios:
  1.002x static
  1.001x sampling
  1.002x rebalance-session

ordered-archive-processing active=4 elapsed ratio versus active=1:
  0.999x

ordered-archive-processing active=4 steady allocation ratio versus active=1:
  1.007x
```

## Answer

```text
No full-cache performance regression was observed after milestone 022 ordered
rebalance/topology commit work.
```

Supported posture:

```text
rebalance-archive default queued-owned remained faster than explicit
BlockingBorrowed end-to-end across static, sampling, and rebalance-session
modes

rebalance correctness, accepted move evidence, validation checksum parity,
processing completeness, worker health, release health, retained pool health,
and terminal retained pressure cleanup all passed

ordered-archive-processing active=4 remained effectively flat against
active=1 on the full cache, with the expected bounded retained prewarm and
active retained pressure shape
```

Carry-forward warning:

```text
this full-cache evidence is archive-producer dominated. The milestone 022
processing-bottleneck matrix remains the better evidence for ordered
rebalance compute overlap under processing/rebalance-shaped load.
```
