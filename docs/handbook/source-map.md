# Source Map

Status: active under milestone 037.

This page maps the current RadarPulse repository layout to the system
responsibilities it implements. It describes where to start when reading or
changing the accepted system after the responsibility-first restructuring
from milestone 034 and the clean architecture hardening from milestone 036.

## Solution Shape

The source tree is organized by Clean Architecture layers first:

```text
src/Domain
  pure domain contracts, models, policies, validation, and deterministic
  services

src/Application
  application-owned ports, request/response contracts, read models, and
  use-case vocabulary

src/Infrastructure
  adapters, storage, archive readers/downloaders, runtime implementations,
  benchmark runners, and product service implementations behind application
  ports

src/Presentation
  CLI, HTTP host, same-origin static delivery, and Angular Operator UI

tests/RadarPulse.Tests
  executable architecture, product, processing, archive, streaming, and
  presentation coverage
```

The .NET projects are:

```text
src/Domain/RadarPulse.Domain.csproj
src/Application/RadarPulse.Application.csproj
src/Infrastructure/RadarPulse.Infrastructure.csproj
src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj
src/Presentation/RadarPulse.Http/RadarPulse.Http.csproj
tests/RadarPulse.Tests/RadarPulse.Tests.csproj
```

Project dependency direction:

```text
Domain:
  references no RadarPulse project

Application:
  references Domain

Infrastructure:
  references Application

Presentation CLI and HTTP:
  reference Application and Infrastructure as composition roots

Tests:
  reference Domain, Infrastructure, Presentation HTTP, and Presentation CLI
```

Milestone 036 added architecture tests that guard this direction. Start at
`tests/RadarPulse.Tests/Architecture` when checking whether a dependency
change is allowed.

## Domain

Domain is the place for rules that should not depend on adapters, hosts,
filesystems, HTTP, CLI, Angular, or package scripts.

Archive domain:

```text
src/Domain/Archive/Archive2
  Archive II message, event, and read result concepts

src/Domain/Archive/Benchmarks
  benchmark result contracts for archive-shaped evidence

src/Domain/Archive/Historical
  historical archive request and discovery models

src/Domain/Archive/Nexrad
  NEXRAD file/cache inspection models

src/Domain/Archive/Publish
  replay and radar-event-batch publish result contracts
```

Streaming domain:

```text
src/Domain/Streaming/Batches
  radar event batch models, metrics, lifetime, builder, and validation

src/Domain/Streaming/Identity
  dense identity catalogs, canonicalization, normalization, and versions

src/Domain/Streaming/Sources
  source keys and source universe mapping

src/Domain/Streaming/Streams
  stream event, dictionary snapshot, checksum, status, and schema models
```

Processing domain:

```text
src/Domain/Processing/Core
  processing options, telemetry, validation, results, and deterministic core
  behavior

src/Domain/Processing/Async
  async batch/work contracts, worker affinity, dispatch scope, and validation

src/Domain/Processing/Workers
  worker identity, lifecycle, health, timeout, and telemetry contracts

src/Domain/Processing/Queueing
  queued provider options, sequence models, readiness, validation, results,
  and provider queue telemetry

src/Domain/Processing/Durable
  durable envelope, batch id, retry, adapter summary, readiness, and queue
  result contracts

src/Domain/Processing/Handlers
  custom handler descriptors, state, snapshots, execution classification,
  per-batch handler deltas, merge results, and merge coordination

src/Domain/Processing/Rebalance
  pressure-aware rebalance candidates, decisions, policies, planners,
  migration validation, telemetry, and session results

src/Domain/Processing/Topology
  topology versions, partition assignment, route state, residency, and
  routing services

src/Domain/Processing/Retention
  retained payload ownership, resource leases, pressure snapshots, and
  cleanup/result contracts

src/Domain/Processing/Pressure
  pressure samples, windows, skew transformation, hot partition
  classification, and quarantine lifecycle

src/Domain/Processing/Benchmarks
  benchmark allocation and synthetic workload result contracts
```

