# Milestone 036: Clean Architecture Hardening Toward 10/10

Status: active.

Milestone 036 starts after the closed milestone 035 code contract
documentation pass. RadarPulse leaves freeze mode for one targeted
behavior-preserving architecture hardening milestone with an explicit quality
goal: raise the current clean architecture, GRASP, SOLID, and GoF assessment
from roughly 7/10 to 10/10 without reopening accepted runtime semantics.

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
continue SRP treatment for remaining oversized adapter, benchmark, and
  session/state-machine classes after the 10/10 boundary posture is recorded
preserve the accepted product API, CLI behavior, runtime defaults, local
  persistence, and demo/readiness posture
```

The intended outcome is a 10/10 architecture posture, not a broad rewrite.
The score should improve because the most important Clean Architecture and
SOLID risks become mechanically guarded, removed, or isolated:

```text
Domain remains pure and dependency-free
Application owns product contracts, use-case vocabulary, and ports
Infrastructure implements adapters over those ports
Presentation maps HTTP/CLI input to Application contracts
architecture tests prevent dependency drift
remaining Application ISP, Domain friend-assembly, and Presentation SRP risks
  are removed rather than accepted as bounded warnings
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
split broad Application product ports into focused use-case ports
remove privileged Domain friend access from Infrastructure
split remaining broad CLI command-family responsibilities out of Program.cs
split or isolate oversized class responsibilities through focused
  collaborators first, using partial class folder layout only as a navigation
  aid when the class identity must remain intact
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

## 10/10 Acceptance Criteria

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
major SRP hotspots have focused extractions rather than score-blocking
  bounded warnings
Application product API contract depends on focused run/query/history/control
  ports rather than one broad service dependency
Domain does not grant InternalsVisibleTo access to Infrastructure
architecture tests fail if Domain grants friend access to Infrastructure
Program.cs is a thin command router for product, archive, and processing
  command families
oversized adapter/benchmark/session classes are reduced through real SRP
  collaborators where behavior-preserving; any partial class conversion uses
  a dedicated per-class folder with files named by responsibility zone
all accepted product, HTTP, CLI, persistence, and runtime tests still pass
```

## Change Log

### Change 1: Open Clean Architecture Hardening Milestone

Status: complete.

Intent:

```text
open a targeted architecture-hardening milestone with an explicit initial
9/10 goal that was later expanded to 10/10
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
inward, add architecture guardrails, and reduce SRP hotspots toward the
initial 9/10 architecture goal that was later expanded to 10/10
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
evaluated during this slice and intentionally left for the later 10/10
expansion; Change 9 removes that friend access explicitly
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

Status: superseded by the 10/10 scope expansion.

Summary:

```text
implementation slices 1-4 are committed
product API boundary now points Presentation at Application contracts
architecture guardrails are executable tests
product service and product CLI SRP hotspots are reduced with bounded notes
decision trace and closeout are intentionally not written yet
10/10 expansion adds follow-up slices for port segregation, Domain friend
  assembly removal, and remaining CLI command-family extraction
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

### Change 7: Expand Scope To 10/10 Architecture Target

Status: complete.

Intent:

```text
replace the earlier 9/10 target with a full 10/10 architecture target inside
milestone 036 rather than deferring the remaining warnings to later
milestones
```

Scope:

```text
docs/milestones/036-clean-architecture-hardening.md
docs/milestones/036-clean-architecture-hardening-plan.md
docs/project-progress.md
docs/handoff.md
```

Outcome:

```text
milestone 036 now requires removal of the remaining Product Application ISP
warning, Domain-to-Infrastructure friend assembly warning, and broad CLI
entrypoint SRP warning
new slices 5-7 are planned for Application port segregation, Domain friend
assembly removal, and CLI command-family extraction
decision trace remains blocked until the 10/10 target can be reviewed
```

Verification:

```text
documentation-only scope expansion
git diff --check
  result: passed
