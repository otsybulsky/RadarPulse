# Milestone 025: Closeout

## Status

Milestone 025 is complete.

RadarPulse now has the scoped in-process handler delta/merge contract needed
for fast custom analytics over deterministic archive-shaped MVP workloads.
The accepted runtime can keep handler-free ordered concurrency, route
explicitly mergeable stateful handlers through ordered concurrent handler
delta compute, preserve sequential fallback for snapshot-only handlers, fail
closed for unsupported handler sets, and expose committed merged handler
output through the milestone 024 read models.

The important milestone result is:

```text
020 accepted RadarProcessingRuntimeArchiveBaseline as the named construction
    profile for composing queued-owned provider defaults with async shard
    transport execution defaults.
021 accepted non-mutating per-batch processing delta compute plus
    provider-sequence ordered commit for the scoped processing-core
    runtime/archive path.
022 accepted ordered rebalance/topology commit for handler-free processing
    deltas, including stale topology recompute before provider-sequence
    topology mutation.
023 accepted the broker-neutral durable envelope contract and deterministic
    in-process durable harness for scoped durable/cross-process runtime
    readiness.
024 accepted committed custom handler output, processing read models, BFF
    query shapes, and MVP readiness diagnostics.
025 adds the mergeable handler fast path: per-batch handler deltas can be
    computed concurrently and merged deterministically by provider sequence.
025 preserves the conservative fallback posture for snapshot-only and
    unsupported handlers.
025 optimized merge state enough that the captured full-cache active=4
    handler delta/merge rows are no longer elapsed-time blockers versus
    active=1.
```

Final readiness posture:

```text
accepted with scoped warnings for handler delta/merge contract and fast
custom analytics over deterministic archive-shaped MVP workloads
```

The accepted warnings and limits are:

```text
mergeable handler boundary:
  the accepted fast path applies only to handlers that explicitly opt into
  deterministic mergeable semantics

arbitrary stateful handlers:
  not made concurrent by default

snapshot-only handlers:
  keep explicit sequential fallback and committed snapshot export

delta serialization:
  accepted as an in-process/versioned contract gate, not a production
  persistent adapter proof

performance certification:
  the focused performance gate is deterministic in-process evidence, not
  cross-machine or production throughput certification

active=4 allocation:
  optimized full-cache elapsed time is flat versus active=1, but allocation
  remains higher than active=1

persistent durable adapter:
  not implemented; now recommended as the next reliability milestone

true live network ingestion:
  not implemented

production BFF and frontend:
  production HTTP BFF host and frontend application are not implemented

production operations:
  deployment, rollback, autoscaling, alerts, and runbooks are not implemented

exactly-once production delivery:
  not claimed; future storage/downstream idempotency gates are needed

known full-suite residual risk:
  one allocation-sensitive synthetic benchmark caveat remains outside the
  focused handler delta/merge gate and passed in isolated rerun
```

## Final Outcome

Implemented:

- Handler execution classification for handler-free, mergeable,
  snapshot-only, and unsupported postures.
- Runtime posture validation that keeps unclassified stateful handlers out
  of ordered concurrent handler delta compute.
- Per-batch `RadarProcessingHandlerDelta` identity, metadata, validation,
  values, deterministic id, and schema versioning.
- In-memory handler delta serialization roundtrip and version mismatch
  diagnostics.
- Deterministic ordered `RadarProcessingHandlerDeltaMergeCoordinator`.
- Out-of-order completion with provider-sequence merge.
- Duplicate replay idempotency and conflicting duplicate rejection.
- First blocking sequence and first blocking reason diagnostics.
- Handler-owned accumulator extension for optimized incremental merge state.
- Lightweight commit merge result for the ordered commit path.
- Sparse touched-source handler delta compute for ordered concurrent MVP
  processing.
- Grouped changed handler value commit without rebuilding broad merged
  snapshots on every committed batch.
- MVP runtime selection for mergeable, snapshot-only, unsupported, and
  handler-free handler sets.
- BFF/read-model compatibility for merged handler output and diagnostics.
- Standard and heavy mergeable benchmark handlers.
- Handler-heavy deterministic performance gate.
- Full-cache handler performance matrix over `data\nexrad`.
- Merge-state optimization after the initial full-cache matrix.
- Gate evidence, decision trace, closeout, handoff, and project-progress
  updates.

Not implemented here:

- Arbitrary stateful handler concurrency without mergeable opt-in.
- Production persistent durable adapter implementation.
- External broker/cloud queue/database runtime adapter; these adapters are
  not planned for this project.
- Production HTTP BFF host.
- Frontend application.
- True live network ingestion.
- Production deployment, rollback, autoscaling, alerting, or runbooks.
- Exactly-once production delivery.
- Cross-machine performance certification.
- Active=4 allocation parity with active=1.
- Changing `RadarProcessingCoreOptions.Default`.
- Reopening the milestone 020 provider/execution baseline decision.
- Reopening the milestone 021 processing delta architecture decision.
- Reopening the milestone 022 ordered rebalance/topology decision.
- Reopening the milestone 023 durable runtime contract decision.
- Reopening the milestone 024 custom handler output/BFF decision.

Still rejected:

```text
running arbitrary stateful handlers through ordered concurrent delta compute
  without a mergeable contract
using worker completion order as handler output order
double-counting duplicate handler delta replay
hiding snapshot-only sequential fallback behind fast-path provenance
exposing merge coordinator internals as BFF/product contracts
claiming production durability, live ingestion, production BFF/frontend, or
  exactly-once delivery from in-process deterministic gates
blocking the milestone on active=4 allocation parity when elapsed,
  correctness, release health, and retained pressure evidence are clean
```

## Final Handler Delta/Merge Baseline

Accepted handler postures:

```text
handler-free:
  keeps the previously accepted ordered concurrent processing-delta runtime
  posture

mergeable:
  may use ordered concurrent handler delta compute
  must provide deterministic handler-owned merge semantics
  produces immutable per-batch handler deltas
  merges only by provider sequence
  exposes committed merged output through milestone 024 read models

snapshot-only:
  keeps explicit sequential fallback and committed snapshot export

unsupported:
  fails closed through readiness diagnostics and first blocking reason
```

Accepted handler delta contract:

```text
handler name
handler contract version
provider sequence
optional durable batch id
event count
source count
payload value count
input checksum
deterministic delta id
schema version
source/field output values
```

Accepted merge behavior:

```text
completed deltas may arrive out of order
merge applies only the next provider sequence
later deltas wait behind missing or invalid earlier deltas
equivalent duplicates are ignored
conflicting duplicates fail closed
invalid earlier deltas block later merge
summary reports pending/applied counts and first blocking reason
commit path may use handler-owned accumulator state for changed values
public summaries remain able to materialize full merged values
```

Accepted MVP runtime surfaces:

```text
RadarProcessingMvpRuntimePlan
RadarProcessingMvpRuntimeResult
RadarProcessingArchiveQueuedOverlapRunner.RunMvpProcessingAsync
RadarProcessingQueuedProcessingSession ordered handler delta path
```

Accepted product-facing output posture:

```text
merged handler output is projected through milestone 024 read models
handler posture and diagnostics are visible
queue/session/merge internals remain outside BFF contracts
```

## Gate Summary

Handler classification:

```text
passed

handler-free posture remains ordered concurrent eligible
existing stateful handlers default to snapshot-only
all-mergeable handler sets are delta/merge eligible
snapshot-only mixes keep sequential fallback
unsupported handler sets fail closed with diagnostics
```

Per-batch handler delta contract:

```text
passed

valid deltas carry deterministic identity and batch metadata
invalid identity, counters, ambiguous fields, or version mismatch fail closed
serialization roundtrip preserves idempotency key and values
retrying delta compute for the same handler and batch is equivalent
```

Deterministic ordered merge coordinator:

```text
passed

out-of-order completed deltas merge in provider sequence
later completed deltas wait behind missing earlier sequence
duplicate delta application does not double-count output
invalid earlier delta blocks later merge
merged output matches sequential fallback for the same input batches
summaries do not expose mutable coordinator state
```

MVP runtime integration and fallback:

```text
passed

all-mergeable MVP plans use ordered delta/merge provenance
snapshot-only MVP plans keep sequential fallback provenance
unsupported handler sets fail closed with diagnostics
handler-free MVP plans keep existing ordered concurrent provenance
merged runtime output matches sequential fallback output
```

BFF compatibility and diagnostics:

```text
passed

BFF run detail exposes merged handler output through existing read models
handler catalog exposes mergeable and snapshot-only posture metadata
diagnostics identify ordered delta/merge versus sequential fallback
blocked or unsupported handler work appears as readiness diagnostics
existing milestone 024 BFF query shape remains compatible
```

