# Milestone 018: Runtime Steady Intake Gate

Status: complete.

This document records the slice 6 runtime-shaped steady intake gate. The gate
uses deterministic local archive replay as a live-input stand-in. It does not
prove true live network ingestion, durable broker behavior, or cross-process
worker behavior.

## Runner

Temporary runner:

```text
data\temp\m018-runtime-gate-runner
```

Committed production code changes:

```text
none
```

The runner uses:

```text
queued-owned candidate:
  RadarProcessingArchiveQueuedOverlapRunner
  ArchiveOwnedRadarEventBatchQueueingPublisher
  RadarProcessingQueuedRebalanceSession
  producer-consumer overlap
  pooled-copy retained payload ownership

borrowed/reference:
  blocking borrowed archive publisher callback
  same rebalance session shape
  no retained payload prewarm

startup-prewarmed candidate:
  explicit harness-owned RadarProcessingRetainedPayloadFactory
  Prewarm(65_536, 67_108_864, 1)
  factory passed through RadarProcessingArchiveQueuedOverlapOptions
```

Runtime contour:

```text
execution: async shard transport
partition count: 24
shard count: 4
worker count: 4
worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536_870_912
provider overlap: producer-consumer
retention strategy: pooled-copy
runtime default posture: undecided
queued-owned posture: explicit runtime gate candidate
```

## Workloads

The gate used bounded local base-data selections:

```text
B1 KTLX 2026-05-04:
  8 base-data files

B2 KTLX 2026-05-05:
  8 base-data files

B3 KINX 2026-05-04:
  8 base-data files

B4 mixed KINX/KTLX 2026-05-04:
  8 base-data files total
```

Rows per workload:

```text
borrowed-reference
queued-owned-natural-first-use
queued-owned-startup-prewarmed
```

Raw output:

```text
data\temp\m018-runtime-gate-runner\output\m018-runtime-20260522-134534.jsonl
data\temp\m018-runtime-gate-runner\output\m018-runtime-20260522-134534.md
```

Smoke output from the same runner with `M018_BASE_DATA_LIMIT=1`:

```text
data\temp\m018-runtime-gate-runner\output\m018-runtime-20260522-134509.md
```

## Safety Summary

All rows passed non-negotiable steady intake safety guardrails:

```text
rows: 12
pass: 12
fail: 0
processing completeness failures: 0
worker failure rows: 0
release failure rows: 0
terminal pressure failure rows: 0
max queue depth high-watermark: 1
max combined retained bytes high-watermark: 48_342_240
```

The observed retained pressure high-watermark stayed below the configured
536_870_912 byte retained-byte budget.

## Steady Cost Results

Elapsed and allocation ratios below are versus the borrowed/reference row in
the same workload.

| Workload | Natural elapsed | Natural alloc | Prewarmed elapsed | Prewarmed alloc |
|---|---:|---:|---:|---:|
| B1 KTLX 2026-05-04 | 0.918x | 1.196x | 0.910x | 1.000x |
| B2 KTLX 2026-05-05 | 0.974x | 2.040x | 0.980x | 1.001x |
| B3 KINX 2026-05-04 | 0.963x | 1.284x | 0.955x | 1.000x |
| B4 mixed KINX/KTLX | 1.005x | 1.373x | 0.997x | 1.002x |

Prewarmed queued-owned candidate interpretation:

```text
elapsed:
  pass in all workloads

allocation:
  pass in all workloads

processing completeness:
  pass in all workloads

worker failures:
  0/0 in all workloads

release failures:
  0 in all workloads

terminal retained pressure:
  clean in all workloads
```

Natural first-use control interpretation:

```text
B1:
  allocation warning at 1.196x

B2:
  allocation fail at 2.040x

B3:
  allocation optimize at 1.284x

B4:
  allocation fail at 1.373x

conclusion:
  natural first-use remains useful control evidence, but does not support
  runtime default readiness without an accepted lifecycle-cost policy
```

## Prewarm Attribution

Startup prewarm was explicit and outside measured steady row allocation.

| Workload | Event count | Payload bytes | Retained batches | Elapsed ms | Alloc bytes | Retained bytes |
|---|---:|---:|---:|---:|---:|---:|
| B1 | 65_536 | 67_108_864 | 1 | 0.5 | 71_303_392 | 71_303_168 |
| B2 | 65_536 | 67_108_864 | 1 | 0.1 | 71_303_392 | 71_303_168 |
| B3 | 65_536 | 67_108_864 | 1 | 2.4 | 71_303_392 | 71_303_168 |
| B4 | 65_536 | 67_108_864 | 1 | 1.1 | 71_303_392 | 71_303_168 |

Interpretation:

```text
prewarm retained bytes are below the retained-byte budget
prewarm allocation is explicit lifecycle cost
prewarm allocation is not hidden in steady measured allocation
borrowed/reference rows remain unprewarmed
natural first-use rows remain unprewarmed
```

## Classification

Slice 6 classification:

```text
steady intake safety:
  pass for bounded deterministic local archive replay rows

startup-prewarmed queued-owned candidate:
  pass for steady elapsed, allocation, processing completeness, worker
  health, release health, and terminal retained pressure

natural first-use queued-owned control:
  allocation warning/optimize/fail depending on workload; not acceptable as
  runtime default evidence by itself

borrowed/reference separation:
  preserved
```

## Coverage Limits

This gate does not close:

```text
true live ingestion coverage
durable queue or broker coverage
cross-process provider/worker coverage
pressure/backpressure/cancellation/failure gate coverage
repeatability or variance classification beyond this bounded run
release-failure runtime replay coverage
runtime default migration
```

## Outcome

Slice 6 is complete.

The explicit startup-prewarmed queued-owned runtime candidate passes bounded
steady intake gate evidence. The natural first-use control remains allocation
blocked for default-readiness interpretation. Proceed to slice 7 pressure,
backpressure, cancellation, and failure gate capture before any decision trace.
