# Milestone 017: Small-File MeasureCache Gate

Status: captured with natural small-file allocation blocker and explicit
prewarmed comparison pass.

This document records the milestone 017 low-count direct `MeasureCache()` gate.
The gate was captured after the slice 5 direct `MeasureFile()` evidence showed
that retained owned snapshot allocation is the file-level blocker.

The default queued-owned rollout contour was not changed. Prewarm remained an
explicit opt-in comparison contour.

## Gate Purpose

The gate asks whether low-count `MeasureCache()` slices amortize the
file-level retained owned snapshot cost enough for small-file readiness.

The natural candidate contour:

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
retained payload factory: default, no hidden prewarm
```

Same-run explicit `BlockingBorrowed` rows remained the oracle:

```text
providerMode: BlockingBorrowed
executionMode: AsyncShardTransport
asyncExecution: workerCount 4, queueCapacity 1
```

## Preconditions

```text
slice 5 natural MeasureFile gate captured: yes
slice 5 prewarmed MeasureFile comparison captured: yes
Release solution build before gate: succeeded, 0 warnings, 0 errors
natural temporary runner build: succeeded, 0 warnings, 0 errors
prewarmed comparison runner build: succeeded, 0 warnings, 0 errors
```

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Natural temporary direct API runner:

```powershell
dotnet build data\temp\m017-small-cache-gate-runner\M017SmallCacheGateRunner.csproj -c Release --no-restore
dotnet run --project data\temp\m017-small-cache-gate-runner\M017SmallCacheGateRunner.csproj -c Release --no-restore
```

Prewarmed comparison runner:

```powershell
dotnet build data\temp\m017-prewarmed-small-cache-gate-runner\M017PrewarmedSmallCacheGateRunner.csproj -c Release
dotnet run --project data\temp\m017-prewarmed-small-cache-gate-runner\M017PrewarmedSmallCacheGateRunner.csproj -c Release --no-restore
```

The runners lived under `data\temp`, wrote ignored local reports, and were not
committed as product surfaces.

Raw local outputs:

```text
data\temp\m017-small-cache-gate-runner\output\m017-small-cache-20260522-094609.jsonl
data\temp\m017-small-cache-gate-runner\output\m017-small-cache-20260522-094609.md

data\temp\m017-prewarmed-small-cache-gate-runner\output\m017-prewarmed-small-cache-20260522-094843.jsonl
data\temp\m017-prewarmed-small-cache-gate-runner\output\m017-prewarmed-small-cache-20260522-094843.md
```

## Runner Configuration

```text
API: RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
mode: RebalanceSession
iterations: 1
warmup iterations: 0
parallelism: 24
partitions: 24
shards: 4
pairs per slice: 2
pair order: explicit BlockingBorrowed, then candidate
classification: by observed published base-data count, not raw max-files
```

Selected slices:

```text
KTLX 2026-05-04:
  max-files 2/4/8 -> published 2/4/8, skipped 0/0/0

KINX 2026-05-04:
  max-files 4/8/16 -> published 2/4/8, skipped 2/4/8

KTLX 2026-05-05:
  max-files 4/8/16 -> published 2/4/8, skipped 2/4/8
```

## Natural Gate Summary

| Slice | Published | Skipped | Status | Elapsed ratio | Allocation ratio | Candidate spread | Retained high-water | Release failures | Worker failures |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| KTLX 2026-05-04 max-files 2 | 2 | 0 | warning | 0.521x | 1.176x | 10.98% | 48_257_280 | 0 | 0/0 |
| KTLX 2026-05-04 max-files 4 | 4 | 0 | optimize | 0.909x | 1.284x | 2.94% | 48_257_280 | 0 | 0/0 |
| KTLX 2026-05-04 max-files 8 | 8 | 0 | warning | 0.900x | 1.189x | 0.06% | 48_257_280 | 0 | 0/0 |
| KINX 2026-05-04 max-files 4 | 2 | 2 | fail | 0.986x | 2.063x | 0.09% | 48_342_240 | 0 | 0/0 |
| KINX 2026-05-04 max-files 8 | 4 | 4 | optimize | 0.956x | 1.375x | 7.37% | 48_342_240 | 0 | 0/0 |
| KINX 2026-05-04 max-files 16 | 8 | 8 | optimize | 0.899x | 1.279x | 3.90% | 48_342_240 | 0 | 0/0 |
| KTLX 2026-05-05 max-files 4 | 2 | 2 | fail | 0.958x | 2.168x | 4.14% | 48_257_280 | 0 | 0/0 |
| KTLX 2026-05-05 max-files 8 | 4 | 4 | fail | 0.947x | 2.077x | 6.72% | 48_257_280 | 0 | 0/0 |
| KTLX 2026-05-05 max-files 16 | 8 | 8 | fail | 0.888x | 1.988x | 1.76% | 48_257_280 | 0 | 0/0 |

## Natural Safety Summary

All 18 borrowed/natural-candidate pairs passed safety guardrails.

```text
validation succeeded: all measurement rows
validation checksum parity: all pairs
raw checksum parity: all pairs
stable totals parity: all pairs
examined/skipped/published file counts matched selected slice expectations
topology/rebalance parity: all pairs
retained payload failed releases: 0
provider overlap failed releases: 0
worker failed batches/items: 0/0
candidate current retained pressure at completion: 0
retained-byte budget exceeded: no
```

## Natural Interpretation

Natural low-count `MeasureCache()` did not amortize the retained owned snapshot
cost enough for small-file readiness.

```text
elapsed:
  every natural group was at or below 0.986x same-run borrowed average
  elapsed is not the small-cache blocker

