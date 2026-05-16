# Milestone 006: Closeout

## Status

Milestone 006 is complete.

The milestone produced a cautious, synchronous, partition-level shard rebalance
foundation over the milestone 005 processing core. RadarPulse can now observe
windowed shard pressure, choose bounded pressure-relief moves, publish
versioned partition ownership, validate state handoff, explain skipped moves,
and measure rebalance behavior on synthetic and real Archive Two data.

Milestone 006 deliberately keeps the first correctness boundary synchronous:
one `RadarEventBatch` is processed against one topology snapshot, and accepted
topology changes are published only between batch/barrier calls. Retained async
worker transport, source-level migration, partition splitting, live ingestion,
and complex radar algorithms remain outside the milestone.

## Final Outcome

Implemented:

- Versioned `PartitionId -> ShardId` topology with immutable snapshots and
  monotonic topology versions.
- Stable `SourceId -> PartitionId` mapping across partition owner moves.
- `RadarProcessingTopologyManager` as the topology publication boundary.
- Route, telemetry, result, pressure sample, decision, migration, and
  validation surfaces that carry topology version context.
- Pressure samples and rolling pressure windows with minimum sample counts,
  deterministic pressure scores, and hysteresis.
- Anti-churn policy state with logical evaluation sequence, partition
  residency, partition/shard cooldowns, global/source/target move budgets,
  projected-benefit gates, and target-headroom gates.
- Rebalance decision telemetry for accepted moves, no-action decisions, and
  rejected candidates with explicit skipped reasons.
- Direct hot-partition relief planner.
- Intrinsic-hot and quarantine classification state.
- Cold-partition evacuation fallback when direct hot movement is unsafe.
- Synchronous migration coordinator for accepted partition moves.
- State handoff validation for partition-owned source-state summaries.
- Rebalance-aware processing session that processes a batch, samples pressure,
  evaluates rebalance, validates handoff, publishes accepted migration, records
  policy state, and lets the next batch use the latest topology version.
- Rebalance validation helpers for topology, routes, telemetry, pressure
  samples, session decisions, migration results, and state handoff diagnostics.
- Deterministic synthetic rebalance workload catalog:
  `Balanced`, `SustainedHotShard`, `IntrinsicHotPartition`,
  `OscillatingSpike`, and `CooldownStorm`.
- Synthetic rebalance benchmark modes:
  `StaticNoRebalance`, `PressureSamplingOnly`, and `RebalanceSession`.
- `processing benchmark rebalance-synthetic` CLI command.
- `processing benchmark rebalance-archive` CLI command for single-file and
  cache-wide real-data benchmark contours.
- Release benchmark capture, real-data smoke capture, cache-wide real-data
  capture, decision trace, and closeout documentation.

## Completion Checklist

```text
[x] versioned PartitionId -> ShardId topology is implemented and tested
[x] topology snapshots are immutable and monotonic
[x] routing and telemetry record the topology version used
[x] one batch is processed against one topology snapshot
[x] pressure samples are derived from partitioned telemetry
[x] pressure windowing and hysteresis are implemented and tested
[x] anti-churn policy supports cooldown, residency, budgets, and benefit gates
[x] direct hot relief is implemented and tested
[x] intrinsic hot partition classification is implemented and tested
[x] cold evacuation fallback is implemented and tested
[x] migration coordinator publishes topology N+1 between barriers
[x] state handoff validation preserves source and handler state summaries
[x] rebalance validation catches topology, route, and handoff errors
[x] synthetic workloads cover balanced, hot, intrinsic-hot, and oscillating cases
[x] processing-only rebalance benchmark reports overhead and decisions
[x] Release rebalance benchmark numbers are captured before closeout
[x] Release benchmark results are compared with milestone 005 processing baselines
[x] Release benchmark results include same-run static no-rebalance comparison
[x] CLI smoke or benchmark command can manually exercise rebalance
[x] decision trace is written
[x] closeout is written
[x] handoff identifies the next milestone input
```

## Final Verification

Recorded verification during milestone 006:

```powershell
dotnet test RadarPulse.sln --no-restore
dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore
```

Recorded verification results:

```text
full solution after slice 16:
  355 tests passed, 3 skipped

processing-focused tests after Release benchmark capture:
  210 tests passed

CLI rebalance benchmark tests after real-data command integration:
  5 tests passed

Release CLI build:
  0 warnings, 0 errors
```

The skipped tests are opt-in live/corpus-style tests, consistent with earlier
milestone handoffs.

