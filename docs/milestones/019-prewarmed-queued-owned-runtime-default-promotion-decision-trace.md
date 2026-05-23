# Milestone 019 Decision Trace

Date: 2026-05-23

Decision: accept the startup-prewarmed queued-owned contour as the omitted
default for the scoped in-process runtime/archive queued-overlap provider
path and as the default baseline for remaining runtime/archive work, with
named scoped warnings.

This decision promotes the benchmark-proven and milestone 018 runtime-explicit
queued-owned contour into `RadarProcessingArchiveQueuedOverlapRunner` omitted
options. The accepted default covers the queued-overlap provider, retained
payload ownership, retained-byte budget, and startup retained payload prewarm
lifecycle. The prewarm cost remains visible and is reported separately from
steady overlap allocation.

This decision also sets the planning baseline for remaining runtime/archive
work: future surfaces should start from this accepted prewarmed queued-owned
default contour rather than re-litigating provider, retention, retained-byte,
and prewarm defaults. Future milestones may still need surface-specific
integration evidence for live network input, durable transport,
cross-process workers, ordered concurrent rebalance, product workflows, or
processing-core construction, but that evidence should be about the new
boundary, not about whether the accepted default contour is still the project
default.

This decision does not automatically rewrite a supplied processing core or
rebalance session into async shard transport. Processing execution mode and
async worker sizing remain owned by the supplied processing core/rebalance
session until a dedicated processing-core defaulting surface adopts the same
baseline explicitly.

The decision is a default-baseline promotion, not a broad production platform
implementation. It closes the integration gap left by milestone 018:
queued-owned was already runtime-safe when selected explicitly, and now the
scoped queued-overlap omitted provider path uses the same prewarmed
queued-owned default contour.

## Decision Matrix

```text
scoped runtime queued-overlap omitted default:
  accepted with scoped warnings

queued-overlap provider default:
  accepted; omitted RadarProcessingArchiveQueuedOverlapOptions now select the
  rollout queue, retained-byte, pooled-copy, and startup-prewarm contour

startup prewarm lifecycle:
  accepted as default lifecycle cost for the scoped queued-overlap provider
  path; prewarm remains visible and separate from steady overlap allocation

explicit options and diagnostics:
  accepted and preserved; explicitly constructed options remain
  snapshot-copy/no-prewarm unless prewarm is requested explicitly

processing execution default:
  not changed; execution mode and async worker sizing remain supplied by the
  processing core or rebalance session

fallback/oracle posture:
  accepted and preserved; no automatic silent borrowed fallback is introduced

failure and cleanup posture:
  accepted for the scoped provider path; focused gates preserve release
  health and terminal retained pressure cleanup

remaining runtime/archive work posture:
  accepted baseline; future in-process runtime/archive work should use the
  prewarmed queued-owned provider/retention/prewarm contour unless a surface
  proves a concrete incompatibility

live/durable/cross-process posture:
  not implemented by this milestone; these surfaces inherit the accepted
  default baseline as their starting contour, while their transport and
  lifecycle boundaries still require surface-specific proof

full-suite residual risk:
  accepted as known allocation-sensitive synthetic benchmark caveat; isolated
  rerun passed and the failure is outside the runtime queued-overlap default
  promotion surface
```

## Decision Explanations

### Accept Scoped Runtime Default Promotion

Decision: promote the startup-prewarmed queued-owned provider contour to the
omitted default for `RadarProcessingArchiveQueuedOverlapRunner` scoped
runtime/archive queued-overlap paths.

Why chosen: milestone 017 already accepted the prewarmed queued-owned contour
for direct archive benchmark default-equivalent `MeasureFile()` and
`MeasureCache()` surfaces. Milestone 018 accepted the same contour as
runtime-safe when selected explicitly for scoped in-process runtime/archive
replay surfaces. Milestone 019 implemented the remaining integration gap:
omitted queued-overlap options now select queue capacity `8`, retained-byte
budget `536870912`, pooled-copy retention, and rollout startup prewarm.

Alternatives: keep queued-owned opt-in only, require a production operator
rollout surface before any omitted default, or broaden the milestone into
true live/durable/cross-process implementation.

Rejected because: keeping opt-in only would ignore that the only remaining
gap was omitted queued-overlap selection; requiring production rollout would
turn a narrow default-promotion milestone into a platform milestone; and
broadening into live/durable/cross-process surfaces would overrun the agreed
scope.

Trade-offs/debt: the accepted default is now the baseline for remaining
runtime/archive work, but the remaining work still needs implementation
evidence for its own boundaries: live input, durable transport,
cross-process ownership, ordered concurrent rebalance, and production
operator workflows.

Review explanation: "The already-proven prewarmed queued-owned provider path
is now the default rail for remaining runtime/archive work; future milestones
integrate it rather than re-decide it."

