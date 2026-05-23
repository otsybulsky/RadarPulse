# Milestone 020 Decision Trace

Date: 2026-05-23

Decision: accept the default-baseline runtime/archive integration milestone
with named scoped warnings.

This decision accepts the milestone 019 startup-prewarmed queued-owned
provider default as the baseline for the scoped in-process runtime/archive
integration surfaces, and accepts milestone 020's named integration profile
as the current construction point for that baseline. The accepted integration
profile composes the milestone 019 provider/retention/prewarm default with
the accepted async shard transport execution contour where the
runtime/archive surface owns processing core or rebalance session
construction.

The accepted baseline now has a concrete code surface:
`RadarProcessingRuntimeArchiveBaseline`. It creates default async execution
options, default processing core options, default processing cores, and
default rebalance sessions for a supplied topology shape. It also keeps
provider and execution matching separately assertable so future surfaces can
prove which half of the baseline they adopted.

This decision does not claim production live ingestion, durable queues,
cross-process provider or worker transport, ordered concurrent rebalance, or
production operator/deployment readiness. It also does not change the
semantics of caller-owned processing cores or caller-owned rebalance
sessions: those remain explicit, and the queued-overlap runner does not
silently rewrite them into async shard transport.

The decision is an integration acceptance, not a final runtime pipeline
completion. It closes the gap between "the provider default is accepted" and
"runtime/archive surfaces that own construction can consume the full accepted
provider plus execution baseline by default."

## Decision Matrix

```text
default-baseline runtime/archive integration:
  accepted with scoped warnings

named baseline profile:
  accepted; RadarProcessingRuntimeArchiveBaseline is the named integration
  surface for composing provider and execution defaults

provider default adoption:
  accepted; the profile delegates queued-overlap provider options to the
  milestone 019 omitted queued-overlap runtime default

execution default adoption:
  accepted only for surfaces that own processing core or rebalance session
  construction; async shard transport uses worker count 4 and worker queue
  capacity 8

caller-owned session behavior:
  accepted and preserved; supplied processing cores and rebalance sessions
  are not silently rewritten

live-adapter-shaped evidence:
  accepted as scoped integration evidence over deterministic in-memory
  archive-shaped batches

true live ingestion:
  not implemented; live network input remains future work

durable and cross-process runtime:
  not implemented; durable queues, brokers, cross-process providers/workers,
  and ordered concurrent rebalance remain future work

reporting and provenance:
  accepted as sufficient for this milestone; existing result, telemetry,
  prewarm, worker, and CLI provenance contracts answer the review questions

full-cache performance:
  accepted; no end-to-end full-cache regression observed versus explicit
  BlockingBorrowed oracle rows

callback attribution:
  accepted with warning; queued-owned processing callback attribution is
  heavier even though end-to-end rows are faster with flat total allocation

multi-batch concurrency:
  not implemented; async execution is currently intra-batch shard transport,
  while queued consumer drain still processes one dequeued batch to completion
  before dequeuing the next batch

fallback/oracle posture:
  accepted and preserved; no automatic silent borrowed fallback is introduced

full-suite residual risk:
  accepted as known allocation-sensitive synthetic benchmark caveat; isolated
  rerun passed and the failure is outside this milestone surface
```

## Decision Explanations

### Accept The Named Runtime/Archive Baseline Profile

Decision: accept `RadarProcessingRuntimeArchiveBaseline` as the named
runtime/archive integration profile for the accepted provider plus execution
baseline.

Why chosen: milestone 019 accepted the startup-prewarmed queued-owned
provider/retention/prewarm contour as the omitted default for the scoped
queued-overlap provider path. Milestone 020 needed a construction-owned
surface that could compose that provider half with async shard transport
defaults without scattering rollout constants through call sites.
`RadarProcessingRuntimeArchiveBaseline` centralizes that composition while
reusing `RadarProcessingArchiveRebalanceRolloutDefaults`.

Alternatives: duplicate constants at every runtime/archive construction site,
change `RadarProcessingCoreOptions.Default`, or keep execution defaulting as
documentation-only.

Rejected because: duplicated constants would drift; changing
`RadarProcessingCoreOptions.Default` would silently alter conservative core
construction across unrelated surfaces; and documentation-only defaulting
would leave no reviewable code surface for runtime/archive integration.

Trade-offs/debt: this is still an in-process construction profile. Future
production operator surfaces may need separate configuration and reporting
contracts around the same baseline.

Review explanation: "The accepted baseline now has one named construction
profile instead of implicit scattered defaults."

### Accept Execution Defaulting Only For Owned Construction

