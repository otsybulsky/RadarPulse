# Milestone 008: Closeout

## Status

Milestone 008 is complete.

The milestone added the first retained async shard worker transport over the
closed milestone 007 synchronous processing and rebalance baseline. RadarPulse
can now run processing-only synthetic, synthetic rebalance, and archive
rebalance benchmarks through retained in-process workers with bounded queues,
explicit worker lifecycle, deterministic completion, bounded worker telemetry,
and same-run synchronous versus async comparison.

The important lifetime boundary is unchanged:

```text
retained workers are allowed.
retained borrowed RadarEventBatch payload is not allowed.
```

Archive callbacks still block on the async completion barrier. That is
intentional for milestone 008 because `RadarEventBatch` payload is borrowed
from the provider callback and is safe only until that callback returns.

## Final Outcome

Implemented:

- Execution mode and async execution options:
  `RadarProcessingExecutionMode.AsyncShardTransport`,
  `RadarProcessingAsyncExecutionOptions`, worker affinity, and timeout policy.
- Worker lifecycle, health, status, failure, cancellation, timeout, start,
  stop, and dispose contracts.
- Borrowed async batch scope, work item, work completion, batch completion, and
  immutable completion summary contracts.
- Bounded worker mailbox implementation with deterministic enqueue/dequeue,
  close, dispose, cancellation, and capacity behavior.
- Retained async worker group runtime with one in-flight borrowed batch by
  default.
- Borrowed-batch guardrails through explicit async core and async rebalance
  sessions.
- Async batch dispatcher that captures one topology snapshot, routes against
  that snapshot, dispatches shard work, waits for completion, and aggregates
  deterministically.
- Completion aggregation that projects async worker output back into the
  existing processing result and telemetry shape.
- Failure, cancellation, timeout, and unhealthy worker-group semantics that do
  not pretend borrowed payload can be released while workers may still read it.
- Bounded worker telemetry summary and recorder with counters, capped recent
  details, dropped-detail counts, worker timing, and failure samples.
- Async processing core integration without breaking existing sequential or
  synchronous partitioned callers.
- Async rebalance session integration where rebalance runs only after completed
  successful async processing.
- Async validation helpers, including benchmark-profile sync versus async
  checksum comparison.
- Async execution support in processing-only synthetic benchmark, synthetic
  rebalance benchmark, and archive rebalance benchmark.
- CLI options for execution mode, worker count, queue capacity, validation, and
  worker telemetry output.
- Performance guardrail and allocation interpretation for processing-only,
  synthetic rebalance, single-file archive, and full local archive cache
  contours.
- Decision trace, closeout, and handoff update.

Not implemented:

- Owned `RadarEventBatch` snapshots or payload ownership transfer.
- Provider-level async queues that can return before processing completes.
- Multi-batch pipeline scheduling.
- Durable broker integration or live ingestion.
- Physical worker-local state transfer.
- Source-level migration.
- Partition splitting or repartitioning.
- Distributed or cross-process workers.
- Complex radar algorithms.

## Completion Checklist

```text
[x] execution mode and async options are implemented and tested
[x] worker lifecycle contracts are implemented and tested
[x] batch scope, work item, and completion contracts are implemented and tested
[x] bounded worker mailbox foundation is implemented and tested
[x] retained worker group runtime is implemented and tested
[x] borrowed batch lifetime guardrails are implemented and tested
[x] async processing dispatcher is implemented and tested
[x] deterministic aggregation and telemetry parity are implemented and tested
[x] failure, cancellation, timeout, and health semantics are implemented and tested
[x] worker telemetry contracts and recorder are implemented and tested
[x] processing core can run async execution without breaking synchronous callers
[x] rebalance session can consume async processing results safely
[x] async validation extensions are implemented and tested
[x] processing-only synthetic benchmark exposes async execution
[x] synthetic rebalance benchmark exposes async execution
[x] archive rebalance benchmark exposes async execution
[x] CLI exposes execution, worker, queue, and worker telemetry options
[x] performance guardrail and allocation pass is captured and interpreted
[x] decision trace is written
[x] closeout is written
[x] handoff is updated
[x] final comprehensive performance comparison is captured and interpreted
```

