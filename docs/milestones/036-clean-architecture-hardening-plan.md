# Milestone 036: Implementation Plan

Status: active.

This plan implements the milestone 036 architecture-hardening boundary in
small behavior-preserving slices. The target is a defensible 10/10 score for
Clean Architecture, GRASP, SOLID, and GoF without changing accepted product,
HTTP, CLI, persistence, runtime, or demo/readiness behavior.

## Implementation Strategy

The work is ordered from dependency direction to automated guardrails, then
to SRP cleanup:

```text
first move product-facing abstractions inward
then make Presentation consume the inward contract
then add architecture tests so the direction cannot regress silently
then split product orchestration responsibilities that are currently
  concentrated in one service
then extract a safe product CLI workflow helper from the large entrypoint
then remove the remaining product Application ISP warning
then remove the Domain-to-Infrastructure friend assembly escape hatch
then split the remaining broad CLI command-family responsibilities out of
  the top-level entrypoint
then stabilize the processing benchmark allocation gates so the full test
  suite is not sensitive to process order
```

Each slice should leave the solution buildable and independently reviewable.
The milestone stops before decision trace so the final architecture posture
can be discussed before closeout evidence is written.

## Slice 1: Application Product API Boundary

Goal:

```text
make the product-facing API and use-case service contracts Application-owned
and make Presentation depend on those contracts rather than concrete
Infrastructure API classes
```

Planned changes:

```text
add IRadarPulseProductPipelineService in Application/Product/Pipeline/Contracts
add IRadarPulseProductPipelineApi in Application/Product/Pipeline/Contracts
move product API response mapping into an Application-owned
  RadarPulseProductPipelineApiContract implementation
make Infrastructure RadarPulseProductPipelineService implement the
  Application service port
update HTTP DI to register IRadarPulseProductPipelineService and
  IRadarPulseProductPipelineApi
update HTTP endpoints to accept IRadarPulseProductPipelineApi
update product tests and CLI product workflow call sites for the new boundary
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Product" -c Release --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
git diff --check
```

## Slice 2: Architecture Guardrail Tests

Goal:

```text
make the intended layer and namespace dependency direction executable
```

Planned changes:

```text
add Architecture test folder
add csproj reference direction tests for Domain, Application, Infrastructure,
  Presentation, and tests
add source/namespace dependency tests that reject Domain/Application
  references to Infrastructure or Presentation namespaces
add reflection/source tests that reject product HTTP endpoints depending on
  the concrete Infrastructure product API contract
add tests proving Application owns product API contract abstractions
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Architecture" -c Release --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
git diff --check
```

## Slice 3: Product Pipeline SRP Cleanup

Goal:

```text
reduce ProductPipelineService responsibility concentration without changing
the accepted product service surface
```

Planned changes:

```text
extract synthetic batch creation into a focused product input factory
extract product handler-set creation into a focused handler factory
extract archive batch capture into a focused publisher/helper
keep RadarPulseProductPipelineService as the application-facing
  orchestrator adapter over these helpers
preserve all existing product service and API behavior
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Product" -c Release --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
git diff --check
```

## Slice 4: Product CLI Entry Point Extraction

Goal:

```text
reduce the largest Presentation SRP hotspot by extracting the product
pipeline workflow/formatting from Program.cs
```

Planned changes:

```text
add a focused product CLI workflow helper under Presentation/RadarPulse.Cli
route product pipeline demo/archive commands through the helper
keep product option parsing and command routing compatible
preserve CLI output text covered by current tests
record remaining CLI size as a bounded warning if deeper extraction would
  risk broad behavior churn
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Presentation.Cli|FullyQualifiedName~Product" -c Release
  --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
git diff --check
```

## Slice 5: Product Application Port Segregation

Goal:

```text
remove the remaining Interface Segregation warning by making the
Application product API contract depend on focused run, query, history, and
control ports instead of one broad service port
```

Planned changes:

```text
split the Application product service surface into focused ports:
  run service, query service, history readiness service, and control service
keep IRadarPulseProductPipelineService as a compatibility aggregate only if
  existing direct tests or composition still need it
make RadarPulseProductPipelineApiContract depend on the focused ports
make Infrastructure RadarPulseProductPipelineService implement the focused
  ports
update HTTP DI and tests to register/verify the segmented ports
extend architecture tests so Presentation/API boundary does not regress to a
  concrete Infrastructure contract or one broad service dependency
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Architecture|FullyQualifiedName~Product" -c Release
  --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
git diff --check
```

## Slice 6: Remove Domain Friend Assembly

Goal:

```text
remove Domain InternalsVisibleTo("RadarPulse.Infrastructure") so the Domain
assembly has no privileged Infrastructure adapter access path
```

Planned changes:

```text
delete src/Domain/Properties/AssemblyInfo.cs
replace Infrastructure calls to internal Domain factories, validation helpers,
  exceptions, and retained-resource owner contracts with explicit public
  domain APIs or domain-owned operations
keep behavior and runtime defaults unchanged
add architecture guardrail coverage that fails if Domain grants friend access
  to Infrastructure again
run processing-focused gates because the affected surface is processing-heavy
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Architecture|FullyQualifiedName~Processing" -c Release
  --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
git diff --check
```

## Slice 7: CLI Command-Family Extraction

Goal:

```text
remove the remaining broad Presentation SRP warning by splitting archive and
processing command-family workflows out of Program.cs so the top-level
entrypoint is a thin command router
```

Planned changes:

```text
extract archive command routing/workflows and archive-specific output helpers
  into focused CLI command helpers
extract processing benchmark command routing/workflows and processing-specific
  output helpers into focused CLI command helpers
leave option records and public command shapes compatible unless a focused
  helper can safely own them without changing tests
add or update presentation CLI tests where routing ownership changes
preserve all accepted CLI output text and exit-code behavior
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Presentation.Cli" -c Release --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
git diff --check
```

## Slice 8: Full Suite Benchmark Stabilization

Goal:

```text
remove the remaining process-order sensitivity from processing benchmark
allocation checks so the full Release test suite passes in one process
```

Planned changes:

```text
use execution-mode-appropriate allocation snapshots in synthetic rebalance
  benchmarking so single-threaded/partitioned runs are not polluted by
  unrelated test-process allocations
make runtime default startup-prewarm assertions compare deterministic
  run-local retention telemetry instead of process-wide allocation counters
preserve async shard transport allocation measurement semantics where worker
  allocations may occur on other threads
rerun the previously order-sensitive tests both directly and inside the full
  suite
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingArchiveQueuedOverlapRunnerTests.OmittedOptionsApplyRuntimeDefaultStartupPrewarm|FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  -c Release --no-restore /p:UseSharedCompilation=false
dotnet build RadarPulse.sln -c Release --no-restore
  /p:UseSharedCompilation=false
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release
  --no-build
git diff --check
```

## Final Pre-Decision State

Before decision trace discussion, update:

```text
docs/milestones/036-clean-architecture-hardening.md
docs/handoff.md
docs/project-progress.md
```

Expected final gate before discussion:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release
  --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
git diff --check
```

Stop condition:

```text
do not write decision trace or closeout until the final architecture posture,
remaining warnings, and 10/10 assessment are discussed
```
