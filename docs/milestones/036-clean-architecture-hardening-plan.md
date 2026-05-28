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
then capture separate full-cache and processing-only performance evidence for
  architecture closeout discussion
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

The earlier 10/10 boundary assessment was recorded before decision trace.
After review, milestone 036 stays open for a final SRP large-class treatment
extension. The extension keeps the accepted 10/10 Clean Architecture boundary
posture and adds targeted maintainability slices for oversized classes.

Partial class rule:

```text
partial classes are allowed only when the class identity intentionally remains
intact and the split improves navigation around an adapter, benchmark, or
compatibility surface
partial is not a substitute for SRP extraction when a focused collaborator can
own a responsibility
every partial conversion creates a dedicated folder named after the class and
places all partial files for that class in that folder
```

## Slice 10: CLI SRP Split

Status: complete.

Goal:

```text
reduce RadarPulseCliApplication responsibility by extracting command-family
collaborators while preserving accepted CLI arguments, output, and exit codes
```

Planned changes:

```text
move archive command execution, processing command execution, product command
routing, option parsing, and formatting into focused collaborators where the
call graph permits behavior-preserving extraction
if any RadarPulseCliApplication partial remains, place it under a dedicated
EntryPoint/RadarPulseCliApplication folder with responsibility-named files
keep Program.cs as the thin entrypoint guarded by architecture tests
```

Implementation result:

```text
RadarPulseCliApplication was reduced to top-level dispatch and expected
exception handling
archive, archive benchmark, archive inspection, archive validation,
processing, processing benchmark execution, processing benchmark reporting,
product routing, shared usage, shared formatting, and CLI option records now
have focused files under the dedicated EntryPoint/RadarPulseCliApplication
folder
no partial class conversion was required
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~RadarPulseCli|FullyQualifiedName~ProductPipelineCli|FullyQualifiedName~Architecture"
  -c Release --no-build
  result: passed, 44 passed, 0 failed, 0 skipped
git diff --check
```

## Slice 11: Benchmark SRP Split

Status: complete.

Goal:

```text
reduce the largest archive/processing benchmark orchestrators by extracting
cache selection, iteration execution, telemetry capture, validation, and
report formatting responsibilities
```

Planned changes:

```text
target RadarProcessingArchiveRebalanceBenchmark,
RadarProcessingArchiveOrderedProcessingBenchmark, and
RadarProcessingSyntheticRebalanceBenchmark first
prefer focused collaborators over partial files
use dedicated partial folders only for compatibility shells where a split
without new collaborators is the least risky behavior-preserving step
```

Implementation result:

```text
RadarProcessingArchiveRebalanceBenchmark, RadarProcessingArchiveOrderedProcessingBenchmark,
and RadarProcessingSyntheticRebalanceBenchmark now use dedicated per-class
partial folders with responsibility-named files
NEXRAD cache file/date/radar matching and auto-sized source-universe logic
were extracted into RadarProcessingArchiveBenchmarkCacheSelection as a shared
benchmark collaborator
public benchmark APIs and measurement semantics are unchanged
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~Benchmark|FullyQualifiedName~ArchiveQueuedOverlap|FullyQualifiedName~Architecture"
  -c Release --no-build
  result: passed, 123 passed, 0 failed, 0 skipped
git diff --check
```

## Slice 12: Queue And Durable Session SRP Split

Status: complete.

Goal:

```text
reduce state-machine class concentration without changing deterministic
ordering, retry, retention, durable, or completion semantics
```

Planned changes:

```text
target RadarProcessingQueuedProcessingSession,
RadarProcessingQueuedRebalanceSession,
RadarProcessingDurableEnvelopeQueue,
RadarProcessingDurableProcessingSession, and
RadarProcessingDurableRebalanceSession only through safe collaborator
extractions
keep lifecycle/state transition invariants local when extraction would make
correctness harder to reason about
```

Implementation result:

```text
RadarProcessingQueuedProcessingSession and RadarProcessingQueuedRebalanceSession
now use dedicated per-class partial folders for public API, ordered concurrent
work, state/telemetry, and completion models
RadarProcessingDurableEnvelopeQueue now uses a dedicated per-class partial
folder for public operations, transitions/persistence, and envelope models
RadarProcessingDurableProcessingSession and RadarProcessingDurableRebalanceSession
now use dedicated per-class partial folders for public API, completion/ordered
commit, and completion models
ordering/retry/durable lifecycle invariants remain local to each owning class
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
  "FullyQualifiedName~QueuedProcessingSession|FullyQualifiedName~QueuedRebalanceSession|FullyQualifiedName~DurableEnvelopeQueue|FullyQualifiedName~DurableProcessingSession|FullyQualifiedName~DurableRebalanceSession|FullyQualifiedName~Architecture"
  -c Release --no-build
  result: passed, 60 passed, 0 failed, 0 skipped
git diff --check
```

## Slice 13: Large-Class Guardrail And Final Validation

Status: complete.

Goal:

```text
record the post-refactor class-size inventory, document accepted residual
large classes, and stop before decision trace with updated evidence
```

Planned changes:

```text
capture src/test class-size counts after SRP slices
document residual large classes as accepted adapter/benchmark/session
surfaces or name follow-up extraction opportunities
update milestone, handoff, and project-progress
```

Implementation result:

```text
post-SRP inventory captured
production maximum physical class declaration span is now 1_089 code-ish lines
versus the pre-SRP 2_411 code-ish line maximum
remaining large declarations are documented as reporting, archive parsing,
decompression, queue ownership, publisher, validator, benchmark model, or
compatibility/state-machine surfaces
milestone is ready to stop before decision trace
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release
  --no-build
  result: passed, 1011 passed, 0 failed, 3 skipped
git diff --check
```

## Slice 14: Oversized Test Fixture SRP Sweep

Status: complete.

Goal:

```text
finish the milestone SRP treatment by reducing oversized test fixtures and
post-refactor residual physical files to a bounded, responsibility-named
structure without changing runtime behavior or test intent
```

Implementation result:

```text
large test fixtures were converted to dedicated per-class folders with
responsibility-named partial files
behavioral assertions, fixtures, helper factories, and test doubles were
separated by scenario family instead of mixed in single files
the final src/tests C# inventory reports 0 files above 250 code-ish lines
the current largest physical C# file is 249 code-ish lines
the final architecture assessment remains 10/10 across Clean Architecture,
GRASP, SOLID, and pragmatic GoF for the accepted milestone scope
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release
  --no-build
  result: passed, 1016 passed, 0 failed, 3 skipped
src/tests C# file inventory
  result: 0 files above 250 code-ish lines
git diff --check
```

## Slice 9: Performance Evidence Capture

Goal:

```text
record full-cache end-to-end performance evidence and a separate
processing-only benchmark suitable for a restrained world-class technology
claim
```

Planned changes:

```text
capture the full-cache rebalance and ordered custom-handler matrix over
  data/nexrad
run a synthetic processing-only benchmark that excludes archive replay,
  decompression, Archive II scanning, identity normalization, and batch
  construction
keep the claim boundary explicit: local Release evidence, not external
  comparative certification
document throughput, allocation, correctness, source coverage, and remaining
  allocation debt
```

Verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  /p:UseSharedCompilation=false
dotnet src/Presentation/RadarPulse.Cli/bin/Release/net10.0/RadarPulse.Cli.dll
  processing benchmark rebalance-archive --cache data/nexrad --max-files
  1000000 --mode all --execution async --workers 4 --iterations 1
  --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
  --validation-profile benchmark
dotnet src/Presentation/RadarPulse.Cli/bin/Release/net10.0/RadarPulse.Cli.dll
  processing benchmark ordered-archive-processing --cache data/nexrad
  --max-files 1000000 --iterations 1 --warmup-iterations 0 --parallelism 24
  --partitions 24 --shards 4 --active-batches <1|4>
  --handlers <none|counter-checksum|counter-checksum-heavy>
dotnet src/Presentation/RadarPulse.Cli/bin/Release/net10.0/RadarPulse.Cli.dll
  processing benchmark synthetic --sources 46080 --batches 256
  --events-per-batch 4096 --payload-values 64 --partitions 24 --shards 4
  --iterations 5 --warmup-iterations 2 --mode
  <sequential|partitioned|async> --handlers
  <none|counter-checksum|counter-checksum-heavy>
git diff --check
```

Before decision trace discussion, update:

```text
docs/milestones/036-clean-architecture-hardening.md
docs/handoff.md
docs/project-progress.md
docs/milestones/036-clean-architecture-hardening-performance-evidence.md
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
