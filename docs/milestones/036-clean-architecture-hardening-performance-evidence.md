# Milestone 036: Performance Evidence

Status: captured before decision trace discussion.

This document records the milestone 036 performance evidence requested after
the architecture hardening implementation slices. It has two scopes:

```text
1. full-cache end-to-end radar pipeline performance
2. processing-only synthetic benchmark for a world-class technology claim
```

The results are local Release measurements. They are suitable as deterministic
project evidence, not as cross-machine certification. A public world-class
claim should still include repeated runs, machine specification, CPU/GC
telemetry, and external baseline methodology.

## Build

```text
dotnet build RadarPulse.sln -c Release --no-restore
  /p:UseSharedCompilation=false

result:
  passed, 0 warnings, 0 errors
```

## Full-Cache End-To-End Matrix

Raw logs:

```text
data/perf/m036-full-cache-20260528-142529
```

Scope:

```text
cache: data/nexrad
logical sources: 46_080
examined files: 1_554
published files: 828
skipped files: 726
stream events: 27_254_760
payload values: 32_306_203_200
payload bytes: 40_232_201_280
```

All rows completed with processing completeness succeeded, zero failed
batches, zero validation-failed batches, zero retained release failures, and
zero retained payload pool misses where retained telemetry applies.

### Rebalance Archive

Command shape:

```text
dotnet src/Presentation/RadarPulse.Cli/bin/Release/net10.0/RadarPulse.Cli.dll
  processing benchmark rebalance-archive
  --cache data/nexrad
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

| Mode | Provider | Elapsed ms | Stream events/s | Processing stream events/s | Allocated bytes | Bytes/event | Default vs borrowed |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| static | blocking-borrowed | 73_879.13 | 368_910.14 | 2_617_888.45 | 3_792_847_264 | 139.16 | baseline |
| static | queued-owned | 60_574.45 | 449_938.23 | 1_995_821.05 | 3_799_317_568 | 139.40 | 0.820x elapsed, 1.002x alloc |
| sampling | blocking-borrowed | 70_035.97 | 389_153.75 | 2_675_769.44 | 3_739_670_720 | 137.21 | baseline |
| sampling | queued-owned | 60_961.71 | 447_079.95 | 2_001_623.36 | 3_744_043_584 | 137.37 | 0.870x elapsed, 1.001x alloc |
| rebalance-session | blocking-borrowed | 68_765.77 | 396_341.95 | 2_687_687.60 | 3_755_944_824 | 137.81 | baseline |
| rebalance-session | queued-owned | 60_521.32 | 450_333.21 | 2_009_113.00 | 3_757_493_384 | 137.87 | 0.880x elapsed, 1.000x alloc |

Result:

```text
default queued-owned remained faster than explicit blocking-borrowed in all
three rebalance modes, while total allocation stayed effectively flat at
1.000x to 1.002x
```

### Ordered Archive Processing And Custom Handlers

Command shape:

```text
dotnet src/Presentation/RadarPulse.Cli/bin/Release/net10.0/RadarPulse.Cli.dll
  processing benchmark ordered-archive-processing
  --cache data/nexrad
  --max-files 1000000
  --iterations 1
  --warmup-iterations 0
  --parallelism 24
  --partitions 24
  --shards 4
  --active-batches <1|4>
  --handlers <none|counter-checksum|counter-checksum-heavy>
```

| Handler | Active batches | Path | Elapsed ms | Stream events/s | Payload values/s | Allocated bytes | Bytes/event |
| --- | ---: | --- | ---: | ---: | ---: | ---: | ---: |
| none | 1 | ordered drain | 61_340.66 | 444_318.04 | 526_668_690.82 | 3_799_589_728 | 139.41 |
| none | 4 | ordered drain | 61_813.81 | 440_916.98 | 522_637_276.66 | 3_828_220_752 | 140.46 |
| counter-checksum | 1 | sequential handler-aware | 61_949.36 | 439_952.27 | 521_493_771.53 | 4_673_000_960 | 171.46 |
| counter-checksum | 4 | handler delta/merge | 60_828.91 | 448_056.05 | 531_099_517.32 | 8_189_970_696 | 300.50 |
| counter-checksum-heavy | 1 | sequential handler-aware | 61_695.05 | 441_765.75 | 523_643_360.44 | 4_675_606_240 | 171.55 |
| counter-checksum-heavy | 4 | handler delta/merge | 60_951.85 | 447_152.29 | 530_028_245.90 | 12_212_257_456 | 448.08 |

Ratios:

| Comparison | Elapsed ratio | Allocation ratio | Stream events/s ratio |
| --- | ---: | ---: | ---: |
| ordered none active=4 / active=1 | 1.008x | 1.008x | 0.992x |
| counter-checksum active=4 / active=1 | 0.982x | 1.753x | 1.018x |
| counter-checksum-heavy active=4 / active=1 | 0.988x | 2.612x | 1.012x |
| heavy / counter active=1 | 0.996x | 1.001x | 1.004x |
| heavy / counter active=4 | 1.002x | 1.491x | 0.998x |

Result:

```text
full-cache handler throughput stayed stable at roughly 440K-448K
RadarStreamEvent/s end-to-end and roughly 522M-531M payload values/s
end-to-end

