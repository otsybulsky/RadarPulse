# Core Workflows

Status: active under milestone 037.

This page explains RadarPulse by following the main flows through the system.
For vocabulary, see [glossary.md](glossary.md). For folder navigation, see
[source-map.md](source-map.md).

## Workflow 1: Local Product Demo

The local product demo is the easiest way to see the accepted system.

High-level flow:

```text
package script
  -> build/check local paths
  -> start RadarPulse.Http
  -> serve product API and built Operator UI from one local origin
  -> check demo readiness
  -> create deterministic demo run
  -> persist run history locally
  -> inspect run through CLI, HTTP, or Operator UI
```

Typical clean run:

```text
1. inspect paths
2. reset local demo history
3. start the product host
4. open http://127.0.0.1:5129
5. check demo readiness
6. create a deterministic demo run
7. inspect history and run detail
```

Entrypoints:

```text
Windows:
  scripts/radarpulse-product-demo.ps1

Linux/macOS/WSL2:
  scripts/radarpulse-product-demo.sh
```

Important paths:

```text
src/Presentation/RadarPulse.Http
  local product API and same-origin static delivery

src/Presentation/OperatorUi
  Angular operator experience

src/Presentation/RadarPulse.Cli
  product and benchmark CLI workflows

.tmp/product-demo/radarpulse-product-history.json
  default local product history file
```

The package script intentionally keeps readiness blockers visible. If history
cannot load, the UI build is missing, the host is unreachable, or product
scope warnings apply, those states should be visible rather than hidden.

## Workflow 2: Synthetic Product Run

Synthetic runs are the default demo path. They create archive-shaped batches
without reading from a live network or external storage.

Flow:

```text
HTTP or package command posts a synthetic run request
  -> Application product API validates and maps the request
  -> Infrastructure product pipeline service creates synthetic RadarEventBatch
     input
  -> product handler set is selected
  -> production-shaped processing pipeline runs the accepted runtime path
  -> processing output is mapped into product run detail
  -> run detail is stored in product history
  -> latest run, batches, sources, handler output, diagnostics, and capacity
     evidence become queryable
```

Primary product route:

```text
POST /product/pipeline/runs/demo
```

Primary source locations:

```text
src/Application/Product/Pipeline/Contracts
src/Application/Product/Pipeline/Models
src/Infrastructure/Product/Pipeline/Batching
src/Infrastructure/Product/Pipeline/Handlers
src/Infrastructure/Product/Pipeline/Services
src/Infrastructure/Processing/ProductPipeline
```

Typical focused tests:

```text
tests/RadarPulse.Tests/Product/Pipeline
tests/RadarPulse.Tests/Processing/ProductPipeline
```

## Workflow 3: Local Archive File Run

Archive file runs use one local NEXRAD archive file as input. This is still a
local archive-shaped workflow, not true live ingestion.

Flow:

```text
HTTP or product API receives archive file request
  -> local NEXRAD archive file is read and decompressed
  -> Archive II messages are projected into radar stream events
  -> radar events are grouped into RadarEventBatch input
  -> batches enter the same production-shaped processing pipeline as the
     synthetic path
  -> output is stored and queried as product run detail
```

Primary product route:

```text
POST /product/pipeline/runs/archive
```

Primary source locations:

```text
src/Application/Archive
src/Domain/Archive
src/Infrastructure/Archive
src/Infrastructure/Product/Pipeline/Archive
src/Infrastructure/Product/Pipeline/Services
```

Typical focused tests:

```text
tests/RadarPulse.Tests/Archive
tests/RadarPulse.Tests/Product/Pipeline
```

## Workflow 4: Archive Input To Radar Event Batch

RadarPulse separates archive reading from processing. Archive code turns file
content into streamable events and then into validated batches.

Flow:

```text
NEXRAD archive key/file/cache selection
  -> file download or local file access
  -> BZip2 decompression
  -> Archive II message scan
  -> gate moment event projection
  -> radar stream identity/source mapping
  -> RadarEventBatch builder
  -> batch validation and metrics
  -> batch publisher or product capture
```

Why this matters:

```text
archive concerns:
  file format, decompression, cache paths, download/preflight, message scan

streaming concerns:
  source identity, event shape, payload metrics, batch validity

processing concerns:
  accepted queue/runtime input, ordering, handler output, diagnostics
```

Primary source locations:

```text
src/Infrastructure/Archive/Archive2
src/Infrastructure/Archive/Compression
src/Infrastructure/Archive/Historical
src/Infrastructure/Archive/Nexrad
src/Domain/Streaming
src/Application/Archive
```

## Workflow 5: Queued Processing And Ordered Commit

The processing runtime may compute multiple batches concurrently, but output
must become externally visible in provider sequence order.

Flow:

```text
RadarEventBatch input
  -> accepted into owned queue
  -> retained payload resources are owned while work is pending/active
  -> workers compute processing deltas or handler deltas
  -> completions may arrive out of order
  -> ordered coordinator publishes only the next provider sequence
  -> retained resources are released
  -> telemetry and diagnostics are updated
```

The key rule:

```text
worker completion order is not product output order
provider sequence is product output order
```

Primary source locations:

```text
src/Domain/Processing/Queueing
src/Domain/Processing/Core
src/Domain/Processing/Handlers
src/Domain/Processing/Retention
src/Infrastructure/Processing/Queueing
src/Infrastructure/Processing/Async
src/Infrastructure/Processing/Workers
```

Typical focused tests:

```text
tests/RadarPulse.Tests/Processing/Queueing
tests/RadarPulse.Tests/Processing/Core
tests/RadarPulse.Tests/Processing/Async
tests/RadarPulse.Tests/Processing/Workers
```

## Workflow 6: Handler Output And Product Read Models

Handlers compute source-level output that the product surface can display.

Flow:

```text
handler set selected for a run
  -> runtime classifies handler posture
  -> handler-free path uses accepted ordered concurrent processing
  -> mergeable handlers compute per-batch handler deltas concurrently
  -> handler deltas merge by provider sequence
  -> snapshot-only handlers use sequential fallback
  -> unsupported handlers fail closed
  -> committed handler output is projected into read models
  -> product API exposes source and handler field output
```

Product query routes:

```text
GET /product/pipeline/runs/{runId}/sources
GET /product/pipeline/runs/{runId}/sources/{sourceId}
GET /product/pipeline/runs/{runId}/handlers/{sourceId}/{fieldName}
```

Primary source locations:

```text
src/Domain/Processing/Handlers
src/Application/Processing/ReadModels
src/Application/Product/Pipeline/Models
src/Infrastructure/Processing/ProductPipeline
src/Infrastructure/Product/Pipeline/Handlers
```

Typical focused tests:

```text
tests/RadarPulse.Tests/Processing/Handlers
tests/RadarPulse.Tests/Processing/ReadModels
tests/RadarPulse.Tests/Product/Pipeline
```

## Workflow 7: Diagnostics, Readiness, And Capacity Evidence

Diagnostics explain whether output is ready and why a run is blocked,
warning-only, failed, or complete.

Flow:

```text
runtime/session/product pipeline captures status
  -> validation, release, retained pressure, worker health, handler posture,
     capacity, and first blocking reason are summarized
  -> product read models preserve the diagnostic meaning
  -> API, CLI, and UI expose the state
```

Important product routes:

```text
GET /product/pipeline/host/readiness
GET /product/pipeline/host/demo-readiness
GET /product/pipeline/runs/{runId}/diagnostics
GET /product/pipeline/runs/{runId}/capacity
GET /product/pipeline/runs
GET /product/pipeline/runs/latest
GET /product/pipeline/runs/{runId}
GET /product/pipeline/runs/{runId}/batches
GET /product/pipeline/runs/{runId}/batches/{providerSequence}
```

Diagnostics should preserve:

```text
processing completeness
processing validation failures
provider sequence and batch status
handler mode and fallback posture
first blocking reason
retained payload pressure
release cleanup health
worker failures
capacity evidence
history readiness
demo readiness
```

Primary source locations:

```text
src/Application/Processing/ReadModels
src/Application/Product/Pipeline/Models
src/Infrastructure/Processing/ProductPipeline
src/Infrastructure/Processing/ArchiveRuntime
src/Presentation/RadarPulse.Http/Product
src/Presentation/OperatorUi/src/app/product
```

## Workflow 8: Product Controls And Recovery

RadarPulse exposes local product controls for recoverable durable pipeline
state. These controls are part of the local diagnostic/demo surface, not a
production operations plane.

Control routes:

```text
POST /product/pipeline/controls/stop-accepting
POST /product/pipeline/controls/drain-accepted
POST /product/pipeline/controls/cancel-open-release
POST /product/pipeline/controls/reject-unsafe-fallback
```

Flow:

```text
control request names a run and durable store path
  -> HTTP route fixes the control action from the route
  -> Application product API validates product input
  -> product control coordinator applies the recoverable action
  -> control summary reports canceled, drained, released, or rejected work
  -> operator summary remains visible through product output
```

Primary source locations:

```text
src/Application/Product/Pipeline/Contracts
src/Application/Product/Pipeline/Models
src/Infrastructure/Processing/ProductPipeline
src/Infrastructure/Processing/Durable
src/Presentation/RadarPulse.Http/Product/Endpoints
```

Typical focused tests:

```text
tests/RadarPulse.Tests/Product/Pipeline
tests/RadarPulse.Tests/Product/Http
tests/RadarPulse.Tests/Processing/Durable
tests/RadarPulse.Tests/Processing/ProductPipeline
```

## Workflow Selection Guide

Use this table when deciding where to inspect or change behavior:

| If you need to understand... | Start with... |
| --- | --- |
| How to run the local demo | Workflow 1, `README.md`, `docs/product-demo-readiness.md` |
| How synthetic demo input enters processing | Workflow 2 |
| How one archive file becomes product output | Workflow 3 and Workflow 4 |
| Why output ordering is deterministic | Workflow 5 |
| Why a handler uses fast path or fallback | Workflow 6 |
| Why a run is blocked or warning-only | Workflow 7 |
| How local recovery controls work | Workflow 8 |

## Evidence Links

Current workflow evidence lives in:

```text
README.md
docs/product-demo-readiness.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-closeout.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-closeout.md
docs/milestones/026-persistent-durable-adapter-readiness-closeout.md
docs/milestones/027-production-pipeline-integration-closeout.md
docs/milestones/028-product-facing-pipeline-console-and-api-closeout.md
docs/milestones/029-product-http-host-and-persistent-run-history-closeout.md
docs/milestones/030-product-operator-angular-spa-closeout.md
docs/milestones/031-operator-ui-hardening-and-integrated-local-delivery-closeout.md
docs/milestones/032-product-demo-readiness-packaging-closeout.md
docs/milestones/033-product-demo-polish-and-portfolio-readiness-closeout.md
```