## Final Verification

Latest implementation verification captured before closeout:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"
dotnet build RadarPulse.sln --no-restore
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~Processing
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded results:

```text
32 passed for focused archive async benchmark, CLI, and allocation coverage.
Solution build succeeded with 0 warnings and 0 errors.
439 passed for processing-focused coverage.
600 passed, 3 skipped for the full test project.
```

Closeout performance spot checks:

```powershell
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark synthetic --mode partitioned --sources 256 --batches 64 --events-per-batch 4096 --payload-values 16 --partitions 24 --shards 4 --handlers counter-checksum --iterations 3 --warmup-iterations 1
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark synthetic --mode async --sources 256 --batches 64 --events-per-batch 4096 --payload-values 16 --partitions 24 --shards 4 --workers 4 --queue-capacity 1 --handlers counter-checksum --iterations 3 --warmup-iterations 1
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload hot-shard --mode rebalance --execution sync --iterations 10000 --warmup-iterations 1000
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload hot-shard --mode rebalance --execution async --workers 4 --queue-capacity 1 --iterations 10000 --warmup-iterations 1000
```

The final closeout slice is documentation plus benchmark capture. No code
changed after the latest full test suite.

## Benchmark Commands

Single-file archive smoke:

```powershell
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode rebalance --execution sync --iterations 1 --warmup-iterations 0 --parallelism 24
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode rebalance --execution async --workers 4 --queue-capacity 1 --iterations 1 --warmup-iterations 0 --parallelism 24
```

Full local KTLX cache guardrail:

```powershell
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --execution sync --iterations 1 --warmup-iterations 0 --parallelism 24
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --execution async --workers 4 --queue-capacity 1 --iterations 1 --warmup-iterations 0 --parallelism 24
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode sampling --execution sync --iterations 1 --warmup-iterations 0 --parallelism 24
dotnet src\Presentation\bin\Debug\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode sampling --execution async --workers 4 --queue-capacity 1 --iterations 1 --warmup-iterations 0 --parallelism 24
```

## Baseline Context

Historical baselines from earlier closeouts:

```text
milestone 005 processing-only Release:
  partitioned 24/24 none             2_622_669_443.85 payload values/s
  partitioned 24/24 counter-checksum 1_745_635_000.27 payload values/s

milestone 006 cache-wide Release:
  rebalance callback                 2_680_685_752.29 payload values/s

milestone 007 cache-wide Release:
  rebalance callback                 3_359_032_002.66 payload values/s
  callback allocation                0.03 bytes/payload value
```

Those rows remain the accepted historical Release baselines. The milestone 008
performance gate below is a same-run sync versus async guardrail captured from
the current Debug CLI after async implementation. It should be used to judge
async transport overhead against its synchronous counterpart, not as a direct
replacement for the milestone 007 Release throughput baseline.

## Processing-Only Comparison

Contour:

```text
benchmark: processing benchmark synthetic
build: Debug CLI
sources: 256
batches per iteration: 64
events per batch: 4_096
payload values per event: 16
partitions/shards: 24/4
handler set: counter-checksum
iterations/warmup: 3/1
payload values per iteration: 4_194_304
```

Result:

```text
mode         elapsed ms  payload values/s  alloc bytes  alloc bytes/payload  checksum
partitioned 367.51      34_238_648.01     57_851_608   4.60                 115_957_924_088_101_331
async        447.90      28_092_983.67     60_087_224   4.78                 115_957_924_088_101_331
```

Async worker telemetry:

```text
workers: 4
queue capacity: 1
dispatched/completed batches: 192/192
submitted/completed/succeeded work items: 768/768/768
failed work items: 0
dispatch: 396.32 ms
queue wait: 17.63 ms
execution: 851.08 ms
aggregation: 1.30 ms
barrier wait: 254.41 ms
async validation: succeeded
sync comparison checksum: 3_048_319_381_679_617_053
async comparison checksum: 3_048_319_381_679_617_053
```

