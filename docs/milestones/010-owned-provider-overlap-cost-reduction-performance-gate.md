# Milestone 010 Performance Gate

Date: 2026-05-20

Scope: performance gate only. This is not the milestone closeout or decision
trace.

Build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Result: Release build succeeded with 0 warnings and 0 errors.

## Benchmark Contours

All commands used the Release CLI:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive ...
```

Common parameters:

```text
--mode rebalance
--iterations 1
--warmup-iterations 0
--parallelism 24
--partitions 24
--shards 4
```

Single-file input:

```text
data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
```

Full-cache input:

```text
--cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
```

The full-cache contour examined 220 files, skipped 22 non-base-data files, and
published 198 Archive Two base-data files.

## Single-File Results

All single-file contours preserved deterministic output:

```text
payload values: 38_759_040
validation checksum: 3_750_039_633_875_006_276
accepted moves: 1
skipped decisions: 0
failed migrations: 0
```

| Contour | Queue capacity | Retention | Overlap | End-to-end ms | Callback ms | Replay/build ms | Retained allocated bytes | Retention ms | Queue depth high watermark | Overlap shared ms | Queued-ahead overlap |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| blocking-borrowed sync | 0 | n/a | none | 374.47 | 56.75 | 317.71 | n/a | n/a | n/a | n/a | n/a |
| blocking-borrowed async | 0 | n/a | none | 346.30 | 63.86 | 282.45 | n/a | n/a | n/a | n/a | n/a |
| queued-owned async | 1 | snapshot-copy | none | 368.61 | 63.86 | 304.75 | 50_331_040 | 4.55 | 1 | n/a | n/a |
| queued-owned async | 1 | pooled-copy | none | 391.76 | 70.87 | 320.89 | 69_206_376 | 5.41 | 1 | n/a | n/a |
| queued-owned overlap async | 8 | pooled-copy | producer-consumer | 347.20 | 62.29 | 284.91 | 69_206_408 | 5.80 | 1 | 271.05 | no |

Single-file interpretation:

- Correctness parity holds across borrowed, snapshot-copy, pooled-copy, and
  producer-consumer overlap contours.
- Pooled-copy does not reduce allocation on the first retained batch. The first
  run has to allocate pool-backed arrays, so the single-file retained allocation
  is higher than snapshot-copy.
- Producer and consumer task lifetimes overlap on the single-file contour, but
  queue depth remains 1 and queued-ahead overlap is absent. This contour is a
  compatibility smoke, not proof of useful buffering.

## Full-Cache Results

All full-cache contours preserved deterministic output:

```text
published files: 198
payload values: 7_660_888_320
validation checksum: 7_480_064_646_096_449_000
accepted moves: 2
skipped decisions: 392
failed migrations: 0
```

| Contour | Queue capacity | Retention | Overlap | End-to-end ms | Callback ms | Replay/build ms | Retained allocated bytes | Retention ms | Enqueue wait ms | Drain ms | Queue depth high watermark | Retained bytes high watermark | Worker queue wait ms |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| blocking-borrowed sync | 0 | n/a | none | 20_120.73 | 2_404.85 | 17_715.88 | n/a | n/a | n/a | n/a | n/a | n/a | n/a |
| blocking-borrowed async | 0 | n/a | none | 16_911.67 | 2_319.03 | 14_592.64 | n/a | n/a | n/a | n/a | n/a | n/a | 45.45 |
| queued-owned async | 1 | snapshot-copy | none | 18_805.48 | 2_439.44 | 16_366.04 | 9_947_507_832 | 512.19 | 2.15 | 2_451.98 | 1 | 48_257_280 | 50.88 |
| queued-owned async | 1 | pooled-copy | none | 19_016.63 | 2_360.95 | 16_655.68 | 102_811_264 | 334.40 | 2.12 | 2_373.81 | 1 | 48_257_280 | 44.58 |
| queued-owned overlap async | 1 | pooled-copy | producer-consumer | 18_908.83 | 2_347.93 | 16_560.91 | 169_920_184 | 328.34 | 3.12 | 18_856.98 | 1 | 48_257_280 | 40.43 |
| queued-owned overlap async | 8 | pooled-copy | producer-consumer | 18_966.63 | 2_356.87 | 16_609.76 | 169_920_184 | 333.78 | 2.95 | 18_913.42 | 1 | 48_257_280 | 40.88 |

Overlap attribution for the full-cache overlap contours:

| Contour | Overlap elapsed ms | Producer active ms | Consumer active ms | Shared active ms | Producer/consumer overlap | Queued-ahead overlap | Provider blocked ms | Consumer idle ms | Measured allocated bytes | Unattributed allocated bytes |
| --- | ---: | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: |
| queued-owned overlap async, capacity 1 | 18_863.24 | 16_506.43 | 18_859.91 | 16_506.43 | yes | no | 3.12 | 16_499.22 | 2_157_196_712 | 1_987_276_528 |
| queued-owned overlap async, capacity 8 | 18_919.45 | 16_553.20 | 18_916.32 | 16_553.20 | yes | no | 2.95 | 16_546.45 | 2_157_216_928 | 1_987_296_744 |

Full-cache interpretation:

- Borrowed-reference parity holds across all measured contours.
- Pooled-copy sharply reduces retained allocation versus snapshot-copy:
  9_947_507_832 bytes down to 102_811_264 bytes in the non-overlapped full-cache
  contour, a reduction of about 98.97%.
- Pooled-copy retention time also drops versus snapshot-copy:
  512.19 ms down to 334.40 ms in the non-overlapped full-cache contour.
- Resource lifecycle cleanup is complete for pooled-copy: 198 retained batches,
  198 released batches, and 0 failed releases.
- Snapshot-copy correctly reports 198 release-not-required batches.
- End-to-end time does not improve versus the borrowed async baseline. The best
  queued-owned contour here is still about 1_894 ms slower than borrowed async.
- Producer and consumer task lifetimes overlap, but useful queued-ahead overlap
  does not happen. Queue depth high watermark remains 1 for both capacity 1 and
  capacity 8, and consumer idle time is about 16.5 seconds.
- The current cache benchmark still invokes the overlap runner one file at a
  time. That proves concurrent producer/consumer task wiring and resource
  cleanup, but it does not create a multi-file producer pipeline that can fill
  the queue ahead of processing.
- Capacity 8 and a 512 MiB retained-byte budget do not change throughput or
  queue depth in this contour because the queue never contains more than one
  retained batch.

## Gate Decision

Milestone 010 passes the allocation-reduction and correctness portions of the
gate:

- deterministic borrowed-reference parity holds;
- retained payload allocation is reduced by about 98.97% on the full-cache
  pooled-copy contour;
- retention elapsed time decreases on the full-cache pooled-copy contour;
- retained resources are released with 0 failures;
- queue and overlap telemetry expose the result clearly enough to reject weak
  overlap claims.

Milestone 010 does not pass the useful-overlap/default-readiness portion of the
gate:

- queued-owned remains slower than the borrowed async baseline in this run;
- producer/consumer lifetime overlap is present, but queued-ahead overlap is
  absent;
- queue capacity and retained-byte budget do not improve throughput because the
  benchmark still runs one archive file through one overlap runner invocation at
  a time.

`blocking-borrowed` should remain the default. `queued-owned` plus `pooled-copy`
is now a strong allocation and lifecycle substrate, but a later slice or
milestone still needs a cache-level producer pipeline before overlap can be
claimed as throughput-useful.

## Repeat Gate After Slice 11

Slice 11 changed the cache contour so `queued-owned + producer-consumer` uses
one shared overlap runner across the selected cache file set instead of one
runner per file.

Repeat Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Result: Release build succeeded with 0 warnings and 0 errors.

Repeated full-cache contours used the same KTLX input:

```text
--cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
```

All repeated contours preserved deterministic output:

```text
published files: 198
payload values: 7_660_888_320
validation checksum: 7_480_064_646_096_449_000
accepted moves: 2
skipped decisions: 392
failed migrations: 0
```

| Contour | Queue capacity | Retention | Overlap | End-to-end ms | Callback ms | Replay/build ms | Retained allocated bytes | Retention ms | Queue depth high watermark | Retained bytes high watermark | Worker queue wait ms |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| blocking-borrowed async | 0 | n/a | none | 16_915.80 | 2_311.23 | 14_604.57 | n/a | n/a | n/a | n/a | 45.07 |
| queued-owned async | 1 | pooled-copy | none | 17_158.62 | 2_382.90 | 14_775.72 | 102_811_264 | 339.95 | 1 | 48_257_280 | 47.46 |
| queued-owned overlap async | 8 | snapshot-copy | producer-consumer | 17_453.76 | 3_182.36 | 14_271.40 | 9_947_502_792 | 844.09 | 1 | 48_257_280 | 194.40 |
| queued-owned overlap async | 1 | pooled-copy | producer-consumer | 15_330.55 | 3_243.30 | 12_087.25 | 1_971_376_272 | 407.02 | 1 | 48_257_280 | 662.98 |
| queued-owned overlap async | 8 | pooled-copy | producer-consumer | 14_947.99 | 3_127.38 | 11_820.61 | 1_971_376_296 | 405.04 | 1 | 48_257_280 | 235.32 |

Repeated overlap attribution:

| Contour | Overlap elapsed ms | Producer active ms | Consumer active ms | Shared active ms | Producer/consumer overlap | Queued-ahead overlap | Provider blocked ms | Consumer idle ms | Measured allocated bytes | Unattributed allocated bytes |
| --- | ---: | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: |
| snapshot-copy overlap async, capacity 8 | 17_434.29 | 17_417.95 | 17_432.23 | 17_417.95 | yes | no | 3.62 | 14_221.57 | 11_933_703_960 | 1_986_201_168 |
| pooled-copy overlap async, capacity 1 | 15_310.74 | 15_294.19 | 15_308.89 | 15_294.19 | yes | no | 3.05 | 12_035.81 | 3_958_226_544 | 1_986_850_272 |
| pooled-copy overlap async, capacity 8 | 14_928.97 | 14_913.52 | 14_927.34 | 14_913.52 | yes | no | 3.04 | 11_772.13 | 3_959_015_576 | 1_987_639_280 |

Repeat gate interpretation:

- Borrowed-reference parity still holds after the cache-level producer pipeline.
- Cache-level producer/consumer overlap now improves wall-clock time. The best
  queued-owned pooled-copy overlap contour is 14_947.99 ms, about 1_967.81 ms
  faster than the borrowed async baseline in this run.
- The shared cache overlap contour is also about 2_210.63 ms faster than the
  non-overlapped queued-owned pooled-copy contour.
- Capacity 8 improves over capacity 1 by about 382.56 ms and greatly reduces
  worker queue wait in this run.
- Pooled-copy remains much cheaper than snapshot-copy on the same overlapped
  contour: 1_971_376_296 retained allocated bytes versus 9_947_502_792,
  about an 80.18% reduction.
- Pooled-copy overlap allocates more than pooled-copy non-overlap because the
  producer can retain the next batch while the consumer is still processing an
  earlier retained batch. This is the expected cost of actual producer/consumer
  overlap with retained owned batches.
- Queue depth high watermark remains 1 and queued-ahead overlap remains `no`.
  The useful overlap here is not a deep queued backlog; it is producer replay
  running while the consumer processes an in-flight retained batch.
- The current queue telemetry does not report an in-flight retained-resource
  high watermark after dequeue. Retained-byte high watermark therefore only
  describes bytes still pending in the queue, not bytes currently held by the
  consumer.

## Updated Gate Decision

Milestone 010 now passes the correctness, allocation-reduction, resource
cleanup, and useful wall-clock overlap portions of the gate:

- deterministic borrowed-reference parity holds;
- pooled-copy remains far cheaper than snapshot-copy;
- retained resource releases complete with 0 failed releases;
- cache-level producer/consumer overlap improves end-to-end time versus both
  borrowed async and non-overlapped queued-owned in this run.

Milestone 010 still does not prove queued-ahead buffering:

- queue depth high watermark remains 1;
- `HasQueuedAheadOverlap` remains `no`;
- retained in-flight consumer resources need better high-water telemetry before
  the gate can explain overlap memory pressure precisely.

`blocking-borrowed` should remain the default until repeated Release runs are
captured and in-flight retention pressure telemetry is strengthened.
`queued-owned + pooled-copy + producer-consumer` is now a credible optimized
benchmark contour, not merely a correctness measurement mode.

## Controlled Queue-Ahead Proof

Slice 12 added a benchmark-only consumer delay for producer-consumer overlap
contours. The delay is disabled by default and is rejected outside
`queued-owned + producer-consumer`. Its purpose is to prove bounded
queue-ahead mechanics under a controlled slow consumer; it is not a production
throughput contour.

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Result: Release build succeeded with 0 warnings and 0 errors.

Controlled contour command:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 32 --mode rebalance --provider queued-owned --provider-overlap producer-consumer --execution async --workers 4 --queue-capacity 8 --retention-strategy pooled-copy --queue-retained-bytes 536870912 --overlap-telemetry summary --overlap-consumer-delay-ms 150 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

Controlled contour deterministic output:

```text
examined files: 32
skipped files: 3
published files: 29
payload values: 1_124_012_160
validation checksum: 565_693_621_370_143_062
accepted moves: 2
skipped decisions: 54
failed migrations: 0
```

| Contour | Queue capacity | Consumer delay ms | End-to-end ms | Callback ms | Replay/build ms | Retained allocated bytes | Queue depth high watermark | Retained bytes high watermark | Queued-ahead overlap | Provider blocked ms | Consumer idle ms | Released batches | Failed releases |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | ---: |
| queued-owned pooled-copy overlap control | 8 | 150.00 | 5_347.63 | 438.48 | 4_909.15 | 1_385_432_184 | 8 | 386_058_240 | yes | 936.31 | 321.04 | 29 | 0 |

Controlled proof interpretation:

- The shared cache-level producer pipeline can queue ahead when the consumer is
  slower than archive replay.
- Queue depth high watermark reached the configured capacity of 8, and
  `HasQueuedAheadOverlap` reported `yes`.
- Bounded backpressure is visible: provider blocked time rose to 936.31 ms.
- Retained-byte pressure is visible in the pending queue high watermark:
  386_058_240 bytes.
- Correctness and cleanup still hold: validation succeeded, rebalance totals
  remained deterministic, 29 retained batches were released, and failed
  releases stayed at 0.
- This closes the controlled mechanics proof. It does not change the natural
  full-cache interpretation, where queue depth still stayed at 1 without a
  synthetic consumer delay.

Full-cache controlled contour command:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --provider queued-owned --provider-overlap producer-consumer --execution async --workers 4 --queue-capacity 8 --retention-strategy pooled-copy --queue-retained-bytes 536870912 --overlap-telemetry summary --overlap-consumer-delay-ms 150 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

Full-cache controlled deterministic output:

```text
examined files: 244
skipped files: 24
published files: 220
payload values: 8_513_587_200
validation checksum: 12_759_860_675_563_334_608
accepted moves: 2
skipped decisions: 436
failed migrations: 0
```

| Contour | Queue capacity | Consumer delay ms | End-to-end ms | Callback ms | Replay/build ms | Retained allocated bytes | Queue depth high watermark | Retained bytes high watermark | Queued-ahead overlap | Provider blocked ms | Consumer idle ms | Released batches | Failed releases |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | ---: |
| queued-owned pooled-copy overlap full-cache control | 8 | 150.00 | 37_967.62 | 2_748.63 | 35_218.98 | 2_455_845_488 | 8 | 386_058_240 | yes | 16_548.62 | 422.86 | 220 | 0 |

Full-cache control interpretation:

- The full local `data\nexrad` cache reached queue depth 8 with the same
  controlled consumer delay, so queued-ahead overlap is not limited to the
  32-file smoke contour.
- The retained-byte queue high watermark stayed below the configured 512 MiB
  budget at 386_058_240 bytes.
- Bounded backpressure is clear at full cache scale: provider blocked time was
  16_548.62 ms while the consumer drained 220 retained batches.
- Correctness and cleanup still held across the whole cache: validation
  succeeded, 220 retained batches were released, and failed releases stayed at
  0.

## Next Work From This Gate

```text
keep queued-owned opt-in
keep pooled-copy available as the optimized retention strategy
carry in-flight retained-resource high-water telemetry into the overlap contour
repeat the gate enough times to separate signal from run-to-run variance
use the updated gate result in the decision trace and closeout
```
