# Milestone 020: Closeout

## Status

Milestone 020 is complete.

RadarPulse now has a named runtime/archive construction profile that composes
the accepted startup-prewarmed queued-owned provider default with the accepted
async shard transport execution contour for scoped in-process surfaces that own
processing core or rebalance session construction.

The important milestone result is:

```text
019 accepted startup-prewarmed queued-owned as the omitted default for the
    scoped in-process runtime/archive queued-overlap provider path.
020 composes that provider default with async shard transport execution
    defaults in a named owned-construction profile.
020 preserves caller-owned processing cores and rebalance sessions as
    explicit; they are not silently rewritten by queued-overlap defaults.
```

Final readiness posture:

```text
accepted with scoped warnings, the scoped in-process runtime/archive
integration boundary is ready to consume the accepted prewarmed queued-owned
plus async execution default baseline without reopening the provider default
decision
```

The accepted warnings and limits are:

```text
startup prewarm:
  accepted as visible default lifecycle cost; not hidden inside steady
  measured allocation

owned construction only:
  async shard transport defaulting applies through
  RadarProcessingRuntimeArchiveBaseline construction helpers and does not
  rewrite caller-supplied cores or sessions

live evidence:
  deterministic in-memory archive-shaped adapter evidence is accepted as
  scoped integration evidence, not true live network ingestion

multi-batch concurrency:
  not implemented; async execution is intra-batch shard transport, while the
  queued consumer still awaits one dequeued batch to completion before
  dequeuing the next

durable/production runtime:
  durable queues, brokers, cross-process workers, production operator
  surfaces, deployment, rollback, and product-facing workflows remain future
  work

performance attribution:
  full-cache end-to-end rows did not regress, but queued-owned processing
  callback allocation and elapsed attribution remain heavier than borrowed
```

## Final Outcome

Implemented:

- `RadarProcessingRuntimeArchiveBaseline` as the named runtime/archive
  baseline profile.
- Default async shard transport execution options using rollout worker count
  `4` and worker queue capacity `8`.
- Default processing core options for caller-supplied partition/shard topology
  shapes.
- Default processing core construction for owned runtime/archive surfaces.
- Default rebalance session construction for owned runtime/archive surfaces.
- Provider/default and execution/default baseline match helpers.
- Focused baseline profile tests.
- Owned rebalance-session construction tests.
- Omitted queued-overlap provider default composition tests.
- Caller-supplied rebalance session preservation tests.
- Deterministic live-adapter-shaped steady intake integration evidence.
- Deterministic live-adapter-shaped validation failure cleanup evidence.
- Reporting/provenance audit showing no production result-contract change is
  required for the scoped in-process integration review.
- Gate evidence, full-cache performance matrix, decision trace, handoff, and
  project-progress updates.

Not implemented here:

- Ordered concurrent multi-batch runtime/archive processing.
- True live network ingestion.
- Durable queues or brokers.
- Cross-process provider or worker transport.
- Ordered concurrent rebalance commit semantics.
- Builder-transfer retained payload execution.
- Production operator/deployment/rollback surfaces.
- Product-facing radar workflows.
- Automatic silent borrowed fallback.
- Changing `RadarProcessingCoreOptions.Default`.
- Silently rewriting caller-owned processing cores or rebalance sessions.

Still rejected:

```text
automatic silent borrowed fallback
```

## Final Runtime/Archive Baseline

Accepted construction profile:

```text
RadarProcessingRuntimeArchiveBaseline
```

Accepted provider contour:

```text
provider path: queued-owned by construction of queued-overlap runner
provider overlap: producer-consumer
retention strategy: pooled-copy
provider queue capacity: 8
retained-byte budget: 536870912
startup retained payload prewarm: enabled
prewarm event count: 65_536
prewarm payload bytes: 67_108_864
prewarm retained batch count: 1
```

Accepted owned-construction execution contour:

```text
execution: async shard transport
worker count: 4
worker queue capacity: 8
```

Accepted helper surface:

```text
RadarProcessingRuntimeArchiveBaseline.CreateAsyncExecution()
RadarProcessingRuntimeArchiveBaseline.CreateCoreOptions()
RadarProcessingRuntimeArchiveBaseline.CreateCore()
RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession()
RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions()
RadarProcessingRuntimeArchiveBaseline.MatchesQueuedOverlapOptions()
```

Ownership boundary:

```text
surfaces that own processing core or rebalance session construction can use
the named profile to adopt the accepted provider plus execution baseline

callers that supply processing cores or rebalance sessions keep explicit
ownership of execution mode and worker sizing
```

## Gate Summary

Baseline contract:

```text
passed

RadarProcessingRuntimeArchiveBaseline creates async shard transport core
options with worker count 4 and worker queue capacity 8

provider options match the milestone 019 queued-overlap omitted provider
default

baseline matching rejects non-rollout execution shapes

RadarProcessingCoreOptions.Default remains the existing conservative
sequential default
```

Owned construction integration:

```text
passed

baseline-created rebalance sessions use async shard transport

baseline-created rebalance sessions compose with omitted queued-overlap
provider defaults

omitted provider defaults apply pooled-copy retention and startup retained
payload prewarm

caller-supplied rebalance sessions keep explicit execution mode and are not
silently rewritten into async shard transport
```

Live-adapter-shaped evidence:

```text
passed

steady path:
  deterministic in-memory archive-shaped batches complete
  provider accepted publish count matches processed batch count
  processing completeness succeeds
  worker failed/canceled/timed-out/rejected counters are zero
  retained release failures are zero
  terminal combined retained pressure returns to zero
  startup retained payload prewarm remains visible

failure path:
  deterministic validation failure faults the consumer path
  failed validation remains visible as failed processing
  accepted remainder is skipped after fault
  retained release failures are zero
  terminal combined retained pressure returns to zero
  no borrowed fallback is used
```

Reporting and provenance:

```text
passed

existing result, telemetry, prewarm, worker, and CLI provenance contracts are
sufficient for milestone 020 gate review; no production result-contract
change is required for the scoped in-process integration boundary
```

## Full-Cache Performance Matrix

Post-gate Release CLI matrix:

```text
processing benchmark rebalance-archive --cache data\nexrad
--max-files 1000000 --mode all
```

Cache shape:

```text
examined files: 1_554
skipped files: 726
published base-data files: 828
stream events: 27_254_760
payload values: 32_306_203_200
```

End-to-end default ratios versus explicit BlockingBorrowed oracle:

```text
static:
  elapsed: 0.793x
  allocation: 1.000x

sampling:
  elapsed: 0.890x
  allocation: 1.002x

rebalance-session:
  elapsed: 0.881x
  allocation: 1.003x
```

Correctness and lifecycle:

```text
validation succeeded
processing completeness succeeded
rebalance-session checksum parity matched
accepted moves matched at 4 vs 4
failed migrations 0
worker failed batches/items 0/0
release failures 0
terminal combined retained pressure 0
```

Interpretation:

```text
no end-to-end full-cache performance regression was observed
default queued-owned was faster than borrowed in static, sampling, and
rebalance-session modes
total allocation was effectively flat against borrowed
```

Carry-forward warning:

```text
queued-owned processing callback attribution is heavier, about 3.25x-3.28x
callback allocation and 1.30x-1.34x callback elapsed versus borrowed, while
end-to-end rows remain faster with flat total allocation
```

## Preserved Invariants

```text
the milestone 019 provider default decision remains closed
startup prewarm cost remains visible and separate from steady measured
  allocation
provider enqueue success remains distinct from processing completion
queued-owned failures fail closed
no automatic borrowed fallback follows queued-owned failure
explicit BlockingBorrowed remains fallback/oracle where supported
release failures remain readiness blockers
retained pressure must return to zero after success, cancellation, failure,
  drain, and cleanup paths
provider defaulting and execution defaulting remain separately assertable
caller-owned cores and sessions remain explicit
RadarProcessingCoreOptions.Default remains conservative/sequential
```

## Verification

Final verification used for closeout:

```text
Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused milestone 020 gate suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests|FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveRebalanceBenchmarkCommandUsesRolloutDefaultsWhenProviderOmitted|FullyQualifiedName~ArchiveRebalanceBenchmarkCommandLabelsDefaultCandidateContour"
  result: 24 passed, 0 failed, 0 skipped

full test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  result: 787 passed, 1 failed, 3 skipped
  failure:
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
    expected bounded benchmark aggregation allocation, got 489_482_744 bytes

isolated rerun of full-suite failure:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed, 0 skipped
```

The full-suite failure matches the known allocation-sensitive synthetic
benchmark caveat carried from milestones 018 and 019. It passed in isolated
rerun and is outside the runtime/archive default-baseline integration surface.

This closeout slice is documentation-only. No additional test run was needed
after closeout text updates.

## Decision Trace

The decision trace is written in
`020-default-baseline-runtime-archive-integration-decision-trace.md`.

Final closeout answer:

```text
accepted with scoped warnings, the scoped in-process runtime/archive
integration boundary is ready to consume the accepted prewarmed queued-owned
plus async execution default baseline without reopening the provider default
decision
```

Recommended next milestone input:

```text
implement ordered concurrent runtime/archive processing over the accepted
default baseline. Preserve deterministic result ordering, topology and
rebalance safety, fail-closed queued-owned behavior, no silent borrowed
fallback, visible startup prewarm, release/cleanup pressure invariants, and
separate provider/execution provenance.
```