Decision: accept async shard transport defaulting with worker count `4` and
worker queue capacity `8` only where the runtime/archive surface owns core or
session construction.

Why chosen: milestone 019 deliberately did not let queued-overlap options
rewrite an already supplied processing core or rebalance session. Milestone
020 preserves that boundary while adding helpers that construct baseline core
options, cores, and rebalance sessions when the integration surface owns that
construction.

Alternatives: rewrite every supplied session into async shard transport,
change the queued-overlap runner to force async execution, or leave
processing execution entirely manual.

Rejected because: silent rewrites would change caller-owned semantics; forcing
execution from provider options would blur ownership; and leaving execution
manual would fail the milestone goal of integrating the accepted provider and
execution baseline into owned construction surfaces.

Trade-offs/debt: callers that supply their own core or rebalance session still
own execution choices. The baseline profile gives them an explicit opt-in
construction path but does not replace their supplied objects.

Review explanation: "Owned construction adopts the baseline; caller-owned
construction remains explicit."

### Accept Scoped Live-Adapter-Shaped Integration Evidence

Decision: accept deterministic in-memory archive-shaped adapter tests as
integration evidence for the scoped in-process runtime/archive boundary.

Why chosen: the milestone needed evidence that the baseline works when data is
fed through a runtime-shaped producer/consumer lifecycle, without claiming
durable or network live ingestion. The added tests feed multiple
archive-shaped batches through the accepted baseline, cover steady completion,
and cover validation failure cleanup.

Alternatives: require true live network ingestion before any integration
acceptance, use only unit-level baseline profile tests, or build durable
transport inside this milestone.

Rejected because: true live ingestion and durable transport are distinct
future milestones; unit-only tests would not prove lifecycle integration; and
durable transport would overrun the agreed milestone boundary.

Trade-offs/debt: the evidence proves scoped in-process lifecycle behavior, not
network input, broker semantics, cross-process cleanup, or production
deployment behavior.

Review explanation: "The live-shaped proof exercises the integration boundary
without overclaiming live ingestion."

### Accept Existing Reporting And Provenance Contracts

Decision: do not add new production result fields for milestone 020.

Why chosen: the provenance audit found that current contracts already expose
the needed review surface: provider defaulting, execution defaulting, startup
prewarm, steady overlap allocation, provider queue telemetry, retained
pressure, worker telemetry, processing completeness, release health, and CLI
benchmark provenance.

Alternatives: add a new production runtime result contract, add duplicate
provenance fields to queued-overlap results, or defer decision trace until a
production operator report exists.

Rejected because: adding fields would duplicate existing evidence without
closing a real attribution gap; deferring to production operator reporting
would turn a scoped integration milestone into a deployment milestone.

Trade-offs/debt: production operator/deployment surfaces still need their own
reporting and rollback contracts later. This decision only says no extra
production fields are required for the scoped in-process integration review.

Review explanation: "The existing telemetry contracts answer the milestone
questions; production operator reporting remains future work."

### Accept Full-Cache Performance Matrix

Decision: accept the post-gate full-cache performance matrix as supporting
evidence that the default-baseline integration did not regress end-to-end
cache performance.

Why chosen: the Release CLI matrix compared the explicit BlockingBorrowed
oracle with the omitted-provider queued-owned rollout default over the full
local cache using `--mode all`. Default elapsed ratios were `0.793x`,
`0.890x`, and `0.881x` borrowed for static, sampling, and rebalance-session
rows. Total allocation ratios were effectively flat at `1.000x`, `1.002x`,
and `1.003x`. Validation, processing completeness, worker health, release
health, retained pressure cleanup, and checksum parity passed.

Alternatives: require repeated full-cache variance runs before decision trace,
ignore performance evidence because the milestone was primarily integration,
or block on heavier processing callback attribution.

Rejected because: repeated variance gates are useful future evidence but not
required for this scoped decision; ignoring a clean full-cache matrix would
discard relevant risk evidence; and callback attribution is a visible cost
shift, not an end-to-end regression.

Trade-offs/debt: queued-owned callback attribution remains heavier:
approximately `3.25x` to `3.28x` callback allocation and `1.30x` to `1.34x`
callback elapsed versus borrowed. This must remain visible in future
runtime/archive and production-pipeline performance reviews.

Review explanation: "Full-cache end-to-end rows improved; the remaining cost
warning is internal callback attribution."

### Preserve Fail-Closed Behavior Without Silent Borrowed Fallback

Decision: preserve fail-closed queued-owned behavior and keep
BlockingBorrowed as explicit oracle/fallback only.

