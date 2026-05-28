# Milestone 036: Implementation Plan

Status: active.

This plan implements the milestone 036 architecture-hardening boundary in
small behavior-preserving slices. The target is a defensible 9/10 score for
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
remaining warnings, and 9/10 assessment are discussed
```
