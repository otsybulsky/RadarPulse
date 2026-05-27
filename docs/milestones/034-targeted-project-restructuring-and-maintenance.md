# Milestone 034: Targeted Project Restructuring And Maintenance

Status: open.

Milestone 034 starts from the closed milestone 033 product demo polish and
portfolio readiness milestone and keeps the project in freeze mode.

Milestone 033 closed with this answer:

```text
accepted with scoped warnings for product demo polish and portfolio readiness
over deterministic local demo/archive-shaped workflows
```

This milestone is a documentation-level container for targeted project
restructuring, small cleanup, and maintenance changes that preserve the
accepted portfolio-ready local demo boundary. It is intentionally not a new
architecture milestone and not a detailed implementation plan.

The important shift is:

```text
from:
  RadarPulse is portfolio-ready and in freeze mode after milestone 033

to:
  RadarPulse can receive a controlled sequence of point changes,
  restructuring steps, documentation corrections, and maintenance fixes
  without reopening accepted runtime, product, HTTP, persistence, UI, or
  demo/readiness decisions
```

## Milestone Goal

Milestone 034 should give the project one visible place to record and execute
small, scoped changes after portfolio readiness.

The milestone should preserve these outcomes:

```text
accepted deterministic local product demo behavior remains intact
project structure changes are recorded before or as they happen
renames, moves, cleanup, and maintenance fixes remain reviewable as separate
  targeted changes
documentation stays aligned with the repository layout
verification is chosen per change, based on touched surface and risk
no new runtime architecture, product feature, live-ingestion, deployment,
  external adapter, security, or delivery-certification scope is introduced
```

## Change Container Rules

This milestone tracks a sequence of small changes. Each change should be
recorded as a short entry, not expanded into a full milestone plan unless the
work proves larger than expected.

Each entry should capture:

```text
change:
  short name of the targeted change

intent:
  why the change exists

scope:
  files, projects, or documentation areas expected to move

status:
  queued, in progress, complete, or blocked

verification:
  no runtime gate, focused test, build, packaged verify, or other relevant
  check

notes:
  blockers, follow-ups, or scope warnings
```

## Safe Work

Safe in milestone 034:

```text
rename projects, folders, namespaces, or documentation headings when the
  accepted behavior is preserved
move files or reorganize project layout when references and docs are updated
remove obsolete or duplicated documentation after checking that no accepted
  milestone evidence is lost
polish README, demo docs, handoff, or progress wording
fix targeted bugs found during maintenance or restructuring
adjust scripts or project files when required by a recorded restructuring
  change
run focused gates that match the touched surface
record deferred follow-ups without promoting them to architecture scope
```

Not safe in milestone 034 unless explicitly reprioritized:

```text
reopening accepted milestone 020-033 runtime, product, HTTP, persistence, UI,
  or demo/readiness decisions
adding true live radar network ingestion
adding external broker/cloud queue/database adapter certification
adding public production deployment automation
adding authentication, authorization, TLS termination, or production CORS
  hardening
claiming exactly-once production delivery
turning maintenance cleanup into an unbounded architecture refactor
silently changing accepted product API or DTO semantics during a rename
```

## Verification Posture

Verification should stay proportional to each targeted change.

Expected gate choices:

```text
documentation-only change:
  no runtime gate required

project/file rename or reference update:
  dotnet build RadarPulse.sln -c Release --no-restore

product HTTP/API behavior touched:
  focused product HTTP/API/readiness Release tests

OperatorUi touched:
  npm test -- --watch=false
  npm run build
  browser smoke when rendered workflow behavior changes

package script or demo workflow touched:
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
    help
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
    paths
  bash scripts/radarpulse-product-demo.sh help
  bash scripts/radarpulse-product-demo.sh paths
  packaged verify when behavior or delivery assumptions change
```

## Change Log

### Change 1: Open Maintenance Milestone

Status: complete.

Intent:

```text
create a documentation-level milestone for a sequence of targeted
restructuring and maintenance changes after milestone 033 freeze mode
```

Scope:

```text
docs/milestones/034-targeted-project-restructuring-and-maintenance.md
docs/handoff.md
docs/project-progress.md
```

Verification:

```text
documentation-only change; no runtime gate required
```

Notes:

```text
no separate implementation plan is opened for this milestone at creation
time; future entries should stay lightweight unless a change grows beyond
targeted maintenance scope
```

### Change 2: Cross-Platform Demo Entrypoints

Status: complete.

Intent:

```text
make the local product demo package runnable from Windows, Linux, macOS, and
WSL2 without requiring PowerShell on Unix-like environments
```

Scope:

```text
scripts/radarpulse-product-demo.ps1
scripts/radarpulse-product-demo.sh
.gitattributes
README.md
docs/product-demo-readiness.md
src/Presentation/OperatorUi/README.md
```

Verification:

```text
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
  help
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
  paths
bash -n scripts/radarpulse-product-demo.sh
bash scripts/radarpulse-product-demo.sh help
bash scripts/radarpulse-product-demo.sh paths
bash scripts/radarpulse-product-demo.sh paths --json
bash scripts/radarpulse-product-demo.sh reset-history
bash scripts/radarpulse-product-demo.sh reset-history --history-path
  .tmp/product-demo/nested/history.json
bash scripts/radarpulse-product-demo.sh reset-history --history-path
  .tmp-outside-guard-*/history.json
bash scripts/radarpulse-product-demo.sh reset-history --history-path
  .tmp/product-demo/../outside-guard-*/history.json
bash scripts/radarpulse-product-demo.sh reset-history --history-path
  .tmp/product-demo
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
  verify
bash scripts/radarpulse-product-demo.sh start --url http://127.0.0.1:5141
bash scripts/radarpulse-product-demo.sh readiness --url http://127.0.0.1:5141
bash scripts/radarpulse-product-demo.sh demo --url http://127.0.0.1:5141
  --run-id bash-host-mode-demo
bash scripts/radarpulse-product-demo.sh history --url http://127.0.0.1:5141
bash scripts/radarpulse-product-demo.sh verify
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
  verify
```

Notes:

```text
Windows keeps the accepted PowerShell package script. Linux, macOS, and WSL2
use a native Bash package script that calls dotnet, npm, and curl directly.
PowerShell 7 remains optional for users who prefer it, but it is not required
for the Unix workflow. Reset-history guards reject outside paths and
dot-dot traversal before deleting the resolved history file. Directory
targets are rejected instead of being removed. Packaged verify refreshes
.NET restore metadata for the current OS before the no-restore gates, which
keeps the same checkout usable when switching between Windows and WSL/Linux in
both directions.
```

### Change 3: Backend Responsibility Folder Structure

Status: in progress; Processing, application, archive, and product slices
complete.

Intent:

```text
make backend code navigation responsibility-first and type-second while
preserving accepted runtime behavior and avoiding namespace/API churn in the
initial move slices
```

Structure rule:

```text
keep existing project/layer boundaries:
  Domain
  Application
  Infrastructure
  Presentation

within each layer, group by responsibility/capability first:
  Processing/Rebalance
  Processing/Queueing
  Processing/Topology
  Processing/Workers
  Processing/Durable
  Processing/Retention
  Processing/Handlers
  Processing/Pressure
  Archive/Nexrad
  Archive/Archive2
  Product/Pipeline
  Product/History

inside a responsibility folder, split by type only when it improves
navigation:
  Models
  Options
  Policies
  Services
  Telemetry
  Validation
  Results
  Stores
  Mappers
  Runners
```

Slice 1 scope:

```text
physically move the domain rebalance code from the flat
src/Domain/Processing folder into:
  src/Domain/Processing/Rebalance/Models
  src/Domain/Processing/Rebalance/Options
  src/Domain/Processing/Rebalance/Policies
  src/Domain/Processing/Rebalance/Services
  src/Domain/Processing/Rebalance/Telemetry
  src/Domain/Processing/Rebalance/Validation
  src/Domain/Processing/Rebalance/Results

move matching rebalance tests into:
  tests/RadarPulse.Tests/Processing/Rebalance
```

Slice 2 scope:

```text
physically move the remaining domain processing code out of the flat
src/Domain/Processing folder into responsibility/type folders:
  src/Domain/Processing/Async
  src/Domain/Processing/Benchmarks
  src/Domain/Processing/Core
  src/Domain/Processing/Durable
  src/Domain/Processing/Handlers
  src/Domain/Processing/Pressure
  src/Domain/Processing/Queueing
  src/Domain/Processing/Retention
  src/Domain/Processing/Topology
  src/Domain/Processing/Workers

physically move the remaining processing tests out of the flat
tests/RadarPulse.Tests/Processing folder into responsibility folders:
  ArchiveRuntime
  Async
  Benchmarks
  Core
  Durable
  Handlers
  Pressure
  ProductPipeline
  Queueing
  ReadModels
  Retention
  Topology
  Workers
```

Slice 3 scope:

```text
physically move infrastructure processing code out of the flat
src/Infrastructure/Processing folder into responsibility/type folders:
  src/Infrastructure/Processing/ArchiveRuntime
  src/Infrastructure/Processing/Async
  src/Infrastructure/Processing/Benchmarks
  src/Infrastructure/Processing/Contracts
  src/Infrastructure/Processing/Core
  src/Infrastructure/Processing/Durable
  src/Infrastructure/Processing/ProductPipeline
  src/Infrastructure/Processing/Queueing
  src/Infrastructure/Processing/Retention
  src/Infrastructure/Processing/Runtime
  src/Infrastructure/Processing/Workers
```

Slice 4 scope:

```text
physically move application archive code out of the flat
src/Application/Archive folder into:
  src/Application/Archive/Contracts
  src/Application/Archive/Options
  src/Application/Archive/Services

physically move application processing code out of the flat
src/Application/Processing folder into:
  src/Application/Processing/Contracts
  src/Application/Processing/ReadModels
  src/Application/Processing/Services
```

Slice 5 scope:

```text
physically move archive code out of flat roots into responsibility folders:
  src/Infrastructure/Archive/Archive2
  src/Infrastructure/Archive/Compression
  src/Infrastructure/Archive/Contracts
  src/Infrastructure/Archive/Historical
  src/Infrastructure/Archive/Nexrad
  src/Domain/Archive/Archive2
  src/Domain/Archive/Benchmarks
  src/Domain/Archive/Historical
  src/Domain/Archive/Nexrad
  src/Domain/Archive/Publish

physically move archive tests out of the flat tests archive root into:
  tests/RadarPulse.Tests/Archive/Archive2
  tests/RadarPulse.Tests/Archive/Historical
  tests/RadarPulse.Tests/Archive/Integration
  tests/RadarPulse.Tests/Archive/Nexrad
```

Slice 6 scope:

```text
physically move product code out of flat roots into responsibility folders:
  src/Application/Product/Pipeline
  src/Application/Product/History
  src/Infrastructure/Product/Pipeline
  src/Infrastructure/Product/History

physically move product tests out of the flat tests product root into:
  tests/RadarPulse.Tests/Product/Pipeline
  tests/RadarPulse.Tests/Product/History
  tests/RadarPulse.Tests/Product/Http
```

Slice 1 verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  passed, 0 warnings, 0 errors

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "(FullyQualifiedName~Rebalance|FullyQualifiedName~Migration|FullyQualifiedName~StateHandoff|FullyQualifiedName~Evacuation|FullyQualifiedName~Relief)&FullyQualifiedName!~RadarProcessingSyntheticRebalance"
  passed: 212, failed: 0, skipped: 0

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarProcessingSyntheticRebalance"
  passed: 34, failed: 0, skipped: 0
```

Slice 2 verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  passed, 0 warnings, 0 errors

responsibility chunk gates, each run as a separate dotnet test process:
  ArchiveRuntime: 28 passed
  Async: 112 passed, 2 skipped
  Benchmarks: 9 passed
  Core: 59 passed
  Durable: 57 passed
  Handlers: 58 passed
  Pressure: 103 passed
  ProductPipeline: 31 passed
  Queueing: 119 passed
  ReadModels: 14 passed
  Retention: 57 passed
  Topology: 61 passed
  Workers: 79 passed
  Rebalance non-synthetic: 212 passed
  Rebalance synthetic: 34 passed
```

Slice 3 verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  passed, 0 warnings, 0 errors

responsibility chunk gates rerun after Infrastructure/Processing moves:
  ArchiveRuntime: 28 passed
  Async: 112 passed, 2 skipped
  Benchmarks: 9 passed
  Core: 59 passed
  Durable: 57 passed
  Handlers: 58 passed
  Pressure: 103 passed
  ProductPipeline: 31 passed
  Queueing: 119 passed
  ReadModels: 14 passed
  Retention: 57 passed
  Topology: 61 passed
  Workers: 79 passed
  Rebalance non-synthetic: 212 passed
  Rebalance synthetic: 34 passed
```

Slice 4 verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  passed, 0 warnings, 0 errors

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "FullyQualifiedName~HistoricalArchiveManifestSelector|FullyQualifiedName~RunReadModel|FullyQualifiedName~BffReadModelStore|FullyQualifiedName~HandlerOutputContract|FullyQualifiedName~HandlerDeltaBffCompatibility|FullyQualifiedName~HandlerDeltaClassification|FullyQualifiedName~MvpArchiveGate"
  passed: 27, failed: 0, skipped: 0
```

Slice 5 verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  passed, 0 warnings, 0 errors

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "FullyQualifiedName~Archive|FullyQualifiedName~Nexrad|FullyQualifiedName~Historical"
  passed: 160, failed: 0, skipped: 3
```

Slice 6 verification:

```text
dotnet build RadarPulse.sln -c Release --no-restore
  passed, 0 warnings, 0 errors

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "FullyQualifiedName~Product"
  passed: 86, failed: 0, skipped: 0
```

Notes:

```text
move slices intentionally preserve existing C# namespaces so this remains a
physical structure cleanup rather than a public API/using rewrite. Namespace
alignment can be handled as a later explicit slice if it is worth the added
blast radius. A broad combined Rebalance filter was also tried and exposed
the existing allocation benchmark sensitivity when the synthetic rebalance
allocation gate shares a process with 200+ other tests; the same synthetic
rebalance class passes when isolated. The full all-tests process also exposes
this existing allocation sensitivity, and one startup-prewarm test also passed
when rerun isolated after a full-suite process failure. Responsibility chunk
gates keep state-sensitive checks isolated while still covering the moved
processing tests.
```