Why chosen: the live-adapter-shaped validation failure test faults the
consumer path, records failed validation, skips accepted remainder after
fault, releases retained resources, returns terminal retained pressure to
zero, and does not silently fall back to borrowed processing. This preserves
the guardrail chain from milestones 018 and 019.

Alternatives: add borrowed retry after queued-owned failure, treat provider
enqueue success as sufficient completion, or remove borrowed oracle paths
from future comparisons.

Rejected because: borrowed retry would hide queued-owned failures; enqueue
success is not processing completion; and borrowed oracle rows remain
important for performance and correctness interpretation.

Trade-offs/debt: future benchmark/default gates should continue using
explicit borrowed oracle rows where comparison is meaningful.

Review explanation: "Default integration does not add hidden fallback."

### Keep Ordered Concurrent Runtime As Future Work

Decision: do not claim multi-batch concurrent runtime processing in milestone
020.

Why chosen: current async execution is intra-batch shard transport.
`RadarProcessingQueuedProcessingSession` and
`RadarProcessingQueuedRebalanceSession` still drain one dequeued batch, await
its full async processing result, record the result, and only then dequeue
the next batch. The queued-overlap runner can overlap archive production with
consumer draining, but the consumer side still processes batches
sequentially.

Alternatives: implement ordered concurrent processing in this milestone,
claim producer/consumer overlap as enough for batch-level concurrency, or
ignore the distinction.

Rejected because: ordered concurrent processing is a separate execution-model
change with topology, ordering, failure, cancellation, release, and commit
semantics; producer/consumer overlap is not the same as multiple active
processing batches; and ignoring the distinction would overstate current
runtime behavior.

Trade-offs/debt: the next milestone can materially improve throughput
headroom by adding ordered concurrent runtime/archive processing while
preserving deterministic result order and topology/rebalance safety.

Review explanation: "Milestone 020 integrates the default baseline; the next
step is batch-level concurrency."

### Accept Verification With Known Full-Suite Allocation Caveat

Decision: accept the milestone 020 verification posture despite the known
full-suite allocation-sensitive synthetic benchmark failure.

Why chosen: Release build passed with `0` warnings and `0` errors. Focused
milestone 020 gate suite passed `24/24`. The full test project produced the
known allocation-sensitive synthetic benchmark failure already carried from
milestones 018 and 019, and the isolated rerun of that failing test passed.
The failure is outside the runtime/archive default-baseline integration
surface.

Alternatives: block milestone 020 on unrelated full-suite allocation
sensitivity, relax the synthetic threshold after seeing the failure, or
ignore the full-suite output.

Rejected because: blocking would conflate this scoped integration change with
a known unrelated sensitivity; threshold changes after measurement are not
allowed; and ignoring full-suite output would hide residual project risk.

Trade-offs/debt: the full-suite allocation sensitivity remains a project-level
caveat until separately stabilized.

Review explanation: "The scoped surface is clean; the known full-suite
allocation caveat remains isolated."

## Included Surface

Included runtime/archive integration surfaces:

```text
RadarProcessingRuntimeArchiveBaseline
RadarProcessingArchiveQueuedOverlapOptions
RadarProcessingArchiveQueuedOverlapRunner
RadarProcessingArchiveQueuedOverlapResult
RadarProcessingQueuedProcessingSession
RadarProcessingQueuedRebalanceSession
RadarProcessingAsyncCoreSession
RadarProcessingAsyncRebalanceSession
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingOwnedBatchQueue
RadarProcessingRetainedPayloadFactory
```

Included baseline profile:

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
execution: async shard transport
worker count: 4
worker queue capacity: 8
```

Included construction helpers:

```text
RadarProcessingRuntimeArchiveBaseline.CreateAsyncExecution()
RadarProcessingRuntimeArchiveBaseline.CreateCoreOptions()
RadarProcessingRuntimeArchiveBaseline.CreateCore()
RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession()
RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions()
RadarProcessingRuntimeArchiveBaseline.MatchesQueuedOverlapOptions()
```

Included evidence shapes:

```text
focused baseline profile contract tests
owned rebalance-session construction tests
omitted queued-overlap provider default composition tests
caller-supplied rebalance session preservation tests
deterministic live-adapter-shaped steady intake tests
deterministic live-adapter-shaped validation failure cleanup tests
Release CLI full-cache borrowed/default performance matrix
```

Excluded:

```text
true live network ingestion implementation
durable queues or brokers
cross-process provider or worker transport
ordered concurrent processing or rebalance across multiple active batches
automatic topology publication concurrency
builder-transfer retained payload execution
automatic silent borrowed fallback
production deployment, alerting, rollback, or operator runbooks
product-facing radar workflows
changing RadarProcessingCoreOptions.Default
silently rewriting caller-owned processing cores or rebalance sessions
```

## Evidence

Primary source documents:

```text
docs/milestones/020-default-baseline-runtime-archive-integration.md
docs/milestones/020-default-baseline-runtime-archive-integration-plan.md
docs/milestones/020-default-baseline-runtime-archive-integration-provenance-audit.md
docs/milestones/020-default-baseline-runtime-archive-integration-gate.md
docs/milestones/020-default-baseline-runtime-archive-integration-full-cache-performance-matrix.md
```

Input evidence from earlier milestones:

```text
milestone 017:
  direct MeasureFile()/MeasureCache() prewarmed queued-owned
  default-equivalent contour accepted with named warnings