### Accept Startup Prewarm As Visible Default Lifecycle Cost

Decision: accept startup retained payload prewarm as part of the scoped
queued-overlap default lifecycle.

Why chosen: natural first-use retained ownership was already classified as
allocation-blocked for omitted default interpretation in earlier milestones.
Prewarm is the accepted contour because it moves the retained-pool allocation
before steady overlap measurement while keeping the cost visible. Milestone
019 runs prewarm before steady overlap allocation capture and reports
`RadarProcessingRetainedPayloadPrewarmResult` on
`RadarProcessingArchiveQueuedOverlapResult`.

Alternatives: hide prewarm inside steady allocation, rely on natural
first-use, or keep prewarm as explicit-only despite promoting the omitted
provider default.

Rejected because: hidden prewarm would make runtime cost attribution
misleading; natural first-use remains allocation-blocked by the accepted
evidence chain; and explicit-only prewarm would leave the omitted default
path different from the benchmark-proven contour.

Trade-offs/debt: startup prewarm is a real up-front lifecycle cost. It must
stay visible in runtime results and cannot be treated as zero-cost behavior.

Review explanation: "Prewarm is default only because the cost is explicit and
separate from steady overlap allocation."

### Preserve Explicit Options And No-Prewarm Diagnostics

Decision: preserve explicit options as explicit diagnostics and overrides.

Why chosen: `RadarProcessingArchiveQueuedOverlapOptions.Default` is now the
runtime rollout contour, but `new RadarProcessingArchiveQueuedOverlapOptions()`
continues to mean the generic queue and retained payload defaults:
snapshot-copy retention and no retained payload prewarm. This keeps existing
guardrail tests and diagnostic shapes precise.

Alternatives: make every constructed options instance use rollout defaults,
remove no-prewarm diagnostics, or make prewarm implicit even for explicit
snapshot-copy options.

Rejected because: changing explicit construction would erase useful
diagnostic surfaces; removing no-prewarm diagnostics would make regression
triage harder; and prewarming snapshot-copy is invalid because prewarm only
has meaning for pooled-copy retained ownership.

Trade-offs/debt: there are now two intentionally different paths:
`Default`/omitted runtime behavior and explicit diagnostic construction. Tests
must continue to name which one they exercise. New runtime/archive surfaces
should choose the accepted default path by default and use diagnostics only
when testing or proving a named exception.

Review explanation: "Omitted runtime defaults are promoted; explicit
diagnostics remain explicit."

### Keep Processing Execution Outside Queued-Overlap Defaults

Decision: do not let queued-overlap options rewrite processing execution mode
or async worker sizing.

Why chosen: `RadarProcessingArchiveQueuedOverlapOptions` owns provider queue,
retained payload ownership, retained-byte budget, retained payload factory,
and prewarm lifecycle. It does not own the already constructed
`RadarProcessingCore` or `RadarProcessingRebalanceSession`. Rewriting
execution mode from the overlap options would blur ownership boundaries and
could silently change processing semantics.

Alternatives: force async shard transport whenever queued-overlap defaults
are omitted, add hidden worker defaults to the overlap runner, or broaden the
milestone into processing-core runtime default migration.

Rejected because: forced execution rewrites would be a larger architecture
change than provider default promotion; hidden worker defaults would be hard
to review; and processing-core runtime default migration was not the agreed
milestone scope.

Trade-offs/debt: the accepted runtime default is provider/retention/prewarm
defaulting. Callers that require the full benchmark contour, including async
shard transport worker count `4` and worker queue capacity `8`, must still
construct the processing core/rebalance session with that contour.

Review explanation: "Queued-overlap defaults choose the provider path; the
processing core still owns execution."

### Preserve Fail-Closed Behavior Without Silent Borrowed Fallback

Decision: keep queued-owned failures fail-closed and preserve explicit
borrowed fallback/oracle behavior only where it is deliberately selected.

Why chosen: milestone 018 established that provider enqueue success is not
processing completion and that queued-owned failure must not silently become
borrowed success. Milestone 019 changes omitted queued-overlap defaults but
does not add any automatic borrowed retry or fallback path.

Alternatives: add borrowed as runtime rescue after queued-owned failure,
remove borrowed reference paths entirely, or treat provider enqueue as enough
for success.

Rejected because: borrowed rescue would hide queued-owned failures; removing
borrowed reference paths would weaken future comparisons; and enqueue success
does not prove processing completion.

Trade-offs/debt: future gates may still need explicit borrowed/reference rows
for cost or correctness comparisons.

Review explanation: "Default promotion does not add silent fallback."

### Accept Verification With Known Full-Suite Allocation Caveat

