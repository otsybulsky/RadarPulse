# Milestone 021: Ordered Processing Full-Cache Performance Matrix

Status: captured after direct CLI benchmark addition; decision trace still not
written.

This document records the Release CLI full-cache matrix for the new
`RunProcessingAsync` ordered-processing runtime/archive path.

## Scope

Surface:

```text
processing benchmark ordered-archive-processing --cache data\nexrad
```

Implementation path:

```text
RadarProcessingArchiveOrderedProcessingBenchmark
RadarProcessingArchiveQueuedOverlapRunner.RunProcessingAsync
RadarProcessingQueuedProcessingSession.DrainOrderedConcurrentAsync
RadarProcessingCore.ComputeProcessingDelta
RadarProcessingCore.CommitProcessingDelta
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
sequential same-path baseline:
  active batch capacity: 1

ordered concurrent default:
  active batch capacity: 4
```

Shared runtime/archive baseline:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
provider queue capacity: 8
retained-byte budget: 536870912
execution: async shard transport
workers: 4
worker queue capacity: 8
```

## Commands

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Focused Release suite:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release --no-restore --no-build --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests"
```

Ordered concurrent default row:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark ordered-archive-processing --cache data\nexrad --max-files 1000000 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

Sequential same-path baseline row:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark ordered-archive-processing --cache data\nexrad --max-files 1000000 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4 --active-batches 1
```

## Result Summary

Release build:

```text
succeeded, 0 warnings, 0 errors
```

Focused Release suite:

```text
38 passed, 0 failed, 0 skipped
```

End-to-end result:

| Row | Active batches | Elapsed ms | Elapsed ratio vs active=1 | Steady allocated bytes | Allocation ratio vs active=1 |
| --- | ---: | ---: | ---: | ---: | ---: |
| sequential same-path | 1 | 68_212.71 | 1.000x | 3_800_878_360 | 1.000x |
| ordered concurrent default | 4 | 67_834.43 | 0.994x | 3_824_315_736 | 1.006x |

Allocation note:

```text
steady allocated bytes exclude startup retained payload prewarm; the CLI
prints startup prewarm separately
```

Interpretation:

```text
the ordered active-batch path did not regress end-to-end elapsed time on the
full cache

steady allocation stayed close to the same-path sequential row, at 1.006x

this full-cache workload remains archive-producer dominated: queue depth high
watermark stayed at 1 and consumer idle time remained high, so active-batch
concurrency has limited elapsed-time leverage on this cache shape
```

## Correctness And Lifecycle

Both rows:

```text
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
worker failed batches: 0
worker failed items: 0
provider queue failed batches: 0
provider queue canceled batches: 0
provider queue skipped after fault batches: 0
retained payload failed releases: 0
current combined retained batches: 0
current combined retained payload bytes: 0
```

Retained resource pressure:

| Row | Active batches | Queue depth high watermark | Active retained batches high watermark | Active retained payload bytes high watermark | Combined retained batches high watermark | Combined retained payload bytes high watermark |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| sequential same-path | 1 | 1 | 1 | 54_413_280 | 1 | 54_413_280 |
| ordered concurrent default | 4 | 1 | 4 | 213_402_240 | 4 | 213_402_240 |

Retained pool health:

| Row | Active batches | Retained payload pool misses | Event array pool misses | Byte array pool misses |
| --- | ---: | ---: | ---: | ---: |
| sequential same-path | 1 | 0 | 0 | 0 |
| ordered concurrent default | 4 | 0 | 0 | 0 |

## Startup Prewarm

The ordered path now scales startup prewarm to ordered active batch capacity
when the caller uses the runtime/archive default prewarm contour.

| Row | Active batches | Prewarm batch count | Prewarm allocated bytes | Prewarm retained bytes |
| --- | ---: | ---: | ---: | ---: |
| sequential same-path | 1 | 1 | 71_303_416 | 71_303_168 |
| ordered concurrent default | 4 | 4 | 285_213_112 | 285_212_672 |

Interpretation:

```text
the larger active-batch prewarm is a visible startup lifecycle cost, not
hidden inside steady measured allocation

the default active=4 row avoided steady retained pool misses after the
ordered-path prewarm/factory adjustment
```

## Answer

```text
The new RunProcessingAsync ordered-processing full-cache CLI benchmark exists
and completed successfully.
```

Result:

```text
accepted as scoped direct performance evidence for the processing-core
runtime/archive ordered path
```

Carry-forward notes:

```text
full-cache elapsed is dominated by archive replay and batch construction on
this cache shape, so active-batch concurrency does not produce a large
end-to-end gain here

active=4 increases visible startup prewarm from 1 retained batch to 4 retained
batches and increases active retained pressure high watermark as expected

steady retained pool misses remain zero after the ordered-path prewarm
adjustment
```
