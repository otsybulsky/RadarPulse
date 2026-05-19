# Milestone 009 Performance Gate

Date: 2026-05-19

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

The full-cache contour examined 220 files, skipped 22 non-base-data files,
and published 198 Archive Two base-data files.

## Single-File Results

All single-file contours preserved deterministic output:

```text
payload values: 38_759_040
validation checksum: 3_750_039_633_875_006_276
accepted moves: 1
skipped decisions: 0
failed migrations: 0
```

| Contour | Queue capacity | End-to-end ms | Callback ms | Replay/build ms | Owned snapshot ms | Owned snapshot allocated bytes | Enqueue wait ms | Drain ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| blocking-borrowed sync | 0 | 353.29 | 56.91 | 296.38 | n/a | n/a | n/a | n/a |
| blocking-borrowed async | 0 | 397.04 | 81.23 | 315.81 | n/a | n/a | n/a | n/a |
| queued-owned sync | 1 | 382.83 | 64.76 | 318.07 | 5.46 | 50_331_016 | 1.37 | 66.44 |
| queued-owned async | 1 | 372.80 | 64.54 | 308.25 | 5.38 | 50_331_016 | 1.19 | 66.09 |
| queued-owned sync | 8 | 384.43 | 58.87 | 325.56 | 6.03 | 50_331_016 | 1.43 | 60.81 |
| queued-owned async | 8 | 400.33 | 74.28 | 326.04 | 5.64 | 50_331_016 | 1.54 | 75.95 |

Single-file interpretation:

- Queued-owned preserves checksum and rebalance behavior against the borrowed
  reference.
- Owned snapshot allocation is the expected dominant additional cost:
  50_331_016 bytes for 48_257_280 payload bytes, or about 1.30 bytes per
  payload value.
- Queue capacity 8 does not improve this contour because the current archive
  benchmark publishes one owned batch and drains it immediately after file
  replay.

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

| Contour | Queue capacity | End-to-end ms | Callback ms | Replay/build ms | Owned snapshot ms | Owned snapshot allocated bytes | Enqueue wait ms | Drain ms | Worker queue wait ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| blocking-borrowed sync | 0 | 16_957.99 | 2_364.97 | 14_593.02 | n/a | n/a | n/a | n/a | n/a |
| blocking-borrowed async | 0 | 16_955.62 | 2_353.03 | 14_602.59 | n/a | n/a | n/a | n/a | 43.31 |
| queued-owned sync | 1 | 17_986.70 | 2_455.87 | 15_530.83 | 575.81 | 9_947_507_808 | 2.58 | 2_469.01 | n/a |
| queued-owned async | 1 | 17_587.79 | 2_333.45 | 15_254.34 | 538.28 | 9_947_506_296 | 2.04 | 2_345.84 | 43.75 |
| queued-owned sync | 8 | 17_971.88 | 2_474.59 | 15_497.29 | 549.70 | 9_947_505_264 | 2.00 | 2_487.23 | n/a |
| queued-owned async | 8 | 17_569.64 | 2_404.31 | 15_165.33 | 528.64 | 9_947_506_296 | 1.96 | 2_416.64 | 45.28 |

Full-cache interpretation:

- Correctness parity holds across all provider and execution contours.
- Owned snapshot allocation is about 9.95 GB for 9.54 GB of payload bytes
  across 198 batches, again about 1.30 bytes per payload value.
- Owned snapshot elapsed time is about 529-576 ms across the full cache.
- End-to-end queued-owned overhead versus blocking-borrowed sync is roughly
  612-1_029 ms in this run, or about 3.6-6.1%.
- Processing callback allocation remains near the borrowed path for sync
  queued-owned because the owned payload copy is attributed to replay/batch
  construction, not callback processing.
- Async worker telemetry remains healthy with 198 dispatched/completed batches,
  792 submitted/completed/succeeded work items, and 0 failed work items.
- Provider enqueue wait is negligible at about 2 ms total for the full cache.
- Queue depth high-water mark remains 1 for both queue capacity 1 and 8.
  Capacity 8 therefore does not show provider/processing overlap in the
  current benchmark integration.
- Provider queue drain time is effectively the processing drain contour:
  2.35-2.49 seconds across the full cache. The current implementation drains
  after each file is published, so it validates ownership and telemetry but
  does not yet overlap archive replay with processing.

## Gate Decision

Queued-owned is acceptable as a correctness-preserving provider-decoupling
substrate. It cleanly exposes the cost of retaining provider payloads, keeps
borrowed-reference parity, preserves async worker telemetry, and reports queue
copy/enqueue/drain costs separately.

Queued-owned should not become the default archive benchmark or production path
yet. The full-cache run adds about 9.95 GB of owned snapshot allocation and
about 0.53-0.58 seconds of explicit copy time for this KTLX contour. The current
file-level drain strategy also prevents queue capacity from producing overlap,
so larger queue capacity only confirms bounded behavior rather than improving
throughput.

Next optimization targets:

- reduce owned snapshot allocation with buffer pooling or move/transfer
  semantics where safe;
- add a producer/consumer archive benchmark contour that overlaps replay and
  processing across files or batches;
- keep `blocking-borrowed` as the default until the owned-copy cost and overlap
  strategy are improved;
- keep `queued-owned` as an explicit measurement and validation mode.
