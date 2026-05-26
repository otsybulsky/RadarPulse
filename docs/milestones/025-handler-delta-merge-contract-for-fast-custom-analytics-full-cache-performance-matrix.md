# Milestone 025: Full-Cache Handler Performance Matrix

Status: captured before decision trace.

This document records the requested full-cache performance matrix for the
milestone 025 handler delta/merge runtime path. It uses the local `data\nexrad`
cache and runs standard benchmark handlers together with the heavier custom
benchmark handler.

## Scope

Surface:

```text
processing benchmark ordered-archive-processing --cache data\nexrad
```

Handler sets:

```text
counter-checksum:
  standard benchmark counter/checksum handler

counter-checksum-heavy:
  standard benchmark counter/checksum handler
  heavy sampled checksum custom benchmark handler
```

Implementation path:

```text
RadarProcessingArchiveOrderedProcessingBenchmark
RadarProcessingArchiveQueuedOverlapRunner.RunMvpProcessingAsync
RadarProcessingQueuedProcessingSession.DrainOrderedConcurrentAsync

active batches 1:
  sequential handler-aware drain through the MVP runtime

active batches 4:
  ordered concurrent handler delta/merge
```

The `rebalance-archive` benchmark remains outside this handler matrix because
it does not expose the custom handler output surface. This evidence targets the
archive-processing MVP runtime path that owns handler output.

## Build And Focused Tests

Release build:

```text
dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

Focused Release suite:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "FullyQualifiedName~RadarProcessingSyntheticBenchmarkTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests|FullyQualifiedName~RadarProcessingHandlerDeltaPerformanceGateTests"

result:
  45 passed, 0 failed, 0 skipped
```

## Workload

All rows used the same local cache:

```text
cache:
  data\nexrad

source count:
  46_080

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

Shared command shape:

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
  --active-batches <1|4>
  --handlers <counter-checksum|counter-checksum-heavy>
```

## Result Summary

End-to-end result:

| Handler set | Active batches | Processing path | Elapsed ms | Ratio vs same handler active=1 | Allocated bytes | Allocation ratio vs same handler active=1 |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| counter-checksum | 1 | sequential handler-aware drain | 69_464.65 | 1.000x | 4_676_870_544 | 1.000x |
| counter-checksum | 4 | handler delta/merge | 78_480.75 | 1.130x | 33_636_660_120 | 7.192x |
| counter-checksum-heavy | 1 | sequential handler-aware drain | 69_075.00 | 1.000x | 4_671_167_384 | 1.000x |
| counter-checksum-heavy | 4 | handler delta/merge | 82_884.73 | 1.200x | 56_545_129_088 | 12.105x |

Cross-handler comparison:

```text
counter-checksum-heavy active=1 vs counter-checksum active=1:
  elapsed: 0.994x
  allocation: 0.999x

counter-checksum-heavy active=4 vs counter-checksum active=4:
  elapsed: 1.056x
  allocation: 1.681x
```

Correctness and lifecycle:

```text
all rows:
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
  retained payload failed releases: 0
  terminal combined retained batches: 0
  terminal combined retained payload bytes: 0
```

Retained pressure and pool health:

| Handler set | Active batches | Queue depth high watermark | Active retained batches high watermark | Active retained payload bytes high watermark | Combined retained batches high watermark | Combined retained payload bytes high watermark | Retained pool misses |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| counter-checksum | 1 | 1 | 1 | 54_413_280 | 1 | 54_413_280 | 0 |
| counter-checksum | 4 | 1 | 4 | 213_402_240 | 4 | 213_402_240 | 0 |
| counter-checksum-heavy | 1 | 1 | 1 | 54_413_280 | 1 | 54_413_280 | 0 |
| counter-checksum-heavy | 4 | 1 | 4 | 213_402_240 | 5 | 257_538_240 | 2 |

Worker telemetry for sequential handler-aware rows:

| Handler set | Active batches | Worker execution ms | Worker barrier wait ms | Worker failed batches/items |
| --- | ---: | ---: | ---: | --- |
| counter-checksum | 1 | 1_941.45 | 2_072.96 | 0/0 |
| counter-checksum-heavy | 1 | 5_152.33 | 5_310.93 | 0/0 |

## Interpretation

The full-cache handler matrix completed successfully and proved correctness for
the standard handler and the standard-plus-heavy handler set over the whole
local cache.

The active=1 rows remained archive-producer dominated. The heavier handler was
visible in worker execution time, but the end-to-end elapsed result stayed flat
inside normal run noise.

The active=4 handler delta/merge rows are correct but not performance-ready as
an accepted high-volume default. They increased end-to-end elapsed time and
allocation materially, with the heavy handler row reaching
`56_545_129_088` allocated bytes and `12.105x` allocation versus its
active=1 row.

The implementation now emits handler delta values only for touched sources,
which makes the full-cache run feasible. The remaining cost is the current
merge coordinator/value-application shape: merged value snapshots are rebuilt
and re-applied repeatedly as touched source coverage grows. The next
performance optimization target is an incremental per-handler/per-source merge
state that avoids dictionary rebuilds and repeated full merged-value
application on every committed batch.

## Decision Trace Carry-Forward

The milestone 025 decision trace should carry this warning:

```text
full-cache handler delta/merge correctness is proven for the benchmark handler
sets, but active=4 high-volume handler delta/merge is not yet accepted as a
fast default because allocation and elapsed time regress materially versus the
sequential handler-aware row
```
