# RadarPulse Product Demo/Readiness Package

This document describes the local product demo/readiness workflow accepted by
milestone 032.

The package is intentionally local:

```text
covered:
  deterministic demo/archive-shaped product workflows
  local RadarPulse.Http product API
  built Angular operator UI served from the same local origin
  deterministic local file-backed product run history
  readiness inspection, demo run, history inspection/reset, and focused gates

not claimed:
  true live radar network ingestion
  public production deployment
  authentication or authorization
  TLS termination
  production CORS hardening
  external broker/cloud queue/database adapter certification
  deployment automation, autoscaling, alert routing, or runbooks
  cross-machine throughput certification
  exactly-once end-to-end production delivery
```

## Prerequisites

Use the same local prerequisites as the existing product surfaces:

```text
.NET SDK for the solution target framework
Node/npm dependencies for src/Presentation/OperatorUi
Bash and curl for Linux/macOS/WSL2 package commands
```

Install UI dependencies when needed:

```sh
cd src/Presentation/OperatorUi
npm install
```

Return to the repository root before using the package script:

```sh
cd ../../..
```

Package entrypoints:

```text
Windows:
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 help

Linux/macOS/WSL2:
  bash scripts/radarpulse-product-demo.sh help

PowerShell 7 optional:
  pwsh -File scripts/radarpulse-product-demo.ps1 help
```

The Linux/macOS/WSL2 path is native Bash and does not require PowerShell.

## Happy-Path Portfolio Demo

Use this sequence for a clean local portfolio demo:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 paths
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 reset-history
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 start
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh paths
bash scripts/radarpulse-product-demo.sh reset-history
bash scripts/radarpulse-product-demo.sh start
```

Open the same-origin operator UI after the host starts:

```text
http://127.0.0.1:5129
```

In another terminal, check readiness and create the default deterministic
demo run:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 readiness
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 demo -RunId product-demo
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 history
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh readiness
bash scripts/radarpulse-product-demo.sh demo --run-id product-demo
bash scripts/radarpulse-product-demo.sh history
```

Use the UI to inspect the latest run, selected run detail, batches, sources,
handler output, diagnostics, and capacity evidence. For a clean portfolio
walkthrough, reset history before the demo. To show persistence across host
restart, skip reset and use the `history` command before opening the UI.

Run the packaged verification command when you want to prove the accepted
local gates after changes:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 verify
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh verify
```

The happy path is intentionally narrow. Blocked readiness, missing UI build
output, unreachable host, rejected product controls, and warning-only scope
posture should remain visible instead of being hidden by the package script.

## Command Surface

The local package entrypoint is:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 help
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh help
```

Inspect resolved package paths:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 paths
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh paths
```

Default resolved local paths:

```text
operator UI project:
  src/Presentation/OperatorUi

operator UI dist:
  src/Presentation/OperatorUi/dist/OperatorUi/browser

product HTTP host:
  src/Presentation/RadarPulse.Http/RadarPulse.Http.csproj

demo workspace:
  .tmp/product-demo

product history:
  .tmp/product-demo/radarpulse-product-history.json

local product URL:
  http://127.0.0.1:5129
```

Source navigation after the milestone 034 restructuring:

```text
product API/application models:
  src/Application/Product/Pipeline
  src/Application/Product/History

product pipeline/history implementations:
  src/Infrastructure/Product/Pipeline
  src/Infrastructure/Product/History

local HTTP host adapter:
  src/Presentation/RadarPulse.Http/Hosting
  src/Presentation/RadarPulse.Http/Product

product-focused tests:
  tests/RadarPulse.Tests/Product/Pipeline
  tests/RadarPulse.Tests/Product/History
  tests/RadarPulse.Tests/Product/Http
```

## Start Local Product Host

Start the local same-origin product host:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 start
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh start
```

The start command:

```text
builds the Angular operator UI unless -SkipUiBuild / --skip-ui-build is
  supplied
starts RadarPulse.Http on the configured local URL
configures deterministic local file-backed history
configures the built Angular UI dist folder as the static asset root
serves the product API and operator UI from the same local origin
```

Open the UI:

```text
http://127.0.0.1:5129
```

Use a different URL when needed:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 start -Url http://127.0.0.1:5130
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh start --url http://127.0.0.1:5130
```

## Check Readiness

In another terminal, check local product demo readiness:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 readiness
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh readiness
```

The readiness command calls:

```text
GET /product/pipeline/host/demo-readiness
```

Readiness includes:

```text
product API route posture
history storage kind, identity, load status, and first blocker
operator UI static asset root posture
local package warnings and non-claims
```