```

### Change 8: Product Application Port Segregation

Status: complete.

Intent:

```text
remove the remaining product Application Interface Segregation warning by
making the Application API contract depend on focused run, query, history,
and control ports instead of one broad service port
```

Scope:

```text
src/Application/Product/Pipeline/Contracts/RadarPulseProductPipelineContracts.cs
src/Presentation/RadarPulse.Http/Product/Composition/RadarPulseProductHttpServiceCollectionExtensions.cs
tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs
tests/RadarPulse.Tests/Product
docs/milestones/036-clean-architecture-hardening.md
docs/handoff.md
```

Outcome:

```text
Application now exposes focused product pipeline run, query, history, and
control service ports
IRadarPulseProductPipelineService remains only as a compatibility aggregate
for direct service consumers
RadarPulseProductPipelineApiContract has one constructor over the focused
ports and stores only focused port dependencies
HTTP DI registers the focused ports to the accepted Infrastructure service
architecture tests now fail if the API contract regresses to depending on the
broad aggregate service port
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Architecture|FullyQualifiedName~Product" -c Release
  --no-restore
  result: passed, 91 passed, 0 failed, 0 skipped
dotnet build RadarPulse.sln -c Release --no-restore
  result: failed once because a parallel test/build run held
    src/Domain/obj/Release/net10.0/RadarPulse.Domain.dll through VBCSCompiler
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
```

### Change 9: Remove Domain Friend Assembly

Status: complete.

Intent:

```text
remove the Domain-to-Infrastructure friend assembly escape hatch and make the
Domain APIs used by Infrastructure explicit public contracts
```

Scope:

```text
src/Domain/Properties/AssemblyInfo.cs
src/Domain/Processing/Core
src/Domain/Processing/Handlers
src/Domain/Processing/Queueing
src/Domain/Processing/Rebalance
src/Domain/Processing/Retention
src/Domain/Streaming/Batches/Models/RadarEventBatch.cs
src/Infrastructure/Processing/Retention/Services/RadarProcessingRetainedPayloadFactory.cs
tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs
docs/milestones/036-clean-architecture-hardening.md
docs/handoff.md
```

Outcome:

```text
Domain no longer grants InternalsVisibleTo("RadarPulse.Infrastructure")
the internal retained payload release-owner contract was removed in favor of
the existing public callback-based retained resource constructor
Infrastructure processing dependencies now use explicit public Domain APIs for
batch metrics, processing-delta lifecycle, async shard work, handler delta
merge commit, queue status validation, rebalance completion, and retained
payload strategy validation
newly public Domain contracts have concise XML summaries
architecture tests now fail if the Infrastructure friend grant is restored
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Architecture" -c Release --no-build
  result: passed, 6 passed, 0 failed, 0 skipped
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Processing" -c Release --no-build
  result: failed in combined processing process, 744 passed, 1 failed,
    0 skipped
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  -c Release --no-build
  result: passed, 1 passed, 0 failed, 0 skipped
git diff --check
  result: passed
```

### Change 10: CLI Command-Family Extraction

Status: complete.

Intent:

```text
remove the remaining Presentation entrypoint SRP hotspot by making Program.cs
a thin entrypoint and moving CLI command-family application logic out of the
top-level file
```

Scope:

```text
src/Presentation/RadarPulse.Cli/EntryPoint/Program.cs
src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication.cs
tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs
docs/milestones/036-clean-architecture-hardening.md
docs/handoff.md
```

Outcome:

```text
Program.cs now delegates to RadarPulseCliApplication.RunAsync and contains no
command workflow functions
RadarPulseCliApplication owns the accepted archive, processing, and product
command-family routing/application logic and preserves the existing global
option record contracts used by tests
architecture tests now fail if Program.cs grows beyond the thin entrypoint
shape or regains static command workflow functions
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Architecture" -c Release --no-restore
  /p:UseSharedCompilation=false
  result: passed, 7 passed, 0 failed, 0 skipped
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~RadarPulseCli|FullyQualifiedName~ProductPipelineCli"
  -c Release --no-build
  result: passed, 37 passed, 0 failed, 0 skipped
