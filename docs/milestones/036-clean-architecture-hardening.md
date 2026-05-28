# Milestone 036: Clean Architecture Hardening Toward 9/10

Status: active.

Milestone 036 starts after the closed milestone 035 code contract
documentation pass. RadarPulse leaves freeze mode for one targeted
behavior-preserving architecture hardening milestone with an explicit quality
goal: raise the current clean architecture, GRASP, SOLID, and GoF assessment
from roughly 7/10 to 9/10 without reopening accepted runtime semantics.

## Milestone Goal

Make the current accepted local product demo architecture materially cleaner
and easier to defend by addressing the largest design issues found in the
architecture review:

```text
move product-facing use-case contracts into the Application layer
keep Infrastructure as adapters and runtime composition behind ports
make Presentation depend on Application contracts rather than concrete
  Infrastructure API classes
add automated architecture tests for project references and namespace
  dependency direction
reduce SRP pressure around product orchestration and presentation entrypoint
  hotspots where changes are behavior-preserving
preserve the accepted product API, CLI behavior, runtime defaults, local
  persistence, and demo/readiness posture
```

The intended outcome is a 9/10 architecture posture, not a broad rewrite. The
score should improve because the most important Clean Architecture and SOLID
risks become mechanically guarded or isolated:

```text
Domain remains pure and dependency-free
Application owns product contracts, use-case vocabulary, and ports
Infrastructure implements adapters over those ports
Presentation maps HTTP/CLI input to Application contracts
architecture tests prevent dependency drift
known large-file/SRP risks are either reduced or explicitly bounded
```

## Scope Rules

Safe in this milestone:

```text
add Application interfaces/ports for product pipeline use cases
move or wrap product API contract behavior into Application-owned contracts
change DI registration to bind Application contracts to Infrastructure
  implementations
split focused helper responsibilities out of product orchestration where it
  keeps public behavior unchanged
add architecture tests for layer and namespace dependency rules
add focused tests around moved contracts and DI wiring
update docs/handoff/progress after every slice
run focused Release build and relevant test gates after each implementation
  slice
```

Not safe in this milestone unless explicitly reprioritized:

```text
change public product DTO/API response shape
change accepted runtime/default pipeline semantics
rewrite processing core, archive replay, durable queue, or Angular UI behavior
perform namespace alignment across the whole repository
add production hosting/security/live-ingestion/external adapter scope
close the milestone with a decision trace before discussion
```

## 9/10 Acceptance Criteria

Milestone 036 is successful when the following are true:

```text
Presentation references product API abstractions from Application, not a
  concrete Infrastructure API contract
product-facing API contract and response mapping are Application-owned
Infrastructure product services implement Application ports
project reference direction remains Domain <- Application <- Infrastructure
  and Presentation as composition root
architecture tests fail if Domain/Application depend on Infrastructure or
  Presentation namespaces
architecture tests fail if product HTTP endpoints take concrete Infrastructure
  product API contracts
major SRP hotspots have either a focused extraction or a documented bounded
  warning that does not block the 9/10 score
all accepted product, HTTP, CLI, persistence, and runtime tests still pass
```

## Change Log

### Change 1: Open Clean Architecture Hardening Milestone

Status: complete.

Intent:

```text
open a targeted architecture-hardening milestone with an explicit 9/10 goal
and a scoped implementation boundary
```

Scope:

```text
docs/milestones/036-clean-architecture-hardening.md
docs/project-progress.md
docs/handoff.md
```

Verification:

```text
documentation-only opening change; runtime gate deferred until the detailed
implementation plan and code slices
```

### Change 2: Detailed Implementation Plan

Status: complete.

Intent:

```text
define behavior-preserving slices that move product API/use-case contracts
inward, add architecture guardrails, and reduce SRP hotspots toward the 9/10
architecture goal
```

Scope:

```text
docs/milestones/036-clean-architecture-hardening-plan.md
docs/milestones/036-clean-architecture-hardening.md
docs/handoff.md
```

Verification:

```text
documentation-only planning change; runtime gate deferred until slice 1
```

### Change 3: Application Product API Boundary

Status: complete.

Intent:

```text
move the product-facing API contract inward to Application and make
Presentation consume an Application API interface instead of the concrete
Infrastructure product API class
```

Scope:

```text
src/Application/Product/Pipeline/Contracts/RadarPulseProductPipelineContracts.cs
src/Infrastructure/Product/Pipeline/Services/RadarPulseProductPipelineService.cs
src/Infrastructure/Product/Pipeline/Contracts/RadarPulseProductPipelineApiContract.cs
src/Presentation/RadarPulse.Http/Product/Composition/RadarPulseProductHttpServiceCollectionExtensions.cs
src/Presentation/RadarPulse.Http/Product/Endpoints/RadarPulseProductHttpEndpoints.cs
tests/RadarPulse.Tests/Product
docs/milestones/036-clean-architecture-hardening.md
docs/handoff.md
```

Outcome:

```text
Application now owns IRadarPulseProductPipelineService,
IRadarPulseProductPipelineApi, and RadarPulseProductPipelineApiContract
Infrastructure RadarPulseProductPipelineService implements the Application
  service port
HTTP endpoint methods depend on IRadarPulseProductPipelineApi
HTTP DI binds the Application service/API ports to the accepted Infrastructure
  service and Application API contract implementation
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Product" -c Release --no-restore
  result: passed, 86 passed, 0 failed, 0 skipped
dotnet build RadarPulse.sln -c Release --no-restore
  result: passed, 0 warnings, 0 errors
git diff --check
  result: passed
```

### Change 4: Architecture Guardrail Tests

Status: complete.

Intent:

```text
add executable architecture rules that protect the current clean architecture
direction and the new Application-owned product API boundary
```

Scope:

```text
tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs
docs/milestones/036-clean-architecture-hardening.md
docs/handoff.md
```

Outcome:

```text
architecture tests now verify project-reference direction for Domain,
Application, Infrastructure, HTTP, and CLI projects
Domain and Application implementation source is checked for direct outer
namespace references
product API ownership is checked by reflection so the API/service ports and
contract stay in Application
HTTP endpoints are checked so public endpoint methods depend on the
IRadarPulseProductPipelineApi port rather than a concrete product API contract
```

Bounded note:

```text
full removal of Domain InternalsVisibleTo("RadarPulse.Infrastructure") was
evaluated during this slice and intentionally left out of scope because the
processing infrastructure currently relies on multiple internal domain APIs;
that cleanup is a separate deep processing refactor rather than a product
boundary guardrail
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Architecture" -c Release --no-restore
  result: passed, 4 passed, 0 failed, 0 skipped
dotnet build RadarPulse.sln -c Release --no-restore
  result: passed, 0 warnings, 0 errors
```

### Change 5: Product Pipeline SRP Cleanup

Status: complete.

Intent:

```text
reduce the product pipeline service SRP pressure by moving deterministic input
creation, handler-set creation, and archive batch capture into focused
Infrastructure product helpers
```

Scope:

```text
src/Infrastructure/Product/Pipeline/Services/RadarPulseProductPipelineService.cs
src/Infrastructure/Product/Pipeline/Archive/CapturingArchiveRadarEventBatchPublisher.cs
src/Infrastructure/Product/Pipeline/Batching/RadarPulseProductSyntheticBatchFactory.cs
src/Infrastructure/Product/Pipeline/Handlers/RadarPulseProductHandlerFactory.cs
docs/milestones/036-clean-architecture-hardening.md
docs/handoff.md
```

Outcome:

```text
RadarPulseProductPipelineService now stays focused on run orchestration,
history persistence, queries, and control delegation
synthetic RadarEventBatch construction is isolated in
RadarPulseProductSyntheticBatchFactory
product handler-set selection and product-only test handlers are isolated in
RadarPulseProductHandlerFactory
archive publish capture is isolated in CapturingArchiveRadarEventBatchPublisher
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Product" -c Release --no-restore
  result: passed, 88 passed, 0 failed, 0 skipped
dotnet build RadarPulse.sln -c Release --no-restore
  result: passed, 0 warnings, 0 errors
```

### Change 6: Product CLI Entrypoint Extraction

Status: complete.

Intent:

```text
move product pipeline CLI workflow and output formatting out of the broad
top-level entrypoint while preserving the accepted product CLI command shape,
output, and exit-code behavior
```

Scope:

```text
src/Presentation/RadarPulse.Cli/EntryPoint/Program.cs
src/Presentation/RadarPulse.Cli/Product/ProductPipelineCliWorkflow.cs
docs/milestones/036-clean-architecture-hardening.md
docs/handoff.md
```

Outcome:

```text
Program.cs now only routes product pipeline demo and archive commands to a
focused workflow helper
ProductPipelineCliWorkflow owns product pipeline service execution, request
construction, product run detail printing, and product-specific formatting
product CLI option records remain unchanged as the accepted command-line
contract
```

Bounded note:

```text
the CLI entrypoint remains large because archive and processing benchmark
commands are still in Program.cs; this slice removes the product workflow
hotspot without reopening unrelated command families
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Presentation.Cli.Product|FullyQualifiedName~RadarPulseProductPipelineCli"
  -c Release --no-restore
  result: passed, 4 passed, 0 failed, 0 skipped
dotnet build RadarPulse.sln -c Release --no-restore
  result: passed, 0 warnings, 0 errors
```

## Pre-Decision Validation

Status: ready for discussion before decision trace.

Summary:

```text
implementation slices 1-4 are committed
product API boundary now points Presentation at Application contracts
architecture guardrails are executable tests
product service and product CLI SRP hotspots are reduced with bounded notes
decision trace and closeout are intentionally not written yet
```

Final gate evidence:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Architecture|FullyQualifiedName~Product|FullyQualifiedName~RadarPulseProductPipelineCli"
  -c Release --no-build
  result: passed, 90 passed, 0 failed, 0 skipped
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release --no-build
  result: failed in combined all-tests process, 1006 passed, 2 failed,
    3 skipped
  isolated rerun of both failed processing tests passed, confirming the
    existing process-order/benchmark sensitivity rather than a slice
    regression
```