Interpretation: async preserved deterministic results. On this prebuilt
processing-only contour, async throughput was `82.1%` of the synchronous
partitioned row and callback allocation increased by `3.86%`. The measured
cost is scheduler machinery: dispatch, barrier wait, worker completion, and
telemetry around relatively small batch work.

## Synthetic Rebalance Comparison

Contour:

```text
benchmark: processing benchmark rebalance-synthetic
build: Debug CLI
workload: hot-shard
mode: rebalance-session
iterations/warmup: 10_000/1_000
batches per iteration: 2
payload values per iteration: 12
validation profile: diagnostic
retention: recent
```

Result:

```text
mode         elapsed ms  payload values/s  alloc bytes  alloc bytes/payload  accepted  skipped  failed migrations  checksum
partitioned 214.76      558_769.78        244_321_488  2_036.01             10_000    20_000   0                  13_335_626_655_261_425_125
async        693.85      172_946.95        439_645_416  3_663.71             10_000    20_000   0                  13_335_626_655_261_425_125
```

Async worker telemetry:

```text
workers: 4
queue capacity: 1
dispatched/completed batches: 20_000/20_000
submitted/completed/succeeded work items: 40_000/40_000/40_000
failed work items: 0
dispatch: 303.75 ms
queue wait: 223.97 ms
execution: 54.56 ms
aggregation: 34.16 ms
barrier wait: 219.16 ms
```

Interpretation: async preserved accepted moves, skipped decisions, validation
checksum, and zero failed migrations. This workload is a behavioral microscope:
only `12` payload values per iteration. Async throughput was `31.0%` of the
synchronous row and allocation increased by `79.95%` because fixed worker
dispatch and telemetry costs dominate tiny work items. This is expected and is
not the production-shaped throughput signal.

## Single-File Archive Comparison

Contour:

```text
file: data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
mode: rebalance-session
build: Debug CLI
archive parallelism: 24
iterations/warmup: 1/0
batches: 1
stream events: 32_400
payload values: 38_759_040
```

Result:

```text
input       mode   callback ms  callback payload values/s  callback allocation bytes  accepted  checksum
file        sync   169.57       228_572_506.93             1_329_384                  1         3_750_039_633_875_006_276
file        async  190.10       203_887_638.09             1_371_224                  1         3_750_039_633_875_006_276
cache x1    sync   166.59       232_661_264.18             1_322_952                  1         3_750_039_633_875_006_276
cache x1    async  181.06       214_067_380.98             1_364_624                  1         3_750_039_633_875_006_276
```

Interpretation: async preserved deterministic processing and rebalance output
on the one-batch archive contour. The single-file shape is intentionally small,
so worker setup, dispatch, and barrier overhead remain visible. Callback
allocation increased by about `3.15%`.

## Full-Cache Archive Comparison

Contour:

```text
cache root: data\nexrad
date/radar: 2026-05-04 KTLX
max files: 220
build: Debug CLI
archive parallelism: 24
iterations/warmup: 1/0
examined files: 220
skipped files: 22
published Archive Two base-data files: 198
compressed bytes: 1_220_681_959
decompressed bytes: 10_029_011_456
stream events: 6_401_760
payload values: 7_660_888_320
```

Rebalance-session result:

```text
mode   end-to-end ms  callback ms  callback payload values/s  callback allocation bytes  accepted  skipped  failed migrations  checksum
sync   78_916.46      27_427.74    279_311_694.78             260_599_080                2         392      0                  7_480_064_646_096_449_000
async  78_334.34      27_428.21    279_306_922.85             262_952_952                2         392      0                  7_480_064_646_096_449_000
```

Async rebalance worker telemetry:

```text
workers: 4
queue capacity: 1
dispatched/completed batches: 198/198
submitted/completed/succeeded work items: 792/792/792
failed work items: 0
dispatch: 26_752.31 ms
queue wait: 70.14 ms
execution: 1_368.89 ms
aggregation: 5.10 ms
barrier wait: 585.32 ms
```

Pressure-sampling result:

```text
mode   end-to-end ms  callback ms  callback payload values/s  callback allocation bytes  evaluations  accepted  checksum
sync   78_196.31      27_512.23    278_453_962.53             258_245_568                198          0         2_540_507_904_059_963_540
async  78_316.08      27_477.16    278_809_294.52             260_567_328                198          0         2_540_507_904_059_963_540
```

