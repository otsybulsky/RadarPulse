# Architecture

Status: active under milestone 037.

This page explains the accepted RadarPulse architecture at maintainer depth.
It focuses on current boundaries and guardrails, not the full historical
decision trail. For the history behind the current posture, start with
`docs/milestones/036-clean-architecture-hardening-closeout.md` and
`docs/milestones/036-clean-architecture-hardening-decision-trace.md`.

## Current Architecture Posture

Milestone 036 accepted a defensible 10/10 architecture posture for the local
product demo/runtime boundary. In practical terms, that means:

```text
Domain stays pure
Application owns product-facing contracts and ports
Infrastructure implements adapters behind Application/Domain contracts
Presentation maps CLI/HTTP/UI input to Application contracts and composes
  concrete Infrastructure implementations
architecture tests guard the important dependency rules
```

This is a scoped architecture claim. It is about the accepted local
deterministic product demo/runtime boundary. It is not a claim of public
production hosting, production security, live ingestion, or external adapter
certification.

## Layer Responsibilities

Domain:

```text
owns:
  pure models, value objects, policies, validation, telemetry contracts,
  deterministic processing behavior, handler contracts, topology, rebalance,
  queueing, durable envelope state, retention, pressure, archive/streaming
  domain concepts

must not own:
  HTTP, CLI, Angular, filesystem adapters, compression libraries, AWS clients,
  product host composition, or external persistence mechanics
```

Application:

```text
owns:
  product-facing request/response vocabulary, product API contract,
  focused product use-case ports, read models, application-level archive and
  processing ports

must not own:
  concrete archive readers, file stores, HTTP endpoint mapping, CLI parsing,
  Angular state, or infrastructure runtime implementation details
```

Infrastructure:

```text
owns:
  archive readers/downloaders, decompression adapters, NEXRAD clients,
  file-backed durable and product history stores, processing queue/session
  implementations, product pipeline service implementation, benchmark
  runners, retained payload factories, worker mailboxes

must not own:
  product-facing API contracts that Presentation depends on
```

Presentation:

```text
owns:
  CLI entrypoint and command routing, HTTP host and endpoint mapping,
  same-origin static delivery, Angular Operator UI

must not own:
  domain rules, product use-case contracts, durable/queue internals, or
  processing runtime semantics
```

## Project Dependency Direction

Accepted project references:

```text
Domain:
  no RadarPulse project references

Application:
  Domain

Infrastructure:
  Application

RadarPulse.Cli:
  Application
  Infrastructure

RadarPulse.Http:
  Application
  Infrastructure

Tests:
  Domain
  Infrastructure
  RadarPulse.Http
  RadarPulse.Cli
```

The Presentation projects reference Infrastructure because they are
composition roots. The important boundary is that endpoint/workflow code
should consume Application-owned abstractions, not concrete Infrastructure
contracts.

## Product API Boundary

The accepted product API shape is Application-owned:

```text
src/Application/Product/Pipeline/Contracts
  IRadarPulseProductPipelineApi
  IRadarPulseProductPipelineRunService
  IRadarPulseProductPipelineQueryService
  IRadarPulseProductPipelineHistoryService
  IRadarPulseProductPipelineControlService
  IRadarPulseProductPipelineService compatibility aggregate
  RadarPulseProductPipelineApiContract
```

The focused ports are the normal dependency target:

```text
run:
  create synthetic or archive-file product runs

query:
  list runs, get run detail, batches, sources, handler output, diagnostics,
  and capacity evidence

history:
  report local run history count/readiness

control:
  apply local recoverable pipeline controls
```

`IRadarPulseProductPipelineService` remains as a compatibility aggregate for
direct service consumers. Presentation-facing API contracts should depend on
the focused ports or `IRadarPulseProductPipelineApi`.

## Ports And Adapters

RadarPulse uses ports where outer details should not leak inward:

