# Processing Runtime

Status: active under milestone 037.

This page explains the accepted processing runtime posture at maintainer
depth. It connects the major runtime ideas without documenting every class.
Use [workflows.md](workflows.md) for end-to-end flow and
[source-map.md](source-map.md) for folder navigation.

## Accepted Runtime Boundary

RadarPulse has a local deterministic runtime/archive boundary. The accepted
runtime can process archive-shaped batches through queued-owned providers,
ordered concurrent processing, durable envelope/recovery contracts, handler
output, product read models, and local diagnostics.

It does not claim:

```text
true live radar network ingestion
external broker/cloud queue/database adapter certification
production deployment or production operations
exactly-once end-to-end production delivery
cross-machine performance certification
```

The runtime is strong local evidence, not production infrastructure
certification.

## Baseline Profile

The named runtime/archive baseline is:

```text
RadarProcessingRuntimeArchiveBaseline
```

Accepted contour:

```text
provider mode:
  queued-owned

provider overlap:
  producer-consumer

retention strategy:
  pooled-copy retained payload ownership

provider queue capacity:
  8

retained-byte budget:
  536870912

startup retained payload prewarm:
  enabled and visible as lifecycle cost

execution:
  async shard transport

worker count:
  4

worker queue capacity:
  8

ordered active batch capacity:
  4
```

Surfaces that own processing core or rebalance session construction can use
the baseline helpers. Caller-supplied processing cores and sessions keep
explicit ownership of their execution settings.

## Runtime Input

The runtime consumes `RadarEventBatch` input. Batches usually come from:

```text
synthetic product input:
  deterministic demo path for local portfolio runs

archive file input:
  local NEXRAD/Archive II path projected into stream events and then batches

benchmark input:
  deterministic synthetic or archive-shaped workloads for gate evidence
```

Before processing, batch shape and source identity are validated by Streaming
and Processing contracts:

```text
source identities must be stable
batch metrics/checksums must be deterministic
payload resources must have clear ownership
provider sequence must preserve accepted external order
```

## Queued-Owned Provider

The queued-owned provider posture means accepted batches are owned while they
move through queueing and processing.

Responsibilities:

```text
accept batches into a bounded queue
track provider sequence
retain payload resources while pending or active
apply backpressure when queue/resource limits are reached
release retained payloads after commit, cancellation, failure, or cleanup
report queue, retention, release, and readiness diagnostics
fail closed when the accepted safety posture is violated
```

Important folders:

```text
src/Domain/Processing/Queueing
src/Domain/Processing/Retention
src/Infrastructure/Processing/Queueing
src/Infrastructure/Processing/Retention
tests/RadarPulse.Tests/Processing/Queueing
tests/RadarPulse.Tests/Processing/Retention
```

## Ordered Concurrent Processing

RadarPulse may compute multiple accepted batches concurrently, but externally
visible commit is ordered by provider sequence.

Flow:

```text
accepted batch enters queue
  -> active worker computes non-mutating processing delta
  -> completions may finish out of order
  -> ordered coordinator waits for the next provider sequence
  -> commit mutates shared processing state
  -> cumulative result and diagnostics are published
```

Key invariant:

```text
completion order is not commit order
provider sequence is commit order
```

Why this matters:

```text
concurrency improves throughput without allowing later batches to become
visible before earlier batches
shared processing state is mutated only during ordered commit
diagnostics can identify missing, failed, blocked, or skipped earlier work
```

Important folders:

```text
src/Domain/Processing/Core
src/Domain/Processing/Async
src/Domain/Processing/Workers
src/Infrastructure/Processing/Async
src/Infrastructure/Processing/Workers
src/Infrastructure/Processing/Queueing
tests/RadarPulse.Tests/Processing/Core
tests/RadarPulse.Tests/Processing/Async
tests/RadarPulse.Tests/Processing/Workers
```

## Rebalance And Topology Commit

Rebalance changes partition/topology state in response to pressure. That
state cannot be safely mutated by worker completion order.

Accepted posture:

```text
processing deltas may be computed concurrently
rebalance decisions, validation, migration, pressure/quarantine telemetry,
and topology mutation commit only by provider sequence
if an active batch was computed against a stale topology version, it is
recomputed against the current topology before ordered commit
```

Important folders:

```text
src/Domain/Processing/Rebalance
src/Domain/Processing/Topology
src/Domain/Processing/Pressure
src/Infrastructure/Processing/Queueing
tests/RadarPulse.Tests/Processing/Rebalance
tests/RadarPulse.Tests/Processing/Topology
tests/RadarPulse.Tests/Processing/Pressure
```

## Durable Envelope And Recovery

The durable contract separates worker completion from externally visible
commit. It gives accepted batches stable identity and explicit lifecycle
state.

Durable envelope state includes:

```text
pending
claimed
completed
committed
failed
poison
abandoned
canceled
released
```

Accepted durable behavior:

```text
stable durable batch id
stable provider sequence
attempt counters
explicit claim, complete, fail, abandon, retry, poison, commit, release, and
  cancellation cleanup
operator-readable readiness and first blocking envelope/reason
local file-backed persistence for deterministic restart/recovery proof
handler delta replay compatibility for mergeable handlers
```

