# Milestone 007: Closeout

## Status

Milestone 007 is complete.

The milestone hardened the synchronous milestone 006 rebalance control plane
before retained async worker transport. RadarPulse can now run cautious
partition-level rebalance with bounded telemetry retention, explicit validation
profiles, quarantine lifecycle recovery, allocation attribution, archive
pressure-skew stress contours, and a final performance gate against the
accepted milestone 005 and milestone 006 baselines.

The synchronous `PartitionedBarrier` path remains the reference correctness
boundary: one leased `RadarEventBatch` is processed inside the publish
callback against one topology snapshot, then a validated accepted migration may
publish topology version `N+1` before the next batch.

## Final Outcome

Implemented:

- Hardening option contracts for telemetry retention, quarantine lifecycle, and
  validation profiles.
- Stable validation profiles: `off`, `essential`, `diagnostic`, and
  `benchmark`.
- Bounded telemetry contracts and recorder support for aggregate counters plus
  capped recent decisions, lifecycle transitions, accepted moves, and
  validation failures.
- Counters-only, recent-detail, and diagnostic retention modes.
- Quarantine lifecycle state, transition evidence, retry eligibility, sustained
  cooling clear, material pressure-change retry, and re-entry behavior.
- Lifecycle-aware direct hot relief and cold evacuation planning.
- Rebalance session result surfaces for hardening telemetry and retention
  stats.
- Allocation attribution for processing-only and archive rebalance benchmark
  contours, including processing-callback allocation separated from archive
  replay allocation.
- Allocation-reduction passes for no-move, skipped-only, and capped-detail
  paths.
- Synthetic quarantine lifecycle workloads and long-running retention stress
  workloads.
- Archive benchmark controls for retention mode, validation profile, quarantine
  lifecycle options, skipped-reason counters, retained/dropped detail counts,
  and pressure skew.
- Benchmark-only pressure skew overlay for archive runs. The overlay changes
  effective pressure samples used by planning while preserving archive payload
  and observed processing telemetry.
- CLI options and output for validation, retention, quarantine lifecycle, and
  skew settings.
- Policy default audit, decision trace, final performance gate, closeout, and
  handoff update.

Not implemented:

- Retained async worker queues.
- Physical worker-local state transfer.
- Source-level migration.
- Multi-core shard execution runtime.
- Partition splitting for intrinsically hot partitions.

## Completion Checklist

```text
[x] hardening options and profiles are implemented and tested
[x] bounded telemetry contracts are implemented and tested
[x] telemetry recorder retains counters and bounded recent detail
[x] quarantine lifecycle state and transitions are implemented and tested
[x] lifecycle evaluator advances quarantine before planning
[x] direct hot relief and cold evacuation honor effective classification
[x] rebalance session exposes hardening telemetry and retention stats
[x] validation profiles are implemented and tested
[x] allocation attribution is reported by benchmark contours
[x] avoidable control-plane allocation is reduced or explicitly justified
[x] lifecycle synthetic workloads are implemented and tested
[x] retention stress workloads are implemented and tested
[x] synthetic and archive benchmark harnesses expose hardening fields
[x] CLI exposes validation, retention, skew, and quarantine options
[x] policy defaults are audited against workloads and real-data contours
[x] decision trace is written
[x] closeout is written
[x] handoff is updated
[x] final comprehensive performance comparison is captured and interpreted
```

## Final Verification

Latest implementation verification captured during milestone 007:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~Processing|FullyQualifiedName~Presentation"
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
dotnet build RadarPulse.sln --configuration Release --no-restore
```

Recorded results:

```text
30 passed for focused CLI/synthetic/allocation coverage.
338 passed for processing/presentation-focused coverage.
481 passed, 3 skipped for the full test suite.
Release build succeeded with 0 warnings and 0 errors.
```

Final performance-gate build:

```powershell
dotnet build RadarPulse.sln --configuration Release --no-restore
```

Result:

```text
Build succeeded with 0 warnings and 0 errors.
```

The final closeout slice is documentation and benchmark capture only; no code
changed after the latest full test suite.

## Benchmark Commands

Synthetic Release capture:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload all --mode all --iterations 10000 --warmup-iterations 1000
```

