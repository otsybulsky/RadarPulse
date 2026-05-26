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
```

Install UI dependencies when needed:

```powershell
cd src\Presentation\OperatorUi
npm install
```

Return to the repository root before using the package script:

```powershell
cd C:\projects\ProjectT\Portfolio\Projects\RadarPulse
```

## Command Surface

The local package entrypoint is:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 help
```

Inspect resolved package paths:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 paths
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

## Start Local Product Host

Start the local same-origin product host:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 start
```

The start command:

```text
builds the Angular operator UI unless -SkipUiBuild is supplied
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

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 start -Url http://127.0.0.1:5130
```

## Check Readiness

In another terminal, check local product demo readiness:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 readiness
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

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 demo -RunId product-demo
```

Useful options:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 demo `
  -RunId product-demo-small `
  -Sources 2 `
  -Batches 2 `
  -EventsPerBatch 2 `
  -Handlers counter-checksum
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

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 history
```

The history command calls:

```text
GET /product/pipeline/host/readiness
GET /product/pipeline/runs
```

## Reset Local Demo History

Reset the default local demo history:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 reset-history
```

The reset command is intentionally narrow:

```text
it only removes the resolved product history file
the default reset path must stay inside .tmp/product-demo
normal startup never clears history implicitly
```

Use reset before a clean demo. Skip reset when you want to show persistence
across host restarts.

## Packaged Verification

Run the packaged milestone 032 verification command from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 verify
```

The verify command runs:

```text
Angular unit tests
Angular production build
Operator UI browser smoke
hosted same-origin browser smoke
focused .NET product HTTP/API/readiness Release gate
.NET Release build
```

It stops at the first failed step and leaves each underlying command visible
for diagnosis.

## Manual Verification

The accepted individual gates remain directly runnable:

```powershell
cd src\Presentation\OperatorUi
npm test -- --watch=false
npm run build
npm run smoke
npm run smoke:hosted
cd ..\..\..
```

Focused .NET product HTTP/API/readiness gate:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release `
  --no-restore `
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
```

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

## Troubleshooting

If readiness says the operator UI is blocked:

```text
run npm run build in src/Presentation/OperatorUi
or start through the package script without -SkipUiBuild
```

If readiness says history is blocked:

```text
check the configured history path
reset the default demo history when using .tmp/product-demo
avoid pointing HistoryPath at a directory
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
