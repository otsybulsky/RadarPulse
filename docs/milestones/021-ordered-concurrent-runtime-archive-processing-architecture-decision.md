# Milestone 021: Ordered Commit Architecture Decision

Status: accepted for implementation.

Slice 3 exposed that ordered result buffering alone is not enough. The
accepted implementation direction is:

```text
snapshot/delta/ordered commit, implemented as a per-batch non-mutating delta
pipeline rather than full RadarProcessingCore cloning
```

## Decision

Runtime/archive ordered concurrency should split processing into two stages:

```text
concurrent compute stage:
  validate immutable batch shape
  route and compute per-batch processing deltas without mutating shared
  RadarProcessingCore state
  capture telemetry inputs, payload metrics, checksums, and worker telemetry
  release/cleanup on failure without publishing hidden fallback success

ordered commit stage:
  commit computed deltas strictly by provider sequence
  validate source-local timestamp ordering against the current committed state
  mutate RadarProcessingCore cumulative state
  increment processed batch count
  create cumulative RadarProcessingResult
  run rebalance pressure/topology decisions only after ordered processing
    commit permits it
  publish externally visible results in provider sequence order
```

## Why This Direction

This is preferred over the alternatives:

```text
full core cloning:
  too broad; it requires cloning state store, handler state, pressure,
  topology, telemetry, policy, and merge semantics

source-disjoint limited concurrency:
  too fragile; it is hard to prove for future batch shapes and still does not
  solve cumulative metrics or rebalance commit ordering

non-mutating pre-processing only:
  useful but too narrow if it never reaches ordered processing commit
```

The delta approach gives concurrency on expensive batch routing and payload
metric work while keeping cumulative source state, pressure, policy, and
topology deterministic.

## Performance Constraints

Implementation must preserve the current performance posture:

```text
avoid per-event heap allocation
avoid LINQ in hot processing paths
reuse arrays or array-pool buffers where practical
represent per-source deltas as dense arrays indexed by source id
commit deltas in source-id order for stable checksum construction
keep retained payload prewarm attribution separate from steady allocation
measure focused allocation/performance before milestone gate
```

The initial implementation may limit ordered concurrent delta processing to
cores without custom handlers. Handler state deltas require a separate
handler-delta contract before they can be safely merged without cloning or
side effects.

## Updated Slice 3 Direction

Slice 3 should now implement the minimum safe processing-delta path:

```text
non-mutating batch delta computation for handler-free processing cores
ordered commit into RadarProcessingCore cumulative state
focused parity tests versus existing sequential/async processing
explicit rejection or fallback prohibition for unsupported handler state
```

Rebalance/topology ordered commit remains after processing delta commit. If
rebalance commit requires additional internal structure, it should be handled
as a later slice without weakening processing-state safety.