Synthetic validation profile comparison:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload long-mixed-skipped-reasons --mode rebalance --validation-profile off --iterations 10000 --warmup-iterations 1000
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload long-mixed-skipped-reasons --mode rebalance --validation-profile essential --iterations 10000 --warmup-iterations 1000
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload long-mixed-skipped-reasons --mode rebalance --validation-profile diagnostic --iterations 10000 --warmup-iterations 1000
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload long-mixed-skipped-reasons --mode rebalance --validation-profile benchmark --iterations 10000 --warmup-iterations 1000
```

Single-file real-data hardening run:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode all --partitions 24 --shards 4 --iterations 3 --warmup-iterations 1 --parallelism 1 --decompressor radarpulse
```

Comparable parallel real-data rerun:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive benchmark stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --iterations 3 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --mode all --partitions 24 --shards 4 --iterations 3 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Cache-wide no-skew baseline:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode all --partitions 24 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse
```

Cache-wide validation profile sweep with counters-only retention:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --partitions 24 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse --retention-mode counters --validation-profile off
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --partitions 24 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse --retention-mode counters --validation-profile essential
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --partitions 24 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse --retention-mode counters --validation-profile diagnostic
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --partitions 24 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse --retention-mode counters --validation-profile benchmark
```

Cache-wide active rebalance pressure-skew stress:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --partitions 96 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse --retention-mode counters --skew-profile hot-shard
```

## Baselines

Milestone 005 processing-only baseline:

```text
mode                                  payload values/s     stream events/s    allocated bytes / payload value
partitioned 24/24 none                2_622_669_443.85     2_192_867.43       0.03
partitioned 24/24 counter-checksum    1_745_635_000.27     1_459_561.04       0.06
```

Milestone 006 cache-wide accepted baseline:

```text
mode       callback payload values/s    end-to-end payload values/s    alloc bytes/payload
static     2_796_597_485.46             355_001_379.25                 0.24
sampling   2_735_817_941.09             385_154_964.58                 0.23
rebalance  2_680_685_752.29             380_667_655.66                 0.23
```

The milestone 006 synthetic catalog remains a tiny behavioral contour. It uses
`8-20` payload values per iteration for the original workloads, while milestone
005 used `38_750_400` payload values per iteration. Same-run static ratios are
therefore the meaningful synthetic overhead signal.

## Synthetic Results

Milestone 007 reran the original milestone 006 workloads under default
hardening settings: validation `diagnostic`, retention `recent`, quarantine
TTL `64`, sustained cooling samples `3`, and material pressure change `0.25`.

```text
workload        static pv/s     sampling pv/s   rebalance pv/s  007 rebalance/static  006 rebalance/static  accepted  skipped  alloc bytes/payload
balanced        1_136_977.35    818_608.61      424_038.08      37.3%                 58.4%                 0         40_000   2_676.16
hot-shard       1_407_228.46    978_290.11      497_280.91      35.3%                 41.5%                 10_000    20_000   1_980.69
intrinsic-hot   3_801_285.68    2_669_237.04    619_097.51      16.3%                 25.6%                 10_000    10_000   2_405.31
oscillating     7_753_109.97    3_558_041.44    1_503_053.08    19.4%                 97.7%                 0         40_000   1_308.89
cooldown-storm  5_978_268.99    3_235_076.86    679_332.06      11.4%                 16.2%                 10_000    20_000   2_668.07
```

All rows validated successfully and reported zero failed migrations.

The lower same-run ratios are expected for these tiny behavioral contours:
milestone 007 now records bounded telemetry, lifecycle state, validation
profile state, skipped-reason counters, and retained/dropped detail accounting.
Those fixed control-plane costs dominate a workload with only a few payload
values per iteration. This is not the large-data throughput signal; the archive
callback rows below are the comparable throughput contour.

Milestone 007 quarantine lifecycle workloads:

```text
workload                              accepted  skipped  payload values/s  rebalance/static  alloc bytes/payload  primary behavior
quarantine-ttl-retry                  0         40_000   720_612.40        15.4%             3_745.45             TTL retry path remains bounded
quarantine-cooling-clear              0         60_000   467_604.73        12.8%             5_725.78             sustained cooling clear path is deterministic
quarantine-pressure-change-retry      0         40_000   922_112.25        16.0%             2_922.02             material pressure-change retry is deterministic
quarantine-retry-reentry              0         40_000   932_010.60        13.2%             2_678.92             retry can re-enter quarantine
quarantine-successful-relief-clear    10_000    20_000   932_729.96        14.3%             2_430.78             successful relief clears quarantine state
```

Milestone 007 retention stress workloads:

```text
workload                       retention  accepted  skipped   payload values/s  rebalance/static  alloc bytes/payload
long-no-hot-shard              recent     0         320_000   2_437_740.49      38.6%             2_075.13
long-cooldown-rejection        recent     10_000    300_000   1_778_858.27      18.9%             2_729.61
long-unsafe-target-rejection   recent     0         320_000   3_233_971.25      32.0%             1_569.81
long-mixed-skipped-reasons     recent     10_000    300_000   2_486_517.12      31.8%             2_026.03
counters-only-retention        counters   0         320_000   3_025_811.59      43.1%             1_798.37
```

Counters-only retention preserved skipped-decision counters while dropping all
decision and accepted-move detail. On the no-hot stress shape it also lowered
allocation from `2_075.13` to `1_798.37` bytes/payload compared with the
recent-detail contour.

Synthetic validation profile comparison on `long-mixed-skipped-reasons`:

```text
profile      accepted  skipped   payload values/s  alloc bytes/payload  alloc bytes/evaluation
off          10_000    300_000   1_386_635.81      2_010.95             12_065.69
essential    10_000    300_000   1_284_481.47      2_010.81             12_064.85
diagnostic   10_000    300_000   1_354_668.99      2_017.75             12_106.48
benchmark    10_000    300_000   1_413_402.80      2_018.00             12_107.99
```

The synthetic profile sweep is useful for behavior and allocation shape, not
for precise throughput ordering. The small workload is noisy; all profiles
preserved the same accepted/skipped behavior and zero failed migrations.

## Real-Data Result

Single-file real-data shape:

```text
file: KTLX20260504_000245_V06
compressed records: 55
decompressed bytes: 50_741_824
batches: 1
stream events: 32_400
payload values: 38_759_040
topology: 24 partitions / 4 shards
parallelism: 1
```

Single-file hardening result compared with the accepted milestone 006 smoke:

```text
mode       006 callback pv/s    007 callback pv/s    007 end-to-end pv/s    accepted  skipped  callback alloc bytes/payload
static     2_589_754_314.69     2_722_263_271.01     91_898_381.65         0         0        0.03
sampling   2_990_889_752.58     3_143_457_456.30     92_766_898.73         0         0        0.03
rebalance  3_061_858_015.59     3_027_734_610.98     96_117_605.63         3         0        0.03
```

All single-file rows used validation `diagnostic`, retention `recent`, skew
`none`, reported successful validation, and reported zero failed migrations.
End-to-end allocation stayed `0.06` bytes/payload, matching the milestone 006
single-file shape, while callback allocation is now attributed separately at
`0.03` bytes/payload.

Comparable parallel real-data shape:

```text
file: KTLX20260504_002334_V06
compressed records: 55
decompressed bytes: 50_741_824
batches: 1
stream events: 32_400
payload values: 38_759_040
parallelism: 24
```

Comparable parallel result:

```text
command/result                                  006 end-to-end pv/s  007 end-to-end pv/s  007 callback pv/s    callback alloc bytes/payload
archive benchmark stream                        430_859_940.37       345_139_916.03       n/a                 n/a
rebalance-archive sampling                      458_420_311.03       450_596_741.58       3_199_022_771.73    0.03
rebalance-archive rebalance                     449_250_477.25       464_813_788.06       3_175_495_534.86    0.03
```

The `archive benchmark stream` row measures archive replay and batch
construction only. It is replay dominated and moved lower in this particular
run, but the rebalance archive rows remained in the accepted range and the
processing callback stayed above `3.17B` payload values/s.

Cache-wide real-data shape:

```text
cache root: data\nexrad
examined files: 244
skipped files: 24
published Archive Two base-data files: 220
compressed bytes: 1_330_634_309
decompressed bytes: 11_145_331_584
batches: 220
stream events: 7_114_560
payload values: 8_513_587_200
topology: 24 partitions / 4 shards
parallelism: 24
skew: none
```

Cache-wide no-skew default-hardening result:

```text
mode       006 callback pv/s    007 callback pv/s    007 vs 006  007 end-to-end pv/s  end-to-end alloc  callback alloc  accepted  skipped  retained/dropped decisions
static     2_796_597_485.46     3_285_712_201.65     117.5%      452_424_134.47       0.23              0.03            0         0        0/0
sampling   2_735_817_941.09     3_459_530_731.77     126.5%      467_960_949.06       0.23              0.03            0         0        0/0
rebalance  2_680_685_752.29     3_359_032_002.66     125.3%      464_305_723.92       0.23              0.03            2         436      128/310
```

Cache-wide rebalance skipped-reason counters:

```text
no-hot-shard=420
no-cold-target-shard=4
source-shard-move-budget-exhausted=12
global-move-budget-exhausted=12
```

The cache-wide no-skew rebalance row is the primary milestone 007 throughput
comparison. It preserved the conservative milestone 006 behavior: only `2`
direct-hot-relief moves across `220` real batches, with the remaining decisions
explained by policy gates and no failed migrations.

Compared with milestone 005, the 007 cache-wide rebalance callback throughput
of `3_359_032_002.66` payload values/s is:

```text
128.1% of milestone 005 partitioned 24/24 none
192.4% of milestone 005 partitioned 24/24 counter-checksum
```

Archive end-to-end throughput is not compared with milestone 005 processing
only numbers because archive replay, decompression, identity normalization, and
batch construction are outside the processing callback.

## Retention And Validation Contours

Cache-wide validation profile sweep used no skew, `24` partitions, `4` shards,
and counters-only retention so retained detail could not dominate the profile
comparison.

```text
profile      callback pv/s        end-to-end pv/s      accepted  skipped  retained/dropped decisions  retained/dropped moves  callback alloc bytes/payload
off          3_194_905_675.91     443_118_051.58      2         436      0/438                       0/2                     0.03
essential    3_206_831_567.98     448_528_127.36      2         436      0/438                       0/2                     0.03
diagnostic   3_202_543_260.13     451_971_222.02      2         436      0/438                       0/2                     0.03
benchmark    3_145_430_918.14     448_549_331.72      2         436      0/438                       0/2                     0.03
```

All four profile rows produced the same accepted move count, skipped decision
count, skipped-reason counters, validation checksum, successful validation
status, and zero failed migrations. The `benchmark` row was lower than the
other profile rows but remained above both milestone 006 cache-wide rebalance
and milestone 005 processing-only baselines.

The retention comparison is accepted:

```text
recent detail:
  default cache-wide rebalance retained 128 decisions, dropped 310 decision
  details, retained 2 accepted moves, and dropped 0 accepted move details