Decision: accept the milestone 019 verification posture despite the known
full-suite allocation-sensitive synthetic benchmark failure.

Why chosen: focused Debug and Release runtime/prewarm suites passed `41/41`,
and Release build passed with `0` warnings and `0` errors. The full test
project produced the same known allocation-sensitive synthetic benchmark
failure carried from milestone 018, and the isolated rerun of that failing
test passed. The failure is outside the runtime queued-overlap default
promotion surface.

Alternatives: block the milestone on unrelated full-suite allocation
sensitivity, loosen the synthetic benchmark threshold, or ignore full-suite
output.

Rejected because: blocking would conflate this scoped runtime default change
with a known unrelated allocation sensitivity; changing thresholds after
measurement is not allowed; and ignoring full-suite output would hide useful
residual risk.

Trade-offs/debt: the full-suite sensitivity remains a project-level caveat
until separately stabilized.

Review explanation: "The focused surface is clean; the full-suite caveat is
known, isolated, and outside this milestone's changed path."

## Included Surface

Included runtime/archive queued-overlap surfaces:

```text
RadarProcessingArchiveQueuedOverlapOptions
RadarProcessingArchiveQueuedOverlapRunner
RadarProcessingArchiveQueuedOverlapResult
RadarProcessingRetainedPayloadPrewarmOptions
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingOwnedBatchQueue
RadarProcessingRetainedPayloadFactory
```

Included promoted omitted default contour:

```text
provider path: queued-owned by construction of queued-overlap runner
provider overlap: producer-consumer queued-overlap runner
retention strategy: pooled-copy
provider queue capacity: 8
retained-byte budget: 536870912
startup retained payload prewarm: enabled
prewarm event count: 65_536
prewarm payload bytes: 67_108_864
prewarm retained batch count: 1
```

Included explicit diagnostic contour:

```text
new RadarProcessingArchiveQueuedOverlapOptions():
  queue options: RadarProcessingProviderQueueOptions.Default
  retained payload options: RadarProcessingRetainedPayloadOptions.Default
  retained payload strategy: snapshot-copy
  retained payload prewarm: none
```

Excluded:

```text
automatic processing-core execution mode defaulting
automatic async worker count or worker queue capacity defaulting
live network adapter implementation
durable queue or broker implementation
cross-process provider or worker transport implementation
ordered concurrent rebalance implementation
multiple active rebalance-enabled processing batches
builder-transfer retained payload execution
source-level migration or partition splitting
automatic silent borrowed fallback
production deployment, alerting, rollback, or operator runbooks
product-facing radar workflows
```

## Evidence

Primary source documents:

```text
docs/milestones/019-prewarmed-queued-owned-runtime-default-promotion.md
docs/milestones/019-prewarmed-queued-owned-runtime-default-promotion-plan.md
docs/milestones/019-prewarmed-queued-owned-runtime-default-promotion-gate.md
```

Input evidence from earlier milestones:

```text
milestone 017:
  direct MeasureFile()/MeasureCache() prewarmed queued-owned
  default-equivalent contour accepted with named warnings

milestone 018:
  startup-prewarmed queued-owned accepted as runtime-safe for scoped
  in-process runtime/archive replay surfaces when selected explicitly
```

Implementation evidence:

```text
RadarProcessingArchiveQueuedOverlapOptions.Default now uses:
  queue capacity 8
  retained-byte budget 536870912
  pooled-copy retained payload strategy
  rollout retained payload prewarm options

RadarProcessingArchiveQueuedOverlapRunner now:
  applies startup retained payload prewarm before steady allocation capture
  passes the prewarmed factory to ArchiveOwnedRadarEventBatchQueueingPublisher
  returns the prewarm result on RadarProcessingArchiveQueuedOverlapResult

RadarProcessingArchiveQueuedOverlapResult now:
  exposes RetainedPayloadPrewarm
  exposes HasRetainedPayloadPrewarm
```

Focused runtime gate:

```text
test:
  RadarProcessingArchiveQueuedOverlapRunnerTests.
    OmittedOptionsApplyRuntimeDefaultStartupPrewarm

assertions:
  result completed true
  retained payload prewarm applied true
  prewarm event count 65_536
  prewarm payload bytes 67_108_864
  prewarm retained batch count 1
  prewarm allocated bytes greater than 0
  prewarm retained bytes greater than 0
  steady measured allocation less than startup prewarm allocated bytes
  retention strategy pooled-copy
  release attempts/releases/failures 1/1/0
  terminal combined retained batch count 0
  terminal combined retained payload bytes 0
```

Explicit no-prewarm guard:

```text
test:
  RadarProcessingArchiveQueuedOverlapRunnerTests.
    ExplicitOptionsDoNotApplyStartupPrewarmUnlessRequested

assertions:
  explicit options complete
  retained payload prewarm applied false
  retention strategy snapshot-copy
```