Blocked readiness means the local package cannot run the accepted demo path
as configured. Warning-only posture preserves the accepted milestone scope
warnings and does not mean the local deterministic package is broken.

## Run Deterministic Demo

Create a deterministic product demo run:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 demo -RunId product-demo
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh demo --run-id product-demo
```

Useful options:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 demo `
  -RunId product-demo-small `
  -Sources 2 `
  -Batches 2 `
  -EventsPerBatch 2 `
  -Handlers counter-checksum
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh demo \
  --run-id product-demo-small \
  --sources 2 \
  --batches 2 \
  --events-per-batch 2 \
  --handlers counter-checksum
```

Handler profiles accepted by the script:

```text
none
counter-checksum
counter-checksum-heavy
snapshot-counting
```

The demo command calls the accepted product route:

```text
POST /product/pipeline/runs/demo
```

It does not create true live radar ingestion. It packages the deterministic
product demo workflow already accepted by the product API.

## Inspect Results

Use the browser UI to inspect:

```text
host/history readiness
latest run and persisted run list
selected run detail
batches and sources
handler output
diagnostics
capacity evidence
control posture
```

A useful handler lookup for the default `counter-checksum` demo is:

```text
source id:
  0

field:
  benchmark.events
```

Inspect history from the command line:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 history
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh history
```

The history command calls:

```text
GET /product/pipeline/host/readiness
GET /product/pipeline/runs
```

## Visual Checkpoints

For a portfolio walkthrough, these UI states are the useful checkpoints:

```text
demo readiness:
  the top overview shows demo readiness, product history storage, loaded run
  count, rejected run count, and any first blocker

latest and persisted runs:
  the latest run panel and persisted run list show the run id, state, batch
  counts, source counts, and warning count

selected run summary:
  the selected run view shows input description, handler mode, processing
  progress, fallback posture, warnings, and first blocking reason

batches and sources:
  the Batches and Sources tabs show provider sequence, processing status,
  event counts, payload bytes, source identity, and checksums

handler output:
  the Handlers tab can load the default counter/checksum output with source
  id 0 and field benchmark.events

diagnostics and capacity:
  the Diagnostics and Capacity tabs show readiness, ordered handler posture,
  release/retained-resource counters, local capacity evidence, and
  configuration contour

controls:
  the controls panel shows whether local product controls are idle, accepted,
  blocked, or rejected for the selected run
```

These checkpoints are descriptive review targets. They do not add production
deployment, security, live-ingestion, external-adapter, or exactly-once
delivery claims.

## Reset Local Demo History

Reset the default local demo history:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 reset-history
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh reset-history
```

The reset command is intentionally narrow:

```text
it only removes the resolved product history file
the default reset path must stay inside .tmp/product-demo
custom history paths are rejected before deletion when they resolve outside
  the demo workspace
directory targets are rejected instead of removed
normal startup never clears history implicitly
```

Use reset before a clean demo. Skip reset when you want to show persistence
across host restarts.

## Packaged Verification

Run the packaged milestone 032 verification command from the repository root:

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 verify
```

Linux/macOS/WSL2:

```sh
bash scripts/radarpulse-product-demo.sh verify
```

The verify command runs:

```text
Angular unit tests
Angular production build
Operator UI browser smoke
hosted same-origin browser smoke
.NET dependency restore
focused .NET product HTTP/API/readiness Release gate
.NET Release build
```

It stops at the first failed step and leaves each underlying command visible
for diagnosis. The .NET restore step uses `--force` so the same checkout can
switch between Windows and WSL/Linux restore metadata before the subsequent
`--no-restore` gates.

## Manual Verification

The accepted individual gates remain directly runnable:

```sh
cd src/Presentation/OperatorUi
npm test -- --watch=false
npm run build
npm run smoke
npm run smoke:hosted
cd ../../..
```

Focused .NET product HTTP/API/readiness gate:

```sh
dotnet restore RadarPulse.sln --force
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release \
  --no-restore \
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
```

Release build:

```sh
dotnet build RadarPulse.sln -c Release --no-restore
```

## Troubleshooting

If readiness says the operator UI is blocked:

```text
run npm run build in src/Presentation/OperatorUi
or start through the package script without -SkipUiBuild / --skip-ui-build
```

If readiness says history is blocked:

```text
check the configured history path
reset the default demo history when using .tmp/product-demo
avoid pointing HistoryPath / --history-path at a directory
```

If the UI opens but API calls fail:

```text
make sure the browser is opened from the RadarPulse.Http URL
use http://127.0.0.1:5129 by default
check /product/pipeline/host/demo-readiness
```

If a product demo run is rejected:

```text
read the product API response message
keep Sources, Batches, and EventsPerBatch greater than zero
use one of the supported handler profiles
```