counters-only:
  validation sweep retained 0 decisions, dropped 438 decision details,
  retained 0 accepted moves, and dropped 2 accepted move details
```

This proves telemetry retention is bounded. Counters preserve totals and
skipped-reason diagnostics when detail retention is disabled.

## Pressure-Skew Stress

Pressure skew is not baseline performance evidence. It is an explicit stress
contour over real archive replay with synthetic effective pressure samples.

Cache-wide hot-shard skew stress:

```text
cache root: data\nexrad
topology: 96 partitions / 4 shards
retention: counters
validation: diagnostic
synthetic pressure overlay: yes
skew profile: hot-shard
skew factor: 1.00
skew period: 8
payload values: 8_513_587_200
rebalance evaluations: 220
accepted moves: 20
skipped decisions: 400
failed migrations: 0
validation: succeeded
processing callback payload values/s: 3_237_706_036.80
end-to-end payload values/s: 448_952_545.93
callback allocated bytes / payload value: 0.04
end-to-end allocated bytes / payload value: 0.24
```

Skipped-reason counters:

```text
no-hot-shard=128
source-shard-move-budget-exhausted=272
target-shard-receive-budget-exhausted=272
global-move-budget-exhausted=272
```

The skew stress made rebalance ten times more active than the no-skew full
cache baseline (`20` accepted moves versus `2`) while preserving validation,
zero failed migrations, callback throughput above `3.23B` payload values/s,
and bounded allocation. The extra `0.01` callback bytes/payload is attributed
to the more active synthetic pressure overlay and move telemetry contour, not
to baseline real-data behavior.

## Performance Assessment

Milestone 007 passes the final performance gate.

The important signals are:

```text
correctness:
  all final synthetic and archive rows reported successful validation and zero
  failed migrations

