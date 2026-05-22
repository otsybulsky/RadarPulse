# Milestone 018: Runtime Readiness Decision Trace

Date: 2026-05-22.

Status: complete.

## Decision

Milestone 018 decision:

```text
explicit opt-in only
```

Queued-owned is runtime-safe for the scoped in-process runtime/archive replay
surfaces when selected explicitly with the startup-prewarmed candidate
lifecycle and the existing queue, pressure, cancellation, failure, cleanup,
and observability guardrails.

Queued-owned is not accepted as the omitted runtime/live ingestion default in
this milestone.

## Decision Boundary

Accepted for explicit opt-in evidence:

```text
in-process queued-owned archive/runtime replay surfaces
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingArchiveQueuedOverlapRunner
RadarProcessingOwnedBatchQueue
RadarProcessingQueuedProcessingSession
RadarProcessingQueuedRebalanceSession
startup-owned retained payload prewarm as explicit candidate lifecycle
deterministic local archive replay as live-input stand-in for steady gates
synthetic leased-batch injection for pressure/failure/cancellation gates
```

Not accepted as default:

```text
omitted runtime/live queued-owned provider defaults
hidden runtime prewarm
natural first-use queued-owned default behavior
automatic borrowed fallback after queued-owned failure
```

Out of scope:

```text
durable queues or brokers
cross-process provider or worker transport
ordered concurrent rebalance
multiple active rebalance-enabled processing batches
builder-transfer retained payload execution
source-level migration or partition splitting
distributed workers
product-facing live radar workflows
production deployment, alerting, rollback, or operator runbooks
```

## Direct Benchmark Evidence

Milestone 017 direct benchmark/file/cache readiness remains valid input
evidence:

```text
direct MeasureFile()/MeasureCache() default-equivalent contour:
  queued-owned
  producer-consumer overlap
  pooled-copy retained payload ownership
  async shard transport
  worker count 4
  worker queue capacity 8
  provider queue capacity 8
  retained-byte budget 536870912
  retained payload prewarm enabled
```

Milestone 017 does not automatically approve runtime/live defaults. Milestone
018 required separate lifecycle, pressure, cancellation, cleanup, fallback,
failure, observability, and runtime cost evidence.

## Runtime Provider Posture

Runtime provider default:

```text
unchanged
not promoted to queued-owned omitted default
```

Runtime queued-owned candidate:

```text
explicit opt-in only
```

Reason:

```text
the startup-prewarmed candidate passes bounded steady evidence
runtime lifecycle and pressure/failure gates pass for scoped in-process
  surfaces
natural first-use remains allocation-blocked for omitted default readiness
true live ingestion and production default/operator surfaces remain coverage
  gaps
```

## Runtime Prewarm Posture

Accepted only as explicit candidate lifecycle:

```text
startup-owned retained payload prewarm
65_536 events
67_108_864 payload bytes
1 retained batch
```

Not accepted:

```text
hidden runtime prewarm
implicit omitted-default prewarm
folding prewarm allocation into steady row allocation
prewarming borrowed/reference rows
prewarming natural first-use control rows
```

Slice 6 prewarm attribution:

```text
allocated bytes per prewarm row: 71_303_392
retained bytes per prewarm row: 71_303_168
retained-byte budget: 536_870_912
```

## Steady Intake Gate

Slice 6 steady intake gate:

```text
rows: 12
pass safety guardrails: 12
processing completeness failures: 0
worker failure rows: 0
release failure rows: 0
terminal pressure failure rows: 0
max queue depth high-watermark: 1
max combined retained bytes high-watermark: 48_342_240
```

Startup-prewarmed queued-owned candidate:

```text
elapsed ratios versus borrowed/reference:
  0.910x, 0.980x, 0.955x, 0.997x

allocation ratios versus borrowed/reference:
  1.000x, 1.001x, 1.000x, 1.002x
```

Interpretation:

```text
passes bounded steady intake evidence
passes processing completeness
passes worker failure guardrail
passes release and terminal pressure cleanup guardrails
```

Natural first-use queued-owned control:

```text
allocation ratios versus borrowed/reference:
  1.196x, 2.040x, 1.284x, 1.373x
```

Interpretation:

```text
warning/optimize/fail control evidence
does not support omitted runtime default readiness by itself
```

## Pressure And Failure Gate

