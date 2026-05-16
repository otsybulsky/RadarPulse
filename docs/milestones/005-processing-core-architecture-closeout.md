# Milestone 005: Closeout

## Status

Milestone 005 is complete.

The milestone produced the first RadarPulse processing core over the milestone
004 normalized stream. The implemented core consumes `RadarEventBatch` directly,
keeps source-local state dense and shard-owned, supports sequential and static
partitioned execution, exposes processing telemetry and validation, and has a
processing-only benchmark boundary that is measured separately from replay
construction.

## Final Outcome

Implemented:

- `RadarProcessingCore` as the processing boundary over `RadarEventBatch`.
- Explicit processing options, execution modes, validation result, processing
  result, metrics, and optional partitioned telemetry.
- Static `SourceId -> PartitionId -> ShardId` topology with contiguous source
  blocks.
- Dense source-local state indexed directly by `SourceId`.
- Processing payload reader helpers for 8-bit and 16-bit big-endian payloads.
- Sequential processing mode as the correctness baseline.
- Synchronous `PartitionedBarrier` execution mode for static routing and shard
  ownership validation.
- Partition and shard telemetry for routed batches.
- Processing-output validation helpers outside the hot path.
- Source-local handler slots with dense `long`/`double` storage and declared
  snapshot fields.
- Synthetic processing-only workload and benchmark harness over prebuilt
  deterministic `RadarEventBatch` values.
- `processing benchmark synthetic` CLI command.
- Decision trace for the core architecture and performance choices.

## Completion Checklist

```text
[x] processing core consumes RadarEventBatch directly
[x] static SourceId -> PartitionId -> ShardId topology is implemented
[x] dense source-local state is implemented
[x] source-local handler slot model is implemented
[x] sequential processing mode is implemented and tested
[x] partitioned completion-barrier mode is implemented and tested
[x] leased batch lifetime is preserved by the processing boundary
[x] owned and leased-equivalent inputs produce matching results
[x] sequential and partitioned modes produce matching deterministic metrics
[x] processing telemetry reports aggregate, shard, and partition load
[x] processing validation catches missing/duplicate/out-of-order work
[x] processing-only benchmark excludes replay construction work
[x] CLI benchmark command can manually exercise the core
[x] decision trace is written
[x] closeout is written
[x] handoff identifies milestone 006 as partition-level shard rebalance
```

## Final Verification

Last verified commands:

```powershell
dotnet test --no-restore
dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode sequential --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 1 --shards 1 --handlers none --iterations 20 --warmup-iterations 3
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode partitioned --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 24 --shards 24 --handlers none --iterations 20 --warmup-iterations 3
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode sequential --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 1 --shards 1 --handlers counter-checksum --iterations 20 --warmup-iterations 3
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode partitioned --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 24 --shards 24 --handlers counter-checksum --iterations 20 --warmup-iterations 3
```

Last verified results:

```text
tests: 234 passed, 3 skipped
release build: 0 warnings, 0 errors

processing-only synthetic workload:
  sources: 32_400
  stream events per iteration: 32_400
  payload values per stream event: 1_196
  payload values per iteration: 38_750_400
  measured iterations: 20
  warmup iterations: 3

mode              handlers          payload values/s   stream events/s   allocated bytes / payload value
sequential        none              2_559_218_888.23   2_139_815.12      0.00
partitioned 24/24 none              2_622_669_443.85   2_192_867.43      0.03
sequential        counter-checksum  1_630_968_124.27   1_363_685.72      0.03
partitioned 24/24 counter-checksum  1_745_635_000.27   1_459_561.04      0.06
```

The skipped tests are opt-in live AWS integration tests and the opt-in local
corpus validation test.

## Baseline Comparison

Milestone 005 measures processing over already-built `RadarEventBatch` values.
The comparable denominator is payload values/s, matching the milestone 004
normalized stream comparison metric.

```text
milestone 004 single-file normalized stream:
  553_123_110.90 payload values/s

milestone 004 cache-wide normalized stream:
  509_716_417.97 payload values/s
```

Processing-only throughput versus milestone 004 single-file:

```text
sequential none:              4.63x
partitioned 24/24 none:       4.74x
sequential counter-checksum:  2.95x
partitioned counter-checksum: 3.16x
```

Processing-only throughput versus milestone 004 cache-wide:

```text
sequential none:              5.02x
partitioned 24/24 none:       5.15x
sequential counter-checksum:  3.20x
partitioned counter-checksum: 3.42x
```

This confirms that the first processing core is not the current throughput
bottleneck relative to normalized stream construction.

## Deferred Work

The following are intentionally left to later milestones:

- Live shard rebalance.
- Partition migration and source-state transfer.
- Real multi-core worker execution behind the partitioned mode.
- Retained async transport beyond the synchronous batch lifetime boundary.
- File-backed processing benchmarks that report replay construction and
  processing timing separately.
- Complex radar algorithms on top of the handler/state model.
- Routing-buffer allocation reduction if it becomes a measured bottleneck.

Known remaining performance debt:

```text
current PartitionedBarrier is synchronous and measures routing/barrier/shard
loop cost, not worker scaling

partitioned routing allocation remains visible:
  none handlers:             40.33 allocated bytes / stream event
  counter-checksum handlers: 72.33 allocated bytes / stream event
```

## Next Milestone Input

Milestone 006 should start from this stable processing-core baseline:

```text
RadarEventBatch
  -> static SourceId -> PartitionId -> ShardId ownership
  -> dense source-local state
  -> source-local handler slots
  -> sequential correctness reference
  -> synchronous partitioned barrier baseline
  -> processing telemetry and validation
  -> processing-only benchmark contour
```

The next milestone should focus on partition-level shard rebalance while
preserving the milestone 004 stream contract and the milestone 005 dense
source-local state ownership model.
