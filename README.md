# RadarPulse

RadarPulse is a local product demo for a radar-processing pipeline. It shows
how deterministic archive-shaped workloads move through the accepted product
API, local HTTP host, file-backed product history, and Angular operator UI.

The repository is intentionally set up as a repeatable local portfolio demo:
one script can build the UI, start the same-origin product host, run a
deterministic demo workload, inspect readiness/history, reset local demo
history, and run the focused verification gates.

## What The Demo Shows

```text
product host:
  src/Presentation/RadarPulse.Http serves product API routes and the built
  Angular operator UI from one local origin

operator UI:
  src/Presentation/OperatorUi inspects readiness, runs, batches, sources,
  handler output, diagnostics, capacity evidence, and controls

product CLI:
  src/Presentation/RadarPulse.Cli provides product-facing command workflows

history:
  deterministic local file-backed product run history survives host
  recreation during the local demo

workflow:
  deterministic demo/archive-shaped workloads exercise the accepted product
  pipeline without requiring a live radar network feed
```

The detailed local operator workflow is documented in
[docs/product-demo-readiness.md](docs/product-demo-readiness.md).

## Quick Start

Install UI dependencies when needed:

```powershell
cd src\Presentation\OperatorUi
npm install
cd ..\..\..
```

Inspect the package paths:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 paths
```

Reset the default local demo history for a clean run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 reset-history
```

Start the local same-origin product host:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 start
```

Open the operator UI:

```text
http://127.0.0.1:5129
```

In another terminal, check readiness and create a deterministic demo run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 readiness
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 demo -RunId product-demo
```

Use the browser UI to inspect the latest run, selected run detail, batches,
sources, handler output, diagnostics, and capacity evidence.

## Verify

Run the packaged local verification command:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 verify
```

The packaged command runs the accepted Angular, browser smoke, focused .NET
HTTP/API/readiness, and Release build gates. Each underlying command remains
visible for diagnosis.

## Useful Commands

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 help
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 paths
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 readiness
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 demo -RunId product-demo
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 history
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 reset-history
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 verify
```

## Scope

RadarPulse currently demonstrates deterministic local product workflows over
archive-shaped inputs. The local demo/readiness package does not claim:

```text
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

Those boundaries are deliberate. The portfolio demo focuses on the accepted
local product surface, operator workflow, deterministic history, diagnostics,
and verification gates.