Slice 7 pressure/failure gate:

```text
rows: 11
pass: 11
fail: 0
terminal pressure clean rows: 11
backpressure rows: 3
cancellation rows: 4
failure rows: 6
release-failure visible rows: 1
max queue depth high-watermark: 3
max combined retained bytes high-watermark: 6
```

Accepted runtime behavior:

```text
queue capacity pressure is visible through full rejection
retained-byte pressure is visible through full rejection
wait-on-full pressure is visible through timeout rejection
enqueue cancellation before start and while waiting is visible
CancelQueued shutdown records accepted pending work as canceled
overlap cancellation after accepted enqueue releases pending resources
active cancellation releases active retained resources
normal drain completes accepted pending work
processing validation failure faults without borrowed fallback
producer failure releases pending retained resources
release failure remains visible and readiness-blocking
all rows return current retained pressure to zero
```

## Fallback And Oracle Posture

Explicit BlockingBorrowed remains:

```text
same-run reference/oracle where supported
separate from queued-owned candidate rows
unprewarmed in slice 6 reference rows
```

Rejected behavior:

```text
automatic silent fallback from queued-owned failure to borrowed success
overwriting queued-owned failure with borrowed success
using borrowed/reference rows to hide queued-owned lifecycle failure
```

No gate evidence observed automatic borrowed fallback.

## Failure, Cancellation, And Cleanup Posture

Accepted for scoped in-process surfaces:

```text
provider enqueue success is not reported as processing completion
processing completeness is required for accepted readiness rows
processing validation failure is visible and faults the consumer
worker failed batches/items remain gateable readiness blockers
release failures remain readiness blockers
cancellation paths return terminal retained pressure to zero
producer and consumer failure paths return terminal retained pressure to zero
drain paths complete accepted work and return terminal retained pressure to
  zero
```

Release failure note:

```text
the injected release-failure row completed processing but still produced
  release failure count 1 and readiness error RetainedResourceReleaseFailed
this is correct visibility/blocking behavior, not a successful readiness row
```

## Observability Posture

Existing lower-level contracts plus temporary gate output were sufficient for
this milestone decision:

```text
provider mode/provenance
prewarm sizing and attribution
queue attempts, accepted, rejected, full, timeout, canceled, and faulted
  counts
queue depth and retained pressure high-watermarks
pending, active, and combined retained cleanup state
retained payload retention and release telemetry
processing completeness
processing validation failed batch count
worker failure count
cancellation and fault terminal states
release failure visibility
raw JSONL plus Markdown gate summaries
```

Production operator-facing runtime reporting remains future work before
broader rollout.

## Accepted Warnings

The explicit opt-in decision carries these warnings:

```text
startup prewarm is accepted only as explicit lifecycle cost
natural first-use remains allocation-blocked for omitted default readiness
bounded steady gate did not include repeated variance classification
pressure/failure gates used synthetic leased-batch injection
true live ingestion remains unproven
durable/cross-process behavior remains unproven and out of scope
production runtime selection and operator reporting were not added
```

## Blockers

No implementation blocker remains inside the scoped milestone 018
in-process runtime/archive replay surface.

Default readiness remains blocked by scope and evidence limits:

```text
natural first-use allocation misses
no accepted omitted runtime prewarm lifecycle
no true live ingestion evidence
no production runtime default/operator rollout surface
no durable or cross-process evidence
```

## Next Milestone Input

Recommended next milestone direction:

```text
gradual runtime rollout for queued-owned explicit opt-in
production runtime provider selection surface
operator-visible runtime reporting for provider/prewarm/pressure/failure
explicit startup prewarm lifecycle wiring where selected
repeatability gates for startup-prewarmed runtime rows
true live ingestion adapter evidence or a narrower named archive-runtime
  rollout target
keep BlockingBorrowed as explicit fallback/oracle, not automatic silent
  fallback
```

The next milestone should decide whether to target:

```text
archive-runtime explicit opt-in rollout only
live ingestion adapter evidence
production operator reporting and config
repeatability and variance gates
runtime default promotion after gradual rollout evidence
```

## Final Answer For Milestone 018

```text
explicit opt-in only, queued-owned is runtime-safe when selected explicitly for
the scoped in-process runtime/archive replay surfaces, but it is not accepted
as the omitted runtime/live ingestion default
```