## Application

Application owns the product-facing vocabulary and the ports that outer
layers depend on.

```text
src/Application/Archive/Contracts
  archive client and publisher ports used by application-level flows

src/Application/Archive/Options
  application-level archive publish options

src/Application/Archive/Services
  historical manifest selection logic

src/Application/Processing/Contracts
  processing output query/read-model ports

src/Application/Processing/ReadModels
  product-facing processing run, batch, source, handler, diagnostic, and
  readiness read models

src/Application/Processing/Services
  in-memory/read-model services that expose processing output to product
  surfaces

src/Application/Product/Pipeline/Contracts
  product pipeline API and focused run/query/history/control service ports

src/Application/Product/Pipeline/Models
  product pipeline requests, responses, configuration, read models, and
  public DTO vocabulary

src/Application/Product/History/Models
  product run history entries, summaries, and persistence-safe models
```

When changing product API shape, read models, or use-case contracts, start in
Application first. Infrastructure should implement those contracts; HTTP and
CLI should consume them.

## Infrastructure

Infrastructure contains concrete adapters and runtime implementations behind
Domain and Application contracts.

Archive infrastructure:

```text
src/Infrastructure/Archive/Archive2
  Archive II file readers, message scanners, projectors, and summaries

src/Infrastructure/Archive/Compression
  BZip2 decompressor adapters and reusable decompression behavior

src/Infrastructure/Archive/Contracts
  archive adapter interfaces that are infrastructure-specific

src/Infrastructure/Archive/Historical
  historical cache metadata, manifest reading/writing, disk preflight, and
  downloader implementation

src/Infrastructure/Archive/Nexrad
  AWS archive client, cache path mapping, file/cache inspection,
  decompression/parse/replay benchmarks, replay publishers, radar event batch
  publishers, and validation
```

Processing infrastructure:

```text
src/Infrastructure/Processing/ArchiveRuntime
  archive-shaped runtime baselines, queued overlap runner, ordered processing
  benchmark, archive rebalance benchmark, runtime options, telemetry, and
  result models

src/Infrastructure/Processing/Async
  async worker groups, dispatch, completion aggregation, ordered result
  coordination, and async core session

src/Infrastructure/Processing/Benchmarks
  synthetic benchmark handlers, workloads, runners, options, and results

src/Infrastructure/Processing/Core
  persistent processing batch/event records

src/Infrastructure/Processing/Durable
  durable envelope queues, durable processing/rebalance sessions, persistent
  envelope store, durable options, and recovery results

src/Infrastructure/Processing/ProductPipeline
  production pipeline runner, control coordinator, recovery runner, option
  resolution, telemetry, mapper, and operator summary models

src/Infrastructure/Processing/Queueing
  owned batch queue, queued processing session, and queued rebalance session
  implementations

src/Infrastructure/Processing/Retention
  retained payload factory and array-pool-backed retained resource ownership

src/Infrastructure/Processing/Runtime
  MVP runtime plan/result implementation models

src/Infrastructure/Processing/Workers
  worker mailbox, mailbox options/results, and worker telemetry store
```

Product infrastructure:

```text
src/Infrastructure/Product/Pipeline
  product pipeline service implementation, archive capture, batching,
  handler-set construction, and product adapter contracts

src/Infrastructure/Product/History
  in-memory and file-backed product run history stores
```

Infrastructure is where adapter and orchestration details live, but it should
not own product-facing contracts that Application exposes.

## Presentation

Presentation maps user or HTTP input into Application contracts and composes
Infrastructure implementations.