```

### Change 11: Full Suite Benchmark Stabilization

Status: complete.

Intent:

```text
remove process-order sensitivity from processing benchmark allocation gates so
the full Release test suite does not fail because unrelated tests ran first
```

Scope:

```text
src/Infrastructure/Processing/Benchmarks/Services/RadarProcessingSyntheticRebalanceBenchmark.cs
tests/RadarPulse.Tests/Processing/Queueing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
docs/milestones/036-clean-architecture-hardening.md
docs/milestones/036-clean-architecture-hardening-plan.md
docs/project-progress.md
docs/handoff.md
```

Outcome:

```text
synthetic rebalance benchmarking now uses current-thread allocation snapshots
for non-async execution modes and keeps process-wide snapshots for async
shard transport, where worker allocations can occur on other threads
the startup-prewarm runtime default test now compares deterministic run-local
retention telemetry instead of process-wide allocation counters
the previously order-sensitive processing tests pass directly and the full
Release test suite now passes in one combined run
```

Verification:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingArchiveQueuedOverlapRunnerTests.OmittedOptionsApplyRuntimeDefaultStartupPrewarm|FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  -c Release --no-restore /p:UseSharedCompilation=false
  result: passed, 2 passed, 0 failed, 0 skipped
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release --no-build
  result: passed, 1011 passed, 0 failed, 3 skipped
git diff --check
  result: passed
```

### Change 12: Performance Evidence Capture

Status: complete.

Intent:

```text
record full-cache end-to-end performance and a separate processing-only
benchmark suitable for a restrained world-class technology claim
```

Scope:

```text
docs/milestones/036-clean-architecture-hardening-performance-evidence.md
docs/milestones/036-clean-architecture-hardening.md
docs/milestones/036-clean-architecture-hardening-plan.md
docs/project-progress.md
docs/handoff.md
```

Outcome:

```text
full-cache matrix records rebalance-archive borrowed/default rows and
ordered-archive-processing handler rows over data/nexrad
processing-only synthetic matrix isolates RadarProcessingCore handler
throughput from archive replay, decompression, Archive II scanning, identity
normalization, and batch construction
performance evidence records throughput, allocation, correctness, source
coverage, raw artifact directories, and claim boundaries
```

Key evidence:

```text
full-cache ordered custom-handler path:
  46_080 logical sources
  27_254_760 RadarStreamEvent records
  32_306_203_200 payload values
  counter-checksum-heavy active=4:
    447_152.29 RadarStreamEvent/s end-to-end
    530_028_245.90 payload values/s end-to-end
    12_212_257_456 allocated bytes
    448.08 bytes/RadarStreamEvent

processing-only heavy handler path:
  46_080 logical sources
  1_048_576 RadarStreamEvent records per measured iteration
  sequential heavy:
    2_101_506.66 RadarStreamEvent/s
  partitioned heavy:
    2_060_612.64 RadarStreamEvent/s
  async heavy:
    1_140_818.38 RadarStreamEvent/s
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
full-cache raw logs:
  data/perf/m036-full-cache-20260528-142529
processing-only raw logs:
  data/perf/m036-world-class-20260528-151123
```

### Change 13: Extend SRP Large-Class Treatment Slices

Status: complete.

Intent:

```text
continue milestone 036 before decision trace with behavior-preserving SRP
treatment for oversized classes found after the 10/10 boundary assessment
prefer focused collaborators over partial-only splits
allow partial classes only when class identity must remain intact, and then
  place every partial file in a dedicated folder named after that class
```

Rules:

```text
do not use partial as a substitute for extracting domain/runtime
  responsibility when a collaborator can own that behavior
partial is acceptable for presentation adapter, benchmark surface, and
  compatibility shell organization when behavior-preserving extraction would
  create more churn than value
every partial class conversion creates a personal folder:
  <Parent>/<ClassName>/<ClassName>.<Responsibility>.cs
shared mutable state must not increase during partial conversion
each new file name must describe one responsibility zone, such as Routing,
  Options, Formatting, Execution, Telemetry, Iteration, or Reporting
runtime, API, CLI, and benchmark output semantics remain accepted unless a
  later decision explicitly changes them
```

Planned slices:

```text
10. CLI SRP split:
    extract command-family collaborators from RadarPulseCliApplication and
    keep any remaining partial files under the dedicated
    EntryPoint/RadarPulseCliApplication folder.
11. Archive/processing benchmark SRP split:
    extract focused collaborators from the largest benchmark orchestrators,
    especially cache/file selection, iteration execution, telemetry capture,
    validation, and report formatting responsibilities.
12. Queue/session SRP split:
    isolate safe lifecycle, completion, retry, ordering, and telemetry
    responsibilities from queued and durable session classes without changing
    state-machine semantics.
13. Large-class guardrail and final validation:
    capture the remaining class-size inventory, document accepted residual
    large classes, run focused and full Release gates, and stop before
    decision trace.
```

