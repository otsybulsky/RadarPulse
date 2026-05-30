# System Overview

Status: active under milestone 037.

RadarPulse is a local product demo for a radar-processing pipeline. It shows
how deterministic archive-shaped radar workloads move through a layered
system: archive input becomes stream batches, stream batches are processed in
ordered runtime paths, handler output is projected into product read models,
and the local HTTP/CLI/UI surfaces expose the result with readiness and
diagnostics.

The short version:

```text
RadarPulse is not a live public radar service.
RadarPulse is a deterministic local product demo over archive-shaped inputs.
It demonstrates architecture, runtime ordering, diagnostics, product API
shape, local persistence, and operator inspectability.
```

## What The System Does

RadarPulse gives a reader or operator one repeatable way to inspect a
radar-processing product workflow:

```text
1. select deterministic demo or archive-shaped input
2. normalize radar stream identities and build validated radar event batches
3. process batches through the accepted runtime/queueing posture
4. preserve externally visible ordering by provider sequence
5. compute handler outputs and processing diagnostics
6. project output into product-facing read models
7. expose run, batch, source, handler, readiness, and history information
   through CLI, HTTP, and the Angular Operator UI
```

The accepted product demo path is local and deterministic. It is meant to be
repeatable for portfolio review, architecture review, and maintainer
verification without depending on a live radar network feed.

## Current Accepted Scope

Covered:

```text
deterministic demo/archive-shaped product workflows
local RadarPulse.Http product API
built Angular Operator UI served from the same local origin
product CLI workflows
deterministic local file-backed product run history
readiness inspection, demo run, history inspection/reset, and focused gates
processing runtime ordering, queueing, durable/recovery contracts, handler
  output, diagnostics, and product read models inside the accepted local
  boundary
```

Not claimed:

```text
true live radar network ingestion
public production deployment
authentication or authorization
TLS termination
production CORS hardening
external broker/cloud queue/database adapter certification
deployment automation, autoscaling, alert routing, or runbooks
database-backed product history
cross-machine throughput certification
exactly-once end-to-end production delivery
public comparative performance certification
```

These limits are deliberate. They keep the system honest: strong local
architecture and deterministic runtime evidence without pretending the local
demo is a production radar platform.

## Mental Model

Think of RadarPulse as six cooperating surfaces:

```text
Archive surface:
  reads deterministic historical/NEXRAD-shaped files and turns them into
  streamable domain events

Streaming surface:
  gives radar sources stable identities and groups events into validated
  batches

Processing runtime:
  processes batches, preserves provider-sequence ordering, manages queueing,
  durable envelopes, retained payload ownership, rebalance, pressure, and
  handler execution posture

Product output surface:
  turns committed processing state into stable read models and product API
  responses

Presentation surface:
  exposes the accepted product workflows through CLI, local HTTP endpoints,
  and the Angular Operator UI

Evidence surface:
  tests, package verification, milestone gates, closeouts, and decision
  traces prove what is accepted and where the warnings are
```

The source map explains where these surfaces live:
[source-map.md](source-map.md).

## Layer Model

RadarPulse follows the current accepted layer shape:

```text
Domain:
  owns pure rules, models, validation, policies, and deterministic services

Application:
  owns product-facing contracts, ports, request/response models, read models,
  and use-case vocabulary

Infrastructure:
  implements archive, processing, durable, queueing, product, persistence,
  and benchmark adapters behind Domain/Application contracts

Presentation:
  maps CLI/HTTP/UI input to Application contracts and composes concrete
  Infrastructure implementations
```

The important dependency idea is simple:

```text
inner layers do not depend on outer adapters
product-facing contracts live in Application
Infrastructure implements adapters
Presentation composes and exposes workflows
```

Architecture tests under `tests/RadarPulse.Tests/Architecture` guard the
accepted dependency direction and key milestone 036 rules.

## Data Flow At A Glance

The usual product/demo flow is:

```text
demo/archive input
  -> archive reader or synthetic product input
  -> radar stream events
  -> source identity normalization and source universe mapping
  -> radar event batches
  -> queued/ordered processing runtime
  -> committed processing state and handler output
  -> processing/product read models
  -> product API response
  -> CLI, HTTP, Operator UI, and local run history
```

The most important invariant is that externally visible output is interpreted
by provider sequence, not by worker completion order. Concurrent work may
finish out of order internally, but accepted output must be committed and
reported deterministically.

## Runtime Posture In One Page

The accepted runtime posture is local, deterministic, and archive-shaped:

```text
queued-owned provider posture:
  owns accepted batches and retained payload resources while processing

ordered concurrent processing:
  may compute multiple accepted batches concurrently, then commit externally
  visible results by provider sequence

durable envelope posture:
  validates broker-neutral durable contracts and persistent local adapter
  behavior inside the accepted local scope, without claiming production
  broker/cloud readiness

handler posture:
  handler-free and explicitly mergeable handlers may use ordered concurrent
  fast paths; snapshot-only handlers keep sequential fallback; unsupported
  handler sets fail closed with diagnostics

diagnostic posture:
  readiness, first blocking reason, retained pressure, release health,
  validation failures, worker health, and capacity evidence should remain
  visible rather than hidden behind friendly summaries
```

The deeper runtime explanation belongs in `processing-runtime.md` once that
planned page is implemented.

## Product Surface In One Page

The product surface is intentionally small and inspectable:

```text
scripts/radarpulse-product-demo.ps1
scripts/radarpulse-product-demo.sh
  package commands for paths, reset-history, start, readiness, demo, history,
  and verify

src/Presentation/RadarPulse.Http
  local product API and same-origin static Operator UI delivery

src/Presentation/RadarPulse.Cli
  product and benchmark command workflows

src/Presentation/OperatorUi
  Angular UI for readiness, runs, batches, sources, handler output,
  diagnostics, capacity, history, and controls

.tmp/product-demo
  default local demo workspace and product history location
```

Use `README.md` and `docs/product-demo-readiness.md` for the current local
demo commands.

## How To Read The Repository

If you are new to the system:

```text
1. read this overview
2. read source-map.md
3. run or inspect the local demo commands from README.md
4. read workflows.md when implemented
5. read architecture.md and processing-runtime.md when implementation detail
   matters
6. use verification-and-evidence.md when you need proof or gate selection
```

If you are changing code, use the source map first. It tells you which layer
owns the behavior and which focused tests usually protect it.

## Historical Evidence

The handbook describes the current system. It does not replace the historical
record. Use these documents for decisions and evidence:

```text
docs/milestones/033-product-demo-polish-and-portfolio-readiness-closeout.md
  accepted portfolio demo scope and non-claims

docs/milestones/034-targeted-project-restructuring-and-maintenance.md
  responsibility-first source/test layout

docs/milestones/035-code-contract-documentation-pass.md
  public/domain-facing code contract documentation pass

docs/milestones/036-clean-architecture-hardening-closeout.md
  final clean architecture posture and verification summary

docs/milestones/036-clean-architecture-hardening-decision-trace.md
  accepted architecture decision trace and scoped warnings
```
