# Milestone 024: Optional Full-Cache Performance Matrix

Status: captured and referenced by decision trace and closeout.

This optional matrix was captured after slice 5 and referenced by the
milestone 024 decision trace and closeout. It reuses the prior full-cache
performance shape from milestones 020-022.

The matrix does not change the milestone 024 scope. It checks that the MVP
handler output/BFF work did not disturb the accepted full-cache benchmark
contours.

## Workload

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

## Commands

Borrowed oracle matrix:

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

Default queued-owned matrix:

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

Ordered processing active=1 baseline:

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

Ordered processing default active=4:

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

## Rebalance-Archive Matrix

End-to-end result:

| Mode | Borrowed elapsed ms | Default elapsed ms | Default elapsed ratio | Borrowed allocated bytes | Default allocated bytes | Default allocation ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| static | 85_157.21 | 69_140.84 | 0.812x | 3_796_970_096 | 3_802_816_432 | 1.002x |
| sampling | 77_244.37 | 71_904.92 | 0.931x | 3_737_306_872 | 3_747_883_344 | 1.003x |
| rebalance-session | 77_532.98 | 68_595.60 | 0.885x | 3_756_719_872 | 3_757_861_384 | 1.000x |

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

Internal callback attribution:

| Mode | Borrowed callback elapsed ms | Default callback elapsed ms | Default callback elapsed ratio | Borrowed callback allocated bytes | Default callback allocated bytes | Default callback allocation ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| static | 10_602.31 | 13_957.28 | 1.316x | 1_105_446_920 | 3_627_461_856 | 3.281x |
| sampling | 10_464.87 | 14_074.45 | 1.345x | 1_109_290_520 | 3_638_259_864 | 3.280x |
| rebalance-session | 10_527.55 | 14_209.62 | 1.350x | 1_125_534_864 | 3_651_360_928 | 3.244x |

## Ordered-Archive-Processing Matrix

End-to-end result:

| Row | Active batches | Elapsed ms | Elapsed ratio vs active=1 | Steady allocated bytes | Allocation ratio vs active=1 |
| --- | ---: | ---: | ---: | ---: | ---: |
| same-path baseline | 1 | 69_136.32 | 1.000x | 3_800_018_392 | 1.000x |
| ordered default | 4 | 67_911.99 | 0.982x | 3_827_555_648 | 1.007x |

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

## Interpretation

```text
No full-cache regression was observed after milestone 024 slice work.

The default queued-owned rebalance-archive contour remained faster than the
explicit BlockingBorrowed oracle end-to-end in static, sampling, and
rebalance-session modes.

Default queued-owned allocation stayed effectively flat against borrowed at
1.000x to 1.003x.

Ordered active=4 was faster than active=1 on this run and kept steady
allocation within the previously accepted 1.007x shape.

Correctness, checksum parity, accepted move parity, processing completeness,
worker health, retained pool health, release health, and terminal retained
pressure cleanup all passed.
```

Carry-forward note:

```text
callback attribution remains heavier under queued-owned, matching the known
cost shape from earlier milestones.

The full-cache workload remains archive-producer dominated. The result is
regression evidence, not proof that handler delta/merge will meet high-volume
custom analytics performance targets.
```
