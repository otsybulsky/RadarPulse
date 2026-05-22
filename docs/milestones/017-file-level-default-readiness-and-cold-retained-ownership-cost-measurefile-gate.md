# Milestone 017: MeasureFile Release Gate

Status: captured with file-level allocation blocker.

This document records the milestone 017 slice 5 direct `MeasureFile()` Release
gate. It is not the final milestone 017 readiness decision because the
small-file `MeasureCache()` transition gate is still pending.

## Gate Purpose

Slice 5 asks whether the accepted queued-owned direct/default contour is ready
for file-level `MeasureFile()` rows when compared with same-run explicit
`BlockingBorrowed` oracle rows.

The candidate contour under test remained unchanged:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async shard transport
worker count: 4
worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536870912
overlap consumer delay: 0
```

Same-run explicit `BlockingBorrowed` rows remained the oracle. Candidate rows
omitted provider-related direct API arguments so the rollout defaults were
exercised naturally.

## Preconditions

```text
file corpus inventory: complete
thresholds recorded before gate interpretation: yes
focused regression before gate: passed, 112 passed, 0 failed, 0 skipped
selected file sanity before gate: passed for all 10 selected MeasureFile rows
CLI omitted-provider alignment spot-check before gate: passed
explicit BlockingBorrowed fallback spot-check before gate: passed
Release solution build before gate: succeeded, 0 warnings, 0 errors
temporary runner build before gate: succeeded, 0 warnings, 0 errors
product runtime behavior changes in slice 5: none
```

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Temporary direct API runner:

```powershell
dotnet build data\temp\m017-gate-runner\M017GateRunner.csproj -c Release --no-restore
dotnet run --project data\temp\m017-gate-runner\M017GateRunner.csproj -c Release --no-restore
```

The runner lived under `data\temp\m017-gate-runner`, wrote local ignored
reports, and was not committed as a product surface.

Raw local outputs:

```text
data\temp\m017-gate-runner\output\m017-measurefile-20260522-083951.jsonl
data\temp\m017-gate-runner\output\m017-measurefile-20260522-083951.md
```

## Runner Configuration

```text
API: RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
mode: RebalanceSession
iterations: 1
warmup iterations: 0
parallelism: 24
partitions: 24
shards: 4
validation: direct result validation/checksum fields
```

Candidate rows used omitted provider-related arguments:

```text
providerMode: omitted
executionMode: omitted
asyncExecution: omitted
queueCapacity: omitted
providerOverlapMode: omitted
retentionStrategy: omitted
queueRetainedPayloadBytes: omitted
overlapConsumerDelay: omitted
```

Borrowed oracle rows used the same oracle posture documented by the milestone
015 and 016 gates:

```text
providerMode: BlockingBorrowed
executionMode: AsyncShardTransport
asyncExecution: workerCount 4, queueCapacity 1
```

Pair ordering:

```text
cold rows:
  queued-owned omitted-default candidate first
  explicit BlockingBorrowed oracle second

warm rows:
  explicit BlockingBorrowed oracle first
  queued-owned omitted-default candidate second
```

The milestone 016 file-smoke row remains coverage-only. Its ratio should not
be treated as a file-level readiness baseline because milestone 017 added
explicit cold/warm classification and per-row thresholding before capture.

## Thresholds

Thresholds were recorded in slice 3 before this Release gate was interpreted.

```text
cold MeasureFile allocation:
  pass <= 1.10x
  warning <= 1.50x
  optimize <= 1.75x
  fail > 1.75x

cold MeasureFile elapsed:
  pass <= 1.00x
  warning <= 1.10x
  optimize <= 1.25x
  fail > 1.25x

warm MeasureFile allocation:
  pass <= 1.10x
  warning <= 1.20x
  optimize <= 1.35x
  fail > 1.35x

warm MeasureFile elapsed average:
  pass <= 1.00x
  warning <= 1.10x
  optimize <= 1.20x
  fail > 1.20x

warm candidate elapsed spread:
  warning if spread > 7.50%
