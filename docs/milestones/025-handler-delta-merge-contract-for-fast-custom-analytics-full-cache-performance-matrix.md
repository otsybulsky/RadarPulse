# Milestone 025: Full-Cache Handler Performance Matrix

Status: captured, optimized, and referenced by decision trace.

This document records the requested full-cache performance matrix for the
milestone 025 handler delta/merge runtime path and the follow-up merge-state
optimization. It uses the local `data\nexrad` cache and runs standard
benchmark handlers together with the heavier custom benchmark handler.

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

Focused Release suite after merge-state optimization:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-build
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests|FullyQualifiedName~RadarProcessingSyntheticBenchmarkTests|FullyQualifiedName~RadarProcessingHandlerDeltaPerformanceGateTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

result:
  53 passed, 0 failed, 0 skipped
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
| counter-checksum | 1 | sequential handler-aware drain | 61_373.01 | 1.000x | 4_671_386_960 | 1.000x |
| counter-checksum | 4 | optimized handler delta/merge | 61_588.17 | 1.004x | 8_188_695_464 | 1.753x |
| counter-checksum-heavy | 1 | sequential handler-aware drain | 62_806.15 | 1.000x | 4_675_001_328 | 1.000x |
| counter-checksum-heavy | 4 | optimized handler delta/merge | 62_687.17 | 0.998x | 12_209_454_512 | 2.612x |

Active=4 comparison against the pre-optimization matrix:

| Handler set | Before elapsed ms | Optimized elapsed ms | Optimized/before elapsed | Before allocated bytes | Optimized allocated bytes | Optimized/before allocation |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| counter-checksum | 78_480.75 | 61_588.17 | 0.785x | 33_636_660_120 | 8_188_695_464 | 0.243x |
| counter-checksum-heavy | 82_884.73 | 62_687.17 | 0.756x | 56_545_129_088 | 12_209_454_512 | 0.216x |

Cross-handler comparison:

```text
counter-checksum-heavy active=1 vs counter-checksum active=1:
  elapsed: 1.023x
  allocation: 1.001x

counter-checksum-heavy active=4 vs counter-checksum active=4:
  elapsed: 1.018x
  allocation: 1.491x
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

```text
all rows:
  retained payload pool misses: 0
  terminal combined retained batches: 0
  terminal combined retained payload bytes: 0
```

Worker telemetry for sequential handler-aware rows:

| Handler set | Active batches | Worker execution ms | Worker barrier wait ms | Worker failed batches/items |
| --- | ---: | ---: | ---: | --- |
| counter-checksum | 1 | 1_787.58 | 1_837.10 | 0/0 |
| counter-checksum-heavy | 1 | 4_745.21 | 4_896.30 | 0/0 |

## Interpretation

The full-cache handler matrix completed successfully and proved correctness for
the standard handler and the standard-plus-heavy handler set over the whole
local cache.

The active=1 rows remained archive-producer dominated. The heavier handler was
visible in worker execution time, but the end-to-end elapsed result stayed flat
inside normal run noise.

The optimized active=4 handler delta/merge rows are no longer an elapsed-time
blocker in this full-cache evidence. `counter-checksum` active=4 measured
`1.004x` elapsed versus active=1, and `counter-checksum-heavy` active=4
measured `0.998x` elapsed versus active=1.

The merge-state optimization materially reduced the active=4 cost. The
standard handler row dropped to `0.785x` of the previous elapsed time and
`0.243x` of the previous allocation. The heavy handler row dropped to `0.756x`
of the previous elapsed time and `0.216x` of the previous allocation.

The optimization combines handler-owned accumulator merge state, a lightweight
commit merge result, direct sparse delta export for touched sources, and
grouped handler-value commit. It avoids rebuilding and reapplying broad merged
snapshots on every committed batch.

Allocation remains above the active=1 handler-aware rows: `1.753x` for
`counter-checksum` active=4 and `2.612x` for `counter-checksum-heavy` active=4.
That overhead is now a scoped performance warning rather than a full-cache
elapsed blocker. If allocation parity is required, the next target is further
reducing per-batch sparse-state and applied-value materialization.

## Decision Trace Carry-Forward

The milestone 025 decision trace should carry this warning:

```text
full-cache handler delta/merge correctness is proven for the benchmark handler
sets, and optimized active=4 elapsed time is flat versus the active=1
handler-aware rows in this matrix; allocation remains higher than active=1
and should be accepted only as a scoped warning unless parity is required
```