milestone 018:
  startup-prewarmed queued-owned accepted as runtime-safe for scoped
  in-process runtime/archive replay surfaces when selected explicitly

milestone 019:
  startup-prewarmed queued-owned accepted as the omitted default for the
  scoped in-process runtime/archive queued-overlap provider path and as the
  default baseline for remaining runtime/archive work
```

Implementation evidence:

```text
RadarProcessingRuntimeArchiveBaseline now:
  creates rollout async execution options
  creates async shard transport core options for supplied topology shape
  creates processing cores with the accepted execution contour
  creates rebalance sessions with the accepted execution contour
  exposes provider and execution baseline match helpers

RadarProcessingArchiveQueuedOverlapOptions.Default remains:
  queue capacity 8
  retained-byte budget 536870912
  pooled-copy retained payload strategy
  rollout startup retained payload prewarm options
```

Focused baseline contract evidence:

```text
baseline profile creates async shard transport core options
worker count is 4
worker queue capacity is 8
provider options match the milestone 019 queued-overlap runtime default
explicit constructed queued-overlap options remain diagnostic/no-prewarm
baseline matching rejects non-rollout execution shapes
RadarProcessingCoreOptions.Default remains sequential
```

Owned construction integration evidence:

```text
baseline-created rebalance session uses async shard transport
baseline-created rebalance session composes with omitted queued-overlap
provider defaults
startup retained payload prewarm is applied
pooled-copy retention is used
worker telemetry reports worker count 4 and queue capacity 8
caller-supplied partitioned-barrier rebalance session remains partitioned and
does not gain worker telemetry
```

Live-adapter-shaped evidence:

```text
steady path:
  deterministic in-memory archive-shaped batches complete
  provider accepted publish count matches processed batch count
  processing completion succeeds
  worker failed/canceled/timed-out/rejected counters are zero
  release failures are zero
  terminal combined retained pressure returns to zero
  startup prewarm remains visible

failure path:
  deterministic validation failure faults consumer path
  failed validation remains visible
  accepted remainder is skipped after fault
  release failures are zero
  terminal combined retained pressure returns to zero
  no borrowed fallback is used
```

Provenance audit:

```text
no production result-contract change required before decision trace
existing result, telemetry, prewarm, worker, and CLI provenance contracts are
sufficient for scoped in-process integration review
```

Verification:

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

isolated rerun of full-suite failure:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed, 0 skipped
```

Full-suite failure:

```text
RadarProcessingSyntheticRebalanceBenchmarkTests.
  AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

expected bounded benchmark aggregation allocation, got 489_482_744 bytes

interpretation:
  known allocation-sensitive synthetic benchmark caveat from milestones 018
  and 019; outside the runtime/archive default-baseline integration surface
```

Post-gate full-cache matrix:

```text
Release CLI matrix:
  processing benchmark rebalance-archive --cache data\nexrad
  --max-files 1000000 --mode all

cache shape:
  examined files: 1_554
  skipped files: 726
  published base-data files: 828
  stream events: 27_254_760
  payload values: 32_306_203_200

default elapsed ratios versus borrowed:
  static: 0.793x
  sampling: 0.890x
  rebalance-session: 0.881x

default total allocation ratios versus borrowed:
  static: 1.000x
  sampling: 1.002x
  rebalance-session: 1.003x

correctness/lifecycle:
  validation succeeded
  processing completeness succeeded
  rebalance-session checksum parity matched
  accepted moves matched at 4 vs 4
  failed migrations 0
  worker failed batches/items 0/0
  release failures 0
  terminal combined retained pressure 0
```

## Operational Posture

Runtime/archive baseline posture:

