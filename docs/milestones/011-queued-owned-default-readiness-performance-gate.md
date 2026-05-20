# Milestone 011 Performance Gate

Date: 2026-05-20

Scope: natural Release gate capture and allocation follow-up for the milestone
011 queued-owned default-readiness candidate. This is not the milestone
closeout or decision trace, and it does not change the default provider.

Candidate contour:

```text
provider: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async
workers: 4
queue capacity: 8
retained-byte budget: 536870912
queue telemetry: summary
overlap telemetry: summary
overlap consumer delay: disabled
```

## Pre-Gate Blocker

The first natural candidate run after slice 9 failed before the matrix could be
captured:

```text
No retained resource was found for queued provider sequence 172.
```

Root cause: the provider queue made an accepted batch visible to a waiting
consumer before `ArchiveOwnedRadarEventBatchQueueingPublisher` registered the
retained resource for that sequence. The consumer could therefore dequeue and
attempt to acquire the retained resource during the small post-enqueue,
pre-registration window.

Slice 10 fixes this by letting `RadarProcessingOwnedBatchQueue.EnqueueAsync`
invoke an accepted-batch callback while still holding the queue lock and before
a waiting `DequeueAsync` can return to consumer code. The archive queueing
publisher registers the retained resource through that callback. Regression
coverage verifies that the accepted callback runs before a waiting dequeue
returns and that a waiting archive consumer can acquire the retained resource
for an accepted publish.

The failed pre-gate run is excluded from the natural performance matrix below.

## Verification Before Capture

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Result: Release build succeeded with 0 warnings and 0 errors.

Focused checks:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests"
```

Recorded result:

```text
20 passed, 0 failed, 0 skipped for CLI rebalance benchmark coverage.
18 passed, 0 failed, 0 skipped for overlap runner and readiness gate coverage.
```

After fixing the retained-resource registration race:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

dotnet build RadarPulse.sln -c Release --no-restore

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Recorded result:

```text
53 passed, 0 failed, 0 skipped for focused queue, publisher, overlap, and CLI coverage.
Release build succeeded with 0 warnings and 0 errors.
735 passed, 0 failed, 3 skipped for the full test project.
```

## Local Data Availability

Initial gate capture:

The local cache contains one radar/date shape:

```text
data\nexrad\level2\2026\05\04\KTLX
244 files total
```

No different local radar/date shape was available for this gate capture.

Expanded-cache follow-up:

After the initial gate, the local cache was expanded to include additional
radar/date shapes:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive download --date 2026-05-04 --radar KINX --output data\nexrad --concurrency 8

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive download --date 2026-05-05 --radar KTLX --output data\nexrad --concurrency 8
```

Recorded download result:

```text
2026-05-04/KINX: 231 files, 1_404_409_198 downloaded bytes
2026-05-05/KTLX: 424 files, 2_232_413_173 downloaded bytes
```

The expanded cache contains these radar/date shapes:

```text
2026-05-04/KINX
2026-05-04/KTLX
2026-05-05/KTLX
```

Measured contours:

```text
primary contour: --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
larger local contour: --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 1000000
expanded mixed-cache contour: --cache data\nexrad --max-files 1000000
```

Common command parameters:

```text
processing benchmark rebalance-archive
--mode rebalance
--execution async
--workers 4
--iterations 1
--warmup-iterations 0
--parallelism 24
--partitions 24
--shards 4
```

Borrowed reference provider parameters:

```text
--provider blocking-borrowed
```

Queued-owned candidate provider parameters:

```text
--provider queued-owned
--provider-overlap producer-consumer
--retention-strategy pooled-copy
--queue-capacity 8
--queue-retained-bytes 536870912
--queue-telemetry summary
--overlap-telemetry summary
```

## Primary Natural Matrix

All rows used the primary KTLX contour:

```text
examined files: 220
skipped files: 22
published files: 198
payload values: 7_660_888_320
raw value checksum: 245_554_417_487
topology versions: 2
rebalance evaluations: 198
accepted moves: 2
skipped decisions: 392
failed migrations: 0
validation: succeeded
validation checksum: 7_480_064_646_096_449_000
skipped reason counters: no-hot-shard=376, no-cold-target-shard=4,
  source-shard-move-budget-exhausted=12, global-move-budget-exhausted=12
```