Verification:

```text
documentation-only scope expansion; code slices run focused Release build and
tests before each slice commit
```

### Change 14: CLI SRP Split

Status: complete.

Intent:

```text
replace the large RadarPulseCliApplication command-family class with focused
CLI collaborators while preserving accepted arguments, output, and exit codes
make RadarPulseCliApplication a thin application router rather than a
container for archive, processing, product, benchmark, inspection, validation,
usage, option, and formatting responsibilities
```

Scope:

```text
src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication/
  RadarPulseCliApplication.cs
  ArchiveCliApplication.cs
  ArchiveBenchmarkCliApplication.cs
  ArchiveInspectionCliApplication.cs
  ArchiveValidationCliApplication.cs
  ProcessingCliApplication.cs
  ProcessingBenchmarkCliApplication.cs
  ProcessingBenchmarkCliReporter.cs
  ProductCliApplication.cs
  RadarPulseCliUsage.cs
  CliFormat.cs
  RadarPulseCliOptions.cs
src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication.cs
  removed
```

Result:

```text
RadarPulseCliApplication now owns only top-level error handling and top-level
archive/processing/product dispatch
archive list/download/replay/stream behavior is isolated from archive
benchmark, inspection, and validation behavior
processing command routing is isolated from processing benchmark execution and
processing benchmark reporting/formatting
product command routing remains focused around the existing product pipeline
workflow
shared usage and formatting helpers are no longer hidden inside the top-level
application router
no partial class conversion was needed for this slice
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~RadarPulseCli|FullyQualifiedName~ProductPipelineCli|FullyQualifiedName~Architecture"
  -c Release --no-build
  result: passed, 44 passed, 0 failed, 0 skipped
```

### Change 15: Benchmark SRP Split

Status: complete.

Intent:

```text
reduce benchmark orchestrator concentration without changing benchmark public
APIs, measurement semantics, output semantics, or accepted benchmark defaults
use dedicated partial folders for compatibility shells where private nested
telemetry/state models make direct collaborator extraction high churn
extract shared collaborators where duplicated benchmark responsibility is
safe to remove
```

Scope:

```text
src/Infrastructure/Processing/ArchiveRuntime/Services/
  RadarProcessingArchiveRebalanceBenchmark/
  RadarProcessingArchiveOrderedProcessingBenchmark/
  RadarProcessingArchiveBenchmarkCacheSelection.cs
src/Infrastructure/Processing/Benchmarks/Services/
  RadarProcessingSyntheticRebalanceBenchmark/
```

Result:

```text
RadarProcessingArchiveRebalanceBenchmark is split into public API,
source-universe/prewarm, iteration execution, cache selection, telemetry,
validation, batch processor, and model zones
RadarProcessingArchiveOrderedProcessingBenchmark is split into public API,
iteration execution, result factory, cache selection, validation, and model
zones
RadarProcessingSyntheticRebalanceBenchmark is split into public API,
iteration execution, validation, and model zones
shared NEXRAD cache file matching, date/radar filtering, and auto-sized
source-universe logic moved into RadarProcessingArchiveBenchmarkCacheSelection
partial conversion used the dedicated per-class folder rule
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Benchmark|FullyQualifiedName~ArchiveQueuedOverlap|FullyQualifiedName~Architecture"
  -c Release --no-build
  result: passed, 123 passed, 0 failed, 0 skipped
```

### Change 16: Queue And Durable Session SRP Split

Status: complete.

Intent:

```text
reduce state-machine/session file concentration while preserving deterministic
provider ordering, retry, durable lifecycle, retained resource release, and
ordered commit semantics
avoid aggressive collaborator extraction where moving lifecycle state would
make correctness harder to audit
```

Scope:

```text
src/Infrastructure/Processing/Queueing/Services/
  RadarProcessingQueuedProcessingSession/
  RadarProcessingQueuedRebalanceSession/
src/Infrastructure/Processing/Durable/Services/
  RadarProcessingDurableEnvelopeQueue/
  RadarProcessingDurableProcessingSession/
  RadarProcessingDurableRebalanceSession/
```

Result:

```text
queued processing and queued rebalance sessions are split into public API,
ordered concurrent execution, session state/telemetry, and completion model
zones
durable envelope queue is split into public operations, transition/persistence,
and envelope-entry model zones
durable processing and durable rebalance sessions are split into public API,
completion/ordered commit, and completion model zones
partial conversion used the dedicated per-class folder rule
state-machine invariants remain inside their owning session classes
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~QueuedProcessingSession|FullyQualifiedName~QueuedRebalanceSession|FullyQualifiedName~DurableEnvelopeQueue|FullyQualifiedName~DurableProcessingSession|FullyQualifiedName~DurableRebalanceSession|FullyQualifiedName~Architecture"
  -c Release --no-build
  result: passed, 60 passed, 0 failed, 0 skipped
```

## Final 10/10 Pre-Decision Validation

Status: superseded by the large-class SRP extension.

Summary:

```text
implementation and evidence slices 1-9 are complete
Product API boundary points Presentation at Application contracts
Application product API contract depends on focused ports
architecture guardrails cover project direction, namespace direction, Product
API ownership, API port segregation, Domain friend access, HTTP endpoint API
dependency, and thin CLI Program.cs entrypoint shape
Domain no longer grants friend access to Infrastructure
Product service, Product CLI workflow, and CLI entrypoint SRP hotspots are
reduced or guarded without changing accepted behavior
processing benchmark allocation gates no longer depend on full-suite process
order for the accepted Release test run
performance evidence is captured separately for full-cache end-to-end runtime
and processing-only handler-engine throughput
decision trace and closeout are intentionally not written yet
```

Final architecture assessment:

| Area | Score | Conclusion |
| --- | ---: | --- |
| Clean Architecture | 10/10 | The accepted dependency direction is now implemented and guarded: Domain stays dependency-free, Application owns product contracts and ports, Infrastructure implements adapters, and Presentation depends on Application product API abstractions. |
| GRASP | 10/10 | Responsibilities are assigned to focused experts/controllers: product orchestration helpers, product CLI workflow, and the top-level CLI entrypoint are separated or guarded, while indirection and protected variation are expressed through Application ports. |
| SOLID | 10/10 | SRP pressure was reduced in the product service and CLI entrypoint, ISP is addressed through focused run/query/history/control ports, DIP is enforced through Application contracts, and no substitutability break was found in the accepted service-port implementations. |
| GoF | 10/10 | The design uses patterns pragmatically where they solve current problems: facade/adapter behavior at the Product API contract boundary, strategy-like handler/policy variation, and composition/factory wiring without pattern-chasing. |
| Automated guardrails | 10/10 | Architecture tests now guard project direction, namespace direction, Product API ownership, Product API port segregation, Domain friend access, HTTP endpoint dependencies, and the thin CLI Program.cs entrypoint shape. |
| Evidence posture | 10/10 | Release build, focused architecture/product/CLI tests, the full Release test suite, benchmark stabilization, and performance evidence are all captured before decision trace discussion. |

Overall verdict:

```text
Milestone 036 reaches a defensible 10/10 architecture posture for the
accepted local product demo/runtime boundary and current portfolio scope.
This is not a claim that no future production optimization or feature work
exists; it means the reviewed Clean Architecture, GRASP, SOLID, and pragmatic
GoF concerns no longer contain material blockers in the current scope.
```

Residual notes that do not reduce the score:

```text
IRadarPulseProductPipelineService remains as a compatibility aggregate, but
  Presentation-facing API code depends on focused Application ports and the
  Application product API abstraction.
RadarPulseCliApplication still owns command-family routing, but Program.cs is
  thin and guardrailed; deeper CLI decomposition would be refinement rather
  than a 10/10 blocker.
Full-cache active=4 heavy-handler allocation pressure remains a performance
  optimization signal, not a Clean Architecture, GRASP, SOLID, or GoF
  violation.
Decision trace and closeout remain intentionally unwritten until the final
  posture, warnings, and assessment are discussed.
```

Final gate evidence:

```text
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Architecture|FullyQualifiedName~Product|FullyQualifiedName~RadarPulseCli|FullyQualifiedName~ProductPipelineCli"
  -c Release --no-build
  result: passed, 126 passed, 0 failed, 0 skipped
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release --no-build
  result: passed, 1011 passed, 0 failed, 3 skipped
docs/milestones/036-clean-architecture-hardening-performance-evidence.md
  result: captured full-cache and processing-only performance evidence
git diff --check
  result: passed
```
