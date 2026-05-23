# Milestone 021: Full-Cache Performance Matrix

Status: captured after gate; decision trace written.

This document records the requested Release CLI full-cache performance matrix
after milestone 021 implementation.

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

Scope boundary:

```text
this matrix exercises the existing rebalance-archive CLI benchmark contour
after milestone 021 changes. It verifies that the accepted full-cache
default contour did not regress versus the explicit BlockingBorrowed oracle.

the direct RunProcessingAsync ordered-processing CLI matrix is captured
separately in:
docs/milestones/021-ordered-concurrent-runtime-archive-processing-ordered-full-cache-performance-matrix.md
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
| static | 70_892.24 | 68_381.85 | 0.965x | 3_790_231_632 | 3_802_981_440 | 1.003x |
| sampling | 76_783.75 | 67_406.62 | 0.878x | 3_741_059_656 | 3_743_617_952 | 1.001x |
| rebalance-session | 76_517.27 | 67_628.87 | 0.884x | 3_755_478_840 | 3_755_569_176 | 1.000x |

Interpretation:

```text
no end-to-end full-cache regression was observed versus the explicit
BlockingBorrowed oracle

the omitted/default queued-owned contour was faster than borrowed in every
mode

total allocation stayed effectively flat against borrowed, at 1.000x to
1.003x
```

## Correctness And Lifecycle

All rows:

```text
validation: succeeded
processing completeness: succeeded
processing validation failed batches: 0
worker failed batches: 0
worker failed items: 0
```

All default rows:

```text
provider queue failed batches: 0
provider queue canceled batches: 0
provider queue skipped after fault batches: 0
retained payload failed releases: 0
retained payload pool misses: 0
current combined retained batches: 0
current combined retained payload bytes: 0
provider queue depth high watermark: 1
provider queue combined retained payload bytes high watermark: 54_413_280
retained-byte budget: 536_870_912
```

Checksum and rebalance parity:

```text
static:
  borrowed validation checksum: 14_394_292_751_884_722_580
  default validation checksum: 14_394_292_751_884_722_580

sampling:
  borrowed validation checksum: 14_685_941_263_972_259_783
  default validation checksum: 14_685_941_263_972_259_783

rebalance-session:
  borrowed accepted moves: 4
  default accepted moves: 4
  borrowed failed migrations: 0
  default failed migrations: 0
  borrowed validation checksum: 8_775_520_679_090_824_038
  default validation checksum: 8_775_520_679_090_824_038
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

Processing callback allocation remains heavier under queued-owned, while
end-to-end rows do not regress.

Processing callback allocation:

| Mode | Borrowed callback allocated bytes | Default callback allocated bytes | Default callback allocation ratio |
| --- | ---: | ---: | ---: |
| static | 1_105_430_552 | 3_634_000_936 | 3.288x |
| sampling | 1_109_315_728 | 3_635_519_408 | 3.277x |
| rebalance-session | 1_125_554_496 | 3_646_468_160 | 3.239x |

Processing callback elapsed:

| Mode | Borrowed callback elapsed ms | Default callback elapsed ms | Default callback elapsed ratio |
| --- | ---: | ---: | ---: |
| static | 10_481.27 | 14_222.48 | 1.357x |
| sampling | 10_582.82 | 14_026.78 | 1.325x |
| rebalance-session | 10_579.67 | 14_015.64 | 1.325x |

Interpretation:

```text
queued-owned still shifts retained ownership, queueing, and overlap work into
the processing callback attribution bucket

the end-to-end rows remain faster because replay and batch construction
elapsed time drops enough to offset the heavier callback bucket

this remains a known attribution/cost shape to carry forward
```

## Answer

```text
No end-to-end full-cache regression was observed for the existing
rebalance-archive full-cache CLI matrix after milestone 021 implementation.
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
direct full-cache evidence for the new ordered processing runtime/archive
RunProcessingAsync path is captured separately; this document remains the
rebalance-archive default-vs-borrowed regression matrix
```