```text
Application product ports:
  product run/query/history/control use cases and product API response shape

Application archive ports:
  archive client/publisher contracts used by archive-shaped workflows

Application processing ports/read models:
  processing output query/read-model surface for product-facing workflows

Infrastructure adapters:
  concrete archive clients, file stores, queue/session implementations,
  product service, processing runners, retained resource pools, and
  benchmark harnesses
```

The practical rule:

```text
if the outside world talks about it as product behavior, start in Application
if it touches files, compression, AWS, HTTP hosting, queue storage, worker
mailboxes, or local persistence, it belongs in Infrastructure or Presentation
behind an accepted contract
```

## Guardrail Tests

Architecture guardrails live in:

```text
tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs
```

They currently guard:

```text
project reference direction
Domain/Application source not referencing outer namespaces
Domain not granting InternalsVisibleTo access to Infrastructure
Application ownership of product API contracts and focused product ports
Product API contract depending on focused ports instead of the broad
  compatibility aggregate
HTTP product endpoints depending on IRadarPulseProductPipelineApi instead of
  a concrete Infrastructure API class
CLI Program.cs staying a thin entrypoint
```

When changing a boundary, run the architecture tests first. If a guardrail
fails, the change is probably an architecture decision, not a small code edit.

## Accepted Composition Pattern

HTTP and CLI are composition roots:

```text
Presentation accepts command/HTTP input
  -> calls Application-owned product API or ports
  -> DI binds Application ports to Infrastructure implementations
  -> Infrastructure uses Domain/Application contracts to perform work
  -> Application/product DTOs return stable response shape
  -> Presentation serializes or formats the response
```

The host may know which Infrastructure class implements a port. The endpoint
method should not make concrete Infrastructure product API types part of its
behavioral contract.

## Where To Put Changes

New product response field:

```text
start:
  src/Application/Product/Pipeline/Models
then:
  src/Infrastructure/Product/Pipeline
  src/Presentation/RadarPulse.Http/Product
  src/Presentation/OperatorUi/src/app/product
tests:
  tests/RadarPulse.Tests/Product
  OperatorUi tests when UI display changes
```

New processing diagnostic:

```text
start:
  src/Domain/Processing or src/Infrastructure/Processing where the evidence
  is produced
then:
  src/Application/Processing/ReadModels
  src/Application/Product/Pipeline/Models
tests:
  tests/RadarPulse.Tests/Processing
  tests/RadarPulse.Tests/Product
```

New adapter or persistence detail:

```text
start:
  Application port only if a new contract is needed
then:
  Infrastructure implementation
tests:
  focused infrastructure/product/processing tests plus architecture tests if
  a layer boundary changes
```

New CLI command-family behavior:

```text
start:
  src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication
or:
  src/Presentation/RadarPulse.Cli/Product
tests:
  tests/RadarPulse.Tests/Presentation
```

New HTTP route:

```text
start:
  Application product API/read model contract
then:
  src/Presentation/RadarPulse.Http/Product/Endpoints
tests:
  tests/RadarPulse.Tests/Product/Http
```

## Rejected Architecture Moves

These remain rejected unless a future milestone explicitly reopens them:

```text
Presentation depending on concrete Infrastructure product API contracts
Domain depending on Application, Infrastructure, Presentation, or ASP.NET
Application depending on Infrastructure, Presentation, or ASP.NET
Domain granting friend access to Infrastructure
one broad Presentation-facing product service dependency when focused ports
  are available
runtime/product behavior changes hidden inside documentation work
claiming production deployment/security/live-ingestion/external-adapter
  readiness from the local architecture boundary
```

## Verification

Architecture-only changes should start with:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Architecture" -c Release --no-restore
```

If product contracts or endpoint behavior move, add:

```text
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Product" -c Release --no-restore
```

Shared contract or layer changes should also use:

```text
dotnet build RadarPulse.sln -c Release --no-restore
```

Use the package verify path only when local demo delivery, scripts, HTTP/UI,
or product readiness behavior changes.