baseline real-data throughput:
  cache-wide no-skew rebalance callback throughput improved from the milestone
  006 accepted 2.68B payload values/s to 3.36B payload values/s

processing-only comparison:
  cache-wide no-skew rebalance callback throughput is 128.1% of the milestone
  005 partitioned/no-handler processing-only baseline

allocation:
  cache-wide end-to-end allocation stayed at 0.23 bytes/payload, matching the
  milestone 006 accepted cache-wide rebalance shape, while callback allocation
  is now separately measured at 0.03 bytes/payload

telemetry retention:
  recent detail retained bounded examples and dropped excess detail; counters
  mode retained no detail and still preserved aggregate decision counts

validation profiles:
  profile selection did not change accepted moves, skipped decisions, checksum,
  or failed migration count on the cache-wide contour

policy behavior:
  no-skew real-data rebalance remained conservative, accepting only two
  direct-hot-relief moves across 220 batches

stress behavior:
  explicit hot-shard skew produced 20 accepted moves without throughput,
  validation, or allocation collapse
```

The known cost is visible in tiny synthetic workloads. Lifecycle, retention,
and skipped-decision diagnostics are fixed control-plane work, so they dominate
workloads with only a few payload values per iteration. That is acceptable
because synthetic workloads are behavioral microscopes. The large archive
callback contours show the production-shaped throughput and allocation result.

## Deferred Work

The following are intentionally left to later milestones:

- Retained async worker queues over the hardened synchronous boundary.
- Physical worker-local state transfer.
- Multi-core shard execution runtime and scheduling policy.
- Source-level migration.
- Partition splitting or repartitioning for intrinsically hot partitions.
- Durable production configuration for validation, retention, quarantine, and
  skew settings.
- Broader multi-radar and longer-running archive benchmark campaigns.
- Deep allocation profiling if a future runtime contour shows callback
  allocation above the accepted `0.03-0.04` bytes/payload range.
- Live ingestion, durable broker integration, visualization, and complex radar
  algorithms.

## Next Milestone Input

The next milestone should start from this closed reference path:

```text
leased RadarEventBatch callback
  -> synchronous processing against one topology snapshot
  -> bounded rebalance telemetry counters plus capped detail
  -> quarantine lifecycle before planning
  -> cautious direct hot relief or cold evacuation
  -> validation profile selected explicitly
  -> accepted topology change only between batches
  -> archive callback throughput and allocation reported separately from replay
```

Recommended next focus:

- Design retained async worker transport over the closed synchronous rebalance
  boundary.
- Preserve the leased batch lifetime rule. Any work that outlives the callback
  needs an explicit owned snapshot or retained payload protocol.
- Keep `SourceId -> PartitionId` stable and move only `PartitionId -> ShardId`
  ownership unless a later source-level migration milestone changes that
  contract explicitly.
- Preserve milestone 007 telemetry and validation surfaces as the correctness
  oracle while adding async scheduling.
- Keep pressure skew as a benchmark-only stress overlay; baseline real-data
  performance must keep `--skew-profile none`.
