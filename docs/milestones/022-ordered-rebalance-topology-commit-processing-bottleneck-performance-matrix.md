# Milestone 022: Processing-Bottleneck Performance Matrix

Status: captured after slice 5.

This matrix records synthetic processing/rebalance evidence for the ordered
rebalance path. The workload uses prebuilt `RadarEventBatch` values, so it
excludes decompression, Archive Two scanning, identity normalization, batch
construction, and archive producer timing. This makes the row useful as a
processing/rebalance bottleneck complement to the archive-producer-dominated
full-cache matrices from milestone 021.

## Build

```text
dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Focused Verification

Debug:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmarkTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

result:
  48 passed, 0 failed, 0 skipped
```

Release:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmarkTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

result:
  48 passed, 0 failed, 0 skipped
```

## Workload

```text
command surface:
  processing benchmark rebalance-synthetic

workload:
  long-mixed-skipped-reasons

mode:
  ordered-rebalance

execution:
  async shard transport
  workers 4
  worker queue capacity 8

iterations:
  2_000 measured
  1 warmup
```

Per-iteration shape:

```text
batches: 16
stream events: 96
payload values: 96
raw value checksum: 1_240
topology versions: 2
```

Measured aggregate correctness:

```text
rebalance evaluations: 32_000
accepted moves: 2_000
direct hot relief moves: 2_000
cold evacuation moves: 0
failed migrations: 0
validation: succeeded
validation checksum:
  18_341_822_938_456_978_981
```

## Active=4 Ordered Rebalance

Command:

```text
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll
  processing benchmark rebalance-synthetic
  --workload long-mixed-skipped-reasons
  --mode ordered-rebalance
  --execution async
  --workers 4
  --queue-capacity 8
  --active-batches 4
  --iterations 2000
  --warmup-iterations 1
  --validation-profile benchmark
```

Result:

```text
elapsed ms: 1_448.88
batches/s: 22_086.03
stream events/s: 132_516.15
payload values/s: 132_516.15
rebalance evaluations/s: 22_086.03
allocated bytes: 802_514_048
allocated bytes / rebalance evaluation: 25_078.56

worker dispatched batches: 39_292
worker completed batches: 39_292
worker failed batches: 0
worker submitted items: 78_584
worker accepted items: 78_584
worker completed items: 78_584
worker succeeded items: 78_584
worker failed items: 0
```

## Active=1 Same-Path Baseline

Command:

```text
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll
  processing benchmark rebalance-synthetic
  --workload long-mixed-skipped-reasons
  --mode ordered-rebalance
  --execution async
  --workers 4
  --queue-capacity 8
  --active-batches 1
  --iterations 2000
  --warmup-iterations 1
  --validation-profile benchmark
```

Result:

```text
elapsed ms: 1_626.53
batches/s: 19_673.76
stream events/s: 118_042.58
payload values/s: 118_042.58
rebalance evaluations/s: 19_673.76
allocated bytes: 705_853_752
allocated bytes / rebalance evaluation: 22_057.93

worker dispatched batches: 32_000
worker completed batches: 32_000
worker failed batches: 0
worker submitted items: 64_000
worker accepted items: 64_000
worker completed items: 64_000
worker succeeded items: 64_000
worker failed items: 0
```

## Interpretation

```text
active=4 elapsed ratio versus active=1:
  0.891x

active=4 allocation ratio versus active=1:
  1.137x

active=4 worker dispatch count versus logical batch count:
  39_292 dispatched for 32_000 logical batches

reason:
  stale topology recompute is doing real work. Earlier ordered rebalance
  commits can migrate topology, so later active deltas that were computed
  against the old topology are discarded and recomputed before commit.
```

The matrix supports the main milestone 022 safety claim:

```text
ordered active rebalance can preserve accepted move counts, topology-version
progression, validation checksum, zero migration failures, and zero worker
failures while overlapping processing-delta compute
```

It also carries a performance warning:

```text
active-batch overlap can improve elapsed time on processing/rebalance-shaped
workloads, but topology churn can increase worker dispatches and allocation
through stale-delta recompute. Broader promotion should consider workload
mix, topology churn rate, and repeated variance evidence.
```

## Implementation Note

During this matrix, active=4 initially exposed a worker mailbox telemetry race:

```text
pendingWorkItemCount ('-1') must be a non-negative value
```

The root cause was mailbox `pendingCount` being incremented after publishing
the item to the channel, allowing a fast worker to dequeue and decrement
before the writer incremented. Slice 5 fixed this by reserving pending count
before `TryWrite` and rolling back on failed write. Focused Debug and Release
tests now cover async ordered rebalance with active capacity above worker
count.