heavy custom handlers did not create a speed blocker, but active=4 handler
delta/merge allocation is the visible performance debt: 12.21 GB total and
448.08 bytes per RadarStreamEvent for counter-checksum-heavy active=4
```

## Processing-Only World-Class Claim Benchmark

Raw logs:

```text
data/perf/m036-world-class-20260528-151123
```

Intent:

```text
isolate the processing core and handler engine from archive replay,
decompression, Archive II scanning, identity normalization, and batch
construction so the technology claim measures routing and handler invocation
rather than file-cache ingestion
```

Workload:

```text
source count: 46_080
batches per iteration: 256
stream events per iteration: 1_048_576
payload values per iteration: 67_108_864
payload values per event: 64
iterations: 5 measured
warmup iterations: 2
partitions: 24
shards: 4
async workers: 4 where async mode is used
async worker queue capacity: 8 where async mode is used
validation profile: benchmark
```

Command shape:

```text
dotnet src/Presentation/RadarPulse.Cli/bin/Release/net10.0/RadarPulse.Cli.dll
  processing benchmark synthetic
  --sources 46080
  --batches 256
  --events-per-batch 4096
  --payload-values 64
  --partitions 24
  --shards 4
  --iterations 5
  --warmup-iterations 2
  --mode <sequential|partitioned|async>
  --handlers <none|counter-checksum|counter-checksum-heavy>
```

| Mode | Handler | Stream events/s | Payload values/s | Elapsed ms | Allocated bytes | Bytes/event | Validation checksum |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| sequential | none | 2_392_962.68 | 153_149_611.24 | 2_190.96 | 327_880 | 0.06 | 15_663_302_234_586_777_583 |
| sequential | counter-checksum | 2_343_742.39 | 149_999_512.73 | 2_236.97 | 168_100_040 | 32.06 | 7_292_345_821_437_281_546 |
| sequential | counter-checksum-heavy | 2_101_506.66 | 134_496_426.11 | 2_494.82 | 168_100_040 | 32.06 | 2_880_488_323_857_062_242 |
| partitioned | none | 2_364_395.67 | 151_321_322.91 | 2_217.43 | 217_176_288 | 41.42 | 15_663_302_234_586_777_583 |
| partitioned | counter-checksum | 2_275_235.40 | 145_615_065.80 | 2_304.32 | 384_944_376 | 73.42 | 7_292_345_821_437_281_546 |
| partitioned | counter-checksum-heavy | 2_060_612.64 | 131_879_208.94 | 2_544.33 | 384_944_376 | 73.42 | 2_880_488_323_857_062_242 |
| async | none | 1_249_775.54 | 79_985_634.52 | 4_195.06 | 231_151_136 | 44.09 | 15_663_302_234_586_777_583 |
| async | counter-checksum | 1_214_815.85 | 77_748_214.19 | 4_315.78 | 398_924_200 | 76.09 | 7_292_345_821_437_281_546 |
| async | counter-checksum-heavy | 1_140_818.38 | 73_012_376.04 | 4_595.72 | 398_928_136 | 76.09 | 2_880_488_323_857_062_242 |

Async worker telemetry:

```text
all async rows:
  worker count: 4
  worker queue capacity: 8
  dispatched/completed batches: 1_280 / 1_280
  failed batches: 0
  submitted/completed/succeeded work items: 5_120 / 5_120 / 5_120
  failed work items: 0
  async validation: yes
  sync comparison checksum matched async comparison checksum
```

Interpretation:

```text
the processing core can process more than 2.0M RadarStreamEvent/s on the
same 46_080 logical-source universe when archive ingestion is excluded

the heavy benchmark handler remains above 2.0M RadarStreamEvent/s in
sequential and partitioned processing-only modes, and above 1.14M
RadarStreamEvent/s through async shard transport with worker queueing

this supports a restrained world-class technology claim for the local
processing engine: high-throughput, validated, source-routed radar event
processing with mergeable custom handler state
```

Claim boundary:

```text
acceptable wording:
  processing-only benchmark demonstrates multi-million RadarStreamEvent/s
  source-routed handler throughput on a 46_080-source workload

avoid wording:
  fastest in the world
  globally certified
  directly faster than Kafka/Flink/ClickHouse/Disruptor

required before public comparative claim:
  publish machine specification
  repeat runs and include min/median/max or confidence interval
  capture CPU utilization, GC collections, memory pressure, and runtime version
  define competitor-equivalent workload shape
  separate end-to-end archive ingestion numbers from processing-only numbers
```

## Overall Answer

```text
full-cache end-to-end performance is strong and correctness-clean:
  about 440K-448K RadarStreamEvent/s with custom handlers
  about 522M-531M payload values/s through the full archive pipeline

processing-only handler engine performance is the stronger technology-claim
number:
  2.10M RadarStreamEvent/s with the heavy handler set in sequential mode
  2.06M RadarStreamEvent/s with the heavy handler set in partitioned mode
  1.14M RadarStreamEvent/s with the heavy handler set through async worker
    transport

the main remaining performance debt is allocation in the full-cache active=4
handler delta/merge path, not speed or correctness
```