Handler-heavy performance gate:

```text
passed

focused Release gate captured correctness, output parity, retained cleanup,
elapsed time, allocation shape, worker health, checksums, and first blocking
reason for deterministic handler-heavy workloads
```

Optimized full-cache handler matrix:

```text
accepted

4/4 rows completed
processing completeness succeeded
processing validation failed batches: 0
terminal retained pressure: 0
retained payload pool misses: 0
optimized active=4 elapsed time is flat versus active=1 in the captured
matrix

warning:
  active=4 allocation remains higher than active=1
```

## Verification

Slice 1 classification suite:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaClassificationTests|FullyQualifiedName~RadarProcessingHandlerOutputContractTests"

result:
  11 passed, 0 failed, 0 skipped
```

Slice 2 delta contract suite:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaContractTests"

result:
  6 passed, 0 failed, 0 skipped
```

Slice 3 merge coordinator suite:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests"

result:
  6 passed, 0 failed, 0 skipped
```

Slice 4 runtime integration suite:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingMvpRuntimePlanTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests"

result:
  7 passed, 0 failed, 0 skipped
```

Slice 5 BFF compatibility suite:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingBffReadModelStoreTests|FullyQualifiedName~RadarProcessingRunReadModelTests|FullyQualifiedName~RadarProcessingHandlerDeltaBffCompatibilityTests"

result:
  11 passed, 0 failed, 0 skipped
```

Focused milestone 025 Release gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaClassificationTests|FullyQualifiedName~RadarProcessingHandlerDeltaContractTests|FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests|FullyQualifiedName~RadarProcessingHandlerDeltaBffCompatibilityTests|FullyQualifiedName~RadarProcessingHandlerDeltaPerformanceGateTests"

result:
  26 passed, 0 failed, 0 skipped
```

Release build:

```text
dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

Merge-state optimization focused Release suite:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-build
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests|FullyQualifiedName~RadarProcessingSyntheticBenchmarkTests|FullyQualifiedName~RadarProcessingHandlerDeltaPerformanceGateTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

result:
  53 passed, 0 failed, 0 skipped
```

Full Release test project:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build

result:
  890 passed, 1 failed, 3 skipped

failure:
  RadarProcessingSyntheticRebalanceBenchmarkTests.
    AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
  Expected bounded benchmark aggregation allocation, got 894196968 bytes.
```

Known allocation-sensitive isolated rerun:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"

result:
  1 passed, 0 failed, 0 skipped
```

The full-suite failure matches the known allocation-sensitive synthetic
benchmark caveat carried from earlier milestones. It passed in isolated rerun
and is outside the handler delta/merge correctness surface.

Optimized full-cache handler matrix:

```text
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-full-cache-performance-matrix.md

counter-checksum active=1:
  61_373.01 ms, 4_671_386_960 allocated bytes

counter-checksum active=4:
  61_588.17 ms, 8_188_695_464 allocated bytes
  elapsed ratio versus active=1: 1.004x
  allocation ratio versus active=1: 1.753x

counter-checksum-heavy active=1:
  62_806.15 ms, 4_675_001_328 allocated bytes

counter-checksum-heavy active=4:
  62_687.17 ms, 12_209_454_512 allocated bytes
  elapsed ratio versus active=1: 0.998x
  allocation ratio versus active=1: 2.612x

active=4 optimization versus previous matrix:
  counter-checksum elapsed 0.785x, allocation 0.243x
  counter-checksum-heavy elapsed 0.756x, allocation 0.216x
```

This closeout slice is documentation-only. No additional test run was needed
after closeout text updates.

## Decision Trace

The decision trace is written in
`025-handler-delta-merge-contract-for-fast-custom-analytics-decision-trace.md`.

Final closeout answer:

```text
accepted with scoped warnings for handler delta/merge contract and fast
custom analytics over deterministic archive-shaped MVP workloads
```

Recommended next milestone input:

```text
persistent durable adapter readiness.

Use the accepted milestone 023 durable envelope contract together with the
milestone 025 handler delta identity, idempotency, replay, and ordered merge
semantics to validate one concrete persistent local adapter shape. The next
milestone should prove local storage ownership, claim/retry, recovery, poison
handling, ordered commit after restart, handler delta replay, release cleanup,
operator diagnostics, and failure visibility without claiming true live
ingestion or exactly-once production delivery prematurely.
```
