# Product Surface

Status: active under milestone 037.

This page explains the user-facing and product-facing surfaces of RadarPulse:
package scripts, product API, CLI, HTTP host, local run history, and Angular
Operator UI. For runtime internals, see [processing-runtime.md](processing-runtime.md).

## Product Boundary

The product surface is scoped to the accepted local demo/runtime boundary:

```text
deterministic synthetic demo runs
local archive-file product runs
local product API
local same-origin Operator UI delivery
local file-backed run history
readiness, diagnostics, capacity, handler output, and control inspection
focused local verification gates
```

It does not claim:

```text
true live radar network ingestion
public production deployment
authentication or authorization
TLS termination
production CORS hardening
database-backed product history
external broker/cloud queue/database adapter certification
deployment automation, autoscaling, alert routing, or runbooks
exactly-once production delivery
```

## Package Scripts

The package scripts are the main local operator entrypoint.

```text
scripts/radarpulse-product-demo.ps1
  Windows PowerShell and optional PowerShell 7 entrypoint

scripts/radarpulse-product-demo.sh
  native Linux/macOS/WSL2 Bash entrypoint
```

Commands:

```text
help:
  command discovery, typical first-run order, docs, and scope boundary

paths:
  resolved repository, Operator UI, HTTP host, demo workspace, history, and
  URL paths

start:
  optional Angular build, environment configuration, and RadarPulse.Http
  startup

readiness:
  GET /product/pipeline/host/demo-readiness

demo:
  POST /product/pipeline/runs/demo

history:
  GET /product/pipeline/host/readiness
  GET /product/pipeline/runs

reset-history:
  removes only the resolved local demo history file inside .tmp/product-demo

verify:
  runs the accepted local package verification chain
```

Default local URL:

```text
http://127.0.0.1:5129
```

Default local history path:

```text
.tmp/product-demo/radarpulse-product-history.json
```

## Product HTTP API

The HTTP product surface is a same-origin local adapter over the
Application-owned product API contract.

Source:

```text
src/Presentation/RadarPulse.Http/Product
src/Application/Product/Pipeline/Contracts
src/Application/Product/Pipeline/Models
```

Run routes:

```text
POST /product/pipeline/runs/demo
POST /product/pipeline/runs/archive
GET /product/pipeline/runs
GET /product/pipeline/runs/latest
GET /product/pipeline/runs/{runId}
```

Run detail routes:

```text
GET /product/pipeline/runs/{runId}/batches
GET /product/pipeline/runs/{runId}/batches/{providerSequence}
GET /product/pipeline/runs/{runId}/sources
GET /product/pipeline/runs/{runId}/sources/{sourceId}
GET /product/pipeline/runs/{runId}/handlers/{sourceId}/{fieldName}
GET /product/pipeline/runs/{runId}/diagnostics
GET /product/pipeline/runs/{runId}/capacity
```

Host/readiness routes:

```text
GET /product/pipeline/host/readiness
GET /product/pipeline/host/demo-readiness
```

Control routes:

```text
POST /product/pipeline/controls/stop-accepting
POST /product/pipeline/controls/drain-accepted
POST /product/pipeline/controls/cancel-open-release
POST /product/pipeline/controls/reject-unsafe-fallback
```

Response shape:

```text
statusCode
isSuccess
body
message
```

The HTTP adapter maps that product API envelope to JSON and uses the envelope
status code as the HTTP status code.

## Product API Contract

Application owns the product API and focused use-case ports:

```text
IRadarPulseProductPipelineApi
IRadarPulseProductPipelineRunService
IRadarPulseProductPipelineQueryService
IRadarPulseProductPipelineHistoryService
IRadarPulseProductPipelineControlService
RadarPulseProductPipelineApiContract
```

Infrastructure implements the service behavior:

```text
src/Infrastructure/Product/Pipeline
src/Infrastructure/Product/History
src/Infrastructure/Processing/ProductPipeline
```

Presentation should consume Application contracts, not concrete
Infrastructure product API classes. Architecture tests guard that boundary.

## Local Run History

The product history store makes demo runs visible across local host
recreation.

Accepted local posture:

```text
file-backed history by default in the package script
in-memory history available for hosted smoke and tests
history readiness exposed through product API
reset-history constrained to the demo workspace
no database-backed production history claim
```

Sources:

```text
src/Application/Product/History
src/Infrastructure/Product/History
tests/RadarPulse.Tests/Product/History
```

## Operator UI

The Operator UI is an Angular app for local inspection.

Source:

```text
src/Presentation/OperatorUi
```

It supports:

```text
host/history readiness
deterministic demo run creation
archive-shaped run creation from a valid local archive path
persisted run list and latest run
selected run summary
batch, source, handler, diagnostics, and capacity inspection
handler output lookup by source id and field name
stop accepting, drain accepted, cancel/release, and reject unsafe fallback
  controls
unreachable host, blocked history, not-found, rejected control, and
  validation/failure posture
```

The UI has two local operating modes:

```text
Angular dev server:
  npm start
  default UI URL: http://localhost:4200
  default product API base URL: http://localhost:5000

same-origin hosted:
  npm run build
  RadarPulse.Http serves the built bundle and product API from the same local
  origin
```

The same-origin package workflow is the accepted portfolio/demo path.

## HTTP Host Configuration

The package script sets these local host options when starting
`RadarPulse.Http`:

```text
RadarPulse__ProductHttp__HistoryPath
RadarPulse__ProductHttp__UseInMemoryHistory=false
RadarPulse__ProductHttp__EnableOperatorUiStaticFiles=true
RadarPulse__ProductHttp__OperatorUiStaticAssetPath
```

The Angular static asset root points to:

```text
src/Presentation/OperatorUi/dist/OperatorUi/browser
```

`RadarPulse.Http` does not build the frontend at runtime. The package script
or UI workflow builds the Angular bundle before hosted static delivery.

## Product CLI

The product CLI workflow lives under:

```text
src/Presentation/RadarPulse.Cli/Product
src/Presentation/RadarPulse.Cli/EntryPoint
```

The top-level CLI entrypoint is intentionally thin. Command-family logic is
split into focused collaborators after milestone 036.

Use CLI tests for product/benchmark command behavior:

```text
tests/RadarPulse.Tests/Presentation/Cli
```

## What A Product Run Contains

A product run detail can expose:

```text
run id
run state and readiness
configuration/options summary
handler mode
batch list and batch details
source summaries
handler output values
diagnostics
capacity/completeness evidence
operator summary
history metadata
control summaries when applicable
```

This is deliberately product vocabulary. Lower-level queue/session internals
should be summarized or linked through diagnostics, not exposed as raw
implementation state.

## Product Surface Evidence

Use these documents for accepted evidence:

```text
docs/product-demo-readiness.md
docs/milestones/028-product-facing-pipeline-console-and-api-closeout.md
docs/milestones/029-product-http-host-and-persistent-run-history-closeout.md
docs/milestones/030-product-operator-angular-spa-closeout.md
docs/milestones/031-operator-ui-hardening-and-integrated-local-delivery-closeout.md
docs/milestones/032-product-demo-readiness-packaging-closeout.md
docs/milestones/033-product-demo-polish-and-portfolio-readiness-closeout.md
docs/milestones/036-clean-architecture-hardening-closeout.md
```