Limits:

```text
file-based persistence is local deterministic evidence
external broker/cloud/database adapters are not included or planned
production broker durability and exactly-once delivery are not claimed
```

Important folders:

```text
src/Domain/Processing/Durable
src/Infrastructure/Processing/Durable
src/Infrastructure/Processing/Contracts
tests/RadarPulse.Tests/Processing/Durable
```

## Handler Execution Posture

Handlers are processing extensions that compute source-level output.

Accepted handler classes:

```text
handler-free:
  no handler state/output participates in the runtime

mergeable:
  handler explicitly supports deterministic per-batch delta compute and
  provider-sequence merge

snapshot-only:
  handler can export committed snapshots but does not have an accepted
  concurrent delta/merge contract, so it uses sequential fallback

unsupported:
  handler set fails closed with diagnostics
```

Mergeable handler flow:

```text
batch is processed concurrently
  -> handler delta is created with deterministic identity and metadata
  -> out-of-order completed deltas wait for missing earlier provider sequence
  -> equivalent duplicate replay is ignored
  -> conflicting duplicate replay is rejected
  -> committed merged output is projected into read models
```

Important folders:

```text
src/Domain/Processing/Handlers
src/Infrastructure/Processing/Queueing
src/Infrastructure/Product/Pipeline/Handlers
tests/RadarPulse.Tests/Processing/Handlers
```

## Retention And Pressure

Retention and pressure make resource ownership visible.

Retention tracks:

```text
retained payload ownership
pending and active retained batch counts
release success/failure
resource pressure snapshots
pool miss evidence
cleanup after cancellation/failure
```

Pressure and quarantine track:

```text
shard/partition pressure samples
rolling pressure windows
hot partition classification
pressure skew transformation
quarantine lifecycle transitions
cooldown and effective classification
```

These signals feed readiness, rebalance, diagnostics, and capacity evidence.

Important folders:

```text
src/Domain/Processing/Retention
src/Domain/Processing/Pressure
src/Infrastructure/Processing/Retention
tests/RadarPulse.Tests/Processing/Retention
tests/RadarPulse.Tests/Processing/Pressure
```

## Product Pipeline Projection

The product pipeline turns runtime output into stable product read models.

Flow:

```text
runtime result
  -> processing run read model
  -> product run detail
  -> product history store
  -> product API responses
  -> HTTP, CLI, Operator UI
```

Product output preserves:

```text
run identity
batch list/detail
provider sequence
source summaries
handler catalog/output values
diagnostics
capacity evidence
history readiness
control summaries
```

Important folders:

```text
src/Application/Processing/ReadModels
src/Application/Product/Pipeline/Models
src/Infrastructure/Processing/ProductPipeline
src/Infrastructure/Product/Pipeline
src/Infrastructure/Product/History
tests/RadarPulse.Tests/Processing/ReadModels
tests/RadarPulse.Tests/Product
```

## Diagnostic Rules

Runtime diagnostics should make failure and warning states visible:

```text
do not hide first blocking reason
do not hide retained pressure
do not hide release failures
do not hide worker failures
do not hide fallback posture
do not report handler output as fast-path when it used sequential fallback
do not report external production durability from local durable evidence
```

The accepted product surface should help an operator answer:

```text
did processing complete?
which batch/source/handler output is visible?
which provider sequence is blocked?
what was the first blocking reason?
were retained resources released?
is history ready?
what handler mode was used?
what capacity/completeness evidence exists?
```

## Milestone Evidence

Runtime evidence is spread across milestones. The current interpretation
comes mainly from:

```text
docs/milestones/020-default-baseline-runtime-archive-integration-closeout.md
  named runtime/archive baseline

docs/milestones/021-ordered-concurrent-runtime-archive-processing-closeout.md
  ordered active-batch processing and provider-sequence commit

docs/milestones/022-ordered-rebalance-topology-commit-closeout.md
  ordered rebalance/topology commit and stale topology recompute

docs/milestones/023-durable-cross-process-runtime-readiness-closeout.md
  broker-neutral durable envelope contract

docs/milestones/024-custom-handler-output-contract-and-bff-readiness-closeout.md
  product-facing handler output and read models

docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-closeout.md
  mergeable handler delta/merge contract

docs/milestones/026-persistent-durable-adapter-readiness-closeout.md
  deterministic local file-based durable adapter

docs/milestones/027-production-pipeline-integration-closeout.md
  production-shaped pipeline integration
```

## Verification

Use focused tests for narrow runtime areas:

```text
Queueing:
  dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Queueing" -c Release --no-restore

Durable:
  dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Durable" -c Release --no-restore

Handlers:
  dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Handler" -c Release --no-restore

Rebalance:
  dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Rebalance" -c Release --no-restore

Product pipeline:
  dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~ProductPipeline|FullyQualifiedName~Product" -c Release --no-restore
```

Use the full Release suite when changing ordering, shared contracts,
retention/release behavior, durable lifecycle, handler merge semantics, or
product-visible runtime output.