Async sampling worker telemetry:

```text
workers: 4
queue capacity: 1
dispatched/completed batches: 198/198
submitted/completed/succeeded work items: 792/792/792
failed work items: 0
dispatch: 26_843.82 ms
queue wait: 52.30 ms
execution: 1_154.00 ms
aggregation: 4.90 ms
barrier wait: 772.19 ms
```

Interpretation: the full-cache archive row is the accepted milestone 008
production-shaped guardrail. Async preserved checksum, accepted moves, skipped
decisions, evaluation count, zero failed migrations, and zero failed work
items. Rebalance callback elapsed time was effectively flat: async was
`+0.47 ms`, less than `0.01%`. Sampling callback elapsed time was `35.07 ms`
faster in this single run, about `0.13%`, which is noise-level parity.

The measurable async cost on the production-shaped contour is allocation:
rebalance callback allocation increased by `2_353_872` bytes, about `0.90%`;
sampling callback allocation increased by `2_321_760` bytes, also about
`0.90%`. That cost is attributed to async dispatch, completion, and bounded
worker telemetry machinery. End-to-end archive time remains dominated by
replay and batch construction, so callback timing is the correct execution
comparison surface.

## Performance Assessment

Milestone 008 passes the final performance gate.

Important signals:

```text
correctness:
  async preserved deterministic checksums, source snapshots, accepted moves,
  skipped decisions, and validation status across captured contours

payload lifetime:
  borrowed RadarEventBatch payload still completes before callback exit; no
  retained borrowed-payload protocol was introduced

callback throughput:
  full-cache archive async callback latency was parity with synchronous
  execution; tiny synthetic contours were slower because scheduler overhead
  dominates small work items

allocation:
  production-shaped archive callback allocation increased by about 0.90%;
  small synthetic contours show larger percentage costs because fixed async
  machinery is amortized over little work

scheduler overhead:
  measured overhead is dispatch, queue wait, completion barrier, aggregation,
  and bounded worker telemetry, not payload copying

rebalance:
  rebalance remained post-processing control-plane work; workers did not
  publish topology or consume partial telemetry

default mode:
  synchronous execution remains the default and the correctness oracle; async
  is selectable and benchmarked explicitly

telemetry:
  worker telemetry stayed bounded and reported complete counters plus capped
  recent detail
```

The milestone value is not an immediate throughput win. It is a safe retained
worker substrate with explicit costs, deterministic result parity, and a clear
path toward future provider decoupling once payload ownership becomes explicit.

## Deferred Work

The following remain intentionally outside milestone 008:

- Owned payload snapshots or explicit payload ownership transfer.
- Provider-level async queues that let replay/live ingestion return before
  processing finishes.
- Multi-batch pipeline scheduling with topology publication ordering.
- Production configuration for worker counts, queue capacity, timeout policy,
  and worker health policy.
- Worker-local state transfer and physical shard residency.
- Source-level migration.
- Partition splitting for intrinsically hot partitions.
- Broader Release benchmark campaign with multiple iterations, medians,
  multiple radars, and explicit statistical noise bounds.
- Scheduler tuning if future real contours show the `0.90%` allocation cost or
  dispatch/barrier timing blocks the next goal.

## Next Milestone Input

The next milestone should start from this closed reference path:

```text
provider callback publishes one borrowed RadarEventBatch
  -> processing captures one topology snapshot
  -> async dispatcher routes shard work against that snapshot
  -> retained workers process bounded shard work
  -> completion barrier finishes before callback return
  -> deterministic aggregation creates processing telemetry
  -> rebalance may publish topology N+1 only after successful processing
```

Recommended next focus:

- Design an explicit owned payload snapshot or ownership-transfer protocol.
- Move provider decoupling behind that ownership boundary, not around it.
- Preserve the synchronous path as the oracle while adding any future
  multi-batch or provider-level queue.
- Keep worker telemetry bounded and comparable to the milestone 008 counters.
- Re-run Release archive comparisons before changing execution defaults.