Verification:

```text
Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused Debug runtime/prewarm suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests"
  result: 41 passed, 0 failed, 0 skipped

focused Release runtime/prewarm suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release --no-restore
    --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests"
  result: 41 passed, 0 failed, 0 skipped

full test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  result: 776 passed, 1 failed, 3 skipped

isolated rerun of full-suite failure:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed
```

Full-suite failure:

```text
RadarProcessingSyntheticRebalanceBenchmarkTests.
  AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

expected bounded benchmark aggregation allocation, got 469_019_824 bytes

interpretation:
  known allocation-sensitive synthetic benchmark caveat from milestone 018;
  outside the runtime queued-overlap default promotion surface
```

## Operational Posture

Runtime queued-overlap default posture:

```text
omitted RadarProcessingArchiveQueuedOverlapOptions use the promoted
startup-prewarmed queued-owned provider/retention/prewarm contour
```

Prewarm posture:

```text
startup retained payload prewarm is default lifecycle cost for the scoped
queued-overlap provider path
prewarm sizing, elapsed time, allocated bytes, and retained bytes remain
visible on the runtime result
prewarm allocation is not folded into steady overlap allocation
```

Explicit override posture:

```text
explicitly constructed options remain diagnostic/no-prewarm unless prewarm is
requested explicitly
```

Execution posture:

```text
processing execution mode remains supplied by the processing core or rebalance
session
queued-overlap defaults do not rewrite execution mode or async worker sizing
```

Fallback posture:

```text
no automatic borrowed fallback is introduced
BlockingBorrowed remains explicit where benchmark/reference surfaces support
it
```

Failure posture:

```text
queued-owned failures remain fail-closed
processing completion remains distinct from provider enqueue
release failures remain readiness blockers
terminal retained pressure must return to zero
```

## Residual Risks And Limits

```text
true live ingestion:
  live adapter mechanics are not implemented by milestone 019, but the
  accepted default baseline should be used when that surface is built

durable/cross-process:
  durable queues, brokers, and cross-process provider/worker behavior are not
  implemented by milestone 019, but they inherit the accepted baseline unless
  their ownership boundary proves a concrete incompatibility

processing execution default:
  not changed by milestone 019; callers still own execution mode and async
  worker sizing until a processing-core defaulting surface adopts the accepted
  baseline explicitly

operator reporting:
  no production operator reporting, alerting, deployment, or rollback surface
  is added by this decision

natural first-use:
  remains rejected as default evidence outside the accepted startup-prewarmed
  contour

full-suite allocation sensitivity:
  one synthetic benchmark allocation-threshold test remains sensitive in the
  full suite but passes in isolated rerun
```

## Decision

Milestone 019 answers **accepted with scoped warnings** for runtime/archive
default-baseline promotion:

```text
startup-prewarmed queued-owned is accepted as the omitted default for the
scoped in-process runtime/archive queued-overlap provider path and as the
default baseline for remaining runtime/archive integration work
```

Milestone 019 answers **yes** for provider/retention/prewarm defaulting:

```text
omitted RadarProcessingArchiveQueuedOverlapOptions now use provider queue
capacity 8, retained-byte budget 536870912, pooled-copy retention, and
rollout startup retained payload prewarm
```

Milestone 019 answers **yes** for visible prewarm cost attribution:

```text
startup retained payload prewarm is reported separately on
RadarProcessingArchiveQueuedOverlapResult and is not folded into steady
overlap allocation
```

Milestone 019 answers **yes** for preserving explicit no-prewarm diagnostics:

```text
explicit constructed options remain snapshot-copy/no-prewarm unless prewarm
is requested explicitly
```

Milestone 019 answers **not implemented here** for automatic processing
execution defaulting:

```text
queued-overlap defaults do not rewrite processing core execution mode or async
worker sizing; future processing-core default work should adopt the accepted
baseline explicitly instead of reopening the provider default decision
```

Milestone 019 answers **accepted baseline, not implemented surface** for
broader live/durable/cross-process work:

```text
true live ingestion, durable queues, brokers, cross-process workers, ordered
concurrent rebalance, builder-transfer, and production operator surfaces are
not implemented by this milestone, but future work should start from the
accepted prewarmed queued-owned default contour unless a concrete surface
incompatibility is proven
```

Milestone 019 can proceed to closeout with no additional implementation
changes required before closeout.

Recommended closeout posture:

```text
close milestone 019 as accepted with scoped warnings, update handoff and
project progress, and carry the posture that prewarmed queued-owned is the
default rail for remaining runtime/archive work; execution mode remains owned
by the supplied processing core/rebalance session until explicitly defaulted
```