```text
src/Presentation/RadarPulse.Cli/EntryPoint
  top-level CLI program, command-family routing, archive commands, processing
  benchmark commands, formatting, and usage output

src/Presentation/RadarPulse.Cli/Product
  product pipeline CLI workflow

src/Presentation/RadarPulse.Http/Hosting
  HTTP host entrypoint

src/Presentation/RadarPulse.Http/Product
  product HTTP composition, endpoints, options, readiness, and static
  Operator UI delivery

src/Presentation/OperatorUi
  Angular Operator UI, product API client, product API state, smoke tests,
  and hosted same-origin browser smoke setup
```

The CLI and HTTP projects are composition roots. They may reference
Infrastructure to wire concrete implementations, but product endpoints and
workflows should speak Application-owned contracts.

## Tests

Tests are organized by the behavior they protect:

```text
tests/RadarPulse.Tests/Architecture
  project/layer dependency direction, Application product API ownership,
  HTTP endpoint dependency, Domain friend access, and thin entrypoint rules

tests/RadarPulse.Tests/Archive
  Archive II scanning, historical download/manifest behavior, NEXRAD client,
  cache, inspector, publisher, validation, and archive benchmark coverage

tests/RadarPulse.Tests/Streaming
  batch builder/validator, identity catalog/normalization, source universe,
  and stream contract coverage

tests/RadarPulse.Tests/Processing
  archive runtime, async transport, benchmark, core, durable, handlers,
  pressure, product pipeline, queueing, read models, rebalance, retention,
  topology, and worker coverage

tests/RadarPulse.Tests/Product
  product pipeline API/service contracts, product HTTP host/control, and
  product run history coverage

tests/RadarPulse.Tests/Presentation
  CLI product workflow and benchmark command coverage
```

Use focused tests when changing a bounded area. Use the full Release suite
when changing shared contracts, cross-layer wiring, runtime order, or
documentation that claims a full-system posture.

## Common Change Starting Points

Product API or read model change:

```text
start:
  src/Application/Product/Pipeline
  src/Application/Processing/ReadModels
then:
  src/Infrastructure/Product/Pipeline
  src/Presentation/RadarPulse.Http/Product
tests:
  tests/RadarPulse.Tests/Product
  tests/RadarPulse.Tests/Processing/ReadModels
```

Archive input or replay change:

```text
start:
  src/Domain/Archive
  src/Infrastructure/Archive
tests:
  tests/RadarPulse.Tests/Archive
```

Streaming batch or identity change:

```text
start:
  src/Domain/Streaming
tests:
  tests/RadarPulse.Tests/Streaming
```

Processing runtime or ordering change:

```text
start:
  src/Domain/Processing
  src/Infrastructure/Processing
tests:
  tests/RadarPulse.Tests/Processing
```

HTTP/local demo delivery change:

```text
start:
  src/Presentation/RadarPulse.Http
  scripts
  docs/product-demo-readiness.md
tests:
  tests/RadarPulse.Tests/Product/Http
  src/Presentation/OperatorUi/smoke
```

Operator UI change:

```text
start:
  src/Presentation/OperatorUi/src/app
tests:
  src/Presentation/OperatorUi/src/app
  src/Presentation/OperatorUi/smoke
```

Architecture boundary change:

```text
start:
  docs/milestones/036-clean-architecture-hardening.md
  tests/RadarPulse.Tests/Architecture
tests:
  architecture tests first, then the focused behavior tests for the touched
  layer
```

## Historical Evidence Links

Use these milestone documents when you need the evidence behind the current
layout:

```text
docs/milestones/034-targeted-project-restructuring-and-maintenance.md
  responsibility-first physical source/test layout

docs/milestones/035-code-contract-documentation-pass.md
  public and domain-facing C# contract documentation pass

docs/milestones/036-clean-architecture-hardening.md
  Application-owned product API boundary, architecture guardrails, Domain
  friend assembly removal, SRP cleanup, and 10/10 architecture posture

docs/milestones/036-clean-architecture-hardening-decision-trace.md
  accepted architecture decision trace and scoped warnings

docs/milestones/036-clean-architecture-hardening-closeout.md
  final milestone 036 outcome and verification summary
```