```text
the accepted provider/retention/prewarm default and accepted async execution
contour are now composed by RadarProcessingRuntimeArchiveBaseline
```

Provider posture:

```text
omitted queued-overlap provider options remain the milestone 019 accepted
queued-owned, producer-consumer, pooled-copy, startup-prewarmed contour
```

Execution posture:

```text
owned runtime/archive construction can create async shard transport cores and
rebalance sessions with worker count 4 and worker queue capacity 8
```

Caller ownership posture:

```text
supplied processing cores and supplied rebalance sessions remain explicit and
are not silently rewritten by queued-overlap runner defaults
```

Prewarm posture:

```text
startup retained payload prewarm remains visible lifecycle cost
prewarm sizing, elapsed time, allocated bytes, and retained bytes remain
separate from steady overlap allocation
```

Fallback posture:

```text
BlockingBorrowed remains explicit oracle/fallback where supported
queued-owned failure does not silently fall back to borrowed success
```

Concurrency posture:

```text
producer/consumer overlap exists
async shard transport exists inside each batch
multi-batch concurrent consumer processing is not implemented
```

Performance posture:

```text
full-cache end-to-end default rows are faster than borrowed and flat on total
allocation
queued-owned callback attribution is heavier and remains a visible
carry-forward cost shape
```

## Residual Risks And Limits

```text
true live ingestion:
  not implemented; deterministic in-memory archive-shaped input was used as
  scoped integration evidence

durable/cross-process:
  durable queues, brokers, and cross-process provider/worker behavior remain
  out of scope

ordered concurrent runtime:
  not implemented; consumer drain still awaits full processing completion
  for the current batch before dequeuing the next batch

startup prewarm:
  remains a visible default lifecycle cost and cannot be hidden in steady row
  allocation

callback attribution:
  queued-owned callback allocation and elapsed attribution are heavier than
  borrowed despite better end-to-end full-cache elapsed rows

operator reporting:
  production deployment, reporting, alerting, and rollback surfaces were not
  added

repeatability:
  full-cache matrix was a single Release CLI borrowed/default run per mode,
  not a repeated variance gate

full-suite allocation sensitivity:
  one synthetic benchmark allocation-threshold test remains sensitive in the
  full suite but passes in isolated rerun
```

## Decision

Milestone 020 answers **accepted with scoped warnings** for default-baseline
runtime/archive integration:

```text
the scoped in-process runtime/archive integration boundary is ready to consume
the accepted prewarmed queued-owned plus async execution default baseline
without reopening the provider default decision
```

Milestone 020 answers **yes** for named baseline construction:

```text
RadarProcessingRuntimeArchiveBaseline is accepted as the named construction
profile for composing queued-owned provider defaults with async shard
transport execution defaults
```

Milestone 020 answers **yes** for owned-construction execution defaulting:

```text
surfaces that own processing core or rebalance session construction can use
the accepted async shard transport contour with worker count 4 and worker
queue capacity 8
```

Milestone 020 answers **yes** for preserving caller-owned explicit sessions:

```text
caller-supplied processing cores and rebalance sessions remain explicit and
are not silently rewritten into async shard transport
```

Milestone 020 answers **yes** for scoped live-adapter-shaped integration
evidence:

```text
deterministic archive-shaped steady and failure paths pass through the
accepted baseline with clean worker health, release health, retained pressure
cleanup, and no silent borrowed fallback
```

Milestone 020 answers **yes** for full-cache end-to-end performance posture:

```text
no end-to-end full-cache regression was observed; default queued-owned was
faster than borrowed in static, sampling, and rebalance-session modes with
effectively flat total allocation
```

Milestone 020 answers **warning** for internal callback attribution:

```text
queued-owned processing callback allocation and elapsed attribution are
heavier than borrowed and must remain visible in future runtime/archive and
production-pipeline performance reviews
```

Milestone 020 answers **not implemented here** for ordered concurrent
multi-batch processing:

```text
async shard transport is currently intra-batch; consumer drain still awaits
full processing of the current batch before dequeuing the next batch
```

Milestone 020 answers **not implemented here** for production runtime
surfaces:

```text
true live network ingestion, durable queues, brokers, cross-process workers,
ordered concurrent rebalance, production operator/deployment/rollback
surfaces, and product-facing workflows remain future work
```

Milestone 020 can proceed to closeout with no additional implementation
changes required before closeout.

Recommended closeout posture:

```text
close milestone 020 as accepted with scoped warnings, update handoff and
project progress, and carry ordered concurrent runtime/archive processing as
the recommended next milestone focus
```