| Run | Borrowed elapsed ms | Candidate elapsed ms | Candidate delta ms | Candidate delta | Borrowed allocated bytes | Candidate allocated bytes |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 17_125.53 | 16_109.13 | -1_016.40 | -5.93% | 1_977_980_224 | 3_956_507_928 |
| 2 | 17_256.16 | 15_471.14 | -1_785.02 | -10.34% | 1_975_358_840 | 4_028_595_192 |
| 3 | 17_462.23 | 15_325.73 | -2_136.50 | -12.23% | 1_971_680_096 | 4_031_624_344 |

Primary contour summary:

```text
borrowed average elapsed ms: 17_281.31
candidate average elapsed ms: 15_635.33
candidate average delta ms: -1_645.97
candidate average delta: -9.52%
borrowed run spread: 336.70 ms, 1.95% of average
candidate run spread: 783.40 ms, 5.01% of average
borrowed average allocated bytes: 1_975_006_387
candidate average allocated bytes: 4_005_575_821
candidate allocation ratio: 2.03x borrowed
```

Candidate retained-resource telemetry was stable across all three primary rows:

```text
provider overlap evidence contour: natural-default-candidate
provider overlap consumer delay ms: 0.00
provider queue depth high watermark: 1
provider overlap queue depth high watermark: 1
provider overlap has producer-consumer overlap: yes
provider overlap has queued-ahead overlap: no
provider queue retained payload bytes high watermark: 48_257_280
provider queue active retained payload bytes high watermark: 48_257_280
provider queue combined retained payload bytes high watermark: 48_257_280
provider overlap combined retained payload bytes high watermark: 48_257_280
retained-byte budget: 536_870_912
high watermark / budget: 8.99%
retained payload attempts: 198
retained payload batches: 198
retained payload release attempts: 198
retained payload released batches: 198
retained payload failed releases: 0
current pending retained batches at completion: 0
current active retained batches at completion: 0
current combined retained batches at completion: 0
```

## Larger Local Cache Row

The larger local contour used `--max-files 1000000`. With the available cache,
that expands the measured input from 220 examined files to all 244 local KTLX
files for the day.

Shared output:

```text
examined files: 244
skipped files: 24
published files: 220
payload values: 8_513_587_200
raw value checksum: 266_648_133_947
topology versions: 2
rebalance evaluations: 220
accepted moves: 2
skipped decisions: 436
failed migrations: 0
validation: succeeded
validation checksum: 12_759_860_675_563_334_608
skipped reason counters: no-hot-shard=420, no-cold-target-shard=4,
  source-shard-move-budget-exhausted=12, global-move-budget-exhausted=12
```

| Contour | End-to-end ms | Callback ms | Replay/build ms | Allocated bytes | Allocated bytes / payload value |
| --- | ---: | ---: | ---: | ---: | ---: |
| blocking-borrowed async | 18_991.42 | 2_581.82 | 16_409.60 | 2_004_995_152 | 0.24 |
| queued-owned pooled-copy producer-consumer | 16_712.45 | 3_587.66 | 13_124.78 | 3_990_915_768 | 0.47 |

Larger local candidate retained-resource telemetry:

```text
provider overlap evidence contour: natural-default-candidate
provider overlap consumer delay ms: 0.00
provider queue depth high watermark: 1
provider overlap queue depth high watermark: 1
provider overlap has producer-consumer overlap: yes
provider overlap has queued-ahead overlap: no
provider queue combined retained payload bytes high watermark: 48_257_280
provider overlap combined retained payload bytes high watermark: 48_257_280
retained payload attempts: 220
retained payload batches: 220
retained payload bytes: 10_599_423_360
retained payload values: 8_513_587_200
retained payload release attempts: 220
retained payload released batches: 220
retained payload failed releases: 0
current pending retained batches at completion: 0
current active retained batches at completion: 0
current combined retained batches at completion: 0
```

## Gate Interpretation

Correctness parity: passed for every captured natural row. Published file count,
payload values, raw checksum, topology count, accepted moves, skipped decisions,
failed migrations, validation status, validation checksum, and skipped reason
counters matched the same-run borrowed reference for the same input contour.

Release health: passed for every captured natural candidate row. All retained
payload batches were released and failed releases stayed at 0.