## Benchmark Commands

Synthetic Release capture:

```powershell
dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload all --mode all --iterations 10000 --warmup-iterations 1000
```

Single-file real-data smoke capture:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode all --partitions 24 --shards 4 --iterations 3 --warmup-iterations 1 --parallelism 1
```

Comparable parallel real-data rerun:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --mode rebalance --partitions 24 --shards 4 --iterations 3 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Cache-wide real-data capture:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode all --partitions 24 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse
```

## Synthetic Benchmark Result

The milestone 006 synthetic rebalance catalog is intentionally a tiny
behavioral contour, not a large throughput contour. It uses `8-20` payload
values per iteration, while the milestone 005 processing-only baseline used
`38_750_400` payload values per iteration. Same-run static ratios are therefore
the primary overhead signal. The milestone 005 ratios are retained only as a
diagnostic comparison.

Milestone 005 comparison baseline:

```text
partitioned 24/24 none:
  payload values/s: 2_622_669_443.85
  allocated bytes / payload value: 0.03
```

Captured Release synthetic results:

```text
workload        mode       topo versions  accepted moves  skipped decisions  payload values/s  alloc bytes/event  vs static  vs 005 baseline
balanced        static     1              0               0                  1_086_338.08      667.00             100.0%     0.0414%
balanced        sampling   1              0               0                    852_609.41    1_008.00              78.5%     0.0325%
balanced        rebalance  1              0              40_000                634_847.07    1_624.06              58.4%     0.0242%
hot-shard       static     1              0               0                  1_899_984.80      443.33             100.0%     0.0724%
hot-shard       sampling   1              0               0                  1_166_343.98      670.67              61.4%     0.0445%
hot-shard       rebalance  2             10_000          20_000                788_605.70    1_322.52              41.5%     0.0301%
intrinsic-hot   static     1              0               0                  2_642_682.85      352.01             100.0%     0.1008%
intrinsic-hot   sampling   1              0               0                  2_201_177.87      512.00              83.3%     0.0839%
intrinsic-hot   rebalance  2             10_000          10_000                675_789.32    1_642.48              25.6%     0.0258%
oscillating     static     1              0               0                  2_797_429.72      382.80             100.0%     0.1067%
oscillating     sampling   1              0               0                  2_375_452.08      583.60              84.9%     0.0906%
oscillating     rebalance  1              0              40_000              2_733_173.90      796.83              97.7%     0.1042%
cooldown-storm  static     1              0               0                  5_077_044.14      445.33             100.0%     0.1936%
cooldown-storm  sampling   1              0               0                  5_660_404.06      672.67             111.5%     0.2158%
cooldown-storm  rebalance  2             10_000          20_000                823_981.71    1_907.32              16.2%     0.0314%
```

Captured synthetic behavior:

```text
balanced:
  no accepted moves; skipped by no-hot-shard

hot-shard:
  direct-hot-relief source 6.00->2.00,
  target 0.00->4.00,
  relief 2.00

intrinsic-hot:
  rejected direct hot movement through no-safe-target and intrinsic-hot gates
  cold-evacuation source 9.00->8.00,
  target 0.00->1.00,
  relief 1.00

oscillating:
  no accepted moves under short spikes

cooldown-storm:
  direct-hot-relief accepted, then cooldown and budget gates block churn
```

All captured synthetic rows reported successful validation and zero failed
migrations.

## Real-Data Benchmark Result

Single-file real-data shape:

```text
file: KTLX20260504_000245_V06
compressed records: 55
decompressed bytes: 50_741_824
batches: 1
stream events: 32_400
payload values: 38_759_040
topology: 24 partitions / 4 shards
```

Single-file real-data smoke results with archive `--parallelism 1`:

```text
mode       topology versions  evaluations  accepted moves  skipped decisions  callback payload values/s  end-to-end payload values/s  alloc bytes/payload value
static     1                  0            0               0                  2_589_754_314.69           92_333_354.54              0.06
sampling   1                  3            0               0                  2_990_889_752.58           92_347_294.79              0.06
rebalance  2                  3            3               0                  3_061_858_015.59           92_350_954.71              0.06
```

Accepted pressure projection:

```text
direct-hot-relief source 51_868.80->42_837.12,
target 0.00->9_031.68,
relief 9_031.68
```

The `92M` end-to-end value is a single-thread archive replay contour, not a
rebalance regression. A comparable `--parallelism 24` real-data rerun on
`KTLX20260504_002334_V06` produced:

```text
command/result                                      end-to-end payload values/s
archive benchmark stream                            430_859_940.37
processing benchmark rebalance-archive sampling     458_420_311.03
processing benchmark rebalance-archive rebalance    449_250_477.25
```

Cache-wide real-data shape:

```text
examined files: 244
skipped files: 24
published Archive Two base-data files: 220
compressed bytes: 1_330_634_309
decompressed bytes: 11_145_331_584
batches: 220
stream events: 7_114_560
payload values: 8_513_587_200
```

Cache-wide real-data results:

```text
mode       topology versions  evaluations  accepted moves  skipped decisions  callback payload values/s  end-to-end payload values/s  alloc bytes/payload value
static     1                  0            0               0                  2_796_597_485.46           355_001_379.25             0.24
sampling   1                  220          0               0                  2_735_817_941.09           385_154_964.58             0.23
rebalance  2                  220          2               436                2_680_685_752.29           380_667_655.66             0.23
```

Cache-wide accepted pressure projections:

```text
direct-hot-relief source 51_868.80->42_837.12,
target 0.00->9_031.68,
relief 9_031.68

direct-hot-relief source 43_966.08->35_038.08,
target 0.00->8_928.00,
relief 8_928.00
```

Cache-wide skipped reasons:

```text
global-move-budget-exhausted
source-shard-move-budget-exhausted
no-cold-target-shard
no-hot-shard
```

All real-data rows reported successful validation and zero failed migrations.

## Performance Assessment

Milestone 006 is successful as a correctness, cautious-rebalance, and
real-data validation milestone.

The important signals are:

```text
correctness:
  synthetic, single-file real-data, parallel real-data, and cache-wide real-data
  runs all validated successfully with zero failed migrations

cautious behavior:
  cache-wide rebalance accepted only 2 direct-hot-relief moves across 220 real
  batches, then policy gates blocked further churn

pressure relief:
  accepted real-data moves reduced source pressure while keeping target
  pressure well below the source pressure after the move

processing callback comparison:
  cache-wide rebalance callback throughput was 2_680_685_752.29 payload
  values/s, about 102.2% of the milestone 005 partitioned/no-handler
  processing-only baseline of 2_622_669_443.85 payload values/s

end-to-end interpretation:
  archive end-to-end numbers are replay dominated and are not directly
  comparable to milestone 005 processing-only numbers

known cost:
  cache-wide real-data allocation is about 0.23 bytes/payload value versus
  0.03 in the milestone 005 processing-only synthetic baseline
```

The allocation delta is production-hardening input, not a blocker for closing
milestone 006. The milestone question was whether cautious, synchronous
partition-level rebalance can preserve correctness, explain decisions, avoid
storms, and work over real leased `RadarEventBatch` streams. The captured data
answers that question yes.

## Deferred Work

The following are intentionally left to later milestones:

- Retained async worker queues and real multi-core shard execution.
- Timer-driven or production scheduler integration beyond the logical
  evaluation sequence.
- Automatic quarantine lifecycle integration: TTL, sustained cooled-sample
  reset, or downgrade when pressure changes enough that retrying is safe.
- Partition splitting or repartitioning for intrinsically hot partitions.
- Source-level migration.
- Long-running production telemetry retention and sampling policy.
- Cache-wide allocation profiling and reduction.
- Repeated cache-wide benchmark runs and longer multi-radar scenarios.
- Policy tuning from broader real-data contours.
- Live ingestion and durable broker integration.
- Complex radar algorithms on top of the source-local handler model.

## Next Milestone Input

The next milestone should start from this closed 006 baseline:

```text
RadarEventBatch
  -> synchronous processing against one topology snapshot
  -> partitioned telemetry with topology version
  -> windowed pressure sample
  -> cautious direct hot relief or cold evacuation
  -> validated migration between batches
  -> topology version N+1 for the next batch
  -> skipped-reason telemetry for every no-move path
```

Recommended next focus areas:

- Decide whether milestone 007 is production hardening for rebalance
  allocation/telemetry/quarantine lifecycle, or the first real async worker
  transport over the now-validated topology boundary.
- Keep the milestone 006 rebalance controller conservative until repeated
  cache-wide and multi-radar runs justify broader policy behavior.
- Preserve the synchronous correctness model as the reference path even if a
  later worker scheduler is added.
- Treat partition splitting as a separate design problem for intrinsically hot
  partitions, not as a retroactive milestone 006 change.
