# Milestone 020: Default-Baseline Runtime/Archive Integration Implementation Plan

Status: decision trace written; awaiting closeout.

This plan implements the milestone 020 architecture defined in
`020-default-baseline-runtime-archive-integration.md`.

The plan is intentionally scoped to in-process runtime/archive integration.
It starts from the accepted milestone 019 provider default baseline and adds
processing-core execution defaulting only through surfaces that explicitly own
core or session construction. It does not implement durable queues, brokers,
cross-process workers, ordered concurrent rebalance, production operator
surfaces, or product-facing workflows.

## Goal

Integrate the accepted prewarmed queued-owned runtime/archive default baseline
into remaining scoped in-process runtime/archive construction surfaces.

The milestone must prove these concrete outcomes:

```text
there is a named runtime/archive baseline profile for the accepted defaults
provider defaults remain the milestone 019 queued-overlap omitted default
processing-core defaults use async shard transport only through surfaces that
  own core construction
provider provenance and execution provenance remain separately visible
startup retained payload prewarm cost remains separate from steady allocation
explicit diagnostic/no-prewarm and BlockingBorrowed/reference paths remain
  explicit
queued-owned failure remains fail-closed with no silent borrowed fallback
retained pressure returns to zero across success, cancellation, failure, and
  drain paths
```

Scope note:

```text
milestone 020 composes accepted defaults; it does not silently rewrite a
caller-supplied processing core or rebalance session. Existing callers that
own core/session construction can opt into the baseline profile or use the
new default integration entry point.
```

## Slice 1: Baseline Profile Contract

Status: complete.

Implementation:

```text
add a named runtime/archive baseline contract for processing and provider
  default composition
reuse RadarProcessingArchiveRebalanceRolloutDefaults constants rather than
  duplicating magic values
expose helpers for:
  default async execution options
  default processing core options for a supplied partition/shard shape
  default queued-overlap provider options
  baseline-match/provenance checks
preserve RadarProcessingCoreOptions.Default as the existing conservative
  sequential core default
```

Tests:

```text
baseline profile creates async shard transport core options
worker count is 4
worker queue capacity is 8
provider options match the milestone 019 queued-overlap runtime default
explicit constructed queued-overlap options remain diagnostic/no-prewarm
baseline match rejects non-async or non-rollout worker sizing
```

Exit criteria:

```text
the runtime/archive default baseline is reviewable as one named profile,
while provider and execution halves remain separately assertable
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests"

result:
  6 passed, 0 failed, 0 skipped
```

## Slice 2: Runtime/Archive Owned Construction Integration

Status: complete.

Implementation:

```text
add or update an in-process runtime/archive entry point that owns processing
  core/session construction and uses the baseline profile by default
do not alter RunRebalanceAsync behavior when a caller supplies an already
  constructed rebalance session
wire default queued-overlap provider options and default async processing
  options through the owned-construction entry point
make the integrated result expose enough evidence to assert execution mode,
  async worker sizing, provider defaulting, and startup prewarm
```

Tests:

```text
omitted owned-construction runtime/archive path uses async shard transport
omitted owned-construction runtime/archive path uses worker count 4 and worker
  queue capacity 8
omitted owned-construction runtime/archive path applies queued-overlap startup
  prewarm
caller-supplied rebalance session keeps its explicit execution mode
```

Exit criteria:

```text
the first runtime/archive surface that owns core/session construction consumes
the accepted default baseline without rewriting caller-owned sessions
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests"

result:
  9 passed, 0 failed, 0 skipped
```

## Slice 3: Live-Adapter-Shaped Integration Evidence

Status: complete.

Implementation:

```text
add a deterministic archive-shaped live adapter harness or focused integration
  test using in-memory archive batches
feed the integrated default path through producer/consumer lifecycle signals
that resemble live intake without adding durable transport
keep output/test assertions focused on integration boundary evidence
```

Tests:

```text
integrated default path completes deterministic archive-shaped intake
processing completeness passes
worker failed batches/items are zero
processing validation failed batches are zero
release failures are zero
terminal retained pressure returns to zero
startup prewarm remains visible
no automatic borrowed fallback occurs
```

Exit criteria:

```text
live-adapter-shaped evidence exists as integration evidence only, with no
claim of true live network, durable queue, broker, or cross-process readiness
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests"

result:
  2 passed, 0 failed, 0 skipped
```

## Slice 4: Reporting And Provenance Pass

Status: complete.

Implementation:

```text
add result/provenance fields only if slice 2 or 3 shows an attribution gap
otherwise document that existing result contracts are sufficient
ensure CLI or test-visible output can distinguish:
  provider defaulting
  execution defaulting
  startup prewarm cost
  steady measured allocation
```

Tests:

```text
focused tests assert provider and execution provenance where exposed
existing CLI benchmark default provenance tests continue to pass
```

Exit criteria:

```text
the integration posture can be reviewed without inferring hidden defaults
```

Audit:

```text
docs/milestones/020-default-baseline-runtime-archive-integration-provenance-audit.md
```

Result:

```text
no production result-contract change is required before milestone 020 gate
capture; existing result, telemetry, prewarm, worker, and CLI provenance
contracts are sufficient for the scoped in-process integration boundary
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests|FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~ArchiveRebalanceBenchmarkCommandUsesRolloutDefaultsWhenProviderOmitted|FullyQualifiedName~ArchiveRebalanceBenchmarkCommandLabelsDefaultCandidateContour"

result:
  13 passed, 0 failed, 0 skipped
```

## Slice 5: Gate Capture And Documentation Checkpoint

Status: complete.

Implementation:

```text
record milestone 020 focused gate evidence in a gate document
update this plan's slice statuses as work completes
update handoff with implemented behavior, verification, and remaining
  warnings
stop before writing decision trace
```

Gate requirements:

```text
baseline contract tests pass
owned-construction integration tests pass
live-adapter-shaped integration tests pass if implemented
startup prewarm is visible and separate
provider and execution defaults are separately assertable
terminal retained pressure is clean
release failures are zero
no silent borrowed fallback occurs
```

Exit criteria:

```text
decision-trace inputs are ready, but no decision trace is written until
review
```

Gate evidence:

```text
docs/milestones/020-default-baseline-runtime-archive-integration-gate.md
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
  known allocation-sensitive synthetic benchmark failure:
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

isolated rerun of full-suite failure:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed, 0 skipped
```

## Verification Plan

Focused verification after early slices:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RuntimeArchive|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests"
```

Build verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
```

Broader verification before stopping for decision trace:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Known caveat carried from milestones 018 and 019:

```text
one synthetic benchmark allocation-threshold test may be sensitive in full
suite execution but has passed in isolated rerun
```

## Stop Conditions

Stop and discuss before decision trace.

Stop earlier only if:

```text
the baseline profile requires changing the accepted direct benchmark contour
the owned-construction integration would silently rewrite caller-owned cores
provider and execution provenance cannot remain separately visible
startup prewarm cannot remain separate from steady allocation
queued-owned failure requires silent borrowed fallback to pass
cleanup/release pressure no longer returns to zero
processing completeness is no longer gateable
local tests show a real correctness regression
```
