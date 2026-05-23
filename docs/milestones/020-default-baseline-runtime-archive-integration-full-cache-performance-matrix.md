# Milestone 020: Full-Cache Performance Matrix

Status: captured after gate, before decision trace.

This document records the Release CLI full-cache performance matrix requested
after the milestone 020 gate capture.

The matrix asks:

```text
Did the accepted queued-owned runtime/archive default baseline regress
full-cache processing benchmark behavior versus the explicit BlockingBorrowed
oracle?
```

## Scope

Surface:

```text
processing benchmark rebalance-archive --cache data\nexrad
```

Cache shape:

```text
cache root: data\nexrad
examined files: 1_554
skipped files: 726
published base-data files: 828
stream events: 27_254_760
payload bytes: 40_232_201_280
payload values: 32_306_203_200
compressed records: 46_070
compressed bytes: 4_933_222_860
decompressed bytes: 42_313_659_680
```

Compared contours:

```text
borrowed oracle:
  provider mode: blocking-borrowed
  execution: async shard transport
  workers: 4
  worker queue capacity: current default, 1

omitted/default:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  provider queue capacity: 8
  retained-byte budget: 536870912
  retained payload prewarm: enabled and separately reported
  execution: async shard transport
  workers: 4
  worker queue capacity: 8
```

Modes:

```text
static
sampling
rebalance-session
```

## Commands

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Borrowed oracle matrix:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode all --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

Omitted/default matrix:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode all --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

## Result Summary

Release build:

```text
succeeded, 0 warnings, 0 errors
```

End-to-end result:

| Mode | Borrowed elapsed ms | Default elapsed ms | Default elapsed ratio | Borrowed allocated bytes | Default allocated bytes | Default allocation ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| static | 84_891.85 | 67_323.57 | 0.793x | 3_799_633_080 | 3_800_138_712 | 1.000x |
| sampling | 75_563.61 | 67_230.67 | 0.890x | 3_739_191_176 | 3_745_692_440 | 1.002x |
| rebalance-session | 75_991.01 | 66_970.72 | 0.881x | 3_748_711_976 | 3_758_633_216 | 1.003x |

Interpretation:

```text
no end-to-end performance regression was observed
default queued-owned was faster than borrowed in every mode
default total allocation stayed effectively flat against borrowed
```

## Correctness And Lifecycle

All default rows:

```text
validation: succeeded
processing completeness: succeeded
processing validation failed batches: 0
worker failed batches: 0
worker failed items: 0
provider queue failed batches: 0
provider queue canceled batches: 0
provider queue skipped after fault batches: 0
retained payload failed releases: 0
current combined retained batches: 0
current combined retained payload bytes: 0
retained payload pool misses: 0
provider queue depth high watermark: 1
provider queue combined retained payload bytes high watermark: 54_413_280
retained-byte budget: 536_870_912
```

Rebalance-session parity:

```text
borrowed accepted moves: 4
default accepted moves: 4
borrowed failed migrations: 0
default failed migrations: 0
borrowed validation checksum: 8_775_520_679_090_824_038
default validation checksum: 8_775_520_679_090_824_038
```

Static checksum parity:

```text
borrowed validation checksum: 14_394_292_751_884_722_580
default validation checksum: 14_394_292_751_884_722_580
```

Sampling checksum parity:

```text
borrowed validation checksum: 14_685_941_263_972_259_783
default validation checksum: 14_685_941_263_972_259_783
```

## Startup Prewarm

Default rows applied retained payload prewarm:

```text
event count: 65_536
payload bytes: 67_108_864
retained batch count: 1
allocated bytes: about 71_303_392 to 71_303_416
retained bytes: 71_303_168
```

Interpretation:

```text
startup prewarm remains a visible lifecycle cost and is not hidden inside
steady measured row allocation
```

## Internal Attribution Note

The only observed cost shift is internal attribution, not an end-to-end
regression.

Processing callback allocation:

| Mode | Borrowed callback allocated bytes | Default callback allocated bytes | Default callback allocation ratio |
| --- | ---: | ---: | ---: |
| static | 1_105_444_408 | 3_629_089_552 | 3.283x |
| sampling | 1_109_277_728 | 3_637_626_704 | 3.279x |
| rebalance-session | 1_125_546_448 | 3_652_430_072 | 3.245x |

Processing callback elapsed:

| Mode | Borrowed callback elapsed ms | Default callback elapsed ms | Default callback elapsed ratio |
| --- | ---: | ---: | ---: |
| static | 10_792.21 | 14_087.82 | 1.305x |
| sampling | 10_527.54 | 14_115.74 | 1.341x |
| rebalance-session | 10_800.72 | 14_067.62 | 1.302x |

Interpretation:

```text
queued-owned shifts retained ownership, queueing, and overlap work into the
processing callback attribution bucket

the end-to-end rows do not regress because replay and batch construction
allocation and elapsed time drop enough to offset the heavier callback bucket

this remains a known attribution/cost shape to carry forward, not a current
full-cache readiness blocker
```

## Answer

```text
No end-to-end full-cache performance regression was observed.
```

The accepted default contour remains supported by this post-gate matrix:

```text
default queued-owned was faster than borrowed in static, sampling, and
rebalance-session modes
total allocation was effectively flat, at 1.000x to 1.003x borrowed
correctness, processing completeness, worker health, release health, retained
pressure cleanup, and checksum parity all passed
```

Carry-forward note:

```text
processing callback attribution is heavier under queued-owned and should
remain visible in future runtime/archive and production-pipeline performance
reviews
```
