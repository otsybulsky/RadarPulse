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

## Next Work From This Gate

```text
keep queued-owned opt-in
keep pooled-copy available as the optimized retention strategy
add a cache-level producer/consumer benchmark contour that can enqueue across
  files before the consumer catches up
carry retained-byte high-water and queue-depth telemetry into that next contour
repeat the gate after useful queued-ahead overlap exists
```