```

## Selected File Rows

| Row | Group | Radar/date | Role | File | Size |
| --- | --- | --- | --- | --- | ---: |
| prior-ktlx-20260504-representative | prior | KTLX 2026-05-04 | representative | `KTLX20260504_000245_V06` | 5_406_854 |
| ktlx-20260504-small | primary | KTLX 2026-05-04 | small | `KTLX20260504_220338_V06` | 4_403_971 |
| ktlx-20260504-representative | primary | KTLX 2026-05-04 | representative | `KTLX20260504_144229_V06` | 6_087_636 |
| ktlx-20260504-large | primary | KTLX 2026-05-04 | large | `KTLX20260504_034117_V06` | 7_757_670 |
| kinx-20260504-small | cross-radar | KINX 2026-05-04 | small | `KINX20260504_124819_V06` | 5_012_884 |
| kinx-20260504-representative | cross-radar | KINX 2026-05-04 | representative | `KINX20260504_093652_V06` | 6_775_011 |
| kinx-20260504-large | cross-radar | KINX 2026-05-04 | large | `KINX20260504_035026_V06` | 8_453_655 |
| ktlx-20260505-small | named-risk | KTLX 2026-05-05 | small | `KTLX20260505_220542_V06` | 2_120_538 |
| ktlx-20260505-representative | named-risk | KTLX 2026-05-05 | representative | `KTLX20260505_154040_V06` | 5_094_087 |
| ktlx-20260505-large | named-risk | KTLX 2026-05-05 | large | `KTLX20260505_034612_V06` | 8_656_438 |

## Group Summary

| Row | Class | Pairs | Status | Elapsed ratio | Allocation ratio | Candidate spread | Candidate retained high-water |
| --- | --- | ---: | --- | ---: | ---: | ---: | ---: |
| prior-ktlx-20260504-representative | cold | 1 | fail | 1.507x | 2.995x | 0.00% | 48_257_280 |
| prior-ktlx-20260504-representative | warm | 3 | fail | 1.077x | 2.128x | 6.94% | 48_257_280 |
| ktlx-20260504-representative | cold | 1 | fail | 1.735x | 2.060x | 0.00% | 48_257_280 |
| ktlx-20260504-representative | warm | 3 | fail | 1.018x | 2.012x | 3.90% | 48_257_280 |
| kinx-20260504-representative | cold | 1 | fail | 0.585x | 1.958x | 0.00% | 48_342_240 |
| kinx-20260504-representative | warm | 2 | fail | 0.961x | 1.916x | 1.42% | 48_342_240 |
| ktlx-20260505-representative | cold | 1 | fail | 1.809x | 2.235x | 0.00% | 48_257_280 |
| ktlx-20260505-representative | warm | 2 | fail | 1.029x | 2.186x | 4.37% | 48_257_280 |
| ktlx-20260504-small | warm | 1 | fail | 0.950x | 1.709x | 0.00% | 31_079_040 |
| ktlx-20260504-large | warm | 1 | fail | 1.115x | 1.791x | 0.00% | 48_257_280 |
| kinx-20260504-small | warm | 1 | fail | 1.160x | 2.207x | 0.00% | 48_342_240 |
| kinx-20260504-large | warm | 1 | fail | 1.057x | 1.750x | 0.00% | 48_342_240 |
| ktlx-20260505-small | warm | 1 | fail | 1.051x | 1.443x | 0.00% | 48_257_280 |
| ktlx-20260505-large | warm | 1 | fail | 1.048x | 1.748x | 0.00% | 51_484_320 |

## Safety Summary

All 20 borrowed/default pairs passed the safety guardrails.

```text
validation succeeded: all measurement rows
validation checksum parity: all pairs
raw checksum parity: all pairs
stable totals parity: all pairs
topology/rebalance parity: all pairs
retained payload failed releases: 0
provider overlap failed releases: 0
worker failed batches/items: 0/0
candidate current retained pressure at completion: 0
max candidate combined retained high-water: 51_484_320
retained-byte budget: 536_870_912
budget exceeded: no
```

This gate therefore names a cost blocker, not a correctness, cleanup, release,
pressure, fallback, or validation blocker.

## Cost Interpretation

Cold representative rows failed the pre-recorded file-level thresholds:

```text
prior KTLX representative:
  elapsed 1.507x, allocation 2.995x
KTLX 2026-05-04 representative:
  elapsed 1.735x, allocation 2.060x
KINX 2026-05-04 representative:
  elapsed 0.585x, allocation 1.958x
KTLX 2026-05-05 representative:
  elapsed 1.809x, allocation 2.235x
```

Warm representative rows also failed because allocation remained above the
warm fail threshold:

```text
prior KTLX representative:
  elapsed 1.077x, allocation 2.128x
KTLX 2026-05-04 representative:
  elapsed 1.018x, allocation 2.012x
KINX 2026-05-04 representative:
  elapsed 0.961x, allocation 1.916x
KTLX 2026-05-05 representative:
  elapsed 1.029x, allocation 2.186x
```

Small and large warm file rows also failed allocation thresholds:

```text
warm allocation ratio range across small/large rows:
  1.443x to 2.207x
warm elapsed ratio range across small/large rows:
  0.950x to 1.160x
