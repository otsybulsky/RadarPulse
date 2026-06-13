# RadarPulse

[![Tests](https://github.com/otsybulsky/RadarPulse/actions/workflows/dotnet.yml/badge.svg?branch=master)](https://github.com/otsybulsky/RadarPulse/actions/workflows/dotnet.yml)

RadarPulse is a local product demo for a radar-processing pipeline. It shows
how deterministic archive-shaped workloads move through the accepted product
API, local HTTP host, file-backed product history, and Angular operator UI.

The repository is intentionally set up as a repeatable local portfolio demo
for Windows, Linux, and macOS: the package script can build the UI, start the
same-origin product host, run a deterministic demo workload, inspect
readiness/history, reset local demo history, and run the focused verification
gates.

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

## System Handbook

The current-system handbook is in
[docs/handbook/README.md](docs/handbook/README.md). Start there when you
want a guided explanation of RadarPulse from purpose and mental model through
source navigation, workflows, architecture, processing runtime, product
surface, verification, and glossary terms.

Milestone documents remain the historical decision and evidence record. The
handbook explains the accepted current system directly and links back to
milestone closeouts, decision traces, and gates where the evidence matters.

## Book Website

The book can be published as a VitePress site. The current published source is
`docs/handbook/book-outline.md` plus `docs/handbook/book`. Local commands:

```sh
npm install
npm run docs:dev
npm run docs:build
```

The GitHub Actions workflow in `.github/workflows/deploy-vitepress.yml` builds
the Ukrainian book site and publishes `docs/.vitepress/dist` to GitHub Pages for
the `master` branch. The project Pages URL is:

```text
https://otsybulsky.github.io/RadarPulse/
```

## Repository Layout

Current C# source folders are organized responsibility-first inside the
existing project/layer boundaries:

```text
src/Domain
  Archive, Processing, Streaming
src/Application
  Archive, Processing, Product
src/Infrastructure
  Archive, Processing, Product
src/Presentation
  RadarPulse.Cli, RadarPulse.Http, OperatorUi
tests/RadarPulse.Tests
  Archive, Processing, Product, Streaming, Presentation
```

Inside those areas, files are grouped by capability and then by type where it
helps navigation, for example `Processing/Rebalance/Policies`,
`Archive/Nexrad/Publishers`, `Product/Pipeline/Services`, and
`Streaming/Identity/Models`. The Angular `OperatorUi` keeps its framework
native app layout.

## Prerequisites

Use the same local prerequisites on Windows, Linux, and macOS:

```text
.NET SDK for the solution target framework
Node.js/npm for src/Presentation/OperatorUi
Bash and curl for Linux/macOS/WSL2 package commands
```

Windows uses the PowerShell package script. Linux, macOS, and WSL2 use the
native Bash package script. PowerShell 7 (`pwsh`) also works with the `.ps1`
script when installed, but it is not required for the Unix workflow.

## Quick Start

Install UI dependencies when needed:

```sh
cd src/Presentation/OperatorUi
npm install
cd ../../..
```

Windows package command:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 help
```

Linux/macOS/WSL2 package command:

```sh
bash scripts/radarpulse-product-demo.sh help
```

PowerShell 7 optional command:

```sh
pwsh -File scripts/radarpulse-product-demo.ps1 help
```

The commands below use the native Linux/macOS/WSL2 entrypoint. On Windows,
run the same package command names through:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 <command>
```

Inspect the package paths:

```sh
bash scripts/radarpulse-product-demo.sh paths
```

Reset the default local demo history for a clean run:

```sh
bash scripts/radarpulse-product-demo.sh reset-history
```

Start the local same-origin product host:

```sh
bash scripts/radarpulse-product-demo.sh start
```

Open the operator UI:

```text
http://127.0.0.1:5129
```

In another terminal, check readiness and create a deterministic demo run:

```sh
bash scripts/radarpulse-product-demo.sh readiness
bash scripts/radarpulse-product-demo.sh demo --run-id product-demo
```

Use the browser UI to inspect the latest run, selected run detail, batches,
sources, handler output, diagnostics, and capacity evidence.

## Verify

Run the packaged local verification command:

```sh
bash scripts/radarpulse-product-demo.sh verify
```

The packaged command runs the accepted Angular, browser smoke, .NET restore,
focused .NET HTTP/API/readiness, and Release build gates. Each underlying
command remains visible for diagnosis. The restore step refreshes .NET assets
for the current OS before the `--no-restore` gates, which matters when the same
checkout is used from both Windows and WSL/Linux.

## Useful Commands

```sh
bash scripts/radarpulse-product-demo.sh help
bash scripts/radarpulse-product-demo.sh paths
bash scripts/radarpulse-product-demo.sh readiness
bash scripts/radarpulse-product-demo.sh demo --run-id product-demo
bash scripts/radarpulse-product-demo.sh history
bash scripts/radarpulse-product-demo.sh reset-history
bash scripts/radarpulse-product-demo.sh verify
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