allocation:
  natural allocation ratios ranged from 1.176x to 2.168x
  only KTLX 2026-05-04 max-files 2 stayed within allocation pass bands
  KTLX 2026-05-04 max-files 8 remained a warning row
  KTLX 2026-05-04 max-files 4 and KINX 4/8-file slices were optimize rows
  KINX 2-file and all KTLX 2026-05-05 slices were fail rows

amortization:
  8 published files are still insufficient for a stable natural pass
  KTLX 2026-05-05 stayed near 2.0x allocation through 8 published files
```

The natural small-file cache result preserves the slice 5 conclusion: the
current default queued-owned contour is safety-clean, but the retained owned
snapshot allocation cost remains a small-file blocker.

## Prewarmed Comparison Summary

Because the natural gate reproduced the allocation blocker, an explicit
prewarmed comparison was captured as slice 7 input. This was not a default
posture change.

| Slice | Published | Skipped | Status | Elapsed ratio | Measured allocation ratio | Prewarm avg alloc | Pool misses | Candidate spread |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: |
| KTLX 2026-05-04 max-files 2 | 2 | 0 | pass | 0.454x | 0.818x | 69_206_252 | 0 | 4.18% |
| KTLX 2026-05-04 max-files 4 | 4 | 0 | pass | 0.934x | 0.998x | 69_206_240 | 0 | 0.42% |
| KTLX 2026-05-04 max-files 8 | 8 | 0 | pass | 0.871x | 0.996x | 69_206_240 | 0 | 0.62% |
| KINX 2026-05-04 max-files 4 | 2 | 2 | pass | 0.945x | 1.000x | 69_206_240 | 0 | 1.46% |
| KINX 2026-05-04 max-files 8 | 4 | 4 | pass | 0.937x | 0.998x | 69_206_240 | 0 | 0.57% |
| KINX 2026-05-04 max-files 16 | 8 | 8 | pass | 0.919x | 0.998x | 69_206_240 | 0 | 0.54% |
| KTLX 2026-05-05 max-files 4 | 2 | 2 | pass | 0.979x | 1.000x | 69_206_240 | 0 | 0.07% |
| KTLX 2026-05-05 max-files 8 | 4 | 4 | pass | 0.943x | 1.000x | 69_206_240 | 0 | 0.18% |
| KTLX 2026-05-05 max-files 16 | 8 | 8 | pass | 0.894x | 1.002x | 69_206_240 | 0 | 0.49% |

## Prewarmed Comparison Interpretation

The explicit prewarmed comparison removed the measured small-cache allocation
blocker.

```text
all 18 borrowed/prewarmed-candidate pairs passed safety guardrails
all prewarmed candidate rows had retained pool misses 0
measured allocation ratios ranged from 0.818x to 1.002x
elapsed ratios ranged from 0.454x to 0.979x
candidate elapsed spread stayed below 4.18%
prewarm allocation remained real and explicit at approximately 69_206_240
  bytes per measured candidate row
```

The correct interpretation is the same as the prewarmed `MeasureFile()` gate:
explicit prewarm moves the retained event-array and byte-array allocation out
of the measured row. It does not make the allocation disappear.

## Gate Result

```text
natural small-file readiness:
  not accepted by slice 6 evidence alone

natural blocker:
  retained owned snapshot allocation cost persists through 2/4/8 published
  small-file cache slices

safety blocker:
  none

timing blocker:
  none; natural and prewarmed elapsed rows were at or below borrowed averages

prewarmed comparison:
  all selected small-cache slices passed measured allocation and elapsed
  thresholds with retained pool misses 0

readiness implication:
  MeasureFile and small-file MeasureCache now point at the same decision:
  natural defaults remain allocation-blocked for file/small-file readiness,
  while explicit prewarm is allocation-ready if its up-front cost is accepted
  and attributed

next interpretation input:
  slice 7 should decide whether to keep MeasureFile/small-file defaults
  aligned with natural cache-level defaults, split file/small-file behavior
  behind explicit prewarm, or close milestone 017 with an optimization/posture
  recommendation before runtime expansion
```