Retained-resource pressure: passed for the measured contours. Active and
combined retained pressure is now visible, current pending/active/combined
pressure returns to 0 at completion, and the combined retained payload
high-water mark is 48_257_280 bytes, about 8.99% of the 536_870_912 byte
budget.

Performance delta: passed on the measured local contours. On the repeated
primary matrix, the candidate averaged 15_635.33 ms versus 17_281.31 ms for
borrowed, a 9.52% improvement. On the larger local row, the candidate measured
16_712.45 ms versus 18_991.42 ms for borrowed, a 12.00% improvement.

Run variance: captured for the primary contour. Borrowed spread was 336.70 ms
or 1.95% of average. Candidate spread was 783.40 ms or 5.01% of average. The
larger local contour has one row only and should not be treated as independent
variance evidence.

Natural evidence separation: passed. All natural rows used
`Provider overlap consumer delay ms: 0.00` and were labeled
`natural-default-candidate`. Controlled proof rows were not included in the
natural matrix.

Natural queue backlog: not accumulated. Producer/consumer lifetime overlap is
present, while queue depth high watermark remains 1 and
`Provider overlap has queued-ahead overlap` remains `no`. This does not
contradict the controlled-delay proof, which already demonstrates that the
queue-ahead mechanics work when the consumer is intentionally slowed. On the
natural measured contours, depth 1 is evidence that the consumer keeps up with
the producer and retained pressure stays bounded. The only claim to avoid is
attributing the favorable timing directly to queued-ahead buffering.

Initial allocation movement: failed for default-readiness. The queued-owned
candidate allocated about 2.03x the borrowed reference on the repeated primary
matrix and about 1.99x the borrowed reference on the larger local row. The time
result was favorable, but the allocation regression required the follow-up
optimization captured below before any default-rollout milestone could be
proposed.

Input diversity: incomplete. The local cache only had one radar/date shape
available, so this gate cannot claim cross-shape readiness.

Expanded-cache follow-up: local input diversity is now available and measured.
The expanded cache mixed contour examined 1_554 files, skipped 726 non-base-data
files and sidecars, and published 828 Archive Two base-data files across KINX
and KTLX on two dates.

Expanded-cache borrowed/candidate parity:

```text
published files: 828
payload values: 32_306_203_200
raw value checksum: 958_518_408_830
topology versions: 2
rebalance evaluations: 607
accepted moves: 2
skipped decisions: 1_210
failed migrations: 0
validation: succeeded
validation checksum: 615_051_108_812_661_629
```

Expanded-cache timing at archive parallelism 24 and workers 4:

```text
blocking-borrowed async elapsed ms: 77_530.68
queued-owned pooled-copy producer-consumer elapsed ms: 72_440.28
candidate delta: -5_090.40 ms
```

Expanded-cache retained-resource and overlap result:

```text
provider overlap consumer delay ms: 0.00
provider overlap evidence contour: natural-default-candidate
provider overlap has producer-consumer overlap: yes
provider overlap has queued-ahead overlap: no
provider queue depth high watermark: 1
provider overlap queue depth high watermark: 1
provider queue combined retained payload bytes high watermark: 54_413_280
provider overlap combined retained payload bytes high watermark: 54_413_280
retained payload attempts: 828
retained payload released batches: 828
retained payload failed releases: 0
```

Additional natural stress rows without controlled delay:

```text
expanded cache, archive parallelism 96, workers 4:
  provider overlap has queued-ahead overlap: no
  provider overlap queue depth high watermark: 1
  end-to-end elapsed ms: 70_965.75

expanded cache, archive parallelism 24, workers 1:
  provider overlap has queued-ahead overlap: no
  provider overlap queue depth high watermark: 1
  end-to-end elapsed ms: 66_050.19
```

Follow-up interpretation: more local data closes the input-diversity gap. The
expanded corpus, higher archive parallelism, and lower consumer worker count
still keep queue depth at 1. Natural producer/consumer lifetime overlap remains
present, and the controlled-delay runs remain the proof that queued-ahead
mechanics work when backpressure exists. On the natural contours, the absence
of backlog is favorable pipeline behavior: replay, retention, queueing, and
processing stay balanced enough that retained queue pressure does not build.

## Allocation Optimization Follow-Up

Slice 11 targets allocation churn in the retained payload copy path without
changing the candidate contour, provider default, queue capacity, retained-byte
budget, release semantics, or correctness oracle.

Implementation:

```text
RadarProcessingRetainedPayloadByteArrayPool now backs the default retained
  payload factory byte pool
small arrays still route to ArrayPool<byte>.Shared
large arrays are retained in a bounded idle pool
large rent capacity is rounded upward to improve cross-file reuse
the idle pool defaults to 4 arrays and 128 MiB
eviction prefers keeping larger reusable arrays within the count/byte budget
injected payload pools keep existing test and fault-injection behavior
```

Validation command:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --provider queued-owned --provider-overlap producer-consumer --retention-strategy pooled-copy --execution async --workers 4 --queue-capacity 8 --queue-retained-bytes 536870912 --queue-telemetry summary --overlap-telemetry summary --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

Post-optimization mixed-cache result:

```text
examined files: 1_554
published base-data files: 828
payload values: 32_306_203_200
raw value checksum: 958_518_408_830
validation checksum: 615_051_108_812_661_629
end-to-end elapsed ms: 71_181.17
provider queue depth high watermark: 1
provider queue combined retained payload bytes high watermark: 54_413_280
retained payload attempts: 828
retained payload released batches: 828
retained payload failed releases: 0
provider overlap has producer-consumer overlap: yes
provider overlap has queued-ahead overlap: no
retained payload allocated bytes: 247_679_944
end-to-end allocated bytes: 4_063_709_976
processing callback allocated bytes: 3_654_244_544
replay and batch construction allocated bytes: 409_465_432
end-to-end allocated bytes / payload value: 0.13
```

Allocation movement against the expanded-cache borrowed reference:

```text
borrowed end-to-end allocated bytes: 3_811_549_280
pre-optimization candidate allocated bytes: 5_897_703_080
post-optimization candidate allocated bytes: 4_063_709_976
pre-optimization candidate ratio to borrowed: 1.547x
post-optimization candidate ratio to borrowed: 1.066x
candidate excess allocation before optimization: 2_086_153_800
candidate excess allocation after optimization: 252_160_696
candidate excess allocation reduction: 87.91%
retained payload allocated bytes before optimization: 2_084_784_408
retained payload allocated bytes after optimization: 247_679_944
retained payload allocation reduction: 88.12%
end-to-end candidate allocation reduction: 31.10%
```

Interpretation: the allocation blocker is reduced from a major readiness
failure to a small residual overhead trace item on the expanded mixed-cache
contour. Correctness parity, release health, retained pressure, and natural
overlap interpretation are unchanged. The post-optimization candidate still
allocates about 6.6% more than borrowed on this contour, so the decision trace
should record the residual overhead rather than claiming zero allocation cost.

## Gate Decision

The slice 10 natural Release gate is captured, slice 11 records the allocation
follow-up against the expanded mixed-cache contour, and slice 12 hardens the
controlled-proof separation contract in CLI output and tests.

Evidence that supports the candidate:

```text
correctness parity holds
release health is clean
active and combined retained pressure are bounded and visible
performance delta is favorable on the measured local contours
controlled queued-ahead overlap mechanics were already proven with controlled
  consumer delay
natural queue depth remains 1 because the measured pipeline keeps up, which is
  favorable for retained pressure
natural and controlled evidence remain separated
expanded local cache now covers multiple radar/date shapes
```

Slice 12 output contract:

```text
natural default-candidate rows print:
  Provider overlap evidence contour: natural-default-candidate
  Provider overlap evidence scope: natural-readiness
controlled consumer-delay rows print:
  Provider overlap evidence contour: controlled-proof
  Provider overlap evidence scope: controlled-mechanics-proof
natural queued-owned producer-consumer opt-in rows outside the readiness
  contour print:
  Provider overlap evidence contour: natural-opt-in
  Provider overlap evidence scope: opt-in-diagnostic
non queued-owned producer-consumer rows print:
  Provider overlap evidence contour: not-applicable
  Provider overlap evidence scope: not-applicable
```

Remaining default-readiness caution after the allocation follow-up:

```text
post-optimization candidate allocation remains 1.066x borrowed on the measured
  expanded-cache contour
default rollout still requires an explicit decision trace and rollout
  milestone; this gate does not change the provider default
```

`queued-owned + pooled-copy + producer-consumer` should remain opt-in. The next
slice should produce the decision trace and closeout, recording the residual
allocation overhead and deciding whether the optimized contour is ready to feed
a separate default-rollout milestone.