```

Warm repeated candidate spread did not fail the 7.50% spread threshold:

```text
prior KTLX representative spread: 6.94%
KTLX 2026-05-04 representative spread: 3.90%
KINX 2026-05-04 representative spread: 1.42%
KTLX 2026-05-05 representative spread: 4.37%
```

## Attribution

The candidate rows show retained pooled-copy ownership cost on every selected
file shape:

```text
candidate retained payload allocated bytes:
  35_651_864 on the smaller KTLX 2026-05-04 small row
  69_206_296 to 69_206_320 on normal 32_400-event rows
  71_303_448 on the 37_440-event KTLX 2026-05-05 large row

candidate owned snapshot allocated bytes:
  same values as retained payload allocated bytes

candidate event-array pool misses:
  1 per row group

candidate byte-array pool misses:
  1 per row group
```

The dominant cost is therefore still the retained owned snapshot produced by
the current queued-owned pooled-copy architecture. The safety results show the
snapshot is released correctly; the blocker is that direct single-file
`MeasureFile()` does not amortize that retained representation against the
same-run borrowed oracle.

## Slice 5 Result

```text
MeasureFile gate status:
  failed file-level cost thresholds

readiness implication:
  file-level queued-owned direct/default readiness is not accepted by slice 5
  evidence alone

blocker type:
  allocation blocker across cold, warm, small, representative, large,
  primary, cross-radar, and named-risk rows

non-blockers:
  correctness, checksum parity, stable totals, topology parity, release,
  retained cleanup, retained budget, worker failures, fallback visibility,
  CLI/direct alignment

next required evidence:
  slice 6 small-file MeasureCache transition gate, to decide whether low-count
  cache slices amortize the file-level retained cost or preserve the same
  blocker
```

## Post-Gate Cold-Start Prewarm Probe

After the slice 5 blocker was identified, a scoped opt-in prewarm path was
prototyped. The default queued-owned rollout contour was not changed.

Implemented opt-in mechanics:

```text
RadarProcessingRetainedEventArrayPool.Prewarm(...)
RadarProcessingRetainedPayloadByteArrayPool.Prewarm(...)
RadarProcessingRetainedPayloadFactory.Prewarm(...)
RadarProcessingArchiveRebalanceBenchmark.MeasureFile(..., retainedPayloadFactory)
RadarProcessingArchiveRebalanceBenchmark.MeasureCache(..., retainedPayloadFactory)
RadarProcessingArchiveQueuedOverlapOptions retainedPayloadFactory passthrough
```

Purpose:

```text
move large retained event-array and byte-array allocation into an explicit
prewarm step
keep the measured MeasureFile() row honest by making prewarm explicit rather
than hiding it in the default path
prove whether the file-level allocation blocker is mostly cold retained-pool
miss cost
```

Focused tests:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"

54 passed, 0 failed, 0 skipped
```

Release build after the opt-in prototype:

```text
dotnet build RadarPulse.sln -c Release --no-restore

succeeded, 0 warnings, 0 errors
```

Temporary local probe:

```text
data\temp\m017-prewarm-runner
```

Representative-file probe results:

| Row | Natural alloc | Explicit prewarm alloc | Prewarmed alloc | Borrowed alloc | Natural ratio | Prewarmed ratio | Prewarmed pool misses |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| prior-ktlx-representative | 140_040_672 | 69_206_240 | 66_897_928 | 135_078_792 | 1.037x | 0.495x | 0 |
| ktlx-20260504-representative | 138_151_728 | 69_206_240 | 68_420_960 | 70_635_296 | 1.956x | 0.969x | 0 |
| kinx-20260504-representative | 145_442_952 | 69_206_240 | 75_571_896 | 76_091_256 | 1.911x | 0.993x | 0 |
| ktlx-20260505-representative | 127_399_104 | 69_206_240 | 58_212_664 | 58_637_520 | 2.173x | 0.993x | 0 |

Interpretation:

```text
the probe confirms the large retained allocation can be moved out of the
measured MeasureFile() path when a prewarmed retained payload factory is used
prewarm allocation cost is still real and was approximately 69 MB for the
representative file shape
prewarmed MeasureFile() rows had zero retained pool misses
prewarmed representative rows landed near or below same-run borrowed
allocation in this probe
this is not yet a readiness decision because it is a small probe, not the
full selected file matrix with cold/warm classification and thresholds
```

Full prewarmed gate follow-up:

```text
document:
  docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-prewarmed-measurefile-gate.md

result:
  measured allocation blocker removed across the selected file matrix
  prewarmed candidate pool misses were 0 for every row
  prewarm allocation cost remained explicit and real
  targeted repeats downgraded elapsed variance to a non-blocking filesystem
    timing note
```
