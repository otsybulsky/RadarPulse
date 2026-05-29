# Verification And Evidence

Status: active under milestone 037.

This page explains how to choose verification for RadarPulse changes and
where to find accepted evidence. It is a guide for maintainers, not a
replacement for milestone gate files, closeouts, or decision traces.

## Verification Principle

Use the smallest gate that proves the touched behavior, then broaden when the
change crosses boundaries.

```text
documentation-only:
  markdown/path/whitespace checks

single module behavior:
  focused tests for that module

cross-layer contract:
  architecture tests plus focused behavior tests

product HTTP/API/UI/package behavior:
  product focused gate, Angular gates, browser smoke, or package verify

runtime ordering/durable/handler semantics:
  focused processing gates, Release build, and full suite when shared
  ordering or product-visible output changes
```

## Documentation-Only Gate

Use this for handbook or milestone text changes that do not touch code,
scripts, UI, product behavior, or executable contracts:

```text
git diff --check
```

Add a path/link spot-check when adding source or evidence references:

```text
Test-Path <referenced path>
```

No runtime test is normally needed for documentation-only changes.

## Architecture Gate

Use when project references, layer ownership, Application product contracts,
HTTP endpoint dependencies, Domain friend access, or CLI entrypoint shape
change:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Architecture" -c Release --no-restore
```

Architecture evidence source:

```text
tests/RadarPulse.Tests/Architecture
docs/milestones/036-clean-architecture-hardening-closeout.md
docs/milestones/036-clean-architecture-hardening-decision-trace.md
```

## Product API And HTTP Gate

Use when product API contracts, product DTOs, HTTP endpoints, run history, or
readiness behavior changes:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
```

Broaden to all product tests when product service or history behavior changes:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Product" -c Release --no-restore
```

Evidence source:

```text
tests/RadarPulse.Tests/Product
tests/RadarPulse.Tests/Presentation/Cli/Product
docs/milestones/028-product-facing-pipeline-console-and-api-closeout.md
docs/milestones/029-product-http-host-and-persistent-run-history-closeout.md
```

## Operator UI Gate

Use when Angular UI behavior, product API client/state, visual workflow, or
browser delivery behavior changes:

```text
cd src/Presentation/OperatorUi
npm test -- --watch=false
npm run build
npm run smoke
```

Use hosted smoke when same-origin `RadarPulse.Http` delivery changes:

```text
cd src/Presentation/OperatorUi
npm run smoke:hosted
```

Evidence source:

```text
src/Presentation/OperatorUi/src/app
src/Presentation/OperatorUi/smoke
src/Presentation/OperatorUi/README.md
docs/milestones/030-product-operator-angular-spa-closeout.md
docs/milestones/031-operator-ui-hardening-and-integrated-local-delivery-closeout.md
```

## Package Verify Gate

Use when scripts, local package behavior, same-origin delivery, product
readiness, HTTP/API readiness, or local demo workflow changes.

Windows:

```text
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 verify
```

Linux/macOS/WSL2:

```text
bash scripts/radarpulse-product-demo.sh verify
```

The package verify chain runs:

```text
Angular unit tests
Angular production build
Operator UI browser smoke
hosted same-origin browser smoke
.NET dependency restore with --force
focused .NET product HTTP/API/readiness Release gate
.NET Release build
```

Evidence source:

```text
docs/product-demo-readiness.md
docs/milestones/032-product-demo-readiness-packaging-gate.md
docs/milestones/032-product-demo-readiness-packaging-closeout.md
docs/milestones/033-product-demo-polish-and-portfolio-readiness-closeout.md
```

## Processing Runtime Gates

Use focused processing gates for bounded runtime areas.

Queueing:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Queueing" -c Release --no-restore
```

Durable:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Durable" -c Release --no-restore
```

Handlers:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Handler" -c Release --no-restore
```

Rebalance:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Rebalance" -c Release --no-restore
```

Core/async/workers:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Core|FullyQualifiedName~Async|FullyQualifiedName~Worker" -c Release --no-restore
```

Runtime/product pipeline:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~ProductPipeline|FullyQualifiedName~ArchiveRuntime" -c Release --no-restore
```

Broaden to full suite when changing:

```text
provider sequence ordering
ordered commit
durable lifecycle
handler delta/merge semantics
retained resource release behavior
shared processing contracts
product-visible runtime output
architecture guardrails
```

## Archive And Streaming Gates

Archive:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Archive|FullyQualifiedName~Nexrad|FullyQualifiedName~Historical" -c Release --no-restore
```

Streaming:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Streaming|FullyQualifiedName~RadarStream|FullyQualifiedName~DenseIdentity|FullyQualifiedName~RadarSourceUniverse|FullyQualifiedName~RadarEventBatch" -c Release --no-restore
```

Use these when archive file parsing, NEXRAD cache/download behavior,
decompression, batch publishing, source identity, or batch validation
changes.

## Build And Full Suite

Release build:

```text
dotnet build RadarPulse.sln -c Release --no-restore
```

Full Release test suite:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release --no-restore --no-build
```

The latest recorded milestone 036 full Release suite result was:

```text
1016 passed, 0 failed, 3 skipped
```

Use the full suite before closeout for broad architecture, runtime, product,
or verification posture claims.

## Evidence Map

Historical archive and replay evidence:

```text
docs/milestones/001-historical-loader-closeout.md
docs/milestones/002-nexrad-archive-inspection-closeout.md
docs/milestones/003-historical-replay-publisher-closeout.md
```

Processing/runtime foundation evidence:

```text
docs/milestones/020-default-baseline-runtime-archive-integration-closeout.md
docs/milestones/021-ordered-concurrent-runtime-archive-processing-closeout.md
docs/milestones/022-ordered-rebalance-topology-commit-closeout.md
docs/milestones/023-durable-cross-process-runtime-readiness-closeout.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-closeout.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-closeout.md
docs/milestones/026-persistent-durable-adapter-readiness-closeout.md
```

Product/demo evidence:

```text
docs/milestones/027-production-pipeline-integration-closeout.md
docs/milestones/028-product-facing-pipeline-console-and-api-closeout.md
docs/milestones/029-product-http-host-and-persistent-run-history-closeout.md
docs/milestones/030-product-operator-angular-spa-closeout.md
docs/milestones/031-operator-ui-hardening-and-integrated-local-delivery-closeout.md
docs/milestones/032-product-demo-readiness-packaging-closeout.md
docs/milestones/033-product-demo-polish-and-portfolio-readiness-closeout.md
```

Maintenance, documentation, and architecture evidence:

```text
docs/milestones/034-targeted-project-restructuring-and-maintenance-closeout.md
docs/milestones/035-code-contract-documentation-pass-closeout.md
docs/milestones/036-clean-architecture-hardening-closeout.md
docs/milestones/036-clean-architecture-hardening-decision-trace.md
docs/milestones/036-clean-architecture-hardening-performance-evidence.md
```

Current project status:

```text
docs/project-progress.md
docs/handoff.md
```

## Evidence Interpretation Rules

When reading evidence:

```text
prefer closeout documents for accepted final posture
use decision traces for why decisions were accepted
use gate files for command output and pre-closeout evidence
respect scoped warnings and non-claims
do not turn local deterministic gates into production deployment claims
do not treat performance matrices as cross-machine certification unless a
future milestone explicitly proves that
```

When writing new evidence:

```text
record command, configuration, result, and scope
separate local proof from production claims
call out skipped tests or known caveats
link raw performance logs when performance claims matter
update the relevant handbook page if the accepted current interpretation
changes
```
